using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

public sealed class PlanningCoordinatorAgent : IPlanningCoordinatorAgent
{
    private readonly IPlanningModelClient _modelClient;
    private readonly IPlanningCoordinatorStore _store;
    private readonly ILogger<PlanningCoordinatorAgent> _logger;

    private static readonly string[] PromptInjectionPatterns =
    [
        "ignore previous",
        "ignore all previous",
        "system override",
        "act as admin",
        "bypass safety",
        "bypass confirmation",
        "book immediately without confirmation",
        "reveal prompt",
        "drop table",
        "grant admin",
        "sudo "
    ];

    private static readonly string[] ExplicitBookingKeywords =
    [
        "book",
        "booking",
        "schedule",
        "reschedule",
        "reserve",
        "make an appointment",
        "appointment with",
        "see a doctor",
        "consult a doctor",
        "need an appointment"
    ];

    private static readonly string[] SymptomKeywords =
    [
        "cough",
        "fever",
        "pain",
        "headache",
        "ache",
        "dizzy",
        "nausea",
        "vomit",
        "breath",
        "bleeding",
        "chest",
        "rash",
        "cold",
        "flu",
        "sore",
        "swollen"
    ];

    public PlanningCoordinatorAgent(
        IPlanningModelClient modelClient,
        IPlanningCoordinatorStore store,
        ILogger<PlanningCoordinatorAgent> logger)
    {
        _modelClient = modelClient;
        _store = store;
        _logger = logger;
    }

    public async Task<PlanningResponseDto> PlanAsync(PlanningRequestDto request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var objective = (request.Objective ?? string.Empty).Trim();
        if (objective.Length < 2)
        {
            throw new ArgumentException("A valid patient objective must be provided.", nameof(request));
        }

        var workflowRecord = new PlanningWorkflowRecord
        {
            PatientId = request.PatientId,
            Objective = objective,
            Status = "Created",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        workflowRecord.AuditEvents.Add(new PlanningAuditEvent
        {
            EventType = "WorkflowInitialized",
            Description = "Planning workflow record created for patient objective.",
            Metadata = $"PatientId: {request.PatientId}"
        });

        // 1. Prompt injection / Adversarial heuristic check
        if (IsPromptInjection(objective))
        {
            return await HandlePromptInjectionAsync(workflowRecord, request, cancellationToken);
        }

        // 2. Query Gemini structured output model
        GeminiPlanningDecision? rawDecision = null;
        var errors = new List<string>();

        try
        {
            rawDecision = await _modelClient.PlanObjectiveAsync(objective, cancellationToken);
            workflowRecord.AuditEvents.Add(new PlanningAuditEvent
            {
                EventType = "ModelPlanningCompleted",
                Description = "Gemini model returned structured JSON planning decision.",
                Metadata = $"RawWorkflowType: {rawDecision.WorkflowType}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini planning client threw an exception for objective. Falling back to deterministic planner.");
            errors.Add($"Model planning fallback engaged: {ex.Message}");
            workflowRecord.AuditEvents.Add(new PlanningAuditEvent
            {
                EventType = "ModelPlanningFailed",
                Description = "Gemini model failed or returned malformed JSON; deterministic safety fallback engaged.",
                Metadata = ex.Message
            });
            rawDecision = DeterministicRuleBasedFallback(objective, request);
        }

        // 3. Post-process, sanitize, and strictly enforce hospital safety rules
        var plan = BuildAndSanitizePlan(rawDecision, objective, request, workflowRecord);

        workflowRecord.Plan = plan;
        workflowRecord.Status = plan.WorkflowType == PlanningWorkflowType.Unsupported.ToString()
            ? "Unsupported"
            : "Planned";

        if (workflowRecord.Status == "Planned")
        {
            workflowRecord.CompletedStages.Add("PlanningStage");
        }

        workflowRecord.Errors.AddRange(errors);
        workflowRecord.UpdatedAt = DateTimeOffset.UtcNow;

        workflowRecord.AuditEvents.Add(new PlanningAuditEvent
        {
            EventType = "PlanFinalized",
            Description = $"Allow-listed plan finalized with workflow type: {plan.WorkflowType}.",
            Metadata = $"AppointmentRequested: {plan.AppointmentRequested}, PatientConfirmationRequired: {plan.PatientConfirmationRequired}"
        });

        await _store.SaveAsync(workflowRecord, cancellationToken);

        return MapToResponseDto(workflowRecord);
    }

    public async Task<PlanningResponseDto?> GetWorkflowStatusAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        var record = await _store.GetAsync(workflowId, cancellationToken);
        return record is null ? null : MapToResponseDto(record);
    }

    private static bool IsPromptInjection(string text)
    {
        var lower = text.ToLowerInvariant();
        return PromptInjectionPatterns.Any(pattern => lower.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<PlanningResponseDto> HandlePromptInjectionAsync(
        PlanningWorkflowRecord record,
        PlanningRequestDto request,
        CancellationToken cancellationToken)
    {
        record.Status = "Unsupported";
        record.Errors.Add("Prompt injection or adversarial instruction detected.");
        record.AuditEvents.Add(new PlanningAuditEvent
        {
            EventType = "SecuritySafetyIntervention",
            Description = "Adversarial prompt or policy violation trapped by heuristic filter.",
            Metadata = "Action: Defaulted to Unsupported with safe controlled response."
        });

        record.Plan = new PlanningPlanDto
        {
            WorkflowType = PlanningWorkflowType.Unsupported.ToString(),
            AppointmentRequested = false,
            PatientConfirmationRequired = true,
            RequiredSteps = PlanningWorkflowSteps.DefaultStepsByWorkflow[PlanningWorkflowType.Unsupported],
            FollowUpQuestions = [],
            Rationale = "Input contained prohibited instructions or security override attempts.",
            SafeResponse = "I can only assist with hospital triage assessments, doctor information, and appointment inquiries. Please specify your health concern or appointment question."
        };

        record.UpdatedAt = DateTimeOffset.UtcNow;
        await _store.SaveAsync(record, cancellationToken);

        return MapToResponseDto(record);
    }

    private PlanningPlanDto BuildAndSanitizePlan(
        GeminiPlanningDecision decision,
        string objective,
        PlanningRequestDto request,
        PlanningWorkflowRecord record)
    {
        // 1. Resolve workflow type to allow-listed enum
        if (!Enum.TryParse<PlanningWorkflowType>(decision.WorkflowType, true, out var workflowType))
        {
            workflowType = PlanningWorkflowType.Unsupported;
            record.AuditEvents.Add(new PlanningAuditEvent
            {
                EventType = "WorkflowTypeSanitized",
                Description = $"Unknown workflow '{decision.WorkflowType}' sanitized to Unsupported.",
                Metadata = null
            });
        }

        // 2. ENFORCE CONSENT RULE: Never infer consent to book from symptoms alone.
        var hasExplicitBookingIntent = HasExplicitBookingIntent(objective);
        var hasSymptoms = HasSymptomKeywords(objective);

        var appointmentRequested = decision.AppointmentRequested;
        if (hasSymptoms && !hasExplicitBookingIntent)
        {
            // Even if model said true, force false because symptoms alone cannot grant booking consent
            if (appointmentRequested)
            {
                record.AuditEvents.Add(new PlanningAuditEvent
                {
                    EventType = "ConsentRuleEnforced",
                    Description = "Overrode appointmentRequested to false because user only reported symptoms without explicit booking consent.",
                    Metadata = "Rule: Never infer consent to book from symptoms."
                });
            }
            appointmentRequested = false;
        }
        else if (hasExplicitBookingIntent)
        {
            appointmentRequested = true;
        }

        // 3. ENFORCE PATIENT CONFIRMATION RULE
        // Booking or proposing always requires explicit patient confirmation
        var confirmationRequired = workflowType is PlanningWorkflowType.TriageThenAppointmentProposal or PlanningWorkflowType.AppointmentProposal
                                   || decision.PatientConfirmationRequired;

        // 4. Sanitize required steps against predefined allow-list
        var sanitizedSteps = new List<string>();
        if (decision.RequiredSteps is { Length: > 0 })
        {
            foreach (var step in decision.RequiredSteps)
            {
                if (PlanningWorkflowSteps.AllAllowedSteps.Contains(step) && !sanitizedSteps.Contains(step))
                {
                    sanitizedSteps.Add(step);
                }
            }
        }

        if (sanitizedSteps.Count == 0)
        {
            sanitizedSteps.AddRange(PlanningWorkflowSteps.DefaultStepsByWorkflow[workflowType]);
        }

        // 5. ENFORCE MAXIMUM 3 FOLLOW-UP QUESTIONS
        // Combine, clean, and take at most 3 questions
        var questions = (decision.FollowUpQuestions ?? [])
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Select(q => q.Trim())
            .Distinct()
            .Take(3)
            .ToList();

        if ((decision.FollowUpQuestions?.Length ?? 0) > 3)
        {
            record.AuditEvents.Add(new PlanningAuditEvent
            {
                EventType = "QuestionLimitEnforced",
                Description = $"Model proposed {decision.FollowUpQuestions!.Length} questions; bounded to maximum 3 questions.",
                Metadata = null
            });
        }

        // 6. Dates & Times
        var preferredDate = !string.IsNullOrWhiteSpace(request.PreferredDate)
            ? request.PreferredDate
            : decision.PreferredDate;

        var preferredTime = !string.IsNullOrWhiteSpace(request.PreferredTime)
            ? request.PreferredTime
            : decision.PreferredTime;

        // 7. Safe response
        var safeResponse = !string.IsNullOrWhiteSpace(decision.SafeResponse)
            ? decision.SafeResponse
            : GetDefaultSafeResponse(workflowType);

        return new PlanningPlanDto
        {
            WorkflowType = workflowType.ToString(),
            AppointmentRequested = appointmentRequested,
            PatientConfirmationRequired = confirmationRequired,
            RequiredSteps = sanitizedSteps,
            PreferredDate = preferredDate,
            PreferredTime = preferredTime,
            FollowUpQuestions = questions,
            Rationale = decision.Rationale ?? string.Empty,
            SafeResponse = safeResponse
        };
    }

    private static bool HasExplicitBookingIntent(string text)
    {
        var lower = text.ToLowerInvariant();
        return ExplicitBookingKeywords.Any(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasSymptomKeywords(string text)
    {
        var lower = text.ToLowerInvariant();
        return SymptomKeywords.Any(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private static GeminiPlanningDecision DeterministicRuleBasedFallback(string objective, PlanningRequestDto request)
    {
        var lower = objective.ToLowerInvariant();

        if (lower.Contains("status") || lower.Contains("my appointment") || lower.Contains("when is my"))
        {
            return new GeminiPlanningDecision
            {
                WorkflowType = PlanningWorkflowType.AppointmentStatus.ToString(),
                AppointmentRequested = false,
                PatientConfirmationRequired = false,
                RequiredSteps = [.. PlanningWorkflowSteps.DefaultStepsByWorkflow[PlanningWorkflowType.AppointmentStatus]],
                FollowUpQuestions = [],
                Rationale = "User requested appointment status lookup.",
                SafeResponse = "I will check your current appointment status."
            };
        }

        var hasSymptoms = HasSymptomKeywords(objective);
        var hasBooking = HasExplicitBookingIntent(objective);

        if (hasSymptoms)
        {
            return new GeminiPlanningDecision
            {
                WorkflowType = PlanningWorkflowType.TriageThenAppointmentProposal.ToString(),
                AppointmentRequested = hasBooking,
                PatientConfirmationRequired = true,
                RequiredSteps = [.. PlanningWorkflowSteps.DefaultStepsByWorkflow[PlanningWorkflowType.TriageThenAppointmentProposal]],
                FollowUpQuestions = ["How long have you experienced these symptoms?", "Are you experiencing severe chest pain or shortness of breath?"],
                Rationale = "Clinical symptoms detected requiring safety triage assessment.",
                SafeResponse = "Your symptoms will be evaluated through our clinical safety triage protocol."
            };
        }

        if (hasBooking)
        {
            return new GeminiPlanningDecision
            {
                WorkflowType = PlanningWorkflowType.AppointmentProposal.ToString(),
                AppointmentRequested = true,
                PatientConfirmationRequired = true,
                RequiredSteps = [.. PlanningWorkflowSteps.DefaultStepsByWorkflow[PlanningWorkflowType.AppointmentProposal]],
                FollowUpQuestions = ["Do you have a preferred doctor or medical specialty?"],
                Rationale = "User requested to book or explore appointment options.",
                SafeResponse = "I can help search for available doctors and appointment slots."
            };
        }

        return new GeminiPlanningDecision
        {
            WorkflowType = PlanningWorkflowType.Unsupported.ToString(),
            AppointmentRequested = false,
            PatientConfirmationRequired = true,
            RequiredSteps = [.. PlanningWorkflowSteps.DefaultStepsByWorkflow[PlanningWorkflowType.Unsupported]],
            FollowUpQuestions = [],
            Rationale = "Request does not match supported triage or appointment workflows.",
            SafeResponse = "I can assist with symptom triage assessments, finding doctors, and checking appointments. Please let me know how I can help with your care."
        };
    }

    private static string GetDefaultSafeResponse(PlanningWorkflowType workflowType) => workflowType switch
    {
        PlanningWorkflowType.TriageThenAppointmentProposal => "We will guide your symptoms through the clinical safety triage process before evaluating appointment options.",
        PlanningWorkflowType.AppointmentProposal => "We will search for eligible doctors and available appointment slots based on your preferences.",
        PlanningWorkflowType.AppointmentStatus => "We will check your current appointment records.",
        _ => "I can only assist with hospital triage assessments, finding doctors, and appointment inquiries."
    };

    private static PlanningResponseDto MapToResponseDto(PlanningWorkflowRecord record)
    {
        return new PlanningResponseDto(
            record.WorkflowId,
            record.Status,
            record.Objective,
            record.Plan,
            record.CompletedStages,
            record.Errors,
            record.AuditEvents.Select(a => new PlanningAuditEventDto(a.Timestamp, a.EventType, a.Description, a.Metadata)).ToList(),
            record.CreatedAt);
    }
}
