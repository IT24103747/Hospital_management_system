using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.Services;

public interface ITriageWorkflowService
{
    Task<TriageWorkflowDto> StartForPatientAsync(int patientId, StartTriageWorkflowDto request);
    Task<TriageWorkflowDto?> GetForPatientAsync(int workflowId, int patientId);
    Task<TriageWorkflowDto?> GetForClinicalReviewerAsync(int workflowId);
    Task<TriageWorkflowDto?> ReviewAsync(int workflowId, int reviewerUserId, ReviewTriageWorkflowDto request);
}
