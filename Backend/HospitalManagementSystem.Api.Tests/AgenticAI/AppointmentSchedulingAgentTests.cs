using HospitalManagementSystem.Api.AgenticAI.AppointmentScheduling;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;
using HospitalManagementSystem.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace HospitalManagementSystem.Api.Tests.AgenticAI;

public class AppointmentSchedulingAgentTests
{
    [Fact]
    public async Task Loop_UsesRealDoctorsAndIndividualTimes_WithoutExposingPrivateFields()
    {
        await using var setup = await Scenario.CreateAsync();
        var model = new ScriptedModel(FindDoctor(), FindSlots(), Recommend());
        var result = await setup.Agent(model).RunAsync(Request(), setup.Patient, default);

        Assert.Equal("Recommendations", result.Status);
        var slot = Assert.Single(result.Slots);
        Assert.Equal(2, slot.AppointmentNumber);
        Assert.Equal(2500m, slot.ConsultationFee);
        Assert.Equal(setup.Slot.StartAt.AddMinutes(30), slot.EstimatedStartAt.UtcDateTime);
        Assert.Equal(TimeSpan.FromMinutes(330), slot.EstimatedStartAt.Offset);
        Assert.Equal(3, model.Calls);
        Assert.Contains("Dr. Ada Doctor", model.LastHistory);
        Assert.Contains("estimatedStartAt", model.LastHistory);
        Assert.DoesNotContain(setup.Patient.Email, model.LastHistory);
        Assert.DoesNotContain("PRIVATE-NIC", model.LastHistory);
        Assert.Single(await setup.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task Booking_UsesExistingServiceAndAuthenticatedPatient_AndStopsAfterOneWrite()
    {
        await using var setup = await Scenario.CreateAsync();
        var model = new ScriptedModel(FindDoctor(), FindSlots(), Book(), Book());
        var result = await setup.Agent(model).RunAsync(Request(true), setup.Patient, default);

        Assert.Equal("Booked", result.Status);
        var booked = await setup.Db.Appointments.SingleAsync(a => a.AppointmentId == result.Booking!.AppointmentId);
        Assert.Equal(setup.Patient.PatientId, booked.PatientId);
        Assert.Equal(setup.Patient.Email, booked.PatientEmail);
        Assert.Equal(setup.Patient.FullName, booked.PatientName);
        Assert.Equal(2, booked.AppointmentNumber);
        Assert.Equal(setup.Slot.StartAt.AddMinutes(30), booked.EstimatedStartAt);
        Assert.Equal("Confirmed", booked.Status);
        Assert.Equal(3, model.Calls);
        Assert.Equal(2, await setup.Db.Appointments.CountAsync());
    }

    [Fact]
    public async Task RecommendationRequest_CannotBeTurnedIntoABookingByTheModel()
    {
        await using var setup = await Scenario.CreateAsync();
        var model = new ScriptedModel(FindDoctor(), FindSlots(), Book(), Recommend());
        var result = await setup.Agent(model).RunAsync(Request(), setup.Patient, default);
        Assert.Equal("Recommendations", result.Status);
        Assert.Contains("Booking is disabled", model.LastHistory);
        Assert.Single(await setup.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task InventedReferences_CannotBookOrBecomeRecommendations()
    {
        await using var setup = await Scenario.CreateAsync();
        var model = new ScriptedModel(
            new() { Action = "find_slots", DoctorRef = "D999" },
            new() { Action = "book_appointment", SlotRef = "S1" },
            new() { Action = "final", Outcome = "recommend", SlotRefs = ["S999"] },
            new() { Action = "final", Outcome = "clarify" });
        var result = await setup.Agent(model).RunAsync(Request(true), setup.Patient, default);
        Assert.Equal("NeedsDetails", result.Status);
        Assert.Null(result.Booking);
        Assert.Empty(result.Slots);
        Assert.Contains("Unknown slot reference", model.LastHistory);
        Assert.Single(await setup.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task Loop_CanSearchAnotherDateAfterAnEmptySearch()
    {
        await using var setup = await Scenario.CreateAsync();
        var model = new ScriptedModel(FindDoctor(),
            new() { Action = "find_slots", DoctorRef = "D1", Date = "2000-01-01" },
            FindSlots(), Recommend());
        var result = await setup.Agent(model).RunAsync(Request(), setup.Patient, default);
        Assert.Equal("Recommendations", result.Status);
        Assert.Single(result.Slots);
        Assert.Contains("\"slots\":[]", model.LastHistory);
    }

    [Fact]
    public async Task ChangedSlot_IsNotBooked_AndWriteIsNotRetried()
    {
        await using var setup = await Scenario.CreateAsync();
        var model = new ScriptedModel(FindDoctor(), FindSlots(), Book(), Book())
        {
            BeforeDecision = async call =>
            {
                if (call == 3)
                {
                    setup.Slot.ConsultationFee = 4000m;
                    await setup.Db.SaveChangesAsync();
                }
            }
        };
        var result = await setup.Agent(model).RunAsync(Request(true), setup.Patient, default);
        Assert.Equal("BookingUnavailable", result.Status);
        Assert.Null(result.Booking);
        Assert.Equal(3, model.Calls);
        Assert.Single(await setup.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task CancelledSlot_IsNotReturnedAsARecommendationAfterModelDelay()
    {
        await using var setup = await Scenario.CreateAsync();
        var model = new ScriptedModel(FindDoctor(), FindSlots(), Recommend(), new() { Action = "final", Outcome = "clarify" })
        {
            BeforeDecision = async call =>
            {
                if (call == 3) { setup.Slot.IsActive = false; await setup.Db.SaveChangesAsync(); }
            }
        };
        var result = await setup.Agent(model).RunAsync(Request(), setup.Patient, default);
        Assert.Equal("NeedsDetails", result.Status);
        Assert.Empty(result.Slots);
    }

    [Fact]
    public async Task Tools_FilterUnavailableSlots_AndUseHospitalLocalDate()
    {
        await using var setup = await Scenario.CreateAsync();
        setup.Slot.StartAt = DateTime.UtcNow.AddDays(12).Date.AddHours(22);
        setup.Slot.EndAt = setup.Slot.StartAt.AddHours(1);
        await setup.Db.SaveChangesAsync();
        var doctor = Assert.Single(await setup.Tools.FindDoctorsAsync("Cardiology"));
        var localDate = DateOnly.FromDateTime(setup.Slot.StartAt.AddHours(5.5));
        Assert.Single(await setup.Tools.FindSlotsAsync(doctor, localDate));
        Assert.Empty(await setup.Tools.FindSlotsAsync(doctor, localDate.AddDays(-1)));
        setup.Slot.Capacity = 1; // The first number is already booked.
        await setup.Db.SaveChangesAsync();
        Assert.Empty(await setup.Tools.FindSlotsAsync(doctor, null));
        setup.Slot.Capacity = 2;
        setup.Slot.IsActive = false;
        await setup.Db.SaveChangesAsync();
        Assert.Empty(await setup.Tools.FindSlotsAsync(doctor, null));
        setup.Slot.IsActive = true;
        setup.Slot.StartAt = DateTime.UtcNow.AddHours(-2);
        setup.Slot.EndAt = DateTime.UtcNow.AddHours(-1);
        await setup.Db.SaveChangesAsync();
        Assert.Empty(await setup.Tools.FindSlotsAsync(doctor, null));
    }

    [Fact]
    public async Task DoctorEligibility_IsOwnedByDoctorService()
    {
        await using var setup = await Scenario.CreateAsync();
        var doctor = Assert.Single(await setup.Tools.FindDoctorsAsync("Ada"));
        Assert.Equal(doctor, Assert.Single(await setup.Tools.FindDoctorsAsync("Dr. Ada Doctor")));
        var entity = await setup.Db.Doctors.SingleAsync();
        entity.RegistrationStatus = DoctorRegistrationStatuses.Declined;
        await setup.Db.SaveChangesAsync();
        Assert.Empty(await setup.Tools.FindDoctorsAsync("Ada"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Tools.FindSlotsAsync(doctor, null));
    }

    [Fact]
    public async Task UnsupportedActions_AndLoopLimit_DoNotWrite()
    {
        await using var setup = await Scenario.CreateAsync();
        var model = new ScriptedModel(new() { Action = "delete_doctor" }, new() { Action = "send_sms" });
        var agent = new AppointmentSchedulingAgent(model, setup.Tools, Options.Create(new AppointmentAgentOptions { MaxSteps = 2 }));
        var result = await agent.RunAsync(Request(true), setup.Patient, default);
        Assert.Equal("NeedsDetails", result.Status);
        Assert.Equal(2, model.Calls);
        Assert.Single(await setup.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task CancelledRequest_DoesNotCallTheModelOrBook()
    {
        await using var setup = await Scenario.CreateAsync();
        var model = new ScriptedModel(Book());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => setup.Agent(model).RunAsync(Request(true), setup.Patient, cancellation.Token));
        Assert.Equal(0, model.Calls);
    }

    private static AppointmentAgentRequest Request(bool book = false) => new()
    { Message = "Find a cardiology consultation and book the earliest if booking is enabled.", AllowBooking = book };
    private static AppointmentAgentDecision FindDoctor() => new() { Action = "find_doctors", Query = "Cardiology" };
    private static AppointmentAgentDecision FindSlots() => new() { Action = "find_slots", DoctorRef = "D1" };
    private static AppointmentAgentDecision Recommend() => new() { Action = "final", Outcome = "recommend", SlotRefs = ["S1"] };
    private static AppointmentAgentDecision Book() => new() { Action = "book_appointment", SlotRef = "S1" };

    private sealed class ScriptedModel(params AppointmentAgentDecision[] decisions) : IOllamaAppointmentClient
    {
        public int Calls { get; private set; }
        public string LastHistory { get; private set; } = "";
        public Func<int, Task>? BeforeDecision { get; init; }
        public async Task<AppointmentAgentDecision> DecideAsync(IReadOnlyList<AppointmentAgentMessage> messages, CancellationToken cancellationToken)
        {
            Calls++;
            LastHistory = string.Join('\n', messages.Select(m => m.Content));
            if (BeforeDecision is not null) await BeforeDecision(Calls);
            return decisions[Calls - 1];
        }
    }

    private sealed class Scenario : IAsyncDisposable
    {
        public required ApplicationDbContext Db { get; init; }
        public required PatientDto Patient { get; init; }
        public required DoctorTimeSlot Slot { get; init; }
        public required AppointmentAgentTools Tools { get; init; }
        public AppointmentSchedulingAgent Agent(IOllamaAppointmentClient model) => new(model, Tools, Options.Create(new AppointmentAgentOptions()));
        public ValueTask DisposeAsync() => Db.DisposeAsync();

        public static async Task<Scenario> CreateAsync()
        {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"agent-{Guid.NewGuid()}").Options);
            var patient = new Patient { FirstName = "Real", LastName = "Patient", Email = "private-patient@example.com", PhoneNumber = "0771234567", NIC = "PATIENT-NIC" };
            var doctor = new Doctor { FirstName = "Ada", LastName = "Doctor", Specialization = "Cardiology",
                RegistrationStatus = DoctorRegistrationStatuses.Approved, NIC = "PRIVATE-NIC", SlmcLicenseNumber = "SLMC-1",
                User = new User { FullName = "Ada Doctor", Email = "private-doctor@example.com", Role = "Doctor", PasswordHash = "hash" } };
            var slot = new DoctorTimeSlot { Doctor = doctor, DoctorName = "Dr. Ada Doctor", Specialty = "Cardiology",
                StartAt = DateTime.UtcNow.AddDays(10).Date.AddHours(3), EndAt = DateTime.UtcNow.AddDays(10).Date.AddHours(4),
                Capacity = 2, ConsultationFee = 2500m, IsActive = true };
            db.Patients.Add(patient);
            db.Doctors.Add(doctor);
            db.DoctorTimeSlots.Add(slot);
            db.Appointments.Add(new Appointment { DoctorTimeSlot = slot, AppointmentNumber = 1,
                EstimatedStartAt = slot.StartAt, Status = "Confirmed", PatientName = "Existing booking", PatientPhone = "0772222222" });
            await db.SaveChangesAsync();
            var patientService = new PatientService(new PatientRepository(db));
            return new Scenario { Db = db, Patient = (await patientService.GetPatientByIdAsync(patient.PatientId))!, Slot = slot,
                Tools = new AppointmentAgentTools(new DoctorService(db, new PasswordHasher<User>()), new AppointmentService(new AppointmentRepository(db))) };
        }
    }
}
