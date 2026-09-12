using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HospitalManagementSystem.Api.Tests;

public class ClinicalAgenticAiComprehensiveTests
{
    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TriageWorkflowService CreateService(ApplicationDbContext db) =>
        new(db, NullLogger<TriageWorkflowService>.Instance);

    [Fact]
    public async Task Triage_Emergency_SevereChestPainAndBreathingDifficulty()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.StartForPatientAsync(1, new StartTriageWorkflowDto
        {
            Symptoms = "I am experiencing severe chest pain and severe breathing difficulty."
        });

        Assert.Equal(TriageLevels.Emergency, result.TriageLevel);
        Assert.Equal(TriageWorkflowStatuses.PendingClinicalReview, result.Status);
        Assert.True(result.RequiresHumanReview);
        Assert.NotEmpty(result.RedFlags);
    }

    [Fact]
    public async Task Triage_Urgent_ChestDiscomfort()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.StartForPatientAsync(1, new StartTriageWorkflowDto
        {
            Symptoms = "I have persistent chest discomfort and mild dizziness."
        });

        Assert.Equal(TriageLevels.Urgent, result.TriageLevel);
        Assert.Equal(TriageWorkflowStatuses.PendingClinicalReview, result.Status);
        Assert.True(result.RequiresHumanReview);
        Assert.NotEmpty(result.UrgentFlags);
    }

    [Fact]
    public async Task Triage_ClinicalReview_ChemotherapyPatient()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.StartForPatientAsync(1, new StartTriageWorkflowDto
        {
            Symptoms = "I am undergoing chemotherapy."
        });

        Assert.Equal(TriageLevels.ClinicalReview, result.TriageLevel);
        Assert.Equal(TriageWorkflowStatuses.PendingClinicalReview, result.Status);
        Assert.True(result.RequiresHumanReview);
        Assert.NotEmpty(result.ClinicalReviewFlags);
    }

    [Fact]
    public async Task Triage_NonUrgent_Headache()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.StartForPatientAsync(1, new StartTriageWorkflowDto
        {
            Symptoms = "I have a throbbing headache since this afternoon."
        });

        Assert.Equal(TriageLevels.NonUrgent, result.TriageLevel);
        Assert.False(result.RequiresHumanReview);
        Assert.NotNull(result.Guidance);
        Assert.Contains("headache", result.Guidance!.Heading, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.Guidance.Actions);
        Assert.NotEmpty(result.Guidance.SeekHelpIf);
    }

    [Fact]
    public async Task Triage_NonUrgent_StomachPainAndNausea()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.StartForPatientAsync(1, new StartTriageWorkflowDto
        {
            Symptoms = "My stomach hurts and I feel nauseous."
        });

        Assert.Equal(TriageLevels.NonUrgent, result.TriageLevel);
        Assert.False(result.RequiresHumanReview);
        Assert.NotNull(result.Guidance);
        Assert.Contains("stomach", result.Guidance!.Heading, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.Guidance.Actions);
    }

    [Fact]
    public async Task Triage_NonUrgent_FeverAndChills()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.StartForPatientAsync(1, new StartTriageWorkflowDto
        {
            Symptoms = "I have a mild fever and body chills."
        });

        Assert.Equal(TriageLevels.NonUrgent, result.TriageLevel);
        Assert.False(result.RequiresHumanReview);
        Assert.NotNull(result.Guidance);
        Assert.Contains("fever", result.Guidance!.Heading, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.Guidance.Actions);
    }

    [Fact]
    public async Task Triage_NonUrgent_CoughAndSoreThroat()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.StartForPatientAsync(1, new StartTriageWorkflowDto
        {
            Symptoms = "I have a dry cough and a sore throat."
        });

        Assert.Equal(TriageLevels.NonUrgent, result.TriageLevel);
        Assert.False(result.RequiresHumanReview);
        Assert.NotNull(result.Guidance);
        Assert.Contains("cough", result.Guidance!.Heading, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Triage_NonUrgent_BackPain()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.StartForPatientAsync(1, new StartTriageWorkflowDto
        {
            Symptoms = "I have lower back pain after moving heavy furniture."
        });

        Assert.Equal(TriageLevels.NonUrgent, result.TriageLevel);
        Assert.False(result.RequiresHumanReview);
        Assert.NotNull(result.Guidance);
        Assert.Contains("pain", result.Guidance!.Heading, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Triage_NonUrgent_SkinRash()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.StartForPatientAsync(1, new StartTriageWorkflowDto
        {
            Symptoms = "I noticed an itchy skin rash on my left forearm."
        });

        Assert.Equal(TriageLevels.NonUrgent, result.TriageLevel);
        Assert.False(result.RequiresHumanReview);
        Assert.NotNull(result.Guidance);
        Assert.Contains("skin", result.Guidance!.Heading, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Triage_InsufficientInformation_InvalidHeartRateVitals()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.StartForPatientAsync(1, new StartTriageWorkflowDto
        {
            Symptoms = "I feel slightly tired.",
            Vitals = new TriageVitalsDto { HeartRateBpm = 600 } // Biologically impossible
        });

        Assert.Equal(TriageLevels.InsufficientInformation, result.TriageLevel);
        Assert.Equal(TriageWorkflowStatuses.FailedSafely, result.Status);
        Assert.True(result.RequiresHumanReview);
        Assert.NotEmpty(result.MissingInformation);
    }
}
