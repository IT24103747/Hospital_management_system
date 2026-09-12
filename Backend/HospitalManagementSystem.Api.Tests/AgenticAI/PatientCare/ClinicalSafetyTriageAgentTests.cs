using HospitalManagementSystem.Api.AgenticAI.PatientCare.ClinicalSafety;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Services;
using Xunit;

namespace HospitalManagementSystem.Api.Tests.AgenticAI.PatientCare;

public class ClinicalSafetyTriageAgentTests
{
    [Fact]
    public async Task EmergencyPhrase_SkipsGeminiAndEscalates()
    {
        var model = new StubExtractionAgent();
        var result = await new ClinicalSafetyTriageAgent(model)
            .AssessAsync(new("I have severe chest pain and difficulty breathing."));

        Assert.Equal("Emergency", result.TriageLevel);
        Assert.True(result.RequiresClinicalReview);
        Assert.Equal(0, model.Calls);
        Assert.Equal(["ValidateVitalsTool", "EvaluateRedFlagsTool"], result.ToolTrace.Select(item => item.Tool));
    }

    [Fact]
    public async Task RoutineInput_UsesGeminiFactsThenDeterministicValidation()
    {
        var model = new StubExtractionAgent();
        var result = await new ClinicalSafetyTriageAgent(model)
            .AssessAsync(new("I have a mild cough for two days."));

        Assert.Equal("NonUrgent", result.TriageLevel);
        Assert.False(result.RequiresClinicalReview);
        Assert.Equal(1, model.Calls);
        Assert.Contains(result.ToolTrace, item => item.Tool == "GeminiStructuredExtractionTool" && item.ValidationPassed);
        Assert.Contains(result.ToolTrace, item => item.Tool == "EvaluateGroundedClinicalFactsTool");
    }

    [Fact]
    public async Task ImpossibleVitals_FailSafelyWithoutCallingGemini()
    {
        var model = new StubExtractionAgent();
        var result = await new ClinicalSafetyTriageAgent(model).AssessAsync(new("I have a cough.",
            new TriageVitalsDto { OxygenSaturationPercent = 150 }));

        Assert.True(result.FailedSafely);
        Assert.Equal("InsufficientInformation", result.TriageLevel);
        Assert.Equal(0, model.Calls);
    }

    private sealed class StubExtractionAgent : IClinicalInformationExtractionAgent
    {
        public int Calls { get; private set; }

        public Task<ClinicalExtractionResult> ExtractAsync(string symptoms, bool includeFollowUpQuestions = true,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new ClinicalExtractionResult(["Cough"], ["severity"],
                new PatientGuidance("Acknowledged cough.", ["Rest and monitor symptoms."], ["Seek help if symptoms become severe."], []),
                "Completed", Concepts: ["cough"], Facts: new ClinicalFactSet
                {
                    PrimaryConcept = "cough",
                    DurationDays = 2,
                    Evidence = [new ClinicalFactEvidence("primaryConcept", "cough", "cough")]
                }));
        }
    }
}
