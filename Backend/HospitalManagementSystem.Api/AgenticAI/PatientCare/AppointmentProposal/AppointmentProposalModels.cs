using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;

public sealed class AppointmentAgentRequest
{
    [Required, StringLength(2000, MinimumLength = 3)]
    public string Message { get; set; } = string.Empty;
    // An explicit API intent, not a permission the model can grant itself.
    public bool AllowBooking { get; set; }
    [Range(1, int.MaxValue)]
    public int? PatientId { get; set; }
}

public sealed record AppointmentAgentResponse(
    string Status, string Message,
    IReadOnlyList<AgentDoctor> Doctors,
    IReadOnlyList<AgentSlot> Slots,
    AgentBooking? Booking = null);

public sealed record AgentDoctor(string Reference, int DoctorId, string Name, string Specialty);
public sealed record AgentSlot(
    string Reference, int DoctorTimeSlotId, int DoctorId, string DoctorName, string Specialty,
    DateTimeOffset StartAt, DateTimeOffset EndAt, int AppointmentNumber,
    int AvailableCount, decimal ConsultationFee, string Location);
public sealed record AgentBooking(int AppointmentId, int DoctorTimeSlotId, int AppointmentNumber,
    string DoctorName, DateTimeOffset StartAt, string Status);

public sealed class AppointmentAgentOptions
{
    public const string SectionName = "AppointmentAgent";
    // Keep this key in User Secrets locally and the cloud platform's secret store in deployment.
    // It must never be returned to React or Flutter.
    public string GeminiApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.5-flash";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxSteps { get; set; } = 8;
}

// Model output contains references and search terms, never writable database DTOs.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AppointmentAgentDecision
{
    public string Action { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string DoctorRef { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string SlotRef { get; set; } = string.Empty;
    public string[] SlotRefs { get; set; } = [];
    public string[] DoctorRefs { get; set; } = [];
    public string Outcome { get; set; } = string.Empty;
}

public sealed record AppointmentAgentMessage(string Role, string Content);
public sealed class AppointmentModelException(string message, Exception? inner = null) : Exception(message, inner);
