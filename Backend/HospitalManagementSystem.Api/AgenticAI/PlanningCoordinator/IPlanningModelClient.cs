namespace HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

public interface IPlanningModelClient
{
    Task<GeminiPlanningDecision> PlanObjectiveAsync(string objective, CancellationToken cancellationToken = default);
}
