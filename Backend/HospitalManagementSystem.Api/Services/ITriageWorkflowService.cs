using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services;

public interface ITriageWorkflowService
{
    Task<TriageWorkflowDto> StartForPatientAsync(int patientId, StartTriageWorkflowDto request);
    Task<TriageWorkflowDto?> ContinueForPatientAsync(int workflowId, int patientId, ContinueTriageWorkflowDto request);
    Task<TriageWorkflowDto?> GetForPatientAsync(int workflowId, int patientId);
    Task<IReadOnlyList<TriageWorkflowDto>> GetHistoryForPatientAsync(int patientId);
    Task<TriageWorkflowDto?> GetForClinicalReviewerAsync(int workflowId);
    Task<IReadOnlyList<TriageWorkflowDto>> GetPendingClinicalReviewsAsync();
    Task<IReadOnlyList<TriageWorkflowEventDto>?> GetAuditEventsAsync(int workflowId);
    Task<TriageWorkflowDto?> ReviewAsync(int workflowId, int reviewerUserId, ReviewTriageWorkflowDto request);
}
