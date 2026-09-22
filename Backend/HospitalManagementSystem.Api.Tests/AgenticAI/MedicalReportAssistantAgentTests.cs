using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;
using HospitalManagementSystem.Api.AgenticAI.MedicalReports;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HospitalManagementSystem.Api.Tests.AgenticAI;

public sealed class MedicalReportAssistantAgentTests
{
    [Fact]
    public void Capability_ExposesCorrectIdAndPrompt()
    {
        var agent = new MedicalReportAssistantAgent(new FakeMedicalRecordRepository([]), new FakeIntelligenceAgent());
        
        Assert.Equal("medical-reports", agent.Capability.Id);
        Assert.Equal("Medical Reports", agent.Capability.Label);
        Assert.True(agent.Capability.Enabled);
        Assert.Equal("Summarize my medical reports", agent.Capability.Prompt);
    }

    [Theory]
    [InlineData("Summarize my medical reports", true)]
    [InlineData("Can you summarize my medical report?", true)]
    [InlineData("Show my medical records", true)]
    [InlineData("Explain my lab reports", true)]
    [InlineData("What are my lab results?", true)]
    [InlineData("What medications did the doctor prescribe?", true)]
    [InlineData("Tell me about my prescriptions", true)]
    [InlineData("Explain my medicines", true)]
    [InlineData("What is my diagnosis?", true)]
    [InlineData("Show my discharge summary", true)]
    [InlineData("What do the doctor notes say?", true)]
    [InlineData("My blood tests summary", true)]
    [InlineData("Book an appointment tomorrow", false)]
    [InlineData("What doctors work in Cardiology?", false)]
    [InlineData("What doctors work in General Medicine?", false)]
    [InlineData("Show me the General Medicine doctors", false)]
    [InlineData("Show my appointments", false)]
    [InlineData("Hello", false)]
    [InlineData("", false)]
    public void CanHandle_MatchesRelevantClinicalInquiries(string input, bool expected)
    {
        var agent = new MedicalReportAssistantAgent(new FakeMedicalRecordRepository([]), new FakeIntelligenceAgent());
        Assert.Equal(expected, agent.CanHandle(input));
    }

    [Fact]
    public async Task ReadAsync_WhenNoRecordsExist_ReturnsPatientGuidance()
    {
        var repo = new FakeMedicalRecordRepository([]);
        var agent = new MedicalReportAssistantAgent(repo, new FakeIntelligenceAgent());
        var patient = new PatientDto { PatientId = 1, FirstName = "Kasun", LastName = "Perera" };

        var reply = await agent.ReadAsync("Summarize my medical reports", patient, CancellationToken.None);

        Assert.Contains("Kasun Perera", reply);
        Assert.Contains("currently do not have any recorded medical reports", reply);
    }

    [Fact]
    public async Task ReadAsync_WhenRecordsExist_InvokesIntelligenceAgent()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 10,
            PatientId = 1,
            RecordDate = DateTime.UtcNow.AddDays(-2),
            Diagnosis = "Acute Bronchitis",
            Symptoms = "Cough, fever",
            TreatmentPlan = "Rest and hydration",
            PrescriptionNotes = "Amoxicillin 500mg TDS for 5 days"
        };
        var repo = new FakeMedicalRecordRepository([record]);
        var fakeAi = new FakeIntelligenceAgent();
        var agent = new MedicalReportAssistantAgent(repo, fakeAi);
        var patient = new PatientDto { PatientId = 1, FirstName = "Kasun", LastName = "Perera" };

        var reply = await agent.ReadAsync("Summarize my medical reports", patient, CancellationToken.None);

        Assert.Equal("Analysis for Kasun Perera: 1 records", reply);
        Assert.Single(fakeAi.ObservedRecords);
        Assert.Equal("Kasun Perera", fakeAi.ObservedPatientName);
    }

    [Fact]
    public void DeterministicSafetyEngine_DetectsAntibioticsAdvisory()
    {
        var records = new[]
        {
            new MedicalRecord
            {
                MedicalRecordId = 1,
                PrescriptionNotes = "Take Amoxicillin 500mg three times daily",
                TreatmentPlan = "Follow medication course"
            }
        };

        var alerts = DeterministicClinicalSafetyEngine.EvaluateSafety(records);

        var abxAlert = Assert.Single(alerts, a => a.DrugName.Equals("Amoxicillin", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Advisory", abxAlert.Severity);
        Assert.Contains("full antibiotic course", abxAlert.Message);
    }

    [Fact]
    public void DeterministicSafetyEngine_DetectsMultipleNsaidsInteraction()
    {
        var records = new[]
        {
            new MedicalRecord
            {
                MedicalRecordId = 1,
                PrescriptionNotes = "Ibuprofen 400mg with Aspirin 75mg daily",
                TreatmentPlan = "Pain management"
            }
        };

        var alerts = DeterministicClinicalSafetyEngine.EvaluateSafety(records);

        var nsaidAlert = Assert.Single(alerts, a => a.Severity == "Warning");
        Assert.Contains("NSAID", nsaidAlert.Message);
        Assert.Contains("Ibuprofen", nsaidAlert.DrugName);
        Assert.Contains("Aspirin", nsaidAlert.DrugName);
    }

    [Fact]
    public void DeterministicSafetyEngine_DetectsParacetamolDosageRule()
    {
        var records = new[]
        {
            new MedicalRecord
            {
                MedicalRecordId = 1,
                PrescriptionNotes = "Paracetamol 500mg 2 tabs PRN for fever",
                TreatmentPlan = "Rest"
            }
        };

        var alerts = DeterministicClinicalSafetyEngine.EvaluateSafety(records);

        var pcmAlert = Assert.Single(alerts, a => a.DrugName.Equals("Paracetamol", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("4,000 mg", pcmAlert.Message);
    }

    [Fact]
    public void DeterministicSafetyEngine_EvaluatesFollowUpDates()
    {
        var upcoming = new MedicalRecord
        {
            MedicalRecordId = 1,
            FollowUpDate = DateTime.UtcNow.AddDays(3),
            TreatmentPlan = "Review wound healing"
        };
        var alertsUpcoming = DeterministicClinicalSafetyEngine.EvaluateSafety([upcoming]);
        Assert.Contains(alertsUpcoming, a => a.Severity == "Reminder" && a.Message.Contains("in 3 days"));

        var overdue = new MedicalRecord
        {
            MedicalRecordId = 2,
            FollowUpDate = DateTime.UtcNow.AddDays(-5),
            TreatmentPlan = "Routine follow up"
        };
        var alertsOverdue = DeterministicClinicalSafetyEngine.EvaluateSafety([overdue]);
        Assert.Contains(alertsOverdue, a => a.Severity == "Advisory" && a.Message.Contains("scheduled for"));
    }

    [Fact]
    public void DeterministicSafetyEngine_BuildHeuristicSummary_FormatsFullClinicalReport()
    {
        var record = new MedicalRecord
        {
            MedicalRecordId = 1,
            RecordDate = new DateTime(2026, 9, 10),
            RecordType = MedicalRecordTypes.Consultation,
            Doctor = new Doctor { FirstName = "Saman", LastName = "Jayasinghe" },
            Diagnosis = "Streptococcal Pharyngitis",
            Symptoms = "Sore throat, fever",
            TreatmentPlan = "Oral antibiotic therapy and salt water gargle",
            PrescriptionNotes = "Amoxicillin 500mg TDS for 7 days; Paracetamol 500mg PRN",
            LabNotes = "Throat swab culture: Streptococcus pyogenes confirmed positive",
            FollowUpDate = DateTime.UtcNow.AddDays(4)
        };

        var summary = DeterministicClinicalSafetyEngine.BuildHeuristicSummary([record], "Nimal Bandara", null);

        Assert.False(summary.UsedGemini);
        Assert.Contains("Dr. Saman Jayasinghe", summary.PlainLanguageSummary);
        Assert.Contains("Streptococcal Pharyngitis", summary.PlainLanguageSummary);
        Assert.Contains("Amoxicillin 500mg", summary.PlainLanguageSummary);
        Assert.Contains("Streptococcus pyogenes", summary.PlainLanguageSummary);
        Assert.Contains("⚠️ Important Safety Precautions", summary.PlainLanguageSummary);
        Assert.Contains("Guidance Disclaimer", summary.PlainLanguageSummary);
    }

    [Fact]
    public void AssistantAgentRegistry_ExposesMedicalReportsCapabilityWhenAgentPresent()
    {
        var readAgent = new MedicalReportAssistantAgent(new FakeMedicalRecordRepository([]), new FakeIntelligenceAgent());
        var registry = new AssistantAgentRegistry([readAgent]);

        var capabilities = registry.Capabilities;
        var medReportsCap = Assert.Single(capabilities, c => c.Id == "medical-reports");
        
        Assert.True(medReportsCap.Enabled);
        Assert.Equal("Medical Reports", medReportsCap.Label);
        Assert.Equal("Summarize my medical reports", medReportsCap.Prompt);
    }

    [Fact]
    public async Task GeminiMedicalRecordClient_FallsBackToDeterministic_WhenApiKeyEmpty()
    {
        var inMemoryConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gemini:ApiKey"] = ""
            })
            .Build();

        using var http = new System.Net.Http.HttpClient();
        var client = new GeminiMedicalRecordClient(http, inMemoryConfig, NullLogger<GeminiMedicalRecordClient>.Instance);

        var record = new MedicalRecord
        {
            MedicalRecordId = 1,
            Diagnosis = "Essential Hypertension",
            TreatmentPlan = "Lifestyle modification",
            PrescriptionNotes = "Amlodipine 5mg OD"
        };

        var result = await client.AnalyzeRecordsAsync([record], "Sunil", null);

        Assert.False(result.UsedGemini);
        Assert.Contains("Essential Hypertension", result.PlainLanguageSummary);
        Assert.Contains("Amlodipine 5mg OD", result.PlainLanguageSummary);
    }

    private sealed class FakeMedicalRecordRepository : IMedicalRecordRepository
    {
        private readonly List<MedicalRecord> _records;

        public FakeMedicalRecordRepository(IEnumerable<MedicalRecord> records)
        {
            _records = records.ToList();
        }

        public Task<IEnumerable<MedicalRecord>> GetByPatientIdAsync(int patientId) =>
            Task.FromResult<IEnumerable<MedicalRecord>>(_records.Where(r => r.PatientId == patientId).ToList());

        public Task<MedicalRecord?> GetByIdAsync(int id) =>
            Task.FromResult(_records.FirstOrDefault(r => r.MedicalRecordId == id));

        public Task<IEnumerable<MedicalRecord>> GetAllAsync(
            int? patientId,
            int? doctorId,
            string? recordType,
            string? status,
            string? search,
            DateTime? fromDate,
            DateTime? toDate,
            string? sortBy,
            string? sortDirection,
            int page,
            int pageSize) =>
            Task.FromResult<IEnumerable<MedicalRecord>>(_records);

        public Task<int> GetTotalCountAsync(
            int? patientId,
            int? doctorId,
            string? recordType,
            string? status,
            string? search,
            DateTime? fromDate,
            DateTime? toDate) =>
            Task.FromResult(_records.Count);

        public Task<MedicalRecordSummaryDto> GetSummaryAsync(int? doctorId = null) =>
            Task.FromResult(new MedicalRecordSummaryDto());

        public Task<MedicalRecord> CreateAsync(MedicalRecord record)
        {
            _records.Add(record);
            return Task.FromResult(record);
        }

        public Task<MedicalRecord> UpdateAsync(MedicalRecord record) => Task.FromResult(record);

        public Task DeleteAsync(MedicalRecord record)
        {
            _records.Remove(record);
            return Task.CompletedTask;
        }

        public Task<MedicalRecordAttachment> AddAttachmentAsync(MedicalRecordAttachment attachment) => Task.FromResult(attachment);

        public Task<MedicalRecordAttachment?> GetAttachmentByIdAsync(int attachmentId) => Task.FromResult<MedicalRecordAttachment?>(null);

        public Task DeleteAttachmentAsync(MedicalRecordAttachment attachment) => Task.CompletedTask;
    }

    private sealed class FakeIntelligenceAgent : IMedicalRecordIntelligenceAgent
    {
        public List<MedicalRecord> ObservedRecords { get; } = [];
        public string? ObservedPatientName { get; private set; }

        public Task<MedicalReportAnalysisResult> AnalyzeRecordsAsync(
            IEnumerable<MedicalRecord> records,
            string patientName,
            string? specificUserQuery,
            CancellationToken cancellationToken = default)
        {
            ObservedRecords.AddRange(records);
            ObservedPatientName = patientName;
            return Task.FromResult(new MedicalReportAnalysisResult(
                Overview: "Mock Overview",
                KeyDiagnoses: ["Mock Diagnosis"],
                PrescribedMedications: ["Mock Med"],
                LabFindings: [],
                SafetyAlerts: [],
                FollowUpInstructions: null,
                PlainLanguageSummary: $"Analysis for {patientName}: {ObservedRecords.Count} records",
                AgentTrajectoryDescription: "Mock Agent",
                UsedGemini: false
            ));
        }
    }
}
