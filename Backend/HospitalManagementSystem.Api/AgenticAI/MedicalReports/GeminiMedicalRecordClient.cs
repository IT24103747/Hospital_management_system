using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HospitalManagementSystem.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HospitalManagementSystem.Api.AgenticAI.MedicalReports;

public sealed class GeminiMedicalRecordClient : IMedicalRecordIntelligenceAgent
{
    private readonly HttpClient _http;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiMedicalRecordClient> _logger;

    public GeminiMedicalRecordClient(
        HttpClient http,
        IConfiguration configuration,
        ILogger<GeminiMedicalRecordClient> logger)
    {
        _http = http;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<MedicalReportAnalysisResult> AnalyzeRecordsAsync(
        IEnumerable<MedicalRecord> records,
        string patientName,
        string? specificUserQuery,
        CancellationToken cancellationToken = default)
    {
        var recordList = records.OrderByDescending(r => r.RecordDate).ToList();
        if (recordList.Count == 0)
        {
            return DeterministicClinicalSafetyEngine.BuildHeuristicSummary(recordList, patientName, specificUserQuery);
        }

        var apiKey = _configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogInformation("Gemini API key not configured; using deterministic clinical intelligence engine.");
            return DeterministicClinicalSafetyEngine.BuildHeuristicSummary(recordList, patientName, specificUserQuery);
        }

        try
        {
            var model = _configuration["Gemini:Model"] ?? "gemini-2.5-flash";
            var timeoutSeconds = Math.Clamp(_configuration.GetValue<int?>("Gemini:TimeoutSeconds") ?? 45, 10, 90);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var recordsContext = string.Join("\n---\n", recordList.Take(5).Select(r =>
                $"Record ID: {r.MedicalRecordId}\n" +
                $"Date: {r.RecordDate:yyyy-MM-dd}\n" +
                $"Type: {r.RecordType}\n" +
                $"Doctor: {(r.Doctor != null ? $"Dr. {r.Doctor.FirstName} {r.Doctor.LastName}".Trim() : "Hospital Clinician")}\n" +
                $"Diagnosis: {r.Diagnosis}\n" +
                $"Symptoms: {r.Symptoms}\n" +
                $"Treatment Plan: {r.TreatmentPlan}\n" +
                $"Prescriptions: {r.PrescriptionNotes ?? "None"}\n" +
                $"Lab Notes: {r.LabNotes ?? "None"}\n" +
                $"Follow-up Date: {(r.FollowUpDate.HasValue ? r.FollowUpDate.Value.ToString("yyyy-MM-dd") : "None")}"
            ));

            var systemPrompt =
                "You are the MediCore Hospital Clinical Intelligence Agent. Your role is to clearly and empathetically explain the patient's verified medical records.\n" +
                "Rules:\n" +
                "1. Ground all statements strictly in the patient's records. NEVER invent diagnoses, drugs, or lab findings.\n" +
                "2. Translate complex medical terms into plain, reassuring English.\n" +
                "3. Explain prescribed medications clearly: dosage, frequency, and instructions.\n" +
                "4. Structure your response with clean bullet points and emoji headers:\n" +
                "   📋 Clinical Overview (Latest Visit & Diagnosis)\n" +
                "   💊 Prescriptions & Medication Advice\n" +
                "   🔬 Lab & Diagnostic Findings\n" +
                "   ⚠️ Important Precautions & Next Follow-up\n" +
                "   ℹ️ Guidance Disclaimer: Non-diagnostic AI summary for patient education; always adhere to your prescribing doctor's orders.\n" +
                "5. Keep the tone compassionate, professional, and accessible.";

            var userPrompt = $"Patient: {patientName}\n\nPatient Records:\n{recordsContext}\n\n";
            if (!string.IsNullOrWhiteSpace(specificUserQuery))
            {
                userPrompt += $"Patient's specific question: {specificUserQuery}";
            }
            else
            {
                userPrompt += "Please summarize my latest medical report and explain my prescribed medications and follow-up.";
            }

            var requestUri = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

            var requestBody = new
            {
                systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
                contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
                generationConfig = new
                {
                    temperature = 0.2,
                    maxOutputTokens = 800
                }
            };

            var response = await _http.PostAsJsonAsync(requestUri, requestBody, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini API call returned status {Status}; falling back to deterministic engine.", response.StatusCode);
                return DeterministicClinicalSafetyEngine.BuildHeuristicSummary(recordList, patientName, specificUserQuery);
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeoutCts.Token));
            var generatedText = doc.RootElement
                .TryGetProperty("candidates", out var candidates) &&
                candidates.ValueKind == JsonValueKind.Array &&
                candidates.GetArrayLength() > 0 &&
                candidates[0].TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array &&
                parts.GetArrayLength() > 0 &&
                parts[0].TryGetProperty("text", out var textProp)
                    ? textProp.GetString()
                    : null;

            if (string.IsNullOrWhiteSpace(generatedText))
            {
                _logger.LogWarning("Gemini returned empty text candidate; engaging deterministic fallback.");
                return DeterministicClinicalSafetyEngine.BuildHeuristicSummary(recordList, patientName, specificUserQuery);
            }

            var safetyAlerts = DeterministicClinicalSafetyEngine.EvaluateSafety(recordList);
            var latest = recordList.First();

            return new MedicalReportAnalysisResult(
                Overview: $"Latest Consultation ({latest.RecordDate:MMM dd, yyyy}) - {latest.Diagnosis}",
                KeyDiagnoses: recordList.Select(r => r.Diagnosis).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().Take(5).ToList(),
                PrescribedMedications: recordList.Select(r => r.PrescriptionNotes).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!).Distinct().Take(5).ToList(),
                LabFindings: recordList.Select(r => r.LabNotes).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l!).Distinct().Take(5).ToList(),
                SafetyAlerts: safetyAlerts,
                FollowUpInstructions: latest.FollowUpDate.HasValue ? latest.FollowUpDate.Value.ToString("MMMM dd, yyyy") : null,
                PlainLanguageSummary: generatedText.Trim(),
                AgentTrajectoryDescription: $"Synthesized by Gemini ({model}) + Deterministic Drug Safety Validator.",
                UsedGemini: true
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini medical record summarizer threw exception; engaging deterministic fallback.");
            return DeterministicClinicalSafetyEngine.BuildHeuristicSummary(recordList, patientName, specificUserQuery);
        }
    }
}
