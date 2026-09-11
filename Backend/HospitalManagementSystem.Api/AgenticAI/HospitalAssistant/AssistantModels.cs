using System.ComponentModel.DataAnnotations;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.ClinicalSafety;
using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;

public sealed class AssistantConversation
{
    public Guid AssistantConversationId { get; set; } = Guid.NewGuid();
    public int PatientId { get; set; }
    public Guid InitialRequestId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StateJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
public sealed class AssistantMessageRequest
{
    public Guid? ConversationId { get; set; }
    [Required, StringLength(4000, MinimumLength = 1)] public string Message { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
}
public sealed class AssistantActionRequest
{
    public Guid ActionId { get; set; }
    [Required] public string Decision { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public int? DoctorTimeSlotId { get; set; }
    public int? AppointmentId { get; set; }
}
public sealed record AssistantCapability(string Id, string Label, bool Enabled, string Prompt);
public sealed record AssistantMessage(string Id, string Role, string Text, DateTime CreatedAt, IReadOnlyList<string> Progress);
public sealed record AssistantQuestion(string Id, string Prompt, bool Required);
public sealed class AssistantPendingAction
{
    public Guid ActionId { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "book";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<AgentSlot> Slots { get; set; } = [];
    public IReadOnlyList<AppointmentDto> Appointments { get; set; } = [];
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(30);
    public int? ProposalId { get; set; }
}
public sealed class AssistantState
{
    public string State { get; set; } = "COMPLETED";
    public List<AssistantMessage> Messages { get; set; } = [];
    public AssistantPendingAction? PendingAction { get; set; }
    public List<AssistantQuestion> Questions { get; set; } = [];
    public IReadOnlyList<AppointmentDto> Appointments { get; set; } = [];
    public IReadOnlyList<AgentSlot> Slots { get; set; } = [];
    public IReadOnlyList<AgentDoctor> Doctors { get; set; } = [];
    public Dictionary<Guid, string> Requests { get; set; } = [];
    public ClinicalSafetyAssessment? Clinical { get; set; }
    public int? WorkflowId { get; set; }
    public List<TriageAnswerDto> Answers { get; set; } = [];
    public bool SafetyBlocked { get; set; }
    public string? SearchQuery { get; set; }
    public DateOnly? PreferredDate { get; set; }
    public DateOnly? ThroughDate { get; set; }
    public string? Period { get; set; }
    public bool WantsAppointment { get; set; }
    public string? Awaiting { get; set; }
    public string? CancellationReason { get; set; }
}
public sealed record AssistantConversationResponse(Guid ConversationId, string Title, string State,
    DateTime UpdatedAt, IReadOnlyList<AssistantMessage> Messages, AssistantPendingAction? PendingAction,
    IReadOnlyList<AssistantQuestion> Questions, IReadOnlyList<AppointmentDto> Appointments,
    IReadOnlyList<AgentSlot> Slots, IReadOnlyList<AgentDoctor> Doctors, IReadOnlyList<AssistantCapability> Capabilities);

// Register future capability handlers here and in the coordinator; clients consume this list.
public interface IHospitalAssistantReadAgent
{
    AssistantCapability Capability { get; }
    bool CanHandle(string message);
    Task<string> ReadAsync(string message, PatientDto patient, CancellationToken cancellationToken);
}

public sealed class AssistantAgentRegistry(IEnumerable<IHospitalAssistantReadAgent> additionalAgents)
{
    public IReadOnlyList<IHospitalAssistantReadAgent> AdditionalAgents { get; } = additionalAgents.ToArray();
    public IReadOnlyList<AssistantCapability> Capabilities => new AssistantCapability[] {
        new("patient-help", "Patient Help", true, "I need help with my symptoms"),
        new("find-doctor", "Find Doctor", true, "Find a doctor"),
        new("appointments", "Appointments", true, "Show my appointments"),
        new("medical-reports", "Medical Reports", false, ""),
        new("doctor-schedules", "Doctor Schedules", false, "")
    }.Where(capability => !AdditionalAgents.Any(agent => agent.Capability.Id == capability.Id))
        .Concat(AdditionalAgents.Select(agent => agent.Capability)).ToArray();
}
