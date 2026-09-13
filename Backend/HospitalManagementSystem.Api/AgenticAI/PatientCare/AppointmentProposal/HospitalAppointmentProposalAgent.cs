using HospitalManagementSystem.Api.AgenticAI.PatientCare.ClinicalSafety;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.Shared;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;


public interface IHospitalAppointmentProposalAgent
{
    Task<HospitalAppointmentProposal> CreateAsync(AppointmentProposalRequest request, CancellationToken cancellationToken = default);
}

public sealed record AppointmentProposalRequest(
    int PatientId,
    ClinicalSafetyAssessment? ClinicalAssessment,
    string Specialty,
    DateOnly? PreferredDate = null,
    string? Period = null,
    DateOnly? ThroughDate = null);

public sealed record HospitalAppointmentProposal(
    string Status,
    int PatientId,
    IReadOnlyList<AgentDoctor> Doctors,
    IReadOnlyList<AgentSlot> Slots,
    IReadOnlyList<AgentToolExecution> ToolTrace,
    string Message,
    IReadOnlyList<string> SuggestedActions,
    int? ProposalId = null);

public sealed class HospitalAppointmentProposalAgent(IAppointmentAgentTools tools, IAppointmentProposalStore store) : IHospitalAppointmentProposalAgent
{
    public async Task<HospitalAppointmentProposal> CreateAsync(AppointmentProposalRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.PatientId <= 0) throw new ArgumentException("A valid patient is required.");
        if (string.IsNullOrWhiteSpace(request.Specialty) || request.Specialty.Length > 100)
            throw new ArgumentException("Provide a valid specialty.");
        if (request.ThroughDate.HasValue && (!request.PreferredDate.HasValue ||
            request.ThroughDate.Value.DayNumber - request.PreferredDate.Value.DayNumber is < 0 or > 6))
            throw new ArgumentException("Search a valid date range of at most seven days.");

        // Urgent and emergency routes must never be converted to routine appointment proposals.
        if (request.ClinicalAssessment is { FailedSafely: true } || request.ClinicalAssessment?.TriageLevel is "Emergency" or "Urgent")
            return new("BlockedByClinicalSafety", request.PatientId, [], [],
                [new("ValidateTriageRouteTool", "Blocked", false, "Urgent and emergency routes cannot create a normal appointment proposal.")],
                "The clinical safety result requires urgent care guidance instead of a normal appointment proposal.",
                ["Seek the urgent care guidance shown above.", "Do not wait for a normal appointment."]);

        var trace = new List<AgentToolExecution>();
        var doctors = await tools.FindDoctorsAsync(request.Specialty.Trim());
        trace.Add(new("FindEligibleDoctorsTool", doctors.Count == 0 ? "NoMatches" : "Completed", doctors.Count > 0, "Approved doctors were retrieved from the hospital service."));
        if (doctors.Count == 0)
            return new("NoOptions", request.PatientId, [], [], trace, "No approved doctors matched the requested specialty.",
                ["Choose another specialty.", "Contact the hospital directly if you need help choosing a service."]);

        var slots = new List<AgentSlot>();
        foreach (var doctor in doctors.Take(5))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (request.ThroughDate.HasValue)
            {
                for (var date = request.PreferredDate!.Value; date <= request.ThroughDate.Value; date = date.AddDays(1))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    slots.AddRange(await tools.FindSlotsAsync(doctor, date));
                }
            }
            else slots.AddRange(await tools.FindSlotsAsync(doctor, request.PreferredDate));
        }
        var verified = slots.Where(slot =>
                (!request.ThroughDate.HasValue || (DateOnly.FromDateTime(slot.StartAt.DateTime) >= request.PreferredDate && DateOnly.FromDateTime(slot.StartAt.DateTime) <= request.ThroughDate)) &&
                (request.Period == null || request.Period switch {
                    "morning" => slot.StartAt.Hour < 12,
                    "afternoon" => slot.StartAt.Hour >= 12 && slot.StartAt.Hour < 17,
                    "evening" => slot.StartAt.Hour >= 17,
                    _ => false
                }))
            .DistinctBy(slot => slot.DoctorTimeSlotId).OrderBy(slot => slot.StartAt).Take(5).ToArray();
        trace.Add(new("FindAvailableSlotsTool", verified.Length == 0 ? "NoMatches" : "Completed", verified.Length > 0, "Only current, available hospital slots were returned."));
        var proposalId = verified.Length == 0 ? (int?)null : await store.CreateAsync(request.PatientId, request.ClinicalAssessment?.TriageLevel ?? "NotAssessed", request.ClinicalAssessment?.RequiresClinicalReview ?? false, verified, cancellationToken);
        return new(verified.Length == 0 ? "NoOptions" : "PendingPatientConfirmation", request.PatientId, doctors.Take(5).ToArray(), verified,
            trace, verified.Length == 0 ? "No available future appointments matched your preferences." : "Select one verified option and explicitly confirm it before any booking is created.",
            verified.Length == 0
                ? ["Try another date.", "Choose another specialty.", "Check again later."]
                : ["Select one appointment option.", "Confirm your selection before booking."], proposalId);
    }
}
