using System.Text.Json;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.Shared;

public interface IPatientCareAssessmentStore
{
    Task<int> SaveAsync(int patientId, string symptoms, string? specialty, bool requestedAppointment, string triageLevel, object clinical, object? proposal, CancellationToken token);
    Task<IReadOnlyList<PatientCareAssessment>> GetHistoryAsync(int patientId, CancellationToken token);
}

public sealed class PatientCareAssessmentStore(ApplicationDbContext db) : IPatientCareAssessmentStore
{
    public async Task<int> SaveAsync(int patientId, string symptoms, string? specialty, bool requestedAppointment, string triageLevel, object clinical, object? proposal, CancellationToken token)
    {
        var assessment = new PatientCareAssessment {
            PatientId = patientId, Symptoms = symptoms, RequestedSpecialty = specialty,
            RequestedAppointmentProposal = requestedAppointment, TriageLevel = triageLevel,
            Status = triageLevel is "Emergency" or "Urgent" ? "SafetyEscalation" : "Completed",
            ClinicalJson = JsonSerializer.Serialize(clinical),
            ProposalJson = proposal is null ? null : JsonSerializer.Serialize(proposal)
        };
        db.PatientCareAssessments.Add(assessment);
        await db.SaveChangesAsync(token);
        return assessment.PatientCareAssessmentId;
    }

    public async Task<IReadOnlyList<PatientCareAssessment>> GetHistoryAsync(int patientId, CancellationToken token) =>
        await db.PatientCareAssessments.AsNoTracking().Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.CreatedAt).Take(30).ToListAsync(token);
}
