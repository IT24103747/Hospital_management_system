using System.Security.Claims;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController]
[Route("api/triage-workflows")]
[Authorize]
public sealed class TriageWorkflowController : ControllerBase
{
    private readonly IPatientService _patients;
    private readonly ITriageWorkflowService _workflows;

    public TriageWorkflowController(IPatientService patients, ITriageWorkflowService workflows)
    {
        _patients = patients;
        _workflows = workflows;
    }

    [HttpPost]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(typeof(TriageWorkflowDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Start([FromBody] StartTriageWorkflowDto request)
    {
        var patient = await CurrentPatient();
        if (patient is null) return NotFound(new { message = "No patient profile found for this account." });
        var workflow = await _workflows.StartForPatientAsync(patient.PatientId, request);
        return CreatedAtAction(nameof(GetMine), new { id = workflow.WorkflowId }, workflow);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Patient")]
    public async Task<IActionResult> GetMine(int id)
    {
        var patient = await CurrentPatient();
        if (patient is null) return NotFound(new { message = "No patient profile found for this account." });
        var workflow = await _workflows.GetForPatientAsync(id, patient.PatientId);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpGet("{id:int}/clinical-review")]
    [Authorize(Roles = "Admin,Doctor")]
    public async Task<IActionResult> GetForReview(int id)
    {
        var workflow = await _workflows.GetForClinicalReviewerAsync(id);
        return workflow is null ? NotFound() : Ok(workflow);
    }

    [HttpPost("{id:int}/review")]
    [Authorize(Roles = "Admin,Doctor")]
    public async Task<IActionResult> Review(int id, [FromBody] ReviewTriageWorkflowDto request)
    {
        try
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var workflow = await _workflows.ReviewAsync(id, userId, request);
            return workflow is null ? NotFound(new { message = "A pending workflow was not found." }) : Ok(workflow);
        }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    private async Task<PatientDto?> CurrentPatient()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        return string.IsNullOrWhiteSpace(email) ? null : await _patients.GetPatientByEmailAsync(email);
    }
}
