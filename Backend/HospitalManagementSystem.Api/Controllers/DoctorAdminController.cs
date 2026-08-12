using System.Security.Claims;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/doctor-registrations")]
public class DoctorAdminController : ControllerBase
{
    private readonly IDoctorService _service;
    public DoctorAdminController(IDoctorService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        try { return Ok(await _service.GetRegistrationsAsync(status)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var doctor = await _service.GetByIdAsync(id);
        return doctor is null ? NotFound(new { message = "Doctor registration was not found." }) : Ok(doctor);
    }

    [HttpPost("{id:int}/approve")]
    public Task<IActionResult> Approve(int id) => Review(id, true, null);

    [HttpPost("{id:int}/decline")]
    public Task<IActionResult> Decline(int id, DeclineDoctorDto dto) => Review(id, false, dto.Reason);

    private async Task<IActionResult> Review(int id, bool approve, string? reason)
    {
        try
        {
            var adminId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var doctor = await _service.ReviewAsync(id, adminId, approve, reason);
            return doctor is null ? NotFound(new { message = "Doctor registration was not found." }) : Ok(doctor);
        }
        catch (InvalidOperationException exception) { return Conflict(new { message = exception.Message }); }
    }
}
