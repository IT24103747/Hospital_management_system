using System.Text.Json;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using Microsoft.EntityFrameworkCore;
using ProposalEntity = HospitalManagementSystem.Api.Models.AppointmentProposal;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;

public interface ISafetyApprovalTools
{
    Task<ProposalEntity?> GetOwnedProposalAsync(int proposalId, int patientId, CancellationToken cancellationToken);
    Task<AgentSlot?> ValidateSelectedSlotAsync(ProposalEntity proposal, int slotId, CancellationToken cancellationToken);
    Task<AgentBooking> FinalizeBookingAsync(AgentSlot slot, PatientDto patient, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

public sealed class SafetyApprovalTools(ApplicationDbContext db, IAppointmentAgentTools appointmentTools) : ISafetyApprovalTools
{
    public Task<ProposalEntity?> GetOwnedProposalAsync(int proposalId, int patientId, CancellationToken token) =>
        db.AppointmentProposals.SingleOrDefaultAsync(p => p.AppointmentProposalId == proposalId && p.PatientId == patientId, token);
    public async Task<AgentSlot?> ValidateSelectedSlotAsync(ProposalEntity proposal, int slotId, CancellationToken token)
    {
        var candidates = JsonSerializer.Deserialize<List<AgentSlot>>(proposal.CandidateSlotsJson) ?? [];
        var selected = candidates.SingleOrDefault(s => s.DoctorTimeSlotId == slotId);
        if (selected is null) return null;
        var fresh = await appointmentTools.FindSlotsAsync(new AgentDoctor($"D{selected.DoctorId}", selected.DoctorId, selected.DoctorName, selected.Specialty), DateOnly.FromDateTime(selected.StartAt.DateTime));
        var current = fresh.SingleOrDefault(s => s.DoctorTimeSlotId == slotId);
        // A patient approves these session details, not just an ID. The queue number
        // and remaining capacity may change before booking; the session itself may not.
        return current != null && current.DoctorId == selected.DoctorId &&
            current.DoctorName == selected.DoctorName && current.Specialty == selected.Specialty &&
            current.StartAt == selected.StartAt && current.EndAt == selected.EndAt &&
            current.ConsultationFee == selected.ConsultationFee && current.Location == selected.Location
                ? current : null;
    }
    public Task<AgentBooking> FinalizeBookingAsync(AgentSlot slot, PatientDto patient, CancellationToken token) => appointmentTools.BookAsync(slot, patient);
    public Task SaveAsync(CancellationToken token) => db.SaveChangesAsync(token);
}
