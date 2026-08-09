using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Repositories;

namespace HospitalManagementSystem.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _context;
        private readonly IDoctorRepository _doctorRepo;

        public AuthService(ApplicationDbContext context, IDoctorRepository doctorRepo)
        {
            _context = context;
            _doctorRepo = doctorRepo;
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            {
                throw new ArgumentException("Email and password are required.");
            }

            var emailLower = dto.Email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);

            if (user == null || !DbInitializer.VerifyPassword(dto.Password, user.PasswordHash))
            {
                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            DoctorDto? doctorDto = null;

            if (user.Role.Equals("Doctor", StringComparison.OrdinalIgnoreCase))
            {
                var doctor = await _doctorRepo.GetByUserIdAsync(user.UserId);
                if (doctor != null)
                {
                    if (doctor.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Your doctor registration request is pending admin approval. You cannot log in yet.");
                    }
                    if (doctor.Status.Equals("Declined", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Your doctor registration request has been declined by an administrator.");
                    }

                    doctorDto = new DoctorDto
                    {
                        DoctorId = doctor.DoctorId,
                        UserId = doctor.UserId,
                        FirstName = doctor.FirstName,
                        LastName = doctor.LastName,
                        Email = doctor.Email,
                        NIC = doctor.NIC,
                        Specialization = doctor.Specialization,
                        SLMCLicenseNumber = doctor.SLMCLicenseNumber,
                        PhoneNumber = doctor.PhoneNumber,
                        Status = doctor.Status,
                        CreatedAt = doctor.CreatedAt,
                        ActionedAt = doctor.ActionedAt
                    };
                }
            }

            // Generate a simple token / token representation
            var token = $"token_{user.UserId}_{Guid.NewGuid():N}";

            return new AuthResponseDto
            {
                Token = token,
                UserId = user.UserId,
                Role = user.Role,
                Email = user.Email,
                FullName = user.FullName,
                DoctorProfile = doctorDto,
                Message = "Login successful"
            };
        }
    }
}
