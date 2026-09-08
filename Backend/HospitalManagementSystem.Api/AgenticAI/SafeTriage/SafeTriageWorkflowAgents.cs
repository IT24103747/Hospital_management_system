using System.Diagnostics;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;

namespace HospitalManagementSystem.Api.AgenticAI.SafeTriage;

/// <summary>
/// Typed, least-privilege contracts for the SafeTriage workflow. Each agent has one purpose and
/// declares the single tool (if any) it may use. The coordinator, not the model, owns execution order.
/// </summary>
public interface ISafeTriageAgent
{
    string Name { get; }
    string ToolName { get; }
    Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default);
}

public sealed class SafeTriageAgentContext
{
    public SafeTriageAgentContext(StartTriageWorkflowDto request)
    {
        Request = request;
        Symptoms = request.Symptoms.Trim();
    }

    public StartTriageWorkflowDto Request { get; }
    public string Symptoms { get; }
    public List<string> ValidationProblems { get; } = [];
    public List<string> RedFlags { get; } = [];
    public List<string> UrgentFlags { get; } = [];
    public List<string> ClinicalReviewFlags { get; } = [];
    public List<string> PlannedInformationNeeds { get; } = [];
    public ClinicalExtractionResult? Extraction { get; set; }
    public string? ProposedRoute { get; set; }
    public bool IsWithinValidatedRoutineScope { get; set; }
    public bool RequiresClinicalApproval { get; set; }
    public bool FailedSafely { get; set; }
}

public sealed record SafeTriageAgentExecution(
    string Agent, string Tool, string Status, bool ValidationPassed,
    string Outcome, int DurationMs, int RetryCount = 0, string? ErrorCode = null);

public sealed class IntakeValidationAgent : ISafeTriageAgent
{
    public string Name => nameof(IntakeValidationAgent);
    public string ToolName => "ValidateVitalsTool";

    public Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        context.ValidationProblems.AddRange(SafeTriageRules.Validate(context.Request));
        context.FailedSafely = context.ValidationProblems.Count > 0;
        watch.Stop();
        return Task.FromResult(new SafeTriageAgentExecution(Name, ToolName,
            context.FailedSafely ? "FailedSafely" : "Completed", !context.FailedSafely,
            context.FailedSafely ? "Input rejected by deterministic vital-sign validation." : "Patient input passed deterministic validation.",
            (int)watch.ElapsedMilliseconds, 0, context.FailedSafely ? "InvalidOrSuspiciousInput" : null));
    }
}

public sealed class SafetyRedFlagAgent : ISafeTriageAgent
{
    public string Name => nameof(SafetyRedFlagAgent);
    public string ToolName => "EvaluateRedFlagsTool";

    public Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        context.RedFlags.AddRange(SafeTriageRules.FindEmergencyFlags(context.Symptoms));
        context.UrgentFlags.AddRange(SafeTriageRules.FindUrgentFlags(context.Symptoms));
        context.ClinicalReviewFlags.AddRange(SafeTriageRules.FindClinicalReviewFlags(context.Symptoms));
        context.IsWithinValidatedRoutineScope = SafeTriageRules.IsWithinValidatedRoutineScope(context.Symptoms);
        context.RequiresClinicalApproval = context.RedFlags.Count > 0 || context.UrgentFlags.Count > 0 || context.ClinicalReviewFlags.Count > 0;
        watch.Stop();
        var status = context.RedFlags.Count > 0 ? "EmergencyEscalation"
            : context.UrgentFlags.Count > 0 ? "UrgentAssessment"
            : context.ClinicalReviewFlags.Count > 0 ? "ClinicalReviewRequired"
            : "Completed";
        return Task.FromResult(new SafeTriageAgentExecution(Name, ToolName, status, true,
            context.RequiresClinicalApproval ? "Configured safety rule requires clinical review." : "No configured escalation rule matched.",
            (int)watch.ElapsedMilliseconds));
    }
}

public sealed class ClinicalInformationExtractionWorkflowAgent(IClinicalInformationExtractionAgent extractionTool) : ISafeTriageAgent
{
    public string Name => "ClinicalInformationExtractionAgent";
    public string ToolName => "OllamaStructuredExtractionTool";

    public async Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        const int maxAttempts = 2;
        var attempts = 0;
        do
        {
            attempts++;
            context.Extraction = await extractionTool.ExtractAsync(context.Symptoms, !context.Request.IsFollowUp, cancellationToken);
        } while (context.Extraction.Status != "Completed" && attempts < maxAttempts && !cancellationToken.IsCancellationRequested);
        watch.Stop();
        var completed = context.Extraction.Status == "Completed";
        return new SafeTriageAgentExecution(Name, ToolName, context.Extraction.Status, completed,
            completed ? "Structured non-diagnostic extraction passed schema and safety validation." : "Structured extraction was unavailable; safe fallback required.",
            (int)watch.ElapsedMilliseconds, attempts - 1, context.Extraction.ErrorCode);
    }
}

public sealed class CareRoutingAgent : ISafeTriageAgent
{
    public string Name => nameof(CareRoutingAgent);
    public string ToolName => "CreateEscalationProposalTool";

    public Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        context.ProposedRoute = context.RedFlags.Count > 0 ? "Emergency Department"
            : context.UrgentFlags.Count > 0 ? "Urgent medical assessment"
            : context.ClinicalReviewFlags.Count > 0 ? "Professional clinical review"
            : context.IsWithinValidatedRoutineScope ? "Routine information with safety-netting"
            : context.Request.IsFollowUp ? "Professional clinical review"
            : "Clarification required";
        context.RequiresClinicalApproval |= context.RedFlags.Count > 0 || context.UrgentFlags.Count > 0 ||
                                            context.ClinicalReviewFlags.Count > 0 ||
                                            (!context.IsWithinValidatedRoutineScope && context.Request.IsFollowUp);
        watch.Stop();
        return Task.FromResult(new SafeTriageAgentExecution(Name, ToolName, "Proposed", true,
            $"Controlled route proposed: {context.ProposedRoute}.", (int)watch.ElapsedMilliseconds));
    }
}

public sealed class AdaptiveQuestionPlanningAgent : ISafeTriageAgent
{
    public string Name => nameof(AdaptiveQuestionPlanningAgent);
    public string ToolName => "RankMissingInformationTool";

    public Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        if (context.Request.IsFollowUp)
        {
            watch.Stop();
            return Task.FromResult(new SafeTriageAgentExecution(Name, ToolName, "Completed", true,
                "Final clarification round reached; no additional question cycle was planned.",
                (int)watch.ElapsedMilliseconds));
        }
        var facts = context.Extraction?.Facts;
        context.PlannedInformationNeeds.Add("warning signs");
        if (facts?.DurationMinutes is null && facts?.DurationDays is null)
            context.PlannedInformationNeeds.Add("onset and duration");
        if (facts?.SeverityScore is null && context.PlannedInformationNeeds.Count < 3)
            context.PlannedInformationNeeds.Add("severity and functional impact");
        if (context.PlannedInformationNeeds.Count < 3)
            context.PlannedInformationNeeds.Add("relevant medical risk context");
        foreach (var missing in context.Extraction?.MissingInformation ?? [])
        {
            if (context.PlannedInformationNeeds.Count >= 3) break;
            if (!context.PlannedInformationNeeds.Contains(missing, StringComparer.OrdinalIgnoreCase))
                context.PlannedInformationNeeds.Add(missing);
        }
        watch.Stop();
        return Task.FromResult(new SafeTriageAgentExecution(Name, ToolName, "Planned", true,
            $"Ranked {context.PlannedInformationNeeds.Count} decision-relevant information needs; maximum follow-up count is 3.",
            (int)watch.ElapsedMilliseconds));
    }
}

public sealed class StructuredSafetyAssessmentAgent : ISafeTriageAgent
{
    public string Name => nameof(StructuredSafetyAssessmentAgent);
    public string ToolName => "EvaluateGroundedClinicalFactsTool";

    public Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var facts = context.Extraction?.Facts;
        if (facts is not null)
        {
            context.RedFlags.AddRange(SafeTriageRules.FindEmergencyFlags(facts));
            context.UrgentFlags.AddRange(SafeTriageRules.FindUrgentFlags(facts));
            context.ClinicalReviewFlags.AddRange(SafeTriageRules.FindClinicalReviewFlags(facts));
            if (SafeTriageRules.HasGroundedConcept(facts)) context.IsWithinValidatedRoutineScope = true;
        }
        Deduplicate(context.RedFlags);
        Deduplicate(context.UrgentFlags);
        Deduplicate(context.ClinicalReviewFlags);
        context.RequiresClinicalApproval = context.RedFlags.Count > 0 || context.UrgentFlags.Count > 0 || context.ClinicalReviewFlags.Count > 0;
        watch.Stop();
        var status = context.RedFlags.Count > 0 ? "EmergencyEscalation"
            : context.UrgentFlags.Count > 0 ? "UrgentAssessment"
            : context.ClinicalReviewFlags.Count > 0 ? "ClinicalReviewRequired"
            : "Completed";
        return Task.FromResult(new SafeTriageAgentExecution(Name, ToolName, status, true,
            facts is null ? "No structured facts were available; raw-text safety results were retained."
                : "Grounded structured clinical facts were evaluated by deterministic safety policy.",
            (int)watch.ElapsedMilliseconds));
    }

    private static void Deduplicate(List<string> values)
    {
        var distinct = values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        values.Clear();
        values.AddRange(distinct);
    }
}

public sealed class SafetyValidationAgent : ISafeTriageAgent
{
    public string Name => nameof(SafetyValidationAgent);
    public string ToolName => "ValidateWorkflowOutcomeTool";

    public Task<SafeTriageAgentExecution> ExecuteAsync(SafeTriageAgentContext context, CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var valid = !string.IsNullOrWhiteSpace(context.ProposedRoute) &&
                    (!context.RequiresClinicalApproval || context.RedFlags.Count > 0 || context.UrgentFlags.Count > 0 ||
                     context.ClinicalReviewFlags.Count > 0 || context.Extraction?.Status != "Completed" ||
                     !context.IsWithinValidatedRoutineScope);
        if (!valid) context.FailedSafely = true;
        watch.Stop();
        return Task.FromResult(new SafeTriageAgentExecution(Name, ToolName, valid ? "Accepted" : "FailedSafely", valid,
            valid ? "Deterministic workflow validation accepted the controlled outcome." : "Workflow outcome failed deterministic validation.",
            (int)watch.ElapsedMilliseconds, 0, valid ? null : "WorkflowValidationFailed"));
    }
}

public sealed class SafeTriageWorkflowCoordinator
{
    private const int MaxSteps = 7;
    private readonly IntakeValidationAgent _intake = new();
    private readonly SafetyRedFlagAgent _redFlags = new();
    private readonly ClinicalInformationExtractionWorkflowAgent _extraction;
    private readonly StructuredSafetyAssessmentAgent _structuredSafety = new();
    private readonly AdaptiveQuestionPlanningAgent _questionPlanning = new();
    private readonly CareRoutingAgent _routing = new();
    private readonly SafetyValidationAgent _validation = new();

    public SafeTriageWorkflowCoordinator(IClinicalInformationExtractionAgent extractionTool) => _extraction = new(extractionTool);

    public async Task<(SafeTriageAgentContext Context, IReadOnlyList<SafeTriageAgentExecution> Trace)> RunAsync(StartTriageWorkflowDto request, CancellationToken cancellationToken = default)
    {
        var context = new SafeTriageAgentContext(request);
        var trace = new List<SafeTriageAgentExecution>();
        async Task Run(ISafeTriageAgent agent)
        {
            if (trace.Count >= MaxSteps) throw new InvalidOperationException("SafeTriage workflow reached its hard execution-step limit.");
            trace.Add(await agent.ExecuteAsync(context, cancellationToken));
        }

        await Run(_intake);
        if (context.FailedSafely) return (context, trace);
        await Run(_redFlags);
        if (!context.RequiresClinicalApproval)
        {
            await Run(_extraction);
            await Run(_structuredSafety);
            if (!context.RequiresClinicalApproval) await Run(_questionPlanning);
        }
        await Run(_routing);
        await Run(_validation);
        return (context, trace);
    }
}

internal static class SafeTriageRules
{
    private static readonly string[] EmergencyPhrases =
    [
        "severe chest pain", "difficulty breathing", "severe breathing difficulty", "cannot breathe", "choking",
        "loss of consciousness", "unconscious", "signs of stroke", "face drooping", "one-sided weakness",
        "severe bleeding", "coughing up blood", "vomiting blood", "seizure", "severe allergic reaction",
        "severe confusion", "blue lips", "suicidal thoughts", "kill myself", "overdose", "poisoning",
        "lost an eye", "lose an eye", "loose an eye", "loose eye", "dislodged eye", "eye came out", "eye is out", "eye missing"
    ];

    private static readonly string[] UrgentPhrases =
    [
        "chest pain", "chest discomfort", "sudden severe pain", "high fever", "persistent vomiting",
        "unable to keep fluids down", "signs of dehydration"
    ];

    private static readonly string[] SeriousContextPhrases =
    [
        "cancer", "tumour", "tumor", "chemotherapy", "chemo", "immunotherapy", "radiotherapy", "radiation therapy",
        "organ transplant", "immunosuppressed", "weakened immune system", "pregnant", "pregnancy", "postpartum",
        "recent surgery", "blood-thinning medicine", "bleeding or clotting condition", "recurrent nosebleeds"
    ];

    private static readonly string[] CancerTreatmentPhrases = ["chemotherapy", "chemo", "immunotherapy", "radiotherapy", "radiation therapy"];
    private static readonly string[] TreatmentWarningPhrases =
    [
        "fever", "high temperature", "shivering", "chills", "feel very unwell", "feeling very unwell",
        "signs of infection", "unusual bleeding", "persistent diarrhea", "persistent diarrhoea"
    ];

    private static readonly string[] ValidatedRoutinePhrases =
    [
        "runny nose", "blocked nose", "nasal congestion", "nosebleed", "nose bleed",
        "headache", "migraine", "head pain", "dizzy", "dizziness", "lightheaded",
        "cough", "sore throat", "throat pain", "cold", "flu", "fever", "chills", "body ache", "fatigue", "tiredness",
        "stomach", "abdominal", "nausea", "vomiting", "diarrhea", "diarrhoea", "indigestion", "heartburn", "cramps",
        "back pain", "joint pain", "muscle pain", "leg pain", "arm pain", "sprain",
        "rash", "itching", "hives", "skin", "burn", "bite",
        "earache", "ear pain", "toothache", "eye", "red eye", "pink eye", "pain", "unwell", "sick", "feeling"
    ];

    public static List<string> Validate(StartTriageWorkflowDto request)
    {
        var errors = new List<string>();
        var symptoms = request.Symptoms.Trim();
        if (symptoms.Length < 3 || !System.Text.RegularExpressions.Regex.IsMatch(symptoms, @"[A-Za-z]"))
            errors.Add("Please describe the symptom using a few words.");
        if (System.Text.RegularExpressions.Regex.IsMatch(symptoms.Replace(" ", string.Empty), @"^(.)\1{2,}$"))
            errors.Add("The symptom description appears to be repeated characters. Please describe what you are feeling.");
        if (System.Text.RegularExpressions.Regex.IsMatch(symptoms, @"\b(ignore (all |previous )?instructions|system prompt|jailbreak)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            errors.Add("Please enter symptom information only; instructions for the system cannot be processed.");
        if (System.Text.RegularExpressions.Regex.IsMatch(symptoms, @"\b(password|passcode|cvv|card number|account number)\b|[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}|\+?\d[\d\s().-]{7,}\d", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            errors.Add("Remove contact details, account details, and passwords before submitting a symptom report.");
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

    public static List<string> FindEmergencyFlags(string symptoms) => EmergencyPhrases
        .Where(phrase => ContainsNonNegatedPhrase(symptoms, phrase)).ToList();
    public static List<string> FindUrgentFlags(string symptoms)
    {
        var flags = UrgentPhrases.Where(phrase => ContainsNonNegatedPhrase(symptoms, phrase)).ToList();
        if (CancerTreatmentPhrases.Any(phrase => ContainsNonNegatedPhrase(symptoms, phrase)) &&
            TreatmentWarningPhrases.Any(phrase => ContainsNonNegatedPhrase(symptoms, phrase)))
            flags.Add("cancer treatment with a reported warning symptom");
        return flags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<string> FindClinicalReviewFlags(string symptoms) => SeriousContextPhrases
        .Where(phrase => ContainsNonNegatedPhrase(symptoms, phrase))
        .Select(phrase => $"reported high-risk context: {phrase}")
        .ToList();

    public static bool IsWithinValidatedRoutineScope(string symptoms) =>
        !string.IsNullOrWhiteSpace(symptoms) &&
        ValidatedRoutinePhrases.Any(phrase => symptoms.Contains(phrase, StringComparison.OrdinalIgnoreCase));

    public static List<string> FindEmergencyFlags(ClinicalFactSet facts)
    {
        var flags = new List<string>();
        foreach (var warning in facts.WarningSigns.Where(warning => IsGrounded(facts, "warningSigns", warning)))
            flags.AddRange(FindEmergencyFlags(warning));

        if (IsGrounded(facts, "primaryConcept", facts.PrimaryConcept) &&
            string.Equals(facts.PrimaryConcept, "nosebleed", StringComparison.OrdinalIgnoreCase) &&
            IsGrounded(facts, "currentlyActive") && facts.CurrentlyActive == true &&
            IsGrounded(facts, "durationMinutes") && facts.DurationMinutes >= 15)
            flags.Add("grounded active nosebleed lasting at least 15 minutes");

        if (string.Equals(facts.PrimaryConcept, "nosebleed", StringComparison.OrdinalIgnoreCase) &&
            facts.WarningSigns.Any(warning => IsGrounded(facts, "warningSigns", warning)))
            flags.Add("grounded nosebleed warning sign");
        return flags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<string> FindUrgentFlags(ClinicalFactSet facts)
    {
        var flags = facts.WarningSigns.Where(warning => IsGrounded(facts, "warningSigns", warning))
            .SelectMany(FindUrgentFlags).ToList();
        if (facts.TemperatureCelsius >= 39.5m && IsGrounded(facts, "temperatureCelsius"))
            flags.Add("grounded measured temperature of at least 39.5°C");
        return flags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static List<string> FindClinicalReviewFlags(ClinicalFactSet facts) => facts.RiskContexts
        .Where(risk => IsGrounded(facts, "riskContexts", risk))
        .SelectMany(FindClinicalReviewFlags)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    public static bool HasGroundedConcept(ClinicalFactSet facts) =>
        !string.IsNullOrWhiteSpace(facts.PrimaryConcept) && IsGrounded(facts, "primaryConcept", facts.PrimaryConcept);

    private static bool IsGrounded(ClinicalFactSet facts, string field, string? value = null) =>
        facts.Evidence.Any(item => string.Equals(item.Field, field, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(item.Quote) &&
            (value is null || string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase)));

    private static bool ContainsNonNegatedPhrase(string text, string phrase)
    {
        var start = 0;
        while (start < text.Length)
        {
            var index = text.IndexOf(phrase, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return false;
            var prefix = text[..index];
            var nearby = prefix[Math.Max(0, prefix.Length - 45)..];
            if (!System.Text.RegularExpressions.Regex.IsMatch(nearby,
                    @"\b(no|not|without|deny|denies|never|don't|do not)\b(?:\W+\w+){0,4}\W*$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return true;
            start = index + phrase.Length;
        }
        return false;
    }
}
