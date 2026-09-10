using System.Security.Claims;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController, Route("api/appointment-proposals"), Authorize(Roles = "Patient")]
public sealed class AppointmentProposalConfirmationController(ISafetyValidationApprovalAgent agent, IPatientService patients) : ControllerBase
{
    [HttpPost("{proposalId:int}/confirm")]
    public async Task<IActionResult> Confirm(int proposalId, [FromBody] ConfirmProposalDto request, CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var patient = string.IsNullOrWhiteSpace(email) ? null : await patients.GetPatientByEmailAsync(email);
        if (patient is null) return NotFound();
        return Ok(await agent.ConfirmAsync(new(proposalId, request.DoctorTimeSlotId), patient, cancellationToken));
    }
}
public sealed class ConfirmProposalDto { public int DoctorTimeSlotId { get; set; } }
