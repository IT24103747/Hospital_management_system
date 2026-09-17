using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HospitalManagementSystem.Api.Models;

namespace HospitalManagementSystem.Api.AgenticAI.MedicalReports;

public static class DeterministicClinicalSafetyEngine
{
    private static readonly string[] CommonAntibiotics =
    [
        "amoxicillin", "ampicillin", "ciprofloxacin", "azithromycin", "cephalexin",
        "doxycycline", "clarithromycin", "metronidazole", "co-amoxiclav", "erythromycin"
    ];

    private static readonly string[] CommonNsaids =
    [
        "ibuprofen", "aspirin", "diclofenac", "naproxen", "meloxicam", "celecoxib", "mefenamic"
    ];

    public static IReadOnlyList<MedicationAlert> EvaluateSafety(IEnumerable<MedicalRecord> records)
    {
        var alerts = new List<MedicationAlert>();
        var detectedDrugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            var text = $"{record.PrescriptionNotes} {record.TreatmentPlan}".ToLowerInvariant();

            // Detect antibiotics
            foreach (var abx in CommonAntibiotics)
            {
                if (Regex.IsMatch(text, $@"\b{abx}\b"))
                {
                    detectedDrugs.Add(abx);
                    alerts.Add(new MedicationAlert(
                        "Advisory",
                        Capitalize(abx),
                        "Complete the full antibiotic course as prescribed even if symptoms improve to prevent bacterial resistance."
                    ));
                }
            }

            // Detect NSAIDs
            var nsaidsInRecord = CommonNsaids.Where(n => Regex.IsMatch(text, $@"\b{n}\b")).ToList();
            if (nsaidsInRecord.Count > 1)
            {
                alerts.Add(new MedicationAlert(
                    "Warning",
                    string.Join(" + ", nsaidsInRecord.Select(Capitalize)),
                    "Multiple anti-inflammatory (NSAID) pain medications detected together. Take with food and consult your doctor to prevent stomach irritation."
                ));
            }
            foreach (var nsaid in nsaidsInRecord)
            {
                detectedDrugs.Add(nsaid);
            }

            // Paracetamol check
            if (Regex.IsMatch(text, @"\b(paracetamol|panadol|acetaminophen)\b"))
            {
                detectedDrugs.Add("Paracetamol");
                alerts.Add(new MedicationAlert(
                    "Info",
                    "Paracetamol",
                    "Do not exceed 4,000 mg (8 x 500mg tablets) within any 24-hour window. Maintain at least 4-6 hours between doses."
                ));
            }

            // Follow-up alert check
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
                        $"Your follow-up date was scheduled for {record.FollowUpDate.Value:MMM dd, yyyy}. Consider contacting your clinic if symptoms persist."
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
        var recordList = records.OrderByDescending(r => r.RecordDate).ToList();
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
            sb.AppendLine("💊 Prescriptions & Medication Advice");
            foreach (var med in meds)
            {
                sb.AppendLine($"• {med}");
            }
        }

        if (labNotes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("🔬 Lab & Diagnostic Notes");
            foreach (var lab in labNotes)
            {
                sb.AppendLine($"• {lab}");
            }
        }

        if (safetyAlerts.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("⚠️ Important Safety Precautions");
            foreach (var alert in safetyAlerts)
            {
                sb.AppendLine($"• [{alert.DrugName}] {alert.Message}");
            }
        }

        sb.AppendLine();
        sb.AppendLine($"📅 Follow-up Status: {followUpText}");
        sb.AppendLine();
        sb.AppendLine("ℹ️ Guidance Disclaimer: This summary is generated to help you understand your clinical record. Always follow the direct instructions on your physical prescription or contact your doctor with any questions.");

        return new MedicalReportAnalysisResult(
            Overview: $"Visit on {latest.RecordDate:MMM dd, yyyy} - {latest.Diagnosis}",
            KeyDiagnoses: diagnoses,
            PrescribedMedications: meds,
            LabFindings: labNotes,
            SafetyAlerts: safetyAlerts,
            FollowUpInstructions: followUpText,
            PlainLanguageSummary: sb.ToString().Trim(),
            AgentTrajectoryDescription: "Executed Deterministic Clinical Safety Engine (Entity extraction + Drug safety validator + Follow-up scheduler).",
            UsedGemini: false
        );
    }

    private static string Capitalize(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0]) + text[1..];
}
