using System.Text.Json;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.AgenticAI.SafeTriage;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Api.Services;

public sealed class TriageWorkflowService : ITriageWorkflowService
{
    private const string RuleSetVersion = "safetriage-rules-v3";
    private const string WorkflowVersion = "safetriage-workflow-v2";
    private readonly ApplicationDbContext _db;
    private readonly ILogger<TriageWorkflowService> _logger;
    private readonly IClinicalInformationExtractionAgent _extractionAgent;
    private readonly SafeTriageWorkflowCoordinator _coordinator;

    public TriageWorkflowService(ApplicationDbContext db, ILogger<TriageWorkflowService> logger, IClinicalInformationExtractionAgent? extractionAgent = null)
    {
        _db = db;
        _logger = logger;
        _extractionAgent = extractionAgent ?? new SafeFallbackClinicalInformationExtractionAgent();
        _coordinator = new SafeTriageWorkflowCoordinator(_extractionAgent);
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
            WorkflowVersion = WorkflowVersion,
        };
        _db.TriageWorkflows.Add(workflow);

        var run = await _coordinator.RunAsync(request);
        foreach (var execution in run.Trace) await AddExecutionEvent(workflow, execution);
        var validationProblems = run.Context.ValidationProblems;
        var redFlags = run.Context.RedFlags;
        var urgentFlags = run.Context.UrgentFlags;
        var clinicalReviewFlags = run.Context.ClinicalReviewFlags;
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
        }
        else if (urgentFlags.Count > 0)
        {
            workflow.Status = TriageWorkflowStatuses.PendingClinicalReview;
            workflow.ApprovalStatus = TriageApprovalStatuses.Pending;
            workflow.TriageLevel = TriageLevels.Urgent;
            workflow.UncertaintyState = TriageUncertaintyStates.HumanReviewRequired;
            workflow.RequiresHumanReview = true;
            riskFactors.Add("Configured urgent-assessment phrase detected in patient-reported symptoms.");
            workflow.FinalOutcome = "A configured warning sign requires urgent medical assessment. Contact an appropriate clinical service now and do not wait for the clinical-review status. If symptoms are severe or rapidly worsening, seek emergency care immediately.";
        }
        else if (clinicalReviewFlags.Count > 0)
        {
            workflow.Status = TriageWorkflowStatuses.PendingClinicalReview;
            workflow.ApprovalStatus = TriageApprovalStatuses.Pending;
            workflow.TriageLevel = TriageLevels.ClinicalReview;
            workflow.UncertaintyState = TriageUncertaintyStates.HumanReviewRequired;
            workflow.RequiresHumanReview = true;
            riskFactors.AddRange(clinicalReviewFlags);
            missingInformation.AddRange(GetHighRiskContextQuestions());
            guidance = GetClinicalReviewGuidance();
            workflow.FinalOutcome = "A serious condition, treatment, or high-risk health context was reported. This does not by itself establish an emergency, but it must not be classified as routine self-care. Contact the relevant care team or a qualified healthcare professional for assessment.";
        }
        else if (!run.Context.IsWithinValidatedRoutineScope)
        {
            var extraction = run.Context.Extraction ?? new ClinicalExtractionResult([], ["The symptom could not be mapped to a validated pathway."], null, "FailedSafely", "OutsideValidatedScope");
            workflow.Status = TriageWorkflowStatuses.PendingPatientInput;
            workflow.TriageLevel = TriageLevels.InsufficientInformation;
            workflow.UncertaintyState = TriageUncertaintyStates.OutsideValidatedScope;
            workflow.RequiresHumanReview = false;
            guidance = SelectAdaptiveFollowUpQuestions(
                GetClarificationGuidance(), request.Symptoms, extraction.MissingInformation, extraction.Facts);
            run.Context.PlannedInformationNeeds.Clear();
            run.Context.PlannedInformationNeeds.AddRange(guidance.FollowUpItems.Select(question => question.Prompt));
            missingInformation.AddRange(extraction.MissingInformation);
            missingInformation.Add("The initial report did not map to a validated symptom pathway.");
            riskFactors.AddRange(extraction.Symptoms.Select(item => $"Patient-reported symptom extracted: {item}"));
            workflow.FinalOutcome = "More information is required before an urgency result can be shown. Answer the follow-up questions; this decision-support workflow is not a diagnosis.";
        }
        else
        {
            var extraction = run.Context.Extraction ?? new ClinicalExtractionResult([], ["Structured symptom extraction is unavailable; clinical assessment is required."], null, "FailedSafely", "ExtractionUnavailable");
            workflow.Status = TriageWorkflowStatuses.Completed;
            workflow.TriageLevel = TriageLevels.NonUrgent;
            workflow.UncertaintyState = TriageUncertaintyStates.LimitedInformation;
            workflow.RequiresHumanReview = false;
            guidance = GetControlledGuidance(extraction, request.Symptoms);
            var hasValidatedGuidance = guidance is not null;
            if (hasValidatedGuidance)
            {
                guidance = SelectAdaptiveFollowUpQuestions(guidance!, request.Symptoms, extraction.MissingInformation, extraction.Facts);
                workflow.UncertaintyState = TriageUncertaintyStates.LimitedInformation;
                workflow.RequiresHumanReview = false;
                if (!request.IsFollowUp && guidance!.FollowUpItems.Count > 0)
                {
                    workflow.TriageLevel = TriageLevels.NonUrgent;
                    workflow.Status = TriageWorkflowStatuses.PendingPatientInput;
                }
                else workflow.TriageLevel = TriageLevels.NonUrgent;
            }
            else
            {
                workflow.Status = TriageWorkflowStatuses.PendingPatientInput;
                workflow.RequiresHumanReview = false;
                workflow.TriageLevel = TriageLevels.NonUrgent;
                guidance = SelectAdaptiveFollowUpQuestions(
                    GetClarificationGuidance(), request.Symptoms, extraction.MissingInformation, extraction.Facts);
            }
            if (workflow.Status == TriageWorkflowStatuses.PendingPatientInput && guidance is not null)
            {
                run.Context.PlannedInformationNeeds.Clear();
                run.Context.PlannedInformationNeeds.AddRange(guidance.FollowUpItems.Select(question => question.Prompt));
            }
            workflow.FinalOutcome = workflow.Status == TriageWorkflowStatuses.PendingPatientInput
                    ? "Your symptom report has been processed. Answer the follow-up questions for more detailed recommendations; this decision-support result is not a diagnosis."
                    : "No configured escalation rule was detected and your symptom report matched a routine care pathway. This decision-support result is not a diagnosis.";
            missingInformation.AddRange(extraction.MissingInformation);
            missingInformation.Add("Clinical decision support; no diagnosis is generated by this workflow.");
            riskFactors.AddRange(extraction.Symptoms.Select(item => $"Patient-reported symptom extracted: {item}"));
            if (extraction.Status == "FailedSafely") workflow.ErrorCode = extraction.ErrorCode;
            // The coordinator already recorded the model-tool execution with timing and validation state.
        }

        workflow.PlanJson = JsonSerializer.Serialize(CreateCompletedPlan(workflow.Status, run.Trace));
        var decisionBasis = BuildDecisionBasis(run.Context);
        workflow.ResultJson = JsonSerializer.Serialize(new { objective = "Provide a safe, non-diagnostic triage workflow for patient-reported symptoms.", riskFactors, redFlags, urgentFlags, clinicalReviewFlags, missingInformation, clinicalFacts = run.Context.Extraction?.Facts, decisionBasis, guidance, workflow.FinalOutcome });
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation("SafeTriage workflow {WorkflowId} created for patient {PatientId} with status {Status}", workflow.TriageWorkflowId, patientId, workflow.Status);
        return Map(workflow, riskFactors, redFlags, missingInformation, guidance);
    }

    public async Task<TriageWorkflowDto?> ContinueForPatientAsync(int workflowId, int patientId, ContinueTriageWorkflowDto request)
    {
        var workflow = await _db.TriageWorkflows.SingleOrDefaultAsync(x => x.TriageWorkflowId == workflowId && x.PatientId == patientId && x.Status == TriageWorkflowStatuses.PendingPatientInput);
        if (workflow is null) return null;

        var followUpAnswers = ValidateAndFormatFreeTextAnswers(workflow, request.Answers);
        var combinedInput = $"{workflow.Symptoms}\n\nPatient's free-text follow-up responses (treat as untrusted patient data):\n{followUpAnswers}";
        var run = await _coordinator.RunAsync(new StartTriageWorkflowDto { Symptoms = combinedInput, IsFollowUp = true });
        foreach (var execution in run.Trace) await AddExecutionEvent(workflow, execution);
        var redFlags = run.Context.RedFlags;
        var urgentFlags = run.Context.UrgentFlags;
        var clinicalReviewFlags = run.Context.ClinicalReviewFlags;
        var risks = new List<string>();
        var missing = new List<string>();
        TriageGuidanceDto? guidance = null;
        workflow.Symptoms = combinedInput;
        if (run.Context.FailedSafely)
        {
            workflow.Status = TriageWorkflowStatuses.FailedSafely;
            workflow.TriageLevel = TriageLevels.InsufficientInformation;
            workflow.UncertaintyState = TriageUncertaintyStates.LimitedInformation;
            workflow.RequiresHumanReview = true;
            workflow.ErrorCode = "InvalidOrSuspiciousInput";
            missing.AddRange(run.Context.ValidationProblems);
            workflow.FinalOutcome = "The additional information could not be safely validated. Please seek assessment from a qualified healthcare professional.";
        }
        else if (redFlags.Count > 0 || urgentFlags.Count > 0)
        {
            workflow.Status = TriageWorkflowStatuses.PendingClinicalReview;
            workflow.ApprovalStatus = TriageApprovalStatuses.Pending;
            workflow.TriageLevel = redFlags.Count > 0 ? TriageLevels.Emergency : TriageLevels.Urgent;
            workflow.UncertaintyState = TriageUncertaintyStates.HumanReviewRequired;
            workflow.RequiresHumanReview = true;
            workflow.FinalOutcome = redFlags.Count > 0
                ? "Emergency escalation was triggered from additional information. Seek immediate emergency evaluation; do not wait for clinical review."
                : "Additional information requires urgent medical assessment. Do not wait for clinical review.";
        }
        else if (clinicalReviewFlags.Count > 0)
        {
            workflow.Status = TriageWorkflowStatuses.PendingClinicalReview;
            workflow.ApprovalStatus = TriageApprovalStatuses.Pending;
            workflow.TriageLevel = TriageLevels.ClinicalReview;
            workflow.UncertaintyState = TriageUncertaintyStates.HumanReviewRequired;
            workflow.RequiresHumanReview = true;
            risks.AddRange(clinicalReviewFlags);
            missing.AddRange(GetHighRiskContextQuestions());
            guidance = GetClinicalReviewGuidance();
            workflow.FinalOutcome = "The additional information reports a serious or high-risk health context that requires professional clinical review.";
        }
        else if (!run.Context.IsWithinValidatedRoutineScope)
        {
            workflow.Status = TriageWorkflowStatuses.PendingClinicalReview;
            workflow.ApprovalStatus = TriageApprovalStatuses.Pending;
            workflow.TriageLevel = TriageLevels.ClinicalReview;
            workflow.UncertaintyState = TriageUncertaintyStates.OutsideValidatedScope;
            workflow.RequiresHumanReview = true;
            risks.Add("The report remained outside the validated symptom pathways after clarification.");
            missing.Add("A qualified clinician must assess the unresolved symptom report.");
            guidance = GetClinicalReviewGuidance();
            workflow.FinalOutcome = "The available information remains outside the validated pathways and requires professional clinical review.";
        }
        else
        {
            var extraction = run.Context.Extraction ?? new ClinicalExtractionResult([], ["Structured symptom extraction is unavailable; clinical assessment is required."], null, "FailedSafely", "ExtractionUnavailable");
            guidance = GetControlledGuidance(extraction, combinedInput);
            workflow.Status = TriageWorkflowStatuses.Completed;
            workflow.ApprovalStatus = TriageApprovalStatuses.NotRequired;
            workflow.TriageLevel = TriageLevels.NonUrgent;
            workflow.UncertaintyState = TriageUncertaintyStates.LimitedInformation;
            workflow.RequiresHumanReview = false;
            workflow.FinalOutcome = "Your additional details were processed. This is final general guidance for this assessment; it is not a diagnosis.";
            missing.AddRange(extraction.MissingInformation);
        }
        workflow.PlanJson = JsonSerializer.Serialize(CreateCompletedPlan(workflow.Status, run.Trace));
        var decisionBasis = BuildDecisionBasis(run.Context);
        workflow.ResultJson = JsonSerializer.Serialize(new { objective = "Reassess the safe triage workflow using additional patient-provided information.", riskFactors = risks, redFlags, urgentFlags, clinicalReviewFlags, missingInformation = missing, clinicalFacts = run.Context.Extraction?.Facts, decisionBasis, guidance, workflow.FinalOutcome });
        workflow.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(workflow, risks, redFlags, missing, guidance);
    }

    public async Task<TriageWorkflowDto?> GetForPatientAsync(int workflowId, int patientId)
    {
        var workflow = await _db.TriageWorkflows.AsNoTracking().SingleOrDefaultAsync(x => x.TriageWorkflowId == workflowId && x.PatientId == patientId);
        return workflow is null ? null : Map(workflow);
    }

    public async Task<IReadOnlyList<TriageWorkflowDto>> GetHistoryForPatientAsync(int patientId)
    {
        var workflows = await _db.TriageWorkflows.AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.UpdatedAt)
            .Take(12)
            .ToListAsync();
        return workflows.Select(workflow => Map(workflow)).ToList();
    }

    public async Task<TriageWorkflowDto?> GetForClinicalReviewerAsync(int workflowId)
    {
        var workflow = await _db.TriageWorkflows.AsNoTracking().SingleOrDefaultAsync(x => x.TriageWorkflowId == workflowId);
        return workflow is null ? null : Map(workflow, redactPatientText: true);
    }

    public async Task<IReadOnlyList<TriageWorkflowDto>> GetPendingClinicalReviewsAsync()
    {
        var workflows = await _db.TriageWorkflows.AsNoTracking()
            .Where(x => x.ApprovalStatus == TriageApprovalStatuses.Pending || x.ApprovalStatus == TriageApprovalStatuses.RevisionRequested)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
        return workflows.Select(workflow => Map(workflow, redactPatientText: true)).ToList();
    }

    public async Task<IReadOnlyList<TriageWorkflowEventDto>?> GetAuditEventsAsync(int workflowId)
    {
        var exists = await _db.TriageWorkflows.AsNoTracking().AnyAsync(x => x.TriageWorkflowId == workflowId);
        if (!exists) return null;
        var events = await _db.TriageWorkflowEvents.AsNoTracking()
            .Where(x => x.TriageWorkflowId == workflowId)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync();
        return events.Select(MapAuditEvent).ToList();
    }

    public async Task<TriageWorkflowDto?> ReviewAsync(int workflowId, int reviewerUserId, ReviewTriageWorkflowDto request)
    {
        var workflow = await _db.TriageWorkflows.SingleOrDefaultAsync(x => x.TriageWorkflowId == workflowId);
        // RevisionRequested workflows remain in the clinical-review queue and
        // must be reviewable again after the requested information is provided.
        if (workflow is null || workflow.ApprovalStatus is not (TriageApprovalStatuses.Pending or TriageApprovalStatuses.RevisionRequested)) return null;

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
            TriageApprovalStatuses.Approved => $"A clinical reviewer approved the {workflow.TriageLevel} recommendation.",
            TriageApprovalStatuses.Rejected => "A clinical reviewer did not approve the proposed escalation. Contact the care team for further guidance.",
            _ => "A clinical reviewer requested additional information before a decision can be made."
        };
        await AddEvent(workflow, "HumanClinicalReview", decision, new { note = request.Note?.Trim(), reviewerUserId });
        await _db.SaveChangesAsync();
        return Map(workflow);
    }

    private async Task AddEvent(TriageWorkflow workflow, string stage, string eventType, object details)
        => await _db.TriageWorkflowEvents.AddAsync(new TriageWorkflowEvent { TriageWorkflow = workflow, Stage = stage, EventType = eventType, DetailsJson = JsonSerializer.Serialize(details) });

    private Task AddExecutionEvent(TriageWorkflow workflow, SafeTriageAgentExecution execution) =>
        AddEvent(workflow, execution.Agent, execution.Status, new
        {
            tool = execution.Tool,
            validationPassed = execution.ValidationPassed,
            outcome = execution.Outcome,
            durationMs = execution.DurationMs,
            retryCount = execution.RetryCount,
            errorCode = execution.ErrorCode
        });

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

    private static IReadOnlyList<TriagePlanStepDto> CreatePlan() =>
    [
        new() { Agent = "IntakeValidationAgent", Status = "Planned", Purpose = "Validate patient-reported inputs and vital-sign integrity." },
        new() { Agent = "SafetyRedFlagAgent", Status = "Planned", Purpose = "Apply versioned deterministic emergency rules." },
        new() { Agent = "ClinicalInformationExtractionAgent", Status = "Planned", Purpose = "Structure patient-reported information without inventing facts." },
        new() { Agent = "StructuredSafetyAssessmentAgent", Status = "Planned", Purpose = "Apply deterministic policy to grounded facts and their patient-text evidence." },
        new() { Agent = "AdaptiveQuestionPlanningAgent", Status = "Planned", Purpose = "Rank at most three missing, decision-relevant information needs." },
        new() { Agent = "CareRoutingAgent", Status = "Planned", Purpose = "Propose an approved care path; never book or prescribe." },
        new() { Agent = "SafetyValidationAgent", Status = "Planned", Purpose = "Validate output and enforce escalation/approval rules." },
    ];

    private static IReadOnlyList<TriagePlanStepDto> CreateCompletedPlan(string workflowStatus, IReadOnlyList<SafeTriageAgentExecution> trace) =>
        CreatePlan().Select(step =>
        {
            var execution = trace.LastOrDefault(item => item.Agent == step.Agent);
            return new TriagePlanStepDto
            {
                Agent = step.Agent,
                Purpose = step.Purpose,
                Status = execution is null ? "NotRun"
                    : execution.Status == "FailedSafely" ? "FailedSafely"
                    : "Completed"
            };
        }).ToList();

    private static TriageWorkflowEventDto MapAuditEvent(TriageWorkflowEvent item)
    {
        var result = new TriageWorkflowEventDto { Stage = item.Stage, EventType = item.EventType, CreatedAt = item.CreatedAt };
        try
        {
            using var details = JsonDocument.Parse(item.DetailsJson);
            var root = details.RootElement;
            result.Tool = root.TryGetProperty("tool", out var tool) ? tool.GetString() : null;
            result.ValidationPassed = root.TryGetProperty("validationPassed", out var valid) && valid.ValueKind is JsonValueKind.True or JsonValueKind.False ? valid.GetBoolean() : null;
            result.Outcome = root.TryGetProperty("outcome", out var outcome) ? outcome.GetString() : null;
            result.DurationMs = root.TryGetProperty("durationMs", out var duration) && duration.TryGetInt32(out var milliseconds) ? milliseconds : null;
            result.RetryCount = root.TryGetProperty("retryCount", out var retry) && retry.TryGetInt32(out var retries) ? retries : null;
            result.ErrorCode = root.TryGetProperty("errorCode", out var error) ? error.GetString() : null;
        }
        catch (JsonException) { /* Preserve the audit event even if legacy details are malformed. */ }
        return result;
    }

    private static TriageGuidanceDto GetControlledGuidance(ClinicalExtractionResult extraction, string originalText)
    {
        var text = string.Join(' ', extraction.Symptoms.Concat(extraction.Concepts ?? []).Append(originalText));
        var lower = text.ToLowerInvariant();

        if (IsNosebleedConcept(lower)) return GetNosebleedGuidance();

        if (lower.Contains("runny nose") || lower.Contains("blocked nose") || lower.Contains("nasal congestion") || lower.Contains("cold"))
        {
            return new TriageGuidanceDto
            {
                Heading = "General information for a runny or blocked nose / cold",
                Summary = "Nasal congestion and cold symptoms are very common and usually clear up within 1 to 2 weeks with supportive care.",
                Actions = ["Rest and drink plenty of fluids (water, warm soups).", "Avoid tobacco smoke, dust, and other nasal irritants.", "A pharmacist can advise on symptom-relief options that are suitable for you."],
                SeekHelpIf = ["You develop difficulty breathing, chest pain, severe confusion, or a sudden severe deterioration — seek emergency care.", "Symptoms worsen, you have a persistent high temperature, or they do not improve after about 10 days — arrange professional assessment."],
                FollowUpQuestions = ["When did this begin?", "Do you also have fever, cough, sore throat, facial pain, or breathing difficulty?"],
                FollowUpItems = GetRoutineNasalQuestions(),
                EvidenceSource = "NHS Common cold guidance (reviewed 22 March 2024): https://www.nhs.uk/conditions/common-cold/"
            };
        }

        if (lower.Contains("headache") || lower.Contains("migraine") || lower.Contains("head pain"))
        {
            return new TriageGuidanceDto
            {
                Heading = "General clinical guidance for headache",
                Summary = "Headaches are common and typically resolve with rest, relaxation, and adequate hydration.",
                Actions = ["Rest in a quiet, dimly lit room.", "Drink water regularly to stay well-hydrated.", "Apply a cool cloth or gentle pressure to the forehead."],
                SeekHelpIf = ["Seek emergency care for sudden severe 'thunderclap' headache, stiff neck, high fever, or vision/speech changes.", "Arrange medical assessment if headaches become frequent, severe, or do not respond to rest."],
                FollowUpQuestions = ["How many hours or days has the headache lasted?", "Is the pain accompanied by nausea, light sensitivity, or neck stiffness?"],
                FollowUpItems = GetHeadacheQuestions(),
                EvidenceSource = "NHS Headache guidance & Clinical Decision Support Pathways"
            };
        }

        if (lower.Contains("fever") || lower.Contains("temperature") || lower.Contains("chills"))
        {
            return new TriageGuidanceDto
            {
                Heading = "General clinical guidance for fever & elevated temperature",
                Summary = "Fever is a natural immune response to infection. Self-management focuses on comfort and fluid balance.",
                Actions = ["Rest comfortably in a cool room with light clothing.", "Sip water, broths, or oral rehydration fluids to prevent dehydration.", "Monitor body temperature using a thermometer."],
                SeekHelpIf = ["Seek emergency medical attention if temperature exceeds 39.5°C (103°F), or if accompanied by difficulty breathing, confusion, or stiff neck.", "Contact a doctor if fever persists for more than 3 days."],
                FollowUpQuestions = ["What is the highest temperature measured?", "How long has the fever been present?"],
                FollowUpItems = GetFeverQuestions(),
                EvidenceSource = "NHS Fever Guidance & CDC Clinical Triage Protocols"
            };
        }

        if (lower.Contains("cough") || lower.Contains("sore throat") || lower.Contains("throat"))
        {
            return new TriageGuidanceDto
            {
                Heading = "General clinical guidance for cough and throat irritation",
                Summary = "Cough and sore throat are common symptoms that generally resolve with hydration and rest.",
                Actions = ["Sip warm liquids such as tea with honey or warm broths.", "Gargle with warm salt water to relieve throat discomfort.", "Rest your voice and avoid smoke or irritants."],
                SeekHelpIf = ["Seek immediate emergency care if you experience shortness of breath, coughing up blood, or chest pain.", "Seek medical evaluation if cough lasts longer than 3 weeks or throat pain prevents swallowing liquids."],
                FollowUpQuestions = ["How many days have you had the cough or sore throat?", "Are you experiencing difficulty breathing or swallowing?"],
                FollowUpItems = GetCoughThroatQuestions(),
                EvidenceSource = "NHS Cough and Sore Throat Guidance"
            };
        }

        if (lower.Contains("stomach") || lower.Contains("abdomen") || lower.Contains("abdominal") || lower.Contains("nausea") || lower.Contains("vomit") || lower.Contains("diarrhea") || lower.Contains("diarrhoea"))
        {
            return new TriageGuidanceDto
            {
                Heading = "General clinical guidance for stomach & digestive symptoms",
                Summary = "Mild abdominal discomfort, nausea, or loose stools usually improve with rest and fluid replacement.",
                Actions = ["Sip water, oral rehydration solution, or clear broth in small frequent sips.", "Eat plain, low-fat foods (like rice, toast, or crackers) when feeling able.", "Avoid spicy, fatty, or caffeinated foods."],
                SeekHelpIf = ["Seek emergency care for severe, sharp, or sudden abdominal pain, persistent vomiting unable to keep liquids down, or blood in vomit/stool.", "Seek medical advice if symptoms persist longer than 48 hours or severe weakness occurs."],
                FollowUpQuestions = ["Are you able to keep fluids down?", "How long have digestive symptoms lasted?"],
                FollowUpItems = GetStomachQuestions(),
                EvidenceSource = "NHS Stomach Ache & Gastroenteritis Guidance"
            };
        }

        // Specific organ/sense checks come FIRST — must be before the generic back/joint/pain block
        // which used to have Contains("pain") that would swallow "ear pain", "eye pain" etc.

        if (IsEarConcept(lower))
        {
            return new TriageGuidanceDto
            {
                Heading = "General clinical guidance for ear pain",
                Summary = "Ear pain can result from infection, fluid build-up, or irritation and often improves within a few days with supportive care.",
                Actions = ["Keep the ear dry and avoid inserting objects into the ear canal.", "Apply a warm (not hot) compress gently against the outer ear for comfort.", "Consult a pharmacist about suitable over-the-counter pain relief options."],
                SeekHelpIf = ["Seek medical evaluation if you experience sudden hearing loss, severe pain, discharge from the ear, high fever, or symptoms lasting more than 3 days.", "Seek emergency care for severe dizziness with vomiting, ear pain after a head injury, or sudden complete hearing loss."],
                FollowUpQuestions = ["Is there any discharge, fluid, or bleeding from the ear?", "How long have you had the ear pain and is it getting worse?"],
                FollowUpItems = GetEarQuestions(),
                EvidenceSource = "NHS Earache Guidance: https://www.nhs.uk/conditions/earache/"
            };
        }

        if (lower.Contains("eye") || lower.Contains("red eye") || lower.Contains("pink eye") || lower.Contains("eye pain") || lower.Contains("blurry vision") || lower.Contains("blurred vision") || lower.Contains("sore eye"))
        {
            return new TriageGuidanceDto
            {
                Heading = "General clinical guidance for eye symptoms",
                Summary = "Red, sore, or irritated eyes can stem from minor infections or irritants and usually improve with basic hygiene and rest.",
                Actions = ["Avoid rubbing or touching your eye. If there is discharge, gently rinse with clean lukewarm water.", "Remove contact lenses and rest the eye away from bright light or screens until symptoms ease."],
                SeekHelpIf = ["Seek emergency eye care for sudden vision loss, severe eye pain, chemical or foreign object in the eye, or eye injury.", "Arrange prompt medical review if the eye is very red with discharge, pain does not ease within 48 hours, or you wear contact lenses with symptoms."],
                FollowUpQuestions = ["Is there discharge or crusting around the eye?", "Has your vision changed or become blurred?"],
                FollowUpItems = GetEyeQuestions(),
                EvidenceSource = "NHS Eye Conditions Guidance: https://www.nhs.uk/conditions/red-eye/"
            };
        }

        if (lower.Contains("dizzy") || lower.Contains("dizziness") || lower.Contains("lightheaded") || lower.Contains("light-headed") || lower.Contains("vertigo") || lower.Contains("spinning"))
        {
            return new TriageGuidanceDto
            {
                Heading = "General clinical guidance for dizziness & lightheadedness",
                Summary = "Dizziness is common and may relate to changes in position, dehydration, inner ear conditions, or low blood pressure. Rest and hydration often help.",
                Actions = ["Sit or lie down immediately when feeling dizzy to prevent falls and injury.", "Sip water slowly to ensure you are well hydrated and avoid sudden posture changes.", "Rise from sitting or lying positions slowly to allow blood pressure to adjust."],
                SeekHelpIf = ["Seek emergency care for sudden severe dizziness with chest pain, vision loss, difficulty speaking, severe headache, or one-sided weakness — these may be stroke signs.", "Contact a doctor if dizziness is recurrent, persistent beyond 24 hours, or accompanied by hearing loss or ringing in the ears."],
                FollowUpQuestions = ["Does the dizziness occur mainly when you change position (e.g., stand up)?", "Do you have any ringing in the ears, hearing changes, nausea, or vomiting alongside the dizziness?"],
                FollowUpItems = GetDizzinessQuestions(),
                EvidenceSource = "NHS Dizziness Guidance: https://www.nhs.uk/conditions/dizziness/"
            };
        }

        if (lower.Contains("fatigue") || lower.Contains("tired") || lower.Contains("exhausted") || lower.Contains("exhaustion") || lower.Contains("low energy") || lower.Contains("lack of energy") || lower.Contains("no energy"))
        {
            return new TriageGuidanceDto
            {
                Heading = "General clinical guidance for fatigue & tiredness",
                Summary = "Fatigue can result from stress, poor sleep, infection, or lifestyle factors. Most cases improve with adequate rest and self-care.",
                Actions = ["Aim for 7–9 hours of quality sleep each night in a dark, quiet room.", "Stay well hydrated, eat balanced regular meals, and include gentle daily movement or short walks."],
                SeekHelpIf = ["Seek medical assessment if fatigue is severe, lasts longer than 4 weeks, or is accompanied by unexplained weight loss, fever, night sweats, or persistent pain.", "Seek emergency care if extreme sudden weakness prevents normal movement or is accompanied by chest pain, difficulty breathing, or fainting."],
                FollowUpQuestions = ["How long have you been experiencing unusual fatigue or low energy?", "Are you sleeping adequately, eating regularly, and managing your stress levels?"],
                FollowUpItems = GetFatigueQuestions(),
                EvidenceSource = "NHS Fatigue Guidance: https://www.nhs.uk/live-well/sleep-and-tiredness/"
            };
        }

        if (lower.Contains("rash") || lower.Contains("skin") || lower.Contains("itching") || lower.Contains("hives"))
        {
            return new TriageGuidanceDto
            {
                Heading = "General clinical guidance for skin rash & irritation",
                Summary = "Skin irritation and non-spreading rashes can usually be managed with gentle skin care and monitoring.",
                Actions = ["Keep the affected area clean, dry, and cool.", "Avoid scratching the rash to prevent skin infection.", "Use unperfumed moisturizers or cool compresses."],
                SeekHelpIf = ["Seek emergency evaluation if rash is accompanied by facial swelling, breathing difficulty, or rapidly spreading purple spots.", "Seek medical advice if the skin looks infected, hot, painful, or does not improve."],
                FollowUpQuestions = ["Is the rash spreading?", "Do you have any swelling or fever?"],
                FollowUpItems = GetSkinQuestions(),
                EvidenceSource = "NHS Skin Rash & Irritation Guidance"
            };
        }

        // Back/joint/muscle block — the generic "pain" keyword is intentionally removed here.
        // Specific pain types (ear pain, eye pain, back pain, etc.) are matched above by their keywords.
        // This block catches back, joint, muscle, and sprain symptoms not already matched.
        if (lower.Contains("back") || lower.Contains("joint") || lower.Contains("muscle") || lower.Contains("knee") || lower.Contains("sprain") || lower.Contains("leg pain") || lower.Contains("arm pain") || lower.Contains("back pain") || lower.Contains("neck pain") || lower.Contains("hip") || lower.Contains("shoulder"))
        {
            return new TriageGuidanceDto
            {
                Heading = "General clinical guidance for muscle, back & joint pain",
                Summary = "Musculoskeletal symptoms typically improve with gentle movement, warm/cold compresses, and proper rest.",
                Actions = ["Apply an ice pack wrapped in a towel for 15-20 minutes, or a warm compress for muscle stiffness.", "Stay gently mobile; avoid prolonged complete bed rest.", "Maintain supportive posture when sitting or standing."],
                SeekHelpIf = ["Seek emergency care for back pain with loss of bladder/bowel control, numbness in the groin/legs, or inability to move legs.", "Consult a doctor if joint pain is severely swollen, red, hot, or accompanied by fever."],
                FollowUpQuestions = ["Did an injury or sudden movement cause the pain?", "Do you have any numbness or tingling?"],
                FollowUpItems = GetBackJointQuestions(),
                EvidenceSource = "NHS Musculoskeletal & Back Pain Guidance"
            };
        }

        var summaryText = extraction.Guidance?.Summary;
        if (string.IsNullOrWhiteSpace(summaryText) || summaryText.Length < 5) summaryText = "Clinical decision support assessment for your reported symptoms.";

        var actions = extraction.Guidance?.GeneralActions is { Count: > 0 } act ? act : new List<string>
        {
            "Rest, maintain adequate fluid intake, and monitor symptom changes.",
            "Consult a pharmacist or qualified healthcare provider for suitable advice on symptom relief."
        };

        var safetyNet = extraction.Guidance?.SafetyNetting is { Count: > 0 } safe ? safe : new List<string>
        {
            "Seek immediate emergency evaluation for severe breathing difficulty, severe chest pain, sudden weakness, or heavy bleeding.",
            "Contact a healthcare professional if symptoms worsen, persist beyond several days, or cause concern."
        };

        return new TriageGuidanceDto
        {
            Heading = "General Agentic AI Clinical Triage Guidance",
            Summary = summaryText,
            Actions = actions,
            SeekHelpIf = safetyNet,
            FollowUpQuestions = extraction.Guidance?.FollowUpQuestions is { Count: > 0 } fq ? fq : new List<string>
            {
                "When did your symptoms start?",
                "Are your symptoms worsening over time?"
            },
            FollowUpItems = GetGeneralClarificationQuestions(),
            EvidenceSource = "SafeTriage Clinical Decision Support Engine & General Medical Triage Standards"
        };
    }

    private static TriageGuidanceDto SelectAdaptiveFollowUpQuestions(
        TriageGuidanceDto guidance,
        string patientInput,
        IReadOnlyList<string> missingInformation,
        ClinicalFactSet? facts)
    {
        const int maximumQuestions = 3;
        if (guidance.FollowUpItems.Count <= maximumQuestions) return guidance;

        var ranked = guidance.FollowUpItems
            .Select((question, index) => new
            {
                Question = question,
                Index = index,
                Score = FollowUpPriority(question, patientInput, missingInformation, facts)
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Index)
            .Take(maximumQuestions)
            .OrderBy(item => item.Index)
            .Select(item => item.Question)
            .ToList();

        guidance.FollowUpItems = ranked;
        guidance.FollowUpQuestions = ranked.Select(question => question.Prompt).ToList();
        return guidance;
    }

    private static int FollowUpPriority(
        TriageFollowUpQuestionDto question,
        string patientInput,
        IReadOnlyList<string> missingInformation,
        ClinicalFactSet? facts)
    {
        var id = question.Id.ToLowerInvariant();
        var category = question.Category?.ToLowerInvariant() ?? string.Empty;
        var missing = string.Join(' ', missingInformation).ToLowerInvariant();
        var score = 50;

        if (category.Contains("safety") || id.Contains("warning")) score = 120;
        else if (id.Contains("active")) score = 115;
        else if (id.Contains("nosebleed_duration")) score = 110;
        else if (id.Contains("risk_context")) score = 100;
        else if (id.Contains("onset")) score = 95;
        else if (id.Contains("severity") || id.Contains("amount") || category.Contains("severity")) score = 90;
        else if (id.Contains("duration")) score = 85;
        else if (id.Contains("main_details")) score = 95;
        else if (id.Contains("progression") || category.Contains("progression")) score = 75;
        else if (category.Contains("associated")) score = 70;

        if (missing.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(term => term.Length >= 5 && (id.Contains(term) || question.Prompt.Contains(term, StringComparison.OrdinalIgnoreCase))))
            score += 20;

        if (QuestionAppearsAnswered(question, patientInput, facts) &&
            !category.Contains("safety") && !id.Contains("risk_context") && !id.StartsWith("nosebleed_", StringComparison.Ordinal))
            score -= 45;

        return score;
    }

    private static bool QuestionAppearsAnswered(TriageFollowUpQuestionDto question, string patientInput, ClinicalFactSet? facts)
    {
        var id = question.Id.ToLowerInvariant();
        var text = patientInput.ToLowerInvariant();

        if (id.Contains("duration") && facts is not null &&
            (Grounded(facts, "durationMinutes") || Grounded(facts, "durationDays"))) return true;
        if (id.Contains("severity") && facts is not null && Grounded(facts, "severityScore")) return true;
        if (id.Contains("progression") && facts is not null && Grounded(facts, "progression")) return true;
        if (id.Contains("duration") || id.Contains("onset"))
            return System.Text.RegularExpressions.Regex.IsMatch(text,
                @"\b(today|yesterday|started|since|for\s+(?:about\s+)?\d+|\d+\s*(?:minute|hour|day|week|month|year)s?)\b");
        if (id.Contains("severity") || id.Contains("amount"))
            return System.Text.RegularExpressions.Regex.IsMatch(text,
                @"\b(mild|moderate|severe|unbearable|[0-9]|10)\b");
        if (id.Contains("progression"))
            return ContainsAny(text, "improving", "worsening", "unchanged", "getting better", "getting worse");
        if (id.Contains("main_details"))
            return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 12;
        return false;
    }

    private static bool Grounded(ClinicalFactSet facts, string field) =>
        facts.Evidence.Any(item => string.Equals(item.Field, field, StringComparison.OrdinalIgnoreCase));

    private static bool IsEarConcept(string text) =>
        text.Contains("earache", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("ear pain", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("ear infection", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("ear discharge", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("sore ear", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("red ear", StringComparison.OrdinalIgnoreCase) ||
        System.Text.RegularExpressions.Regex.IsMatch(text, @"\bear(s)?\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static bool IsNosebleedConcept(string text) =>
        text.Contains("nosebleed", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("nose bleed", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("nasal bleeding", StringComparison.OrdinalIgnoreCase) ||
        (text.Contains("bleeding", StringComparison.OrdinalIgnoreCase) && text.Contains("nose", StringComparison.OrdinalIgnoreCase));

    private static TriageGuidanceDto GetNosebleedGuidance() => new()
    {
        Heading = "Nosebleed safety assessment",
        Summary = "Nosebleeds have different care needs depending on duration, amount, injury, associated symptoms, and health risks.",
        Actions = ["Sit upright, lean forward, and pinch the soft part of the nose continuously while breathing through the mouth.", "Use the follow-up questions to report how long it has continued and any warning signs."],
        SeekHelpIf = ["Seek emergency assessment if bleeding is excessive, continues beyond the configured safety threshold, follows a significant head injury, or occurs with weakness, dizziness, or breathing difficulty."],
        FollowUpQuestions = ["Is it still bleeding?", "How many minutes has it continued?", "How much bleeding is there?", "Are there warning signs, an injury, repeated nosebleeds, or relevant medicines/conditions?"],
        FollowUpItems = GetNosebleedQuestions(),
        EvidenceSource = "NHS Nosebleed guidance: https://www.nhs.uk/conditions/nosebleed/ — decision support only, not a diagnosis."
    };

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetNosebleedQuestions() =>
    [
        new() { Id = "nosebleed_active", Category = "🚨 Safety Check", Prompt = "Is the nose still bleeding?", Type = "yesNo", Required = true, Hint = "Check if active bleeding is present right now." },
        new() { Id = "nosebleed_duration", Category = "⏱ Onset & Duration", Prompt = "For how many minutes has it continued?", Type = "number", Required = true, Unit = "minutes", Minimum = 0, Maximum = 1440, Hint = "Enter total continuous minutes of bleeding." },
        new() { Id = "nosebleed_amount", Category = "📊 Severity", Prompt = "How much bleeding is there?", Type = "singleChoice", Required = true, Options = ["Light", "Moderate", "Heavy or excessive"], Hint = "Assess the flow rate and volume lost." },
        new() { Id = "nosebleed_warning_signs", Category = "🚨 Safety Check", Prompt = "Select every warning sign that applies.", Type = "multipleChoice", Required = true, Options = ["None of these", "Difficulty breathing", "Weak or dizzy", "Fainting or loss of consciousness", "Vomiting swallowed blood", "Severe bleeding"], Hint = "Red flag symptoms require urgent or emergency assessment." },
        new() { Id = "nosebleed_injury", Category = "⚕ Associated Symptoms", Prompt = "Did it begin after a significant head or facial injury?", Type = "yesNo", Required = true, Hint = "Head trauma nosebleeds require skull fracture screening." },
        new() { Id = "nosebleed_recurrent", Category = "🔄 Progression", Prompt = "Do nosebleeds happen repeatedly?", Type = "yesNo", Required = true, Hint = "Frequent nosebleeds may indicate hypertension or nasal vascular fragility." },
        new() { Id = "nosebleed_risk_context", Category = "🧬 Medical Context", Prompt = "Select every health context that applies.", Type = "multipleChoice", Required = true, Options = ["None of these", "Blood-thinning medicine", "Bleeding or clotting condition", "Pregnant", "Cancer treatment", "Weakened immune system"], Hint = "Anticoagulants and blood disorders significantly increase risk." }
    ];

    private static IReadOnlyList<string> GetHighRiskContextQuestions() =>
    [
        "Describe the current symptoms, when they started, their severity, and whether they are worsening.",
        "Provide relevant current treatment, recent procedures, medicines, and the care team's instructions.",
        "Provide measured temperature and other available vital signs, including when and how they were measured."
    ];

    private static TriageGuidanceDto GetClinicalReviewGuidance() => new()
    {
        Heading = "Professional clinical review is required",
        Summary = "A higher-risk health context was reported. A clinician should review the symptoms before this is treated as routine self-care.",
        Actions = ["Contact the relevant care team or a qualified healthcare professional for assessment.", "Have your current symptoms, medicines, allergies, and any measured vital signs available."],
        SeekHelpIf = ["Seek emergency care immediately for severe breathing difficulty, severe chest pain, loss of consciousness, stroke signs, seizure, heavy bleeding, or another life-threatening emergency."],
        FollowUpQuestions = GetHighRiskContextQuestions(),
        EvidenceSource = "Controlled higher-risk-context safety template; this is not a diagnosis or personalized treatment plan."
    };

    private static TriageGuidanceDto GetClarificationGuidance() => new()
    {
        Heading = "More information is needed",
        Summary = "The report is outside the currently validated routine pathways, so the system cannot safely assign urgency from the initial description.",
        Actions = ["Answer the follow-up questions with measured facts where possible.", "Do not delay professional care while waiting for this software if you are concerned or symptoms are worsening."],
        SeekHelpIf = ["Seek emergency care immediately for severe breathing difficulty, severe chest pain, loss of consciousness, stroke signs, seizure, heavy bleeding, or another life-threatening emergency."],
        FollowUpQuestions = ["What is the main current symptom, when did it start, and is it worsening?", "How severe is it, and what activities can you no longer do normally?", "Do you have fever, breathing difficulty, chest pain, confusion, fainting, seizure, unusual bleeding, persistent vomiting, or severe pain?", "Are you pregnant, receiving cancer treatment, immunosuppressed, recently out of surgery, or living with another serious condition?"],
        FollowUpItems = GetGeneralClarificationQuestions(),
        EvidenceSource = "Controlled SafeTriage clarification template; this is not a diagnosis or personalized treatment plan."
    };

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetGeneralClarificationQuestions() =>
    [
        new() { Id = "main_details", Category = "⏱ Onset & Duration", Prompt = "Describe the main symptom, where it is, when it started, and how it affects you.", Type = "shortText", Required = true, Hint = "Be specific about timing and location of symptoms." },
        new() { Id = "severity", Category = "📊 Severity", Prompt = "How severe is the main symptom from 0 to 10?", Type = "severityScale", Required = true, Minimum = 0, Maximum = 10, Hint = "0 = no discomfort, 10 = worst imaginable pain." },
        new() { Id = "progression", Category = "🔄 Progression", Prompt = "How is the symptom changing?", Type = "singleChoice", Required = true, Options = ["Improving", "Unchanged", "Worsening", "Suddenly much worse"], Hint = "Identifies acute progression trajectories." },
        new() { Id = "warning_signs", Category = "🚨 Safety Check", Prompt = "Select every warning sign that applies.", Type = "multipleChoice", Required = true, Options = ["None of these", "Difficulty breathing", "Severe chest pain", "Severe bleeding", "Fainting or loss of consciousness", "Seizure", "Stroke signs", "High fever or chills", "Persistent vomiting", "Severe pain"], Hint = "Red flag symptoms require immediate clinical evaluation." },
        new() { Id = "risk_context", Category = "🧬 Medical Context", Prompt = "Select every health context that applies.", Type = "multipleChoice", Required = true, Options = ["None of these", "Pregnant or postpartum", "Cancer treatment", "Weakened immune system", "Organ transplant", "Recent surgery", "Blood-thinning medicine", "Serious long-term condition"], Hint = "Medical context helps calibrate triage safety thresholds." }
    ];

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetRoutineNasalQuestions() =>
    [
        new() { Id = "nasal_duration", Category = "⏱ Onset & Duration", Prompt = "How many days have the nasal symptoms been present?", Type = "number", Required = true, Unit = "days", Minimum = 0, Maximum = 365, Hint = "Enter total days of nasal symptoms." },
        new() { Id = "nasal_progression", Category = "🔄 Progression", Prompt = "How are the symptoms changing?", Type = "singleChoice", Required = true, Options = ["Improving", "Unchanged", "Worsening", "Suddenly much worse"], Hint = "Identifies acute progression trajectories." },
        new() { Id = "nasal_warning_signs", Category = "🚨 Safety Check", Prompt = "Select every warning sign that applies.", Type = "multipleChoice", Required = true, Options = ["None of these", "Difficulty breathing", "Severe chest pain", "Severe confusion", "High fever or chills", "Severe pain"], Hint = "Screening for sinus complications and respiratory compromise." },
        new() { Id = "nasal_risk_context", Category = "🧬 Medical Context", Prompt = "Select every health context that applies.", Type = "multipleChoice", Required = true, Options = ["None of these", "Pregnant or postpartum", "Cancer treatment", "Weakened immune system", "Blood-thinning medicine", "Serious long-term condition"], Hint = "Helps determine if self-care or prompt review is safest." }
    ];

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetHeadacheQuestions() =>
    [
        new() { Id = "headache_onset", Category = "⏱ Onset & Duration", Prompt = "How quickly did the headache reach its peak intensity?", Type = "singleChoice", Required = true, Options = ["Suddenly reached maximum intensity within seconds ('thunderclap')", "Built up gradually over minutes to hours", "Came on over days or weeks"], Hint = "Sudden 'thunderclap' onset is a critical medical red flag." },
        new() { Id = "headache_severity", Category = "📊 Severity", Prompt = "Rate the severity of your headache (0 = mild, 10 = unbearable):", Type = "severityScale", Required = true, Minimum = 0, Maximum = 10, Hint = "0 is no pain, 10 is the worst pain of your life." },
        new() { Id = "headache_associated", Category = "⚕ Associated Symptoms", Prompt = "Select any associated symptoms you are experiencing:", Type = "multipleChoice", Required = true, Options = ["None of these", "Nausea or vomiting", "Sensitivity to light (photophobia) or sound", "Stiff neck (difficulty touching chin to chest)", "Visual disturbances (flashing lights, blurry vision, aura)"], Hint = "Helps differentiate tension, migraine, and meningeal signs." },
        new() { Id = "headache_warning_signs", Category = "🚨 Safety Check", Prompt = "Do you have any of these emergency headache signs?", Type = "multipleChoice", Required = true, Options = ["None of these", "Fever or chills with neck stiffness", "Numbness, tingling, or weakness on one side of face/body", "Difficulty speaking, confusion, or memory loss", "Headache after a recent head injury"], Hint = "Indicates need for immediate emergency neurological assessment." },
        new() { Id = "headache_risk_context", Category = "🧬 Medical Context", Prompt = "Select any medical context that applies:", Type = "multipleChoice", Required = true, Options = ["None of these", "History of migraines", "High blood pressure", "Pregnant or postpartum", "Active cancer diagnosis", "Age 50+ with new type of headache"], Hint = "New headache onset in over-50s or immunocompromised individuals requires review." }
    ];

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetFeverQuestions() =>
    [
        new() { Id = "fever_temperature", Category = "📊 Severity", Prompt = "What is your highest measured body temperature?", Type = "singleChoice", Required = true, Options = ["Not measured with thermometer", "Below 38.0°C (100.4°F) - Mild", "38.0°C – 39.4°C (100.4°F – 102.9°F) - Moderate", "39.5°C or higher (103°F+) - High fever"], Hint = "Use an oral, tympanic, or forehead thermometer reading if available." },
        new() { Id = "fever_duration", Category = "⏱ Onset & Duration", Prompt = "For how many continuous days have you had a fever?", Type = "number", Required = true, Unit = "days", Minimum = 0, Maximum = 30, Hint = "Fevers lasting longer than 3 days warrant medical evaluation." },
        new() { Id = "fever_warning_signs", Category = "🚨 Safety Check", Prompt = "Select any severe symptoms occurring with the fever:", Type = "multipleChoice", Required = true, Options = ["None of these", "Difficulty breathing or rapid shallow breathing", "Stiff neck or severe headache", "Rash that does not fade when pressed (non-blanching)", "Severe confusion or drowsiness", "Inability to keep liquids down"], Hint = "Screening for sepsis, meningitis, and severe systemic infection." },
        new() { Id = "fever_risk_context", Category = "🧬 Medical Context", Prompt = "Select any high-risk health context:", Type = "multipleChoice", Required = true, Options = ["None of these", "Active chemotherapy or cancer treatment", "Immunosuppressive medication or organ transplant", "Recent surgery or hospital admission", "Recent foreign travel (past 30 days)"], Hint = "Neutropenic fever in cancer patients is a medical emergency." }
    ];

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetCoughThroatQuestions() =>
    [
        new() { Id = "cough_type", Category = "⚕ Associated Symptoms", Prompt = "Describe the nature of your cough or throat pain:", Type = "singleChoice", Required = true, Options = ["Dry hacking cough", "Productive cough (bringing up phlegm/mucus)", "Severe sore throat with pain on swallowing", "Hoarseness or loss of voice"], Hint = "Characterizes upper vs lower respiratory tract involvement." },
        new() { Id = "cough_duration", Category = "⏱ Onset & Duration", Prompt = "How many days have you had the cough or throat symptoms?", Type = "number", Required = true, Unit = "days", Minimum = 0, Maximum = 365, Hint = "Coughs lasting >3 weeks require clinical investigation." },
        new() { Id = "cough_warning_signs", Category = "🚨 Safety Check", Prompt = "Select any red-flag respiratory warning signs:", Type = "multipleChoice", Required = true, Options = ["None of these", "Coughing up pink foam or red blood", "Shortness of breath or struggling to speak full sentences", "Straining to swallow saliva or open mouth", "Chest pain when breathing in (pleuritic pain)"], Hint = "Hemoptysis, respiratory distress, and quinsy signs require prompt urgent care." },
        new() { Id = "cough_risk_context", Category = "🧬 Medical Context", Prompt = "Select any medical conditions that apply:", Type = "multipleChoice", Required = true, Options = ["None of these", "Asthma, COPD, or bronchiectasis", "Heart failure or cardiac condition", "Smoker or history of heavy smoking", "Weakened immune system"], Hint = "Underlying chronic lung conditions increase risk of exacerbations." }
    ];

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetStomachQuestions() =>
    [
        new() { Id = "stomach_location", Category = "⚕ Associated Symptoms", Prompt = "Where is the main digestive discomfort or pain felt?", Type = "singleChoice", Required = true, Options = ["Upper stomach / indigestion area", "Around belly button", "Lower right abdomen", "Lower left abdomen", "All over the stomach / generalized", "Nausea or vomiting without sharp pain"], Hint = "Lower right pain can indicate appendicitis." },
        new() { Id = "stomach_fluids", Category = "📊 Severity", Prompt = "Are you able to keep liquids down without vomiting?", Type = "singleChoice", Required = true, Options = ["Yes, keeping fluids down normally", "Sipping small amounts with mild nausea", "No, throwing up all liquids / persistent vomiting"], Hint = "Inability to keep fluids down causes rapid dehydration." },
        new() { Id = "stomach_warning_signs", Category = "🚨 Safety Check", Prompt = "Select any emergency digestive signs:", Type = "multipleChoice", Required = true, Options = ["None of these", "Severe, sharp, or unbearable sudden abdominal pain", "Blood in vomit or dark coffee-ground vomit", "Black tarry stools or bright red blood in stool", "High fever with rigid, hard-to-touch stomach", "Dizziness or feeling about to pass out"], Hint = "Screening for GI bleeding, perforation, and acute abdomen." },
        new() { Id = "stomach_risk_context", Category = "🧬 Medical Context", Prompt = "Select any health factors that apply:", Type = "multipleChoice", Required = true, Options = ["None of these", "Recent abdominal surgery or procedure", "Pregnant or possibility of pregnancy", "Inflammatory bowel disease (Crohn's / Colitis)", "Taking daily NSAID pain relievers (Ibuprofen, Naproxen)"], Hint = "NSAIDs increase risk of stomach ulceration and bleeding." }
    ];

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetBackJointQuestions() =>
    [
        new() { Id = "back_trigger", Category = "⏱ Onset & Duration", Prompt = "Did the pain start after an injury, heavy lifting, or sudden movement?", Type = "singleChoice", Required = true, Options = ["Direct impact injury or fall", "Lifting or sudden twisting movement", "Gradual onset over time without clear trigger", "Woke up with stiffness / pain"], Hint = "Distinguishes traumatic vs mechanical/inflammatory musculoskeletal pain." },
        new() { Id = "back_numbness", Category = "🚨 Safety Check", Prompt = "Do you have any numbness, weakness, or nerve symptoms?", Type = "multipleChoice", Required = true, Options = ["None of these", "Numbness or tingling spreading down leg(s) or arm(s)", "Numbness around groin, buttocks, or saddle area", "New weakness when trying to lift foot or toes ('foot drop')", "Loss of bowel or bladder control / inability to urinate"], Hint = "Saddle anesthesia and sphincter incontinence indicate cauda equina syndrome." },
        new() { Id = "back_joint_signs", Category = "⚕ Associated Symptoms", Prompt = "Is a joint severely swollen, hot, red, or unable to bear weight?", Type = "yesNo", Required = true, Hint = "Hot swollen single joints screen for septic arthritis." },
        new() { Id = "back_risk_context", Category = "🧬 Medical Context", Prompt = "Select any relevant medical context:", Type = "multipleChoice", Required = true, Options = ["None of these", "History of osteoporosis or bone fractures", "Known cancer diagnosis", "Long-term steroid medication", "Unexplained recent weight loss"], Hint = "Cancer history + new spinal pain requires investigation for metastases." }
    ];

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetSkinQuestions() =>
    [
        new() { Id = "skin_spread", Category = "🔄 Progression", Prompt = "How rapidly is the rash or skin symptom spreading?", Type = "singleChoice", Required = true, Options = ["Confined to one small area", "Spreading slowly over days", "Spreading rapidly over hours", "Covering large parts of the body"], Hint = "Rapidly spreading erythema screens for cellulitis or acute drug reaction." },
        new() { Id = "skin_warning_signs", Category = "🚨 Safety Check", Prompt = "Select any high-risk skin warning signs:", Type = "multipleChoice", Required = true, Options = ["None of these", "Small purple/red spots or bruises that do not fade when pressed under a glass", "Swelling of lips, tongue, face, or throat", "Skin pain, blistering, or peeling skin", "High fever with hot, swollen, painful skin"], Hint = "Non-blanching petechial rash is a emergency meningitis sign." },
        new() { Id = "skin_itch_pain", Category = "⚕ Associated Symptoms", Prompt = "Is the rash primarily itchy, painful, or tender to touch?", Type = "singleChoice", Required = true, Options = ["Very itchy (hives / eczema style)", "Painful or burning sensation (shingles / infection style)", "Neither painful nor itchy"], Hint = "Differentiates allergic reactions vs shingles / bacterial infection." },
        new() { Id = "skin_risk_context", Category = "🧬 Medical Context", Prompt = "Select any health context that applies:", Type = "multipleChoice", Required = true, Options = ["None of these", "Started a new medicine in the past 4 weeks", "Known severe food or drug allergies", "Diabetes or poor leg circulation", "Weakened immune system"], Hint = "New drug introductions screen for severe cutaneous adverse reactions." }
    ];

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetEarQuestions() =>
    [
        new() { Id = "ear_discharge", Category = "⚕ Associated Symptoms", Prompt = "Is there any discharge, fluid, or bleeding coming from the ear?", Type = "singleChoice", Required = true, Options = ["No discharge", "Clear fluid or yellow/green pus", "Blood or blood-stained fluid"], Hint = "Fluid or blood from the ear screens for perforated eardrum or head trauma." },
        new() { Id = "ear_hearing", Category = "📊 Severity", Prompt = "Have you noticed any hearing loss or loud ringing (tinnitus)?", Type = "singleChoice", Required = true, Options = ["Normal hearing, no ringing", "Muffled hearing / feeling blocked", "Sudden significant hearing loss", "Loud ringing or buzzing in ear"], Hint = "Sudden sensorineural hearing loss requires urgent ENT referral." },
        new() { Id = "ear_warning_signs", Category = "🚨 Safety Check", Prompt = "Do you have any of these warning signs?", Type = "multipleChoice", Required = true, Options = ["None of these", "Ear pain started after head injury", "Swelling, redness, or pain behind the ear over the bone", "High fever with severe headache or stiff neck", "Spinning dizziness with severe vomiting"], Hint = "Swelling over mastoid bone behind ear screens for mastoiditis." },
        new() { Id = "ear_risk_context", Category = "🧬 Medical Context", Prompt = "Select any medical context that applies:", Type = "multipleChoice", Required = true, Options = ["None of these", "Frequent ear infections", "Diabetes", "Weakened immune system", "Grommets or ear tubes fitted"], Hint = "Malignant otitis externa risk is higher in diabetic patients." }
    ];

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetEyeQuestions() =>
    [
        new() { Id = "eye_vision", Category = "🚨 Safety Check", Prompt = "Has your vision changed in the affected eye?", Type = "singleChoice", Required = true, Options = ["Vision is completely normal", "Mild blurriness cleared by blinking", "Persistent blurry or double vision", "Sudden partial or complete loss of vision"], Hint = "Sudden vision loss is an ophthalmic emergency." },
        new() { Id = "eye_pain_light", Category = "📊 Severity", Prompt = "Are you experiencing severe eye pain or extreme sensitivity to light?", Type = "singleChoice", Required = true, Options = ["Mild discomfort or itching only", "Moderate pain / uncomfortable in normal light", "Severe aching eye pain or severe light sensitivity"], Hint = "Deep eye pain and photophobia screen for acute glaucoma or uveitis." },
        new() { Id = "eye_warning_signs", Category = "🚨 Safety Check", Prompt = "Select any severe eye warnings:", Type = "multipleChoice", Required = true, Options = ["None of these", "Chemical or foreign object splashed/entered eye", "Direct impact injury to the eye", "Irregular shaped pupil or cloudy cornea", "Haloes around light sources"], Hint = "Chemical burns and acute angle closure require immediate emergency care." },
        new() { Id = "eye_risk_context", Category = "🧬 Medical Context", Prompt = "Select any eye context that applies:", Type = "multipleChoice", Required = true, Options = ["None of these", "Wear contact lenses", "Recent eye surgery or procedure", "History of glaucoma or macular degeneration"], Hint = "Contact lens wearers with red eye have higher risk of corneal ulcers." }
    ];

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetDizzinessQuestions() =>
    [
        new() { Id = "dizzy_type", Category = "⚕ Associated Symptoms", Prompt = "Describe the sensation of dizziness:", Type = "singleChoice", Required = true, Options = ["Room is spinning around me (vertigo)", "Feeling faint, lightheaded, or about to pass out (presyncope)", "Feeling off-balance when walking (unsteadiness)"], Hint = "Distinguishes vestibular vertigo vs cardiovascular presyncope." },
        new() { Id = "dizzy_trigger", Category = "⏱ Onset & Duration", Prompt = "Does the dizziness happen mainly when changing position (e.g. standing up or turning head)?", Type = "yesNo", Required = true, Hint = "Positional triggers suggest orthostatic hypotension or BPPV." },
        new() { Id = "dizzy_warning_signs", Category = "🚨 Safety Check", Prompt = "Do you have any stroke or neurological warning signs with dizziness?", Type = "multipleChoice", Required = true, Options = ["None of these", "Chest pain, pressure, or shortness of breath", "Face drooping, arm weakness, or slurred speech", "Sudden severe headache ('thunderclap')", "Double vision, confusion, or difficulty walking"], Hint = "Screening for stroke (FAST protocol) and acute myocardial infarction." },
        new() { Id = "dizzy_risk_context", Category = "🧬 Medical Context", Prompt = "Select any health factors that apply:", Type = "multipleChoice", Required = true, Options = ["None of these", "Taking blood pressure medication", "History of heart conditions or arrhythmia", "Diabetes (risk of hypoglycemia)", "Dehydration or recent illness with vomiting"], Hint = "Medication side effects and dehydration are frequent causes." }
    ];

    private static IReadOnlyList<TriageFollowUpQuestionDto> GetFatigueQuestions() =>
    [
        new() { Id = "fatigue_duration", Category = "⏱ Onset & Duration", Prompt = "How long have you been experiencing persistent fatigue or exhaustion?", Type = "singleChoice", Required = true, Options = ["A few days (less than a week)", "1 to 4 weeks", "1 to 6 months", "More than 6 months"], Hint = "Fatigue lasting >4 weeks warrants blood work and clinical review." },
        new() { Id = "fatigue_impact", Category = "📊 Severity", Prompt = "How much does the fatigue impact your daily life?", Type = "singleChoice", Required = true, Options = ["Mild – able to manage normal work and activities", "Moderate – struggling to complete normal daily routines", "Severe – unable to get out of bed or care for self"], Hint = "Measures functional impairment level." },
        new() { Id = "fatigue_warning_signs", Category = "🚨 Safety Check", Prompt = "Select any associated symptoms you have noticed:", Type = "multipleChoice", Required = true, Options = ["None of these", "Unexplained weight loss without trying", "Night sweats soaking sheets or clothes", "Swollen lymph nodes in neck, armpits, or groin", "Shortness of breath on mild exertion", "Pale skin, easy bruising, or unusual bleeding"], Hint = "Screens for red flags including anemia, occult infection, or malignancy." },
        new() { Id = "fatigue_risk_context", Category = "🧬 Medical Context", Prompt = "Select any lifestyle or health factors:", Type = "multipleChoice", Required = true, Options = ["None of these", "Poor sleep (less than 6 hours per night)", "High stress, low mood, or anxiety", "Known thyroid condition or anemia", "Recent viral illness (e.g. flu, COVID-19)"], Hint = "Post-viral fatigue and thyroid dysfunction are common causes." }
    ];

    private static string ValidateAndFormatFreeTextAnswers(TriageWorkflow workflow, IReadOnlyList<TriageAnswerDto> answers)
    {
        using var result = JsonDocument.Parse(workflow.ResultJson);
        if (!result.RootElement.TryGetProperty("guidance", out var guidanceElement) || guidanceElement.ValueKind == JsonValueKind.Null)
            throw new ArgumentException("This workflow has no follow-up questionnaire.");
        var guidance = guidanceElement.Deserialize<TriageGuidanceDto>();
        var questions = guidance?.FollowUpItems ?? [];
        if (questions.Count == 0) throw new ArgumentException("This workflow uses an unsupported legacy questionnaire. Start a new assessment.");

        var duplicate = answers.GroupBy(answer => answer.QuestionId.Trim(), StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new ArgumentException($"Duplicate answer for question '{duplicate.Key}'.");
        var supplied = answers.ToDictionary(answer => answer.QuestionId.Trim(), StringComparer.Ordinal);
        if (supplied.Keys.Any(id => questions.All(question => question.Id != id)))
            throw new ArgumentException("A follow-up answer contains an unknown question identifier.");

        var formatted = new List<string>();
        foreach (var question in questions)
        {
            if (!supplied.TryGetValue(question.Id, out var answer) || string.IsNullOrWhiteSpace(answer.Value))
            {
                if (question.Required) throw new ArgumentException($"Answer required: {question.Prompt}");
                continue;
            }
            var value = answer.Value.Trim();
            formatted.Add($"Patient response for follow-up field '{question.Id}': {value}");
        }

        if (supplied.TryGetValue("nosebleed_active", out var active) && IsAffirmative(active.Value) &&
            supplied.TryGetValue("nosebleed_duration", out var duration) && TryReadNumber(duration.Value, out var minutes) && minutes >= 15)
            formatted.Add("Validated safety protocol signal: severe bleeding lasting at least 15 minutes.");
        if (supplied.TryGetValue("nosebleed_amount", out var amount) && ContainsAny(amount.Value, "heavy", "excessive", "severe"))
            formatted.Add("Validated safety protocol signal: severe bleeding.");
        if (supplied.TryGetValue("nosebleed_injury", out var injury) && IsAffirmative(injury.Value))
            formatted.Add("Validated safety protocol signal: severe bleeding after a significant head injury.");
        if (supplied.TryGetValue("nosebleed_warning_signs", out var warnings) &&
            ContainsAny(warnings.Value, "weak", "dizzy", "vomiting", "swallowed blood", "difficulty breathing", "faint"))
            formatted.Add("Validated safety protocol signal: severe bleeding with associated warning signs.");
        if (supplied.TryGetValue("nosebleed_recurrent", out var recurrent) &&
            (IsAffirmative(recurrent.Value) || ContainsAny(recurrent.Value, "recurrent", "repeatedly", "often")))
            formatted.Add("Validated clinical-review context: recurrent nosebleeds.");
        return string.Join('\n', formatted);
    }

    private static bool IsAffirmative(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        if (System.Text.RegularExpressions.Regex.IsMatch(normalized, @"\b(no|not|isn't|isnt|didn't|didnt|stopped)\b")) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^(yes|yeah|yep|correct|it is|i do)\b") ||
               ContainsAny(normalized, "still bleeding", "still happening");
    }

    private static bool TryReadNumber(string value, out decimal number)
    {
        number = 0;
        var match = System.Text.RegularExpressions.Regex.Match(value, @"\d+(?:\.\d+)?");
        return match.Success && decimal.TryParse(match.Value, System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out number);
    }

    private static bool ContainsAny(string value, params string[] phrases) =>
        phrases.Any(phrase => value.Contains(phrase, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> BuildDecisionBasis(SafeTriageAgentContext context)
    {
        var basis = new List<string>();
        if (context.RedFlags.Count > 0) basis.AddRange(context.RedFlags.Select(flag => $"Emergency policy matched: {flag}."));
        else if (context.UrgentFlags.Count > 0) basis.AddRange(context.UrgentFlags.Select(flag => $"Urgent policy matched: {flag}."));
        else if (context.ClinicalReviewFlags.Count > 0) basis.AddRange(context.ClinicalReviewFlags.Select(flag => $"Clinical-review policy matched: {flag}."));
        else basis.Add("No configured escalation policy matched the available grounded facts.");
        if (context.Extraction?.Facts?.Evidence.Count > 0)
            basis.Add("Structured facts used by policy are linked to exact patient-provided evidence spans.");
        if (context.Request.IsFollowUp) basis.Add("The original report and follow-up responses were reassessed together.");
        if (context.PlannedInformationNeeds.Count > 0)
            basis.Add($"Adaptive question planner prioritized: {string.Join(", ", context.PlannedInformationNeeds)}.");
        return basis;
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

    private static TriageWorkflowDto Map(TriageWorkflow workflow, List<string>? risks = null, List<string>? flags = null, List<string>? missing = null, TriageGuidanceDto? guidance = null, bool redactPatientText = false)
    {
        using var result = JsonDocument.Parse(workflow.ResultJson);
        risks ??= result.RootElement.TryGetProperty("riskFactors", out var riskElement) ? riskElement.Deserialize<List<string>>() ?? [] : [];
        flags ??= result.RootElement.TryGetProperty("redFlags", out var flagElement) ? flagElement.Deserialize<List<string>>() ?? [] : [];
        missing ??= result.RootElement.TryGetProperty("missingInformation", out var missingElement) ? missingElement.Deserialize<List<string>>() ?? [] : [];
        guidance ??= result.RootElement.TryGetProperty("guidance", out var guidanceElement) && guidanceElement.ValueKind != JsonValueKind.Null ? guidanceElement.Deserialize<TriageGuidanceDto>() : null;
        var urgentFlags = result.RootElement.TryGetProperty("urgentFlags", out var urgentElement) ? urgentElement.Deserialize<List<string>>() ?? [] : [];
        var clinicalReviewFlags = result.RootElement.TryGetProperty("clinicalReviewFlags", out var reviewElement) ? reviewElement.Deserialize<List<string>>() ?? [] : [];
        var facts = result.RootElement.TryGetProperty("clinicalFacts", out var factsElement) && factsElement.ValueKind == JsonValueKind.Object
            ? factsElement.Deserialize<ClinicalFactSet>()
            : null;
        var decisionBasis = result.RootElement.TryGetProperty("decisionBasis", out var basisElement)
            ? basisElement.Deserialize<List<string>>() ?? []
            : [];
        return new TriageWorkflowDto { WorkflowId = workflow.TriageWorkflowId, Status = workflow.Status, ApprovalStatus = workflow.ApprovalStatus, TriageLevel = workflow.TriageLevel, UncertaintyState = workflow.UncertaintyState, RequiresHumanReview = workflow.RequiresHumanReview, PatientMessage = workflow.FinalOutcome ?? "The system cannot safely assess this situation.", PatientReportedSymptoms = redactPatientText ? RedactUnneededIdentifiers(workflow.Symptoms) : workflow.Symptoms, Guidance = guidance, RiskFactors = risks, RedFlags = flags, UrgentFlags = urgentFlags, ClinicalReviewFlags = clinicalReviewFlags, MissingInformation = missing, ClinicalFacts = MapFacts(facts), DecisionBasis = decisionBasis, Plan = JsonSerializer.Deserialize<List<TriagePlanStepDto>>(workflow.PlanJson) ?? [], RuleSetVersion = workflow.RuleSetVersion, WorkflowVersion = workflow.WorkflowVersion, CreatedAt = workflow.CreatedAt, UpdatedAt = workflow.UpdatedAt };
    }

    private static string RedactUnneededIdentifiers(string text) => System.Text.RegularExpressions.Regex
        .Replace(System.Text.RegularExpressions.Regex.Replace(text,
            @"[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}", "[redacted email]"),
            @"\+?\d[\d\s().-]{7,}\d", "[redacted phone]");

    private static TriageClinicalFactsDto? MapFacts(ClinicalFactSet? facts) => facts is null ? null : new TriageClinicalFactsDto
    {
        PrimaryConcept = facts.PrimaryConcept,
        CurrentlyActive = facts.CurrentlyActive,
        DurationMinutes = facts.DurationMinutes,
        DurationDays = facts.DurationDays,
        SeverityScore = facts.SeverityScore,
        TemperatureCelsius = facts.TemperatureCelsius,
        Progression = facts.Progression,
        WarningSigns = facts.WarningSigns,
        NegatedWarningSigns = facts.NegatedWarningSigns,
        RiskContexts = facts.RiskContexts,
        Evidence = facts.Evidence.Select(item => new TriageFactEvidenceDto { Field = item.Field, Value = item.Value, Quote = item.Quote }).ToList()
    };
}
