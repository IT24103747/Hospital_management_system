using System.Diagnostics;
using HospitalManagementSystem.Api.AgenticAI.SafeTriage;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.ClinicalSafety;


public interface IClinicalSafetyTriageAgent
{
    Task<ClinicalSafetyAssessment> AssessAsync(ClinicalSafetyTriageRequest request, CancellationToken cancellationToken = default);
}


public sealed class ClinicalSafetyTriageAgent(IClinicalInformationExtractionAgent extraction) : IClinicalSafetyTriageAgent
{
    public async Task<ClinicalSafetyAssessment> AssessAsync(ClinicalSafetyTriageRequest request, CancellationToken cancellationToken = default)
    {
        var trace = new List<ClinicalSafetyToolExecution>();
        var workflowRequest = new StartTriageWorkflowDto
        {
            Symptoms = request.Symptoms?.Trim() ?? string.Empty,
            Vitals = request.Vitals,
            IsFollowUp = request.IsFollowUp
        };

        var validation = Timed("ValidateVitalsTool", () => ClinicalSafetyTools.ValidateVitals(workflowRequest));
        trace.Add(new("ValidateVitalsTool", validation.Value.Count == 0 ? "Completed" : "FailedSafely",
            validation.Value.Count == 0, validation.Value.Count == 0 ? "Patient input passed deterministic validation." : "Input was rejected by deterministic safety validation.", validation.DurationMs,
            ErrorCode: validation.Value.Count == 0 ? null : "InvalidOrSuspiciousInput"));
        if (validation.Value.Count > 0)
            return Result(TriageLevels.InsufficientInformation, "Clinical assessment required", true, true,
                validation.Value, [], [], [], null, [], trace);

        var rawFlags = Timed("EvaluateRedFlagsTool", () => ClinicalSafetyTools.EvaluateRedFlags(workflowRequest.Symptoms));
        trace.Add(new("EvaluateRedFlagsTool", rawFlags.Value.Status, true, "Deterministic red-flag policy evaluated the patient report.", rawFlags.DurationMs));
        if (rawFlags.Value.HasEscalation)
            return Escalated(rawFlags.Value, null, trace);

        var modelWatch = Stopwatch.StartNew();
        var extracted = await extraction.ExtractAsync(workflowRequest.Symptoms, !workflowRequest.IsFollowUp, cancellationToken);
        modelWatch.Stop();
        trace.Add(new("GeminiStructuredExtractionTool", extracted.Status,
            extracted.Status == "Completed", extracted.Status == "Completed"
                ? "Gemini returned structured, non-diagnostic facts."
                : "Gemini was unavailable or invalid; controlled heuristic fallback was used.",
            (int)modelWatch.ElapsedMilliseconds, extracted.Status == "Completed" ? 0 : 1, extracted.ErrorCode));

        var grounded = Timed("EvaluateGroundedClinicalFactsTool", () => ClinicalSafetyTools.EvaluateGroundedFacts(extracted.Facts));
        trace.Add(new("EvaluateGroundedClinicalFactsTool", grounded.Value.Status, true,
            "Deterministic policy evaluated only structured facts grounded in patient text.", grounded.DurationMs));

        if (grounded.Value.HasEscalation) return Escalated(grounded.Value, extracted, trace);

        var missing = extracted.MissingInformation.Take(3).ToArray();
        var route = ClinicalSafetyTools.IsRoutine(workflowRequest.Symptoms, extracted.Facts)
            ? "Routine clinical assessment or appointment proposal"
            : "Clinical assessment required";
        var reviewRequired = route == "Clinical assessment required" || request.IsFollowUp;
        return Result(reviewRequired ? TriageLevels.ClinicalReview : TriageLevels.NonUrgent, route, reviewRequired,
            false, [], [], [], [], extracted, missing, trace);
    }

    private static ClinicalSafetyAssessment Escalated(ClinicalFlagSet flags, ClinicalExtractionResult? extraction,
        IReadOnlyList<ClinicalSafetyToolExecution> trace) =>
        Result(flags.Emergency.Count > 0 ? TriageLevels.Emergency : flags.Urgent.Count > 0 ? TriageLevels.Urgent : TriageLevels.ClinicalReview,
            flags.Emergency.Count > 0 ? "Emergency Department" : flags.Urgent.Count > 0 ? "Urgent medical assessment" : "Professional clinical review",
            true, false, [], flags.Emergency, flags.Urgent, flags.ClinicalReview, extraction, [], trace);

    private static ClinicalSafetyAssessment Result(string level, string route, bool review, bool failed,
        IReadOnlyList<string> validation, IReadOnlyList<string> emergency, IReadOnlyList<string> urgent,
        IReadOnlyList<string> clinicalReview, ClinicalExtractionResult? extraction, IReadOnlyList<string> missing,
        IReadOnlyList<ClinicalSafetyToolExecution> trace) =>
        new(level, route, review, failed, validation, emergency, urgent, clinicalReview, extraction, missing, trace);

    private static (T Value, int DurationMs) Timed<T>(string tool, Func<T> operation)
    {
        var watch = Stopwatch.StartNew();
        var value = operation();
        watch.Stop();
        return (value, (int)watch.ElapsedMilliseconds);
    }

}
