using System.Text.RegularExpressions;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;

namespace HospitalManagementSystem.Api.Services
{
    public class DoctorService : IDoctorService
    {
        private readonly IDoctorRepository _doctorRepo;
        private readonly ApplicationDbContext _context;

        public DoctorService(IDoctorRepository doctorRepo, ApplicationDbContext context)
        {
            _doctorRepo = doctorRepo;
            _context = context;
        }

        public async Task<DoctorDto> RegisterDoctorAsync(DoctorRegisterDto dto)
        {
            // 1. Password validation
            if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 8)
            {
                throw new ArgumentException("Password must be at least 8 characters long.");
            }

            if (!Regex.IsMatch(dto.Password, @"[a-zA-Z]") || !Regex.IsMatch(dto.Password, @"[0-9]"))
            {
                throw new ArgumentException("Password must contain both letters and numbers.");
            }

            if (dto.Password != dto.ConfirmPassword)
            {
                throw new ArgumentException("Password and confirm password do not match.");
            }

            // 2. Sri Lankan Phone validation
            // Formats supported: 0771234567, +94771234567, 94771234567, 0112345678
            var cleanedPhone = dto.PhoneNumber.Trim().Replace(" ", "").Replace("-", "");
            if (!Regex.IsMatch(cleanedPhone, @"^(?:\+94|94|0)(?:7[01245678]\d{7}|[1-9]\d{8})$"))
            {
                throw new ArgumentException("Invalid Sri Lankan phone number format. Example: 0771234567 or +94771234567.");
            }

            // 3. Sri Lankan NIC validation
            // Old format: 9 digits + V/X. New format: 12 digits.
            var cleanedNic = dto.NIC.Trim();
            if (!Regex.IsMatch(cleanedNic, @"^([0-9]{9}[vVxX]|[0-9]{12})$"))
            {
                throw new ArgumentException("Invalid Sri Lankan NIC number. Must be 9 digits followed by V/X or 12 digits.");
            }

            // 4. Duplicate checks
            if (await _doctorRepo.ExistsByEmailAsync(dto.Email))
            {
                throw new InvalidOperationException("A doctor with this email address is already registered.");
            }

            if (await _doctorRepo.ExistsByNICAsync(cleanedNic))
            {
                throw new InvalidOperationException("A doctor with this NIC is already registered.");
            }

            if (await _doctorRepo.ExistsBySLMCAsync(dto.SLMCLicenseNumber.Trim()))
            {
                throw new InvalidOperationException("A doctor with this SLMC License Number is already registered.");
            }

            // 5. Create User account (Role = "Doctor")
            var user = new User
            {
                FullName = $"{dto.FirstName.Trim()} {dto.LastName.Trim()}",
                Email = dto.Email.Trim().ToLower(),
                PasswordHash = DbInitializer.HashPassword(dto.Password),
                Role = "Doctor",
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // 6. Create Doctor entity (Status = "Pending")
            var doctor = new Doctor
            {
                UserId = user.UserId,
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Email = dto.Email.Trim().ToLower(),
                NIC = cleanedNic,
                Specialization = dto.Specialization.Trim(),
                SLMCLicenseNumber = dto.SLMCLicenseNumber.Trim(),
                PhoneNumber = cleanedPhone,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            await _doctorRepo.CreateAsync(doctor);
            return MapToDto(doctor);
        }

        public async Task<IEnumerable<DoctorDto>> GetAllDoctorsAsync(string? status = null, string? search = null)
        {
            var doctors = await _doctorRepo.GetAllAsync(status, search);
            return doctors.Select(MapToDto);
        }

        public async Task<DoctorDto?> GetDoctorByIdAsync(int doctorId)
        {
            var doctor = await _doctorRepo.GetByIdAsync(doctorId);
            return doctor == null ? null : MapToDto(doctor);
        }

        public async Task<DoctorDto?> GetDoctorByUserIdAsync(int userId)
        {
            var doctor = await _doctorRepo.GetByUserIdAsync(userId);
            return doctor == null ? null : MapToDto(doctor);
        }

        public async Task<DoctorDto> ActionDoctorRequestAsync(int doctorId, string status)
        {
            var doctor = await _doctorRepo.GetByIdAsync(doctorId);
            if (doctor == null)
            {
                throw new KeyNotFoundException("Doctor request not found.");
            }

            var formattedStatus = status.Trim().ToLower() == "approved" ? "Approved" : "Declined";
            doctor.Status = formattedStatus;
            doctor.ActionedAt = DateTime.UtcNow;

            await _doctorRepo.UpdateAsync(doctor);
            return MapToDto(doctor);
        }

        public async Task<DoctorDto> UpdateDoctorProfileAsync(int doctorId, UpdateDoctorProfileDto dto)
        {
            var doctor = await _doctorRepo.GetByIdAsync(doctorId);
            if (doctor == null)
            {
                throw new KeyNotFoundException("Doctor profile not found.");
            }

            if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
            {
                throw new ArgumentException("First name and last name are required.");
            }

            var cleanedPhone = dto.PhoneNumber.Trim().Replace(" ", "").Replace("-", "");
            if (!Regex.IsMatch(cleanedPhone, @"^(?:\+94|94|0)(?:7[01245678]\d{7}|[1-9]\d{8})$"))
            {
                throw new ArgumentException("Invalid Sri Lankan phone number format. Example: 0771234567 or +94771234567.");
            }

            doctor.FirstName = dto.FirstName.Trim();
            doctor.LastName = dto.LastName.Trim();
            doctor.PhoneNumber = cleanedPhone;

            // Also update associated User FullName
            var user = await _context.Users.FindAsync(doctor.UserId);
            if (user != null)
            {
                user.FullName = $"{doctor.FirstName} {doctor.LastName}";
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
            }

            await _doctorRepo.UpdateAsync(doctor);
            return MapToDto(doctor);
        }

        private static DoctorDto MapToDto(Doctor doc)
        {
            return new DoctorDto
            {
                DoctorId = doc.DoctorId,
                UserId = doc.UserId,
                FirstName = doc.FirstName,
                LastName = doc.LastName,
                Email = doc.Email,
                NIC = doc.NIC,
                Specialization = doc.Specialization,
                SLMCLicenseNumber = doc.SLMCLicenseNumber,
                PhoneNumber = doc.PhoneNumber,
                Status = doc.Status,
                CreatedAt = doc.CreatedAt,
                ActionedAt = doc.ActionedAt
            };
        }
    }
}
