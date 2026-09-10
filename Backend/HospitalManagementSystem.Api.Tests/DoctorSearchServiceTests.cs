using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HospitalManagementSystem.Api.Tests;

public class DoctorSearchServiceTests
{
    [Theory]
    [InlineData("nimal")]
    [InlineData("PERERA")]
    [InlineData("cardio")]
    public async Task SearchApprovedDoctorsAsync_FindsByNameOrSpecialization(string query)
    {
        await using var db = CreateContext();
        await SeedDoctors(db);
        var service = CreateService(db);

        var results = await service.SearchApprovedDoctorsAsync(query);

        var doctor = Assert.Single(results);
        Assert.Equal("Nimal Perera", doctor.FullName);
        Assert.Equal("Cardiology", doctor.Specialization);
    }

    [Fact]
    public async Task SearchApprovedDoctorsAsync_HidesPendingAndDeclinedDoctors()
    {
        await using var db = CreateContext();
        await SeedDoctors(db);
        var service = CreateService(db);

        var results = await service.SearchApprovedDoctorsAsync(null);

        var doctor = Assert.Single(results);
        Assert.Equal("Nimal Perera", doctor.FullName);
    }

    [Fact]
    public async Task GetApprovedDoctorProfileAsync_ReturnsOnlyApprovedDoctorDetails()
    {
        await using var db = CreateContext();
        var ids = await SeedDoctors(db);
        var service = CreateService(db);

        var approved = await service.GetApprovedDoctorProfileAsync(ids.ApprovedId);
        var pending = await service.GetApprovedDoctorProfileAsync(ids.PendingId);

        Assert.NotNull(approved);
        Assert.Equal("doctor@hospital.lk", approved!.Email);
        Assert.Equal("SLMC-100", approved.SlmcLicenseNumber);
        Assert.Null(pending);
    }

    private static DoctorService CreateService(ApplicationDbContext db) =>
        new(db, new PasswordHasher<User>());

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(int ApprovedId, int PendingId)> SeedDoctors(ApplicationDbContext db)
    {
        var approvedUser = new User
        {
            FullName = "Nimal Perera",
            Email = "doctor@hospital.lk",
            PasswordHash = "hash",
            Role = "Doctor"
        };
        var pendingUser = new User
        {
            FullName = "Kamal Silva",
            Email = "pending@hospital.lk",
            PasswordHash = "hash",
            Role = "Doctor"
        };
        var declinedUser = new User
        {
            FullName = "Sunil Fernando",
            Email = "declined@hospital.lk",
            PasswordHash = "hash",
            Role = "Doctor"
        };

        var approved = Doctor(approvedUser, "Nimal", "Perera", "Cardiology", "SLMC-100",
            DoctorRegistrationStatuses.Approved);
        var pending = Doctor(pendingUser, "Kamal", "Silva", "Neurology", "SLMC-200",
            DoctorRegistrationStatuses.Pending);
        var declined = Doctor(declinedUser, "Sunil", "Fernando", "Pediatrics", "SLMC-300",
            DoctorRegistrationStatuses.Declined);
        db.Doctors.AddRange(approved, pending, declined);
        await db.SaveChangesAsync();
        return (approved.DoctorId, pending.DoctorId);
    }

    private static Doctor Doctor(User user, string firstName, string lastName,
        string specialization, string license, string status) => new()
    {
        User = user,
        FirstName = firstName,
        LastName = lastName,
        NIC = $"20000000000{license[^1]}",
        Specialization = specialization,
        SlmcLicenseNumber = license,
        PhoneNumber = "0771234567",
        RegistrationStatus = status
    };
}
