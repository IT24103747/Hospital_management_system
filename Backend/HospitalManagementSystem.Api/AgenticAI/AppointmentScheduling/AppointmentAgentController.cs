using System.Security.Claims;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HospitalManagementSystem.Api.AgenticAI.AppointmentScheduling;

[ApiController]
[Route("api/appointment-agent")]
[Authorize(Roles = "Admin,Patient")]
public sealed class AppointmentAgentController(
    IAppointmentSchedulingAgent agent, IPatientService patients,
    ILogger<AppointmentAgentController> logger) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentAgentResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Schedule(AppointmentAgentRequest request, CancellationToken cancellationToken)
    {
        PatientDto? patient = null;
        if (User.IsInRole("Patient"))
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email)) return Unauthorized(new { message = "The access token does not contain a user email." });
            patient = await patients.GetPatientByEmailAsync(email);
            if (patient is null) return NotFound(new { message = "No patient profile found for this account." });
            if (request.PatientId.HasValue && request.PatientId != patient.PatientId) return Forbid();
        }
        else if (request.PatientId.HasValue)
        {
            patient = await patients.GetPatientByIdAsync(request.PatientId.Value);
            if (patient is null) return NotFound(new { message = "Patient not found." });
        }
        if (request.AllowBooking && patient is null) return BadRequest(new { message = "Select a patient before requesting a booking." });

        try { return Ok(await agent.RunAsync(request, patient, cancellationToken)); }
        catch (AppointmentModelException)
        {
            logger.LogWarning("Appointment agent local model did not return a usable decision.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The local appointment assistant is unavailable. Please try again or use regular appointment booking." });
        }
        // The existing unique appointment-number constraint is the final concurrency guard.
        // Do not retry after a database write error using the same tracked context.
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: "23505" })
        {
            return Conflict(new { message = "This appointment number was just booked. Search again for current availability." });
        }
    }
}
