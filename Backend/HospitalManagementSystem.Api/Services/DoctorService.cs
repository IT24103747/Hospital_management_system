using System.Text.RegularExpressions;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Api.Services;

public partial class DoctorService : IDoctorService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<User> _passwordHasher;

    public DoctorService(ApplicationDbContext db, IPasswordHasher<User> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task RegisterAsync(RegisterDoctorDto dto)
    {
        var firstName = ValidateName(dto.FirstName, "First name");
        var lastName = ValidateName(dto.LastName, "Last name");
        var email = dto.Email.Trim().ToLowerInvariant();
        var nic = dto.NIC.Trim().ToUpperInvariant();
        var license = dto.SlmcLicenseNumber.Trim().ToUpperInvariant();
        var phone = NormalizePhone(dto.PhoneNumber);

        if (!NicRegex().IsMatch(nic))
            throw new ArgumentException("NIC must use the Sri Lankan old format (9 digits followed by V/X) or new 12-digit format.");
        if (!PasswordLetterRegex().IsMatch(dto.Password) || !PasswordDigitRegex().IsMatch(dto.Password))
            throw new ArgumentException("Password must be at least 8 characters and contain at least one letter and one number.");
        if (string.IsNullOrWhiteSpace(dto.Specialization))
            throw new ArgumentException("Specialization is required.");
        if (!LicenseRegex().IsMatch(license))
            throw new ArgumentException("SLMC license number may contain only letters, numbers, spaces and hyphens.");
        if (await _db.Users.AnyAsync(user => user.Email == email))
            throw new InvalidOperationException("An account already exists for this email.");
        if (await _db.Doctors.AnyAsync(doctor => doctor.NIC == nic))
            throw new InvalidOperationException("A doctor registration already exists for this NIC.");
        if (await _db.Doctors.AnyAsync(doctor => doctor.SlmcLicenseNumber == license))
            throw new InvalidOperationException("A doctor registration already exists for this SLMC license number.");

        await using var transaction = await _db.Database.BeginTransactionAsync();
        var user = new User { FullName = $"{firstName} {lastName}", Email = email, Role = "Doctor" };
        user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
        _db.Users.Add(user);
        _db.Doctors.Add(new Doctor
        {
            User = user,
            FirstName = firstName,
            LastName = lastName,
            NIC = nic,
            Specialization = dto.Specialization.Trim(),
            SlmcLicenseNumber = license,
            PhoneNumber = phone,
            RegistrationStatus = DoctorRegistrationStatuses.Pending
        });
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<IEnumerable<DoctorDto>> GetRegistrationsAsync(string? status)
    {
        var query = _db.Doctors.Include(d => d.User).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = NormalizeStatus(status);
            query = query.Where(d => d.RegistrationStatus == normalized);
        }
        return (await query.OrderByDescending(d => d.CreatedAt).ToListAsync()).Select(Map);
    }

    public async Task<DoctorDto?> GetByIdAsync(int doctorId)
    {
        var doctor = await _db.Doctors.Include(d => d.User).AsNoTracking().SingleOrDefaultAsync(d => d.DoctorId == doctorId);
        return doctor is null ? null : Map(doctor);
    }

    public async Task<DoctorDto?> ReviewAsync(int doctorId, int adminUserId, bool approve, string? declineReason)
    {
        var doctor = await _db.Doctors.Include(d => d.User).SingleOrDefaultAsync(d => d.DoctorId == doctorId);
        if (doctor is null) return null;
        if (doctor.RegistrationStatus != DoctorRegistrationStatuses.Pending)
            throw new InvalidOperationException("Only pending doctor registrations can be reviewed.");

        doctor.RegistrationStatus = approve ? DoctorRegistrationStatuses.Approved : DoctorRegistrationStatuses.Declined;
        doctor.DeclineReason = approve ? null : declineReason?.Trim();
        doctor.ReviewedByUserId = adminUserId;
        doctor.ReviewedAt = DateTime.UtcNow;
        doctor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(doctor);
    }

    public async Task<DoctorDto?> GetByUserIdAsync(int userId)
    {
        var doctor = await _db.Doctors.Include(d => d.User).AsNoTracking().SingleOrDefaultAsync(d => d.UserId == userId);
        return doctor is null ? null : Map(doctor);
    }

    public async Task<DoctorDto?> UpdateProfileAsync(int userId, UpdateDoctorProfileDto dto)
    {
        var doctor = await _db.Doctors.Include(d => d.User).SingleOrDefaultAsync(d => d.UserId == userId);
        if (doctor is null) return null;
        doctor.FirstName = ValidateName(dto.FirstName, "First name");
        doctor.LastName = ValidateName(dto.LastName, "Last name");
        doctor.PhoneNumber = NormalizePhone(dto.PhoneNumber);
        doctor.User.FullName = $"{doctor.FirstName} {doctor.LastName}";
        doctor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(doctor);
    }

    private static string NormalizeStatus(string status)
    {
        var value = status.Trim();
        return value.ToLowerInvariant() switch
        {
            "pending" => DoctorRegistrationStatuses.Pending,
            "approved" => DoctorRegistrationStatuses.Approved,
            "declined" => DoctorRegistrationStatuses.Declined,
            _ => throw new ArgumentException("Registration status must be Pending, Approved, or Declined.")
        };
    }

    private static string ValidateName(string value, string label)
    {
        var name = value.Trim();
        if (!NameRegex().IsMatch(name))
            throw new ArgumentException($"{label} may contain only letters, spaces, apostrophes and hyphens.");
        return name;
    }

    private static string NormalizePhone(string value)
    {
        var compact = value.Replace(" ", "").Replace("-", "");
        if (compact.StartsWith("+94")) compact = $"0{compact[3..]}";
        if (!PhoneRegex().IsMatch(compact))
            throw new ArgumentException("Phone number must be a valid Sri Lankan mobile number (07XXXXXXXX or +947XXXXXXXX). ");
        return compact;
    }

    private static DoctorDto Map(Doctor doctor) => new()
    {
        DoctorId = doctor.DoctorId, UserId = doctor.UserId, FirstName = doctor.FirstName,
        LastName = doctor.LastName, Email = doctor.User.Email, NIC = doctor.NIC,
        Specialization = doctor.Specialization, SlmcLicenseNumber = doctor.SlmcLicenseNumber,
        PhoneNumber = doctor.PhoneNumber, RegistrationStatus = doctor.RegistrationStatus,
        DeclineReason = doctor.DeclineReason, CreatedAt = doctor.CreatedAt, ReviewedAt = doctor.ReviewedAt
    };

    [GeneratedRegex(@"^(?:\d{9}[VX]|\d{12})$", RegexOptions.IgnoreCase)] private static partial Regex NicRegex();
    [GeneratedRegex(@"^07\d{8}$")] private static partial Regex PhoneRegex();
    [GeneratedRegex(@"^[\p{L}][\p{L} '\-]*$")] private static partial Regex NameRegex();
    [GeneratedRegex(@"^[A-Z0-9][A-Z0-9 \-]{2,49}$", RegexOptions.IgnoreCase)] private static partial Regex LicenseRegex();
    [GeneratedRegex(@"[A-Za-z]")] private static partial Regex PasswordLetterRegex();
    [GeneratedRegex(@"\d")] private static partial Regex PasswordDigitRegex();
}
