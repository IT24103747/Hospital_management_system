using System.Text.Json;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Api.Services;

/// <summary>
/// Safety-first prototype workflow. Clinical rules are configured, versioned and deterministic;
/// no LLM is trusted to make a clinical decision or execute an action.
/// </summary>
public sealed class TriageWorkflowService : ITriageWorkflowService
{
    private const string RuleSetVersion = "safetriage-rules-v1";
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TriageWorkflowService> _logger;
    private readonly IClinicalInformationExtractionAgent _extractionAgent;

    // These reviewable phrases are deliberately configuration-like safety triggers, not diagnostic thresholds.
    private static readonly string[] EmergencyPhrases =
    [
        "severe chest pain", "difficulty breathing", "cannot breathe", "loss of consciousness",
        "unconscious", "signs of stroke", "face drooping", "severe bleeding", "seizure",
        "severe allergic reaction", "severe confusion"
    ];
    // Versioned escalation phrase requiring timely professional assessment.
    // Emergency combinations are still handled by EmergencyPhrases above.
    private static readonly string[] UrgentPhrases = ["chest pain", "chest discomfort"];

    public TriageWorkflowService(ApplicationDbContext db, ILogger<TriageWorkflowService> logger, IClinicalInformationExtractionAgent? extractionAgent = null)
    {
        _db = db;
        _logger = logger;
        _extractionAgent = extractionAgent ?? new SafeFallbackClinicalInformationExtractionAgent();
    }

    public async Task<TriageWorkflowDto> StartForPatientAsync(int patientId, StartTriageWorkflowDto request)
    {
        var workflow = new TriageWorkflow
        {
            PatientId = patientId,
            Symptoms = request.Symptoms.Trim(),
            VitalsJson = request.Vitals is null ? null : JsonSerializer.Serialize(request.Vitals),
            PlanJson = JsonSerializer.Serialize(CreatePlan()),
            RuleSetVersion = RuleSetVersion,
        };
        _db.TriageWorkflows.Add(workflow);
        await AddEvent(workflow, "IntakeValidationAgent", "Started", new { source = "patient-reported" });

        var validationProblems = Validate(request);
        var redFlags = FindRedFlags(request.Symptoms);
        var urgentFlags = FindUrgentFlags(request.Symptoms);
        var riskFactors = new List<string>();
        var missingInformation = new List<string>();
        TriageGuidanceDto? guidance = null;

        if (request.Vitals is null) missingInformation.Add("No verified vital signs were supplied.");
        if (validationProblems.Count > 0)
        {
            workflow.Status = TriageWorkflowStatuses.FailedSafely;
            workflow.TriageLevel = TriageLevels.InsufficientInformation;
            workflow.UncertaintyState = TriageUncertaintyStates.LimitedInformation;
            workflow.RequiresHumanReview = true;
            workflow.ErrorCode = "InvalidOrSuspiciousInput";
            workflow.FinalOutcome = "The system cannot safely assess this situation with the available information. Please seek assessment from a qualified healthcare professional.";
            missingInformation.AddRange(validationProblems);
            await AddEvent(workflow, "IntakeValidationAgent", "FailedSafely", new { validationProblems });
        }
        else if (redFlags.Count > 0)
        {
            workflow.Status = TriageWorkflowStatuses.PendingClinicalReview;
            workflow.ApprovalStatus = TriageApprovalStatuses.Pending;
            workflow.TriageLevel = TriageLevels.Emergency;
            workflow.UncertaintyState = TriageUncertaintyStates.HumanReviewRequired;
            workflow.RequiresHumanReview = true;
            riskFactors.Add("Configured emergency red-flag phrase detected in patient-reported symptoms.");
            workflow.FinalOutcome = "Emergency escalation was triggered because submitted symptoms matched configured emergency criteria. Seek immediate emergency evaluation; this decision-support result is not a diagnosis.";
            await AddEvent(workflow, "SafetyRedFlagAgent", "EmergencyEscalation", new { redFlags, ruleSetVersion = RuleSetVersion });
            await AddEvent(workflow, "CareRoutingAgent", "Proposed", new { route = "Emergency Department", requiresClinicalApproval = true });
        }
        else if (urgentFlags.Count > 0)
        {
            workflow.Status = TriageWorkflowStatuses.PendingClinicalReview;
            workflow.ApprovalStatus = TriageApprovalStatuses.Pending;
            workflow.TriageLevel = TriageLevels.Urgent;
            workflow.UncertaintyState = TriageUncertaintyStates.HumanReviewRequired;
            workflow.RequiresHumanReview = true;
            riskFactors.Add("Configured urgent-assessment phrase detected in patient-reported symptoms.");
            workflow.FinalOutcome = "Chest pain requires urgent medical assessment. Do not wait for the clinical-review status. Seek urgent care now. If the pain is sudden, severe, persistent, spreads to the arm, neck, jaw, stomach, or back, or occurs with breathlessness, sweating, sickness, or light-headedness, seek emergency care immediately.";
            await AddEvent(workflow, "SafetyRedFlagAgent", "UrgentAssessment", new { urgentFlags, ruleSetVersion = RuleSetVersion });
            await AddEvent(workflow, "CareRoutingAgent", "Proposed", new { route = "Urgent medical assessment", requiresClinicalApproval = true });
        }
        else
        {
            var extraction = await _extractionAgent.ExtractAsync(request.Symptoms, !request.IsFollowUp);
            workflow.Status = TriageWorkflowStatuses.Completed;
            workflow.TriageLevel = TriageLevels.InsufficientInformation;
            workflow.UncertaintyState = TriageUncertaintyStates.LimitedInformation;
            workflow.RequiresHumanReview = true;
            guidance = MapGuidance(extraction.Guidance) ?? GetControlledGuidance(extraction.Symptoms, request.Symptoms);
            var hasValidatedGuidance = extraction.Status == "Completed" && guidance is not null;
            if (hasValidatedGuidance)
            {
                // This is a controlled information route, not a clinical diagnosis or clearance.
                // Emergency routing remains exclusively deterministic above.
                workflow.TriageLevel = TriageLevels.NonUrgent;
                workflow.UncertaintyState = TriageUncertaintyStates.LimitedInformation;
                workflow.RequiresHumanReview = false;
                if (!request.IsFollowUp && guidance!.FollowUpQuestions.Count > 0)
                    workflow.Status = TriageWorkflowStatuses.PendingPatientInput;
            }
            else
            {
                workflow.TriageLevel = TriageLevels.InsufficientInformation;
                workflow.UncertaintyState = TriageUncertaintyStates.LimitedInformation;
                workflow.RequiresHumanReview = true;
            }
            workflow.FinalOutcome = guidance is null
                ? "No configured emergency red flag was detected. This system cannot determine a diagnosis or clinical urgency from symptoms alone. Please obtain assessment from a qualified healthcare professional."
                : "No configured emergency red flag was detected. This is a non-urgent general-information route based on limited patient-reported information; it cannot identify the cause, confirm safety, or make a diagnosis.";
            missingInformation.AddRange(extraction.MissingInformation);
            missingInformation.Add("Clinical assessment is required; no diagnosis is generated by this workflow.");
            riskFactors.AddRange(extraction.Symptoms.Select(item => $"Patient-reported symptom extracted: {item}"));
            if (extraction.Status == "FailedSafely") workflow.ErrorCode = extraction.ErrorCode;
            await AddEvent(workflow, "ClinicalInformationExtractionAgent", extraction.Status, new { tool = "OllamaLocalModel", structuredOutputValidated = extraction.Status == "Completed", request.IsFollowUp, extraction.ErrorCode });
            await AddEvent(workflow, "RiskAssessmentAgent", hasValidatedGuidance ? "ControlledNonUrgentInformationRoute" : "ConservativeFallback", new { triageLevel = workflow.TriageLevel, structuredGuidanceValidated = hasValidatedGuidance });
            await AddEvent(workflow, "CareRoutingAgent", "Proposed", new { route = hasValidatedGuidance ? "Self-care information with safety-netting" : "General clinical assessment", requiresClinicalApproval = !hasValidatedGuidance });
            await AddEvent(workflow, "SafetyValidationAgent", "Accepted", new { requiresHumanReview = !hasValidatedGuidance });
        }

        workflow.PlanJson = JsonSerializer.Serialize(CreateCompletedPlan(workflow.Status, redFlags.Count > 0 || urgentFlags.Count > 0));
        workflow.ResultJson = JsonSerializer.Serialize(new { riskFactors, redFlags, urgentFlags, missingInformation, guidance, workflow.FinalOutcome });
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation("SafeTriage workflow {WorkflowId} created for patient {PatientId} with status {Status}", workflow.TriageWorkflowId, patientId, workflow.Status);
        return Map(workflow, riskFactors, redFlags, missingInformation, guidance);
    }

    public async Task<TriageWorkflowDto?> ContinueForPatientAsync(int workflowId, int patientId, ContinueTriageWorkflowDto request)
    {
        var workflow = await _db.TriageWorkflows.SingleOrDefaultAsync(x => x.TriageWorkflowId == workflowId && x.PatientId == patientId && x.Status == TriageWorkflowStatuses.PendingPatientInput);
        if (workflow is null) return null;

        var combinedInput = $"{workflow.Symptoms}\n\nAdditional patient-reported details:\n{request.Answers.Trim()}";
        var redFlags = FindRedFlags(combinedInput);
        var urgentFlags = FindUrgentFlags(combinedInput);
        var risks = new List<string>();
        var missing = new List<string>();
        TriageGuidanceDto? guidance = null;
        workflow.Symptoms = combinedInput;
        if (redFlags.Count > 0 || urgentFlags.Count > 0)
        {
            workflow.Status = TriageWorkflowStatuses.PendingClinicalReview;
            workflow.ApprovalStatus = TriageApprovalStatuses.Pending;
            workflow.TriageLevel = redFlags.Count > 0 ? TriageLevels.Emergency : TriageLevels.Urgent;
            workflow.UncertaintyState = TriageUncertaintyStates.HumanReviewRequired;
            workflow.RequiresHumanReview = true;
            workflow.FinalOutcome = redFlags.Count > 0
                ? "Emergency escalation was triggered from additional information. Seek immediate emergency evaluation; do not wait for clinical review."
                : "Additional information requires urgent medical assessment. Do not wait for clinical review.";
            await AddEvent(workflow, "SafetyRedFlagAgent", redFlags.Count > 0 ? "EmergencyEscalationAfterPatientInput" : "UrgentAssessmentAfterPatientInput", new { redFlags, urgentFlags });
        }
        else
        {
            var extraction = await _extractionAgent.ExtractAsync(combinedInput, includeFollowUpQuestions: false);
            guidance = MapGuidance(extraction.Guidance);
            workflow.Status = TriageWorkflowStatuses.Completed;
            workflow.TriageLevel = guidance is null ? TriageLevels.InsufficientInformation : TriageLevels.NonUrgent;
            workflow.UncertaintyState = TriageUncertaintyStates.LimitedInformation;
            workflow.RequiresHumanReview = guidance is null;
            workflow.FinalOutcome = guidance is null
                ? "The system cannot safely provide general guidance with the available information. Please seek assessment from a qualified healthcare professional."
                : "Your additional details were processed. This is final general guidance for this assessment; it is not a diagnosis.";
            missing.AddRange(extraction.MissingInformation);
            await AddEvent(workflow, "ClinicalInformationExtractionAgent", extraction.Status, new { resumedWorkflow = true, structuredOutputValidated = extraction.Status == "Completed" });
            await AddEvent(workflow, "SafetyValidationAgent", "FinalGuidanceAccepted", new { workflow.TriageLevel });
        }
        workflow.PlanJson = JsonSerializer.Serialize(CreateCompletedPlan(workflow.Status, redFlags.Count > 0 || urgentFlags.Count > 0));
        workflow.ResultJson = JsonSerializer.Serialize(new { riskFactors = risks, redFlags, urgentFlags, missingInformation = missing, guidance, workflow.FinalOutcome });
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(workflow, risks, redFlags, missing, guidance);
    }

    public async Task<TriageWorkflowDto?> GetForPatientAsync(int workflowId, int patientId)
    {
        var workflow = await _db.TriageWorkflows.AsNoTracking().SingleOrDefaultAsync(x => x.TriageWorkflowId == workflowId && x.PatientId == patientId);
        return workflow is null ? null : Map(workflow);
    }

    public async Task<TriageWorkflowDto?> GetForClinicalReviewerAsync(int workflowId)
    {
        var workflow = await _db.TriageWorkflows.AsNoTracking().SingleOrDefaultAsync(x => x.TriageWorkflowId == workflowId);
        return workflow is null ? null : Map(workflow);
    }

    public async Task<IReadOnlyList<TriageWorkflowDto>> GetPendingClinicalReviewsAsync()
    {
        var workflows = await _db.TriageWorkflows.AsNoTracking()
            .Where(x => x.ApprovalStatus == TriageApprovalStatuses.Pending)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
        return workflows.Select(workflow => Map(workflow)).ToList();
    }

    public async Task<IReadOnlyList<TriageWorkflowEventDto>?> GetAuditEventsAsync(int workflowId)
    {
        var exists = await _db.TriageWorkflows.AsNoTracking().AnyAsync(x => x.TriageWorkflowId == workflowId);
        if (!exists) return null;
        return await _db.TriageWorkflowEvents.AsNoTracking()
            .Where(x => x.TriageWorkflowId == workflowId)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new TriageWorkflowEventDto { Stage = x.Stage, EventType = x.EventType, CreatedAt = x.CreatedAt })
            .ToListAsync();
    }

    public async Task<TriageWorkflowDto?> ReviewAsync(int workflowId, int reviewerUserId, ReviewTriageWorkflowDto request)
    {
        var workflow = await _db.TriageWorkflows.SingleOrDefaultAsync(x => x.TriageWorkflowId == workflowId);
        if (workflow is null || workflow.ApprovalStatus != TriageApprovalStatuses.Pending) return null;

        var decision = request.Decision.Trim();
        if (decision is not (TriageApprovalStatuses.Approved or TriageApprovalStatuses.Rejected or TriageApprovalStatuses.RevisionRequested))
            throw new ArgumentException("Decision must be Approved, Rejected, or RevisionRequested.");

        workflow.ApprovalStatus = decision;
        workflow.ReviewedByUserId = reviewerUserId;
        workflow.ReviewedAt = DateTime.UtcNow;
        workflow.UpdatedAt = DateTime.UtcNow;
        workflow.Status = decision == TriageApprovalStatuses.RevisionRequested ? TriageWorkflowStatuses.PendingClinicalReview : TriageWorkflowStatuses.Completed;
        workflow.FinalOutcome = decision switch
        {
            TriageApprovalStatuses.Approved => "A clinical reviewer approved the emergency escalation recommendation.",
            TriageApprovalStatuses.Rejected => "A clinical reviewer did not approve the proposed escalation. Contact the care team for further guidance.",
            _ => "A clinical reviewer requested additional information before a decision can be made."
        };
        await AddEvent(workflow, "HumanClinicalReview", decision, new { note = request.Note?.Trim(), reviewerUserId });
        await _db.SaveChangesAsync();
        return Map(workflow);
    }

    private async Task AddEvent(TriageWorkflow workflow, string stage, string eventType, object details)
        => await _db.TriageWorkflowEvents.AddAsync(new TriageWorkflowEvent { TriageWorkflow = workflow, Stage = stage, EventType = eventType, DetailsJson = JsonSerializer.Serialize(details) });

    private static List<string> Validate(StartTriageWorkflowDto request)
    {
        var errors = new List<string>();
        var vitals = request.Vitals;
        if (vitals is null) return errors;
        if (vitals.TemperatureCelsius is < 25 or > 45) errors.Add("Temperature is outside the accepted input range.");
        if (vitals.HeartRateBpm is < 20 or > 300) errors.Add("Heart rate is outside the accepted input range.");
        if (vitals.SystolicBloodPressure is < 40 or > 300 || vitals.DiastolicBloodPressure is < 20 or > 200) errors.Add("Blood pressure is outside the accepted input range.");
        if (vitals.SystolicBloodPressure.HasValue && vitals.DiastolicBloodPressure.HasValue && vitals.SystolicBloodPressure <= vitals.DiastolicBloodPressure) errors.Add("Blood pressure values are contradictory.");
        if (vitals.OxygenSaturationPercent is < 1 or > 100) errors.Add("Oxygen saturation must be between 1 and 100 percent.");
        if (vitals.ObservedAt > DateTime.UtcNow.AddMinutes(5)) errors.Add("Vital-sign observation time cannot be in the future.");
        return errors;
    }

    private static List<string> FindRedFlags(string symptoms) => EmergencyPhrases.Where(phrase => symptoms.Contains(phrase, StringComparison.OrdinalIgnoreCase)).ToList();
    private static List<string> FindUrgentFlags(string symptoms) => UrgentPhrases.Where(phrase => symptoms.Contains(phrase, StringComparison.OrdinalIgnoreCase)).ToList();
    private static IReadOnlyList<TriagePlanStepDto> CreatePlan() =>
    [
        new() { Agent = "IntakeValidationAgent", Status = "Planned", Purpose = "Validate patient-reported inputs and vital-sign integrity." },
        new() { Agent = "ClinicalInformationExtractionAgent", Status = "Planned", Purpose = "Structure patient-reported information without inventing facts." },
        new() { Agent = "SafetyRedFlagAgent", Status = "Planned", Purpose = "Apply versioned deterministic emergency rules." },
        new() { Agent = "RiskAssessmentAgent", Status = "Planned", Purpose = "Propose only controlled triage categories." },
        new() { Agent = "CareRoutingAgent", Status = "Planned", Purpose = "Propose an approved care path; never book or prescribe." },
        new() { Agent = "SafetyValidationAgent", Status = "Planned", Purpose = "Validate output and enforce escalation/approval rules." },
    ];

    private static IReadOnlyList<TriagePlanStepDto> CreateCompletedPlan(string workflowStatus, bool emergency) =>
    [
        new() { Agent = "IntakeValidationAgent", Status = workflowStatus == TriageWorkflowStatuses.FailedSafely ? "FailedSafely" : "Completed", Purpose = "Validated patient-reported inputs and vital-sign integrity." },
        new() { Agent = "ClinicalInformationExtractionAgent", Status = emergency || workflowStatus == TriageWorkflowStatuses.FailedSafely ? "NotRun" : "Completed", Purpose = "Structured patient-reported information without inventing facts." },
        new() { Agent = "SafetyRedFlagAgent", Status = workflowStatus == TriageWorkflowStatuses.FailedSafely ? "NotRun" : "Completed", Purpose = "Applied versioned deterministic emergency rules." },
        new() { Agent = "RiskAssessmentAgent", Status = workflowStatus == TriageWorkflowStatuses.FailedSafely ? "NotRun" : "Completed", Purpose = "Proposed only controlled triage categories." },
        new() { Agent = "CareRoutingAgent", Status = workflowStatus == TriageWorkflowStatuses.FailedSafely ? "NotRun" : "Completed", Purpose = "Proposed an approved care path; never booked or prescribed." },
        new() { Agent = "SafetyValidationAgent", Status = workflowStatus == TriageWorkflowStatuses.FailedSafely ? "NotRun" : "Completed", Purpose = "Validated output and enforced escalation/approval rules." },
    ];

    private static TriageGuidanceDto? GetControlledGuidance(IEnumerable<string> extractedSymptoms, string originalText)
    {
        var text = string.Join(' ', extractedSymptoms.Append(originalText));
        if (!text.Contains("runny nose", StringComparison.OrdinalIgnoreCase) && !text.Contains("blocked nose", StringComparison.OrdinalIgnoreCase) && !text.Contains("nasal congestion", StringComparison.OrdinalIgnoreCase)) return null;
        return new TriageGuidanceDto
        {
            Heading = "General information for a runny or blocked nose",
            Actions = ["Rest and drink fluids if you are able to.", "Avoid smoking and other irritants.", "A pharmacist can advise on symptom-relief options that are suitable for you."],
            SeekHelpIf = ["You develop difficulty breathing, chest pain, severe confusion, or a sudden severe deterioration — seek emergency care.", "Symptoms worsen, you have a persistent high temperature, or they do not improve after about 10 days — arrange professional assessment.", "Seek earlier advice if you are pregnant, immunocompromised, or have a significant long-term condition."],
            FollowUpQuestions = ["When did this begin?", "Do you also have fever, cough, sore throat, facial pain, or breathing difficulty?", "Do you have a long-term condition, weakened immune system, or pregnancy that may affect care?"],
            EvidenceSource = "NHS Common cold guidance (reviewed 22 March 2024): https://www.nhs.uk/conditions/common-cold/"
        };
    }

    private static TriageGuidanceDto? MapGuidance(PatientGuidance? guidance)
    {
        if (guidance is null) return null;
        return new TriageGuidanceDto
        {
            Heading = "General guidance based on what you reported",
            Summary = guidance.Summary,
            Actions = guidance.GeneralActions,
            SeekHelpIf = guidance.SafetyNetting,
            FollowUpQuestions = guidance.FollowUpQuestions,
            EvidenceSource = "General decision-support information. This is not a diagnosis or personalized treatment plan."
        };
    }

    private static TriageWorkflowDto Map(TriageWorkflow workflow, List<string>? risks = null, List<string>? flags = null, List<string>? missing = null, TriageGuidanceDto? guidance = null)
    {
        if (risks is null || flags is null || missing is null)
        {
            using var result = JsonDocument.Parse(workflow.ResultJson);
            risks = result.RootElement.TryGetProperty("riskFactors", out var riskElement) ? riskElement.Deserialize<List<string>>() ?? [] : [];
            flags = result.RootElement.TryGetProperty("redFlags", out var flagElement) ? flagElement.Deserialize<List<string>>() ?? [] : [];
            missing = result.RootElement.TryGetProperty("missingInformation", out var missingElement) ? missingElement.Deserialize<List<string>>() ?? [] : [];
            guidance = result.RootElement.TryGetProperty("guidance", out var guidanceElement) && guidanceElement.ValueKind != JsonValueKind.Null ? guidanceElement.Deserialize<TriageGuidanceDto>() : null;
        }
        return new TriageWorkflowDto { WorkflowId = workflow.TriageWorkflowId, Status = workflow.Status, ApprovalStatus = workflow.ApprovalStatus, TriageLevel = workflow.TriageLevel, UncertaintyState = workflow.UncertaintyState, RequiresHumanReview = workflow.RequiresHumanReview, PatientMessage = workflow.FinalOutcome ?? "The system cannot safely assess this situation.", Guidance = guidance, RiskFactors = risks, RedFlags = flags, MissingInformation = missing, Plan = JsonSerializer.Deserialize<List<TriagePlanStepDto>>(workflow.PlanJson) ?? [], RuleSetVersion = workflow.RuleSetVersion, CreatedAt = workflow.CreatedAt };
    }
}
