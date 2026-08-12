using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IPatientService _patientService;

    public AuthController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher, IPatientService patientService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _patientService = patientService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (user is null ||
            !string.Equals(user.Role, "Patient", StringComparison.OrdinalIgnoreCase) ||
            _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid email or password." });

        return Ok(new LoginResponseDto { UserId = user.UserId, FullName = user.FullName, Email = user.Email, Role = user.Role });
    }

    [HttpPost("register")]
    public async Task<ActionResult<LoginResponseDto>> Register(RegisterPatientDto dto)
    {
        var email = dto.Email!.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(user => user.Email == email))
            return Conflict(new { message = "An account already exists for this email." });

        try
        {
            await _patientService.CreatePatientAsync(dto);
            var user = new User { FullName = $"{dto.FirstName.Trim()} {dto.LastName.Trim()}", Email = email, Role = "Patient" };
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Created("api/auth/login", new LoginResponseDto { UserId = user.UserId, FullName = user.FullName, Email = user.Email, Role = user.Role });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { message = exception.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.InnerException?.Message ?? ex.Message });
        }
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _context.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (user is null)
            return NotFound(new { message = "User not found." });

        if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword) == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "Incorrect current password." });

        user.PasswordHash = _passwordHasher.HashPassword(user, dto.NewPassword);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Password updated successfully." });
    }
}
