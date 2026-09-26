using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.AgenticAI.MedicalReports;

public static class DeterministicClinicalSafetyEngine
{
    public static IReadOnlyList<MedicationAlert> EvaluateSafety(IEnumerable<MedicalRecord> records)
    {
        var alerts = new List<MedicationAlert>();

        foreach (var record in records)
        {
            // Only dates explicitly recorded by a clinician are safe to surface
            // deterministically. Medication advice must never be inferred from a
            // drug name or supplied from a hardcoded rule.
            if (record.FollowUpDate.HasValue)
            {
                var daysUntil = (record.FollowUpDate.Value.Date - DateTime.UtcNow.Date).TotalDays;
                if (daysUntil >= 0 && daysUntil <= 7)
                {
                    alerts.Add(new MedicationAlert(
                        "Reminder",
                        "Clinical Follow-up",
                        $"You have a recommended follow-up review on {record.FollowUpDate.Value:MMM dd, yyyy} (in {(int)daysUntil} days)."
                    ));
                }
                else if (daysUntil < 0)
                {
                    alerts.Add(new MedicationAlert(
                        "Advisory",
                        "Overdue Follow-up",
                        $"The follow-up date recorded by your clinician was {record.FollowUpDate.Value:MMM dd, yyyy}, and that date has passed."
                    ));
                }
            }
        }

        return alerts
            .GroupBy(a => $"{a.Severity}:{a.DrugName}:{a.Message}")
            .Select(g => g.First())
            .ToList();
    }

    public static MedicalReportAnalysisResult BuildHeuristicSummary(
        IEnumerable<MedicalRecord> records,
        string patientName,
        string? userQuery)
    {
        var recordList = records
            .OrderByDescending(r => r.RecordDate.Date)
            .ThenByDescending(r => r.CreatedAt)
            .ThenByDescending(r => r.MedicalRecordId)
            .ToList();
        if (recordList.Count == 0)
        {
            return new MedicalReportAnalysisResult(
                Overview: "No records found",
                KeyDiagnoses: [],
                PrescribedMedications: [],
                LabFindings: [],
                SafetyAlerts: [],
                FollowUpInstructions: null,
                PlainLanguageSummary: $"Hello {patientName}, you do not have any recorded medical consultations, prescriptions, or lab reports on file yet.",
                AgentTrajectoryDescription: "Retrieved 0 records; returned patient onboarding guidance.",
                UsedGemini: false
            );
        }

        var latest = recordList.First();
        var diagnoses = recordList.Select(r => r.Diagnosis).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().Take(5).ToList();
        var meds = recordList.Select(r => r.PrescriptionNotes).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!).Distinct().Take(5).ToList();
        var labNotes = recordList.Select(r => r.LabNotes).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l!).Distinct().Take(5).ToList();
        var safetyAlerts = EvaluateSafety(recordList);

        var focused = FocusedAnswer(recordList, userQuery);
        if (focused is not null)
            return new MedicalReportAnalysisResult(
                Overview: $"Verified medical-record answer ({latest.RecordDate:MMM dd, yyyy})",
                KeyDiagnoses: diagnoses,
                PrescribedMedications: meds,
                LabFindings: labNotes,
                SafetyAlerts: safetyAlerts,
                FollowUpInstructions: latest.FollowUpDate?.ToString("MMMM dd, yyyy"),
                PlainLanguageSummary: focused,
                AgentTrajectoryDescription: "Answered from finalized structured medical-record fields using the deterministic grounded fallback.",
                UsedGemini: false);

        var doctorText = latest.Doctor != null ? $"Dr. {latest.Doctor.FirstName} {latest.Doctor.LastName}" : "Hospital Clinician";
        var followUpText = latest.FollowUpDate.HasValue
            ? $"Scheduled for {latest.FollowUpDate.Value:MMMM dd, yyyy}"
            : "No specific follow-up date recorded.";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Here is a clear summary of your medical reports, {patientName}:");
        sb.AppendLine();
        sb.AppendLine($"📋 Latest Clinical Visit ({latest.RecordDate:MMM dd, yyyy})");
        sb.AppendLine($"• Attending Doctor: {doctorText}");
        sb.AppendLine($"• Primary Diagnosis: {latest.Diagnosis}");
        if (!string.IsNullOrWhiteSpace(latest.Symptoms))
        {
            sb.AppendLine($"• Symptoms Addressed: {latest.Symptoms}");
        }
        sb.AppendLine($"• Care Plan: {latest.TreatmentPlan}");

        if (meds.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Prescriptions recorded by your clinician");
            foreach (var med in meds)
            {
                sb.AppendLine($"• {med}");
            }
        }

        if (labNotes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Lab and diagnostic notes");
            foreach (var lab in labNotes)
            {
                sb.AppendLine($"• {lab}");
            }
        }

        if (safetyAlerts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Recorded follow-up reminders");
            foreach (var alert in safetyAlerts)
            {
                sb.AppendLine($"• [{alert.DrugName}] {alert.Message}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"Follow-up status: {followUpText}");
        sb.AppendLine();
        sb.AppendLine("Guidance disclaimer: This explains finalized information already in your record. It is not a diagnosis or a new treatment plan. Follow the instructions recorded by your doctor or pharmacist.");

        return new MedicalReportAnalysisResult(
            Overview: $"Visit on {latest.RecordDate:MMM dd, yyyy} - {latest.Diagnosis}",
            KeyDiagnoses: diagnoses,
            PrescribedMedications: meds,
            LabFindings: labNotes,
            SafetyAlerts: safetyAlerts,
            FollowUpInstructions: followUpText,
            PlainLanguageSummary: sb.ToString().Trim(),
                AgentTrajectoryDescription: "Generated a finalized-record summary with deterministic follow-up-date checks.",
            UsedGemini: false
        );
    }

    private static string? FocusedAnswer(IReadOnlyList<MedicalRecord> records, string? userQuery)
    {
        if (string.IsNullOrWhiteSpace(userQuery)) return null;
        var query = userQuery.ToLowerInvariant();
        var latest = records[0];
        const string disclaimer = " This is an explanation of the finalized record, not a diagnosis or a new prescription.";

        if (Regex.IsMatch(query, @"\b(medicine|medicines|medication|medications|prescription|prescribed)\b"))
        {
            var entries = records.Where(record => !string.IsNullOrWhiteSpace(record.PrescriptionNotes))
                .Select(record => $"{record.RecordDate:yyyy-MM-dd}: {record.PrescriptionNotes}").Take(5).ToArray();
            return entries.Length == 0
                ? "No prescription details are recorded in your finalized medical records. Please confirm medication instructions with your doctor or pharmacist."
                : "The following prescription information is recorded:\n- " + string.Join("\n- ", entries) + disclaimer;
        }
        if (Regex.IsMatch(query, @"\b(lab|laboratory|blood test|test result|results)\b"))
        {
            var entries = records.Where(record => !string.IsNullOrWhiteSpace(record.LabNotes))
                .Select(record => $"{record.RecordDate:yyyy-MM-dd}: {record.LabNotes}").Take(5).ToArray();
            return entries.Length == 0
                ? "No lab result details or reference ranges are recorded in your finalized medical records."
                : "The following lab information is recorded:\n- " + string.Join("\n- ", entries) + disclaimer;
        }
        if (Regex.IsMatch(query, @"\b(diagnosis|diagnoses|diagnosed)\b"))
            return string.IsNullOrWhiteSpace(latest.Diagnosis)
                ? $"No specific diagnosis was recorded for the finalized visit on {latest.RecordDate:yyyy-MM-dd}."
                : $"The finalized record dated {latest.RecordDate:yyyy-MM-dd} lists the diagnosis as: {latest.Diagnosis}.{disclaimer}";
        if (Regex.IsMatch(query, @"\b(follow-up|follow up|return|next visit)\b"))
            return latest.FollowUpDate.HasValue
                ? $"The latest finalized record lists a follow-up date of {latest.FollowUpDate.Value:MMMM dd, yyyy}.{disclaimer}"
                : "No follow-up date is recorded in the latest finalized medical record.";
        return null;
    }
}
