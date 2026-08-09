using System.ComponentModel.DataAnnotations;

namespace HospitalManagementSystem.Api.DTOs;

public class LoginDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class RegisterPatientDto : CreatePatientDto
{
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
}
