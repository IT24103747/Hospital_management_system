using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;
using HospitalManagementSystem.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HospitalManagementSystem.Api.Tests.AgenticAI;

public sealed class HospitalAssistantTests
{
    [Theory]
    [InlineData(false, TriageApprovalStatuses.Approved)]
    [InlineData(false, TriageApprovalStatuses.Rejected)]
    [InlineData(true, TriageApprovalStatuses.Approved)]
    [InlineData(true, TriageApprovalStatuses.Rejected)]
    public async Task FailedInputCanBeReviewedAndResumeBookingWithoutAutomaticMutation(bool legacy, string decision)
    {
        await using var h = await Harness.Create();
        var failed = await h.Workflows.StartForPatientAsync(1, new() {
            Symptoms = "I feel dizzy", Vitals = new TriageVitalsDto { TemperatureCelsius = 98 }
        });
        Assert.Equal(TriageWorkflowStatuses.FailedSafely, failed.Status);
        Assert.Equal(TriageApprovalStatuses.Pending, failed.ApprovalStatus);
        var record = await h.Db.TriageWorkflows.SingleAsync();
        if (legacy) { record.ApprovalStatus = TriageApprovalStatuses.NotRequired; await h.Db.SaveChangesAsync(); }
        Assert.Contains(await h.Workflows.GetPendingClinicalReviewsAsync(), w => w.WorkflowId == failed.WorkflowId);
        Assert.Equal(legacy ? TriageApprovalStatuses.NotRequired : TriageApprovalStatuses.Pending, record.ApprovalStatus);
        var blocked = await h.Send("Book a cardiologist tomorrow");
        Assert.Null(blocked.PendingAction);
        var revised = await h.Workflows.ReviewAsync(failed.WorkflowId, 42, new() { Decision = TriageApprovalStatuses.RevisionRequested });
        Assert.NotNull(revised);
        var stillBlocked = await h.Send("Book a cardiologist tomorrow", blocked.ConversationId);
        Assert.Null(stillBlocked.PendingAction);
        var reviewed = await h.Workflows.ReviewAsync(failed.WorkflowId, 42, new() { Decision = decision, Note = "Reviewed test assessment" });
        Assert.Equal(TriageWorkflowStatuses.Completed, reviewed!.Status);
        Assert.Equal(42, record.ReviewedByUserId);
        Assert.Equal("InvalidOrSuspiciousInput", record.ErrorCode);
        Assert.Equal(legacy ? 1 : 0, await h.Db.TriageWorkflowEvents.CountAsync(e => e.EventType == "LegacyFailedAssessmentRecovered"));
        var resumed = await h.Send("Book a cardiologist tomorrow", blocked.ConversationId);
        Assert.NotNull(resumed.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task FinalizedEmergencyReviewDoesNotClearUrgentConversationSafety()
    {
        await using var h = await Harness.Create();
        var blocked = await h.Send("I have severe chest pain and difficulty breathing. Book a cardiologist tomorrow.");
        var record = await h.Db.TriageWorkflows.SingleAsync();
        await h.Workflows.ReviewAsync(record.TriageWorkflowId, 42, new() { Decision = TriageApprovalStatuses.Approved });
        var later = await h.Send("Book a cardiologist tomorrow", blocked.ConversationId);
        Assert.Null(later.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Theory]
    [InlineData(TriageWorkflowStatuses.PendingPatientInput, "needs more information")]
    [InlineData(TriageWorkflowStatuses.FailedSafely, "could not be completed safely")]
    public async Task BookingBlockExplainsActualAssessmentAndIncludesSavedGuidance(string status, string expected)
    {
        await using var h = await Harness.Create();
        h.Db.TriageWorkflows.Add(new TriageWorkflow {
            PatientId = 1, Status = status, FinalOutcome = "Saved assessment instructions."
        });
        await h.Db.SaveChangesAsync();
        var response = await h.Send("Book a cardiologist tomorrow");
        Assert.Null(response.PendingAction);
        var reply = response.Messages.Last().Text;
        Assert.Contains(expected, reply);
        Assert.Contains("Saved assessment instructions.", reply);
        Assert.DoesNotContain("urgent safety", reply);
        Assert.DoesNotContain("shown above", reply);
    }

    [Fact]
    public async Task ResolvedNonUrgentAssessmentClearsCachedBlockInExistingConversation()
    {
        await using var h = await Harness.Create();
        var workflow = new TriageWorkflow {
            PatientId = 1, Status = TriageWorkflowStatuses.PendingPatientInput
        };
        h.Db.TriageWorkflows.Add(workflow);
        await h.Db.SaveChangesAsync();
        var blocked = await h.Send("Book a cardiologist tomorrow");
        Assert.Null(blocked.PendingAction);
        workflow.Status = TriageWorkflowStatuses.Completed;
        workflow.TriageLevel = TriageLevels.NonUrgent;
        workflow.ApprovalStatus = TriageApprovalStatuses.Approved;
        await h.Db.SaveChangesAsync();
        var resumed = await h.Send("Book a cardiologist tomorrow", blocked.ConversationId);
        Assert.NotNull(resumed.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task NewConversationShowsExistingUrgentGuidanceWithBookingBlock()
    {
        await using var h = await Harness.Create();
        h.Db.TriageWorkflows.Add(new TriageWorkflow {
            PatientId = 1, Status = TriageWorkflowStatuses.PendingClinicalReview,
            ApprovalStatus = TriageApprovalStatuses.Pending, TriageLevel = TriageLevels.Urgent,
            RequiresHumanReview = true, FinalOutcome = "Contact the care team for your saved urgent assessment."
        });
        await h.Db.SaveChangesAsync();
        var response = await h.Send("Book a cardiologist tomorrow");
        Assert.Null(response.PendingAction);
        Assert.Contains("Contact the care team for your saved urgent assessment.", response.Messages.Last().Text);
        Assert.Contains("urgent safety concerns", response.Messages.Last().Text);
    }

    [Fact]
    public async Task SearchAndConversationalAssentNeverBook_ExplicitConfirmationUsesBackendNumber_AndIsIdempotent()
    {
        await using var h = await Harness.Create();
        var result = await h.Send("Find a cardiologist tomorrow afternoon");
        Assert.NotNull(result.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
        var assent = await h.Send("that looks good", result.ConversationId);
        Assert.Equal(result.PendingAction.ActionId, assent.PendingAction!.ActionId);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
        var request = h.Confirm(result.PendingAction);
        var booked = await h.Service.DecideAsync(h.Patient, result.ConversationId, request, default);
        Assert.Equal("COMPLETED", booked.State);
        Assert.Equal(1, Assert.Single(booked.Appointments).AppointmentNumber);
        Assert.Null(booked.PendingAction);
        var retry = await h.Service.DecideAsync(h.Patient, result.ConversationId, request, default);
        Assert.Equal(booked.Appointments[0].AppointmentId, retry.Appointments[0].AppointmentId);
        Assert.Single(await h.Db.Appointments.ToListAsync());
        request.RequestId = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.DecideAsync(h.Patient, result.ConversationId, request, default));
    }

    [Fact]
    public async Task EmergencyBlocksBookingAcrossTurnsAndNewConversations_AndCreatesClinicalReview()
    {
        await using var h = await Harness.Create();
        var emergency = await h.Send("I have severe chest pain and difficulty breathing. Book a cardiologist tomorrow.");
        Assert.Null(emergency.PendingAction);
        Assert.Equal("WAITING_FOR_HUMAN_APPROVAL", emergency.State);
        Assert.Contains("emergency", string.Join(" ", emergency.Messages.Select(m => m.Text)), StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(await h.Workflows.GetPendingClinicalReviewsAsync());
        var later = await h.Send("Book a cardiologist tomorrow", emergency.ConversationId);
        Assert.Null(later.PendingAction);
        var fresh = await h.Send("Book a cardiologist tomorrow");
        Assert.Null(fresh.PendingAction);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task RoutineSymptomsCanRunSafetyThenAppointmentProposal_WithoutBooking()
    {
        await using var h = await Harness.Create();
        var result = await h.Send("I have a mild headache. Find a cardiologist tomorrow afternoon.");
        Assert.NotEmpty(await h.Db.TriageWorkflows.ToListAsync());
        Assert.Null(result.PendingAction);
        for (var round = 0; result.Questions.Count > 0 && round < 12; round++)
            result = await h.Send("Mild symptoms, started yesterday, getting better. None of the warning signs.", result.ConversationId);
        Assert.NotNull(result.PendingAction);
        Assert.Contains(result.Messages, m => m.Progress.Contains("The existing safety workflow checked your report."));
        Assert.Contains(result.Messages, m => m.Progress.Contains("Available hospital sessions were checked."));
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task NewPreferencesInvalidateOldApproval_UnknownSlotCannotBeConfirmed()
    {
        await using var h = await Harness.Create();
        var first = await h.Send("Find a cardiologist tomorrow afternoon");
        var oldAction = first.PendingAction!;
        var second = await h.Send("Try tomorrow morning", first.ConversationId);
        Assert.Null(second.PendingAction);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.DecideAsync(h.Patient, first.ConversationId, h.Confirm(oldAction), default));
        Assert.Equal("Superseded", (await h.Db.AppointmentProposals.SingleAsync()).Status);
        var third = await h.Send("Try tomorrow afternoon", first.ConversationId);
        var invalid = h.Confirm(third.PendingAction!);
        invalid.DoctorTimeSlotId = 99999;
        await Assert.ThrowsAsync<ArgumentException>(() => h.Service.DecideAsync(h.Patient, first.ConversationId, invalid, default));
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task ConversationOwnershipAndRequestFingerprintsAreEnforced()
    {
        await using var h = await Harness.Create();
        var request = new AssistantMessageRequest { Message = "Find a doctor", RequestId = Guid.NewGuid() };
        var result = await h.Service.MessageAsync(h.Patient, request, default);
        var repeated = await h.Service.MessageAsync(h.Patient, request, default);
        Assert.Equal(result.ConversationId, repeated.ConversationId);
        Assert.Equal(2, repeated.Messages.Count);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => h.Service.GetAsync(999, result.ConversationId, default));
        request.Message = "Book a cardiologist";
        await Assert.ThrowsAsync<ArgumentException>(() => h.Service.MessageAsync(h.Patient, request, default));
    }

    [Fact]
    public async Task CancellationRequiresReasonSelectionAndApproval()
    {
        await using var h = await Harness.Create();
        var search = await h.Send("Book a cardiologist tomorrow");
        var booked = await h.Service.DecideAsync(h.Patient, search.ConversationId, h.Confirm(search.PendingAction!), default);
        var request = await h.Send("Cancel my appointment", search.ConversationId);
        Assert.Null(request.PendingAction);
        var proposed = await h.Send("I cannot attend because of work", search.ConversationId);
        Assert.Equal("cancel", proposed.PendingAction!.Type);
        Assert.Equal("Confirmed", (await h.Db.Appointments.SingleAsync()).Status);
        var cancelled = await h.Service.DecideAsync(h.Patient, search.ConversationId, new() {
            ActionId = proposed.PendingAction.ActionId, Decision = "confirm", RequestId = Guid.NewGuid(),
            AppointmentId = booked.Appointments[0].AppointmentId
        }, default);
        Assert.Equal("Cancelled", Assert.Single(cancelled.Appointments).Status);
        Assert.Equal("I cannot attend because of work", cancelled.Appointments[0].CancellationReason);
    }

    [Fact]
    public async Task ExpiredAndDismissedRequestsCannotWrite()
    {
        await using var h = await Harness.Create();
        var result = await h.Send("Book a cardiologist tomorrow");
        var row = await h.Db.AssistantConversations.SingleAsync();
        var options = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var state = System.Text.Json.JsonSerializer.Deserialize<AssistantState>(row.StateJson, options)!;
        state.PendingAction!.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        row.StateJson = System.Text.Json.JsonSerializer.Serialize(state, options);
        await h.Db.SaveChangesAsync();
        var expired = await h.Service.DecideAsync(h.Patient, result.ConversationId, h.Confirm(result.PendingAction!), default);
        Assert.Null(expired.PendingAction);
        Assert.Equal("CANCELLED", expired.State);
        Assert.Empty(await h.Db.Appointments.ToListAsync());
    }

    [Fact]
    public async Task ChangedSessionDetailsNeedNewApproval_ButQueueNumberChangesAreAllowed()
    {
        await using var h = await Harness.Create();
        var first = await h.Send("Book a cardiologist tomorrow");
        var slot = await h.Db.DoctorTimeSlots.SingleAsync();
        slot.StartAt = slot.StartAt.AddMinutes(30);
        await h.Db.SaveChangesAsync();
        var rejected = await h.Service.DecideAsync(h.Patient, first.ConversationId, h.Confirm(first.PendingAction!), default);
        Assert.Equal("FAILED", rejected.State);
        Assert.Empty(await h.Db.Appointments.ToListAsync());

        var second = await h.Send("Book a cardiologist tomorrow", first.ConversationId);
        h.Db.Appointments.Add(new Appointment { DoctorTimeSlotId = slot.DoctorTimeSlotId, PatientId = 999,
            AppointmentNumber = 1, PatientName = "Another patient", Status = "Confirmed" });
        await h.Db.SaveChangesAsync();
        var confirmed = await h.Service.DecideAsync(h.Patient, first.ConversationId, h.Confirm(second.PendingAction!), default);
        Assert.Equal(2, Assert.Single(confirmed.Appointments).AppointmentNumber);
    }

    [Fact]
    public async Task EmergencyDuringFollowupInterruptsQuestionsAndPreventsProposal()
    {
        await using var h = await Harness.Create();
        var first = await h.Send("I have a mild headache and want a cardiologist tomorrow");
        Assert.NotEmpty(first.Questions);
        var emergency = await h.Send("Now I have severe chest pain and difficulty breathing", first.ConversationId);
        Assert.Empty(emergency.Questions);
        Assert.Null(emergency.PendingAction);
        Assert.Equal("WAITING_FOR_HUMAN_APPROVAL", emergency.State);
    }

    [Fact]
    public async Task CapabilityRegistryDisablesUnimplementedFeatures()
    {
        await using var h = await Harness.Create();
        Assert.False(h.Service.Capabilities.Single(c => c.Id == "medical-reports").Enabled);
        var result = await h.Send("Summarize my medical reports");
        Assert.Null(result.PendingAction);
        Assert.Contains("coming soon", result.Messages.Last().Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AlternativeRequestSearchesWithoutCancellingCurrentAppointment()
    {
        await using var h = await Harness.Create();
        var search = await h.Send("Book a cardiologist tomorrow");
        await h.Service.DecideAsync(h.Patient, search.ConversationId, h.Confirm(search.PendingAction!), default);
        var alternative = await h.Send("I cannot attend my current appointment. Find another one tomorrow.", search.ConversationId);
        Assert.NotNull(alternative.PendingAction);
        Assert.Equal("book", alternative.PendingAction.Type);
        Assert.Equal("Confirmed", (await h.Db.Appointments.SingleAsync()).Status);
        Assert.Contains(alternative.Messages, m => m.Text.Contains("current appointment will remain active"));
    }

    [Fact]
    public async Task AssistantEndpointsRequirePatientRoleAndOwnedConversation()
    {
        await using var factory = new AppointmentApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/hospital-assistant/capabilities")).StatusCode);
        await Login("admin@medicore.lk", "Admin1234");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/hospital-assistant/capabilities")).StatusCode);
        await Login("amal.perera@email.com", "Patient123!");
        var response = await client.PostAsJsonAsync("/api/hospital-assistant/messages", new { message = "hello", requestId = Guid.NewGuid() });
        response.EnsureSuccessStatusCode();
        var conversation = await response.Content.ReadFromJsonAsync<AssistantConversationResponse>();
        await Login("nimesha.silva@email.com", "Patient123!");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/hospital-assistant/conversations/{conversation!.ConversationId}")).StatusCode);

        async Task Login(string email, string password)
        {
            var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
            login.EnsureSuccessStatusCode();
            var json = await login.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        }
    }

    [Theory]
    [InlineData("next Friday afternoon", "2026-09-18", null, "afternoon")]
    [InlineData("tomorrow morning", "2026-09-12", null, "morning")]
    [InlineData("next week", "2026-09-14", "2026-09-20", null)]
    public void DatePreferencesAreDeterministic(string input, string start, string? end, string? period)
    {
        var state = new AssistantState();
        Assert.Null(AssistantPreferences.ApplyDates(input, state, new DateOnly(2026, 9, 11)));
        Assert.Equal(DateOnly.Parse(start), state.PreferredDate);
        Assert.Equal(end == null ? null : (DateOnly?)DateOnly.Parse(end), state.ThroughDate);
        Assert.Equal(period, state.Period);
    }

    private sealed class Harness : IAsyncDisposable
    {
        public required ApplicationDbContext Db { get; init; }
        public required HospitalAssistantService Service { get; init; }
        public required TriageWorkflowService Workflows { get; init; }
        public PatientDto Patient { get; } = new() { PatientId = 1, FirstName = "Test", LastName = "Patient", Email = "test@example.com", PhoneNumber = "0771234567" };
        public Task<AssistantConversationResponse> Send(string message, Guid? id = null) => Service.MessageAsync(Patient,
            new() { ConversationId = id, Message = message, RequestId = Guid.NewGuid() }, default);
        public AssistantActionRequest Confirm(AssistantPendingAction action) => new() {
            ActionId = action.ActionId, Decision = "confirm", RequestId = Guid.NewGuid(), DoctorTimeSlotId = action.Slots[0].DoctorTimeSlotId
        };
        public static async Task<Harness> Create()
        {
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var tomorrow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, AppointmentAgentTools.HospitalTimeZone).Date.AddDays(1).AddHours(13.5);
            var start = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(tomorrow, DateTimeKind.Unspecified), AppointmentAgentTools.HospitalTimeZone);
            var slot = new DoctorTimeSlot { DoctorName = "Dr. Silva", DoctorId = 1, Specialty = "Cardiology",
                StartAt = start, EndAt = start.AddHours(2), Capacity = 5, IsActive = true };
            db.DoctorTimeSlots.Add(slot);
            await db.SaveChangesAsync();
            var appointments = new AppointmentService(new AppointmentRepository(db));
            var tools = new FakeTools(appointments, slot);
            var workflows = new TriageWorkflowService(db, NullLogger<TriageWorkflowService>.Instance);
            return new() { Db = db, Workflows = workflows, Service = new(db, new AssistantAgentRegistry([]), workflows,
                new HospitalAppointmentProposalAgent(tools, new AppointmentProposalStore(db)),
                new SafetyValidationApprovalAgent(new SafetyApprovalTools(db, tools)), tools, appointments) };
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    private sealed class FakeTools(IAppointmentService appointments, DoctorTimeSlot slot) : IAppointmentAgentTools
    {
        public Task<IReadOnlyList<AgentDoctor>> FindDoctorsAsync(string query) => Task.FromResult<IReadOnlyList<AgentDoctor>>(
            string.IsNullOrEmpty(query) || "Cardiology Dr. Silva".Contains(query, StringComparison.OrdinalIgnoreCase)
                ? [new("D1", 1, "Dr. Silva", "Cardiology")] : []);
        public async Task<IReadOnlyList<AgentSlot>> FindSlotsAsync(AgentDoctor doctor, DateOnly? date)
        {
            var available = await appointments.GetSlotsAsync(null, null, true, 1);
            return available.Where(s => !date.HasValue || DateOnly.FromDateTime(AppointmentAgentTools.Local(s.StartAt).DateTime) == date)
                .Select(s => new AgentSlot("S" + s.DoctorTimeSlotId, s.DoctorTimeSlotId, 1, s.DoctorName, s.Specialty,
                    AppointmentAgentTools.Local(s.StartAt), AppointmentAgentTools.Local(s.EndAt), s.NextAppointmentNumber, s.AvailableCount, s.ConsultationFee, "Room 1")).ToArray();
        }
        public async Task<AgentBooking> BookAsync(AgentSlot observedSlot, PatientDto patient)
        {
            var created = await appointments.CreateAppointmentAsync(new() { DoctorTimeSlotId = slot.DoctorTimeSlotId, PatientId = patient.PatientId,
                PatientName = patient.FullName, PatientEmail = patient.Email, PatientPhone = patient.PhoneNumber, AppointmentType = "Consultation" });
            return new(created.AppointmentId, created.DoctorTimeSlotId, created.AppointmentNumber, created.DoctorName,
                AppointmentAgentTools.Local(created.StartAt), created.Status);
        }
    }
}
