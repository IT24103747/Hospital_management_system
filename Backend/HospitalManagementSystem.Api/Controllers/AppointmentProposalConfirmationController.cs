using System.Security.Claims;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;
using HospitalManagementSystem.Api.Services;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController, Route("api/appointment-proposals"), Authorize(Roles = "Patient")]
public sealed class AppointmentProposalConfirmationController(ISafetyValidationApprovalAgent agent, IPatientService patients,
    ApplicationDbContext db, ITriageWorkflowService workflows) : ControllerBase
{
    [HttpPost("{proposalId:int}/confirm")]
    public async Task<IActionResult> Confirm(int proposalId, [FromBody] ConfirmProposalDto request, CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var patient = string.IsNullOrWhiteSpace(email) ? null : await patients.GetPatientByEmailAsync(email);
        if (patient is null) return NotFound();
        // Share the assistant's patient lock so old clients cannot race a unified confirmation.
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        if (db.Database.IsNpgsql())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1396916552, {patient.PatientId})", cancellationToken);
        var history = await workflows.GetHistoryForPatientAsync(patient.PatientId);
        if (history.Any(w => w.ApprovalStatus != TriageApprovalStatuses.Approved &&
            (w.RequiresHumanReview || w.Status is TriageWorkflowStatuses.FailedSafely or TriageWorkflowStatuses.PendingPatientInput)))
            return Ok(new SafetyApprovalResult("Rejected", true, "Complete the safety assessment or required clinical review before confirming an appointment."));
        var result = await agent.ConfirmAsync(new(proposalId, request.DoctorTimeSlotId), patient, cancellationToken);
        if (transaction != null) await transaction.CommitAsync(cancellationToken);
        return Ok(result);
    }
}
public sealed class ConfirmProposalDto { public int DoctorTimeSlotId { get; set; } }
