using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.ClinicalSafety;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.Shared;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare;

[ApiController]
[Route("api/patient-care")]
[Authorize(Roles = "Patient")]
public sealed class PatientCareController(
    IClinicalSafetyTriageAgent clinicalSafety,
    IHospitalAppointmentProposalAgent appointmentProposal,
    IPatientCareAssessmentStore assessments,
    IPatientService patients) : ControllerBase
{
    [HttpPost("triage-appointment-proposal")]
    public async Task<IActionResult> CreateProposal([FromBody] TriageAppointmentProposalRequest request, CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var patient = string.IsNullOrWhiteSpace(email) ? null : await patients.GetPatientByEmailAsync(email);
        if (patient is null) return NotFound(new { message = "No patient profile was found for this account." });

        var clinical = await clinicalSafety.AssessAsync(new(request.Symptoms, request.Vitals), cancellationToken);
        if (clinical.FailedSafely || clinical.TriageLevel is "Emergency" or "Urgent")
            return Ok(await SaveResponse(patient.PatientId, request, clinical, null, cancellationToken));

        if (!request.RequestAppointmentProposal)
            return Ok(await SaveResponse(patient.PatientId, request, clinical, null, cancellationToken));

        if (string.IsNullOrWhiteSpace(request.Specialty) || request.Specialty.Trim().Length < 2)
            return BadRequest(new { message = "Choose a specialty before requesting appointment options." });

        var proposal = await appointmentProposal.CreateAsync(new(patient.PatientId, clinical, request.Specialty!, request.PreferredDate), cancellationToken);
        return Ok(await SaveResponse(patient.PatientId, request, clinical, proposal, cancellationToken));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var patient = string.IsNullOrWhiteSpace(email) ? null : await patients.GetPatientByEmailAsync(email);
        if (patient is null) return NotFound(new { message = "No patient profile was found for this account." });
        var history = await assessments.GetHistoryAsync(patient.PatientId, cancellationToken);
        return Ok(history.Select(item => new {
            assessmentId = item.PatientCareAssessmentId, item.Symptoms, item.RequestedSpecialty,
            item.RequestedAppointmentProposal, item.TriageLevel, item.Status, item.CreatedAt,
            clinical = ParseJson(item.ClinicalJson), proposal = ParseJson(item.ProposalJson)
        }));
    }

    private async Task<object> SaveResponse(int patientId, TriageAppointmentProposalRequest request, ClinicalSafetyAssessment clinical,
        HospitalAppointmentProposal? proposal, CancellationToken token)
    {
        var assessmentId = await assessments.SaveAsync(patientId, request.Symptoms, request.Specialty,
            request.RequestAppointmentProposal, clinical.TriageLevel, clinical, proposal, token);
        return new { assessmentId, clinical, proposal };
    }

    private static JsonElement? ParseJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        using var document = JsonDocument.Parse(value);
        return document.RootElement.Clone();
    }
}

public sealed class TriageAppointmentProposalRequest
{
    [Required, StringLength(4000, MinimumLength = 3)] public string Symptoms { get; set; } = string.Empty;
    [StringLength(100, MinimumLength = 2)] public string? Specialty { get; set; }
    public bool RequestAppointmentProposal { get; set; }
    public DateOnly? PreferredDate { get; set; }
    public TriageVitalsDto? Vitals { get; set; }
}
