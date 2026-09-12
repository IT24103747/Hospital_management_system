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
        return fresh.SingleOrDefault(s => s.DoctorTimeSlotId == slotId);
    }
    public Task<AgentBooking> FinalizeBookingAsync(AgentSlot slot, PatientDto patient, CancellationToken token) => appointmentTools.BookAsync(slot, patient);
    public Task SaveAsync(CancellationToken token) => db.SaveChangesAsync(token);
}
