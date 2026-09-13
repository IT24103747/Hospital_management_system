using System.Collections.Concurrent;

namespace HospitalManagementSystem.Api.AgenticAI.PlanningCoordinator;

public sealed class PlanningCoordinatorStore : IPlanningCoordinatorStore
{
    private readonly ConcurrentDictionary<string, PlanningWorkflowRecord> _workflows = new();

    public Task SaveAsync(PlanningWorkflowRecord record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(record);

        record.UpdatedAt = DateTimeOffset.UtcNow;
        _workflows.AddOrUpdate(record.WorkflowId, record, (_, _) => record);
        return Task.CompletedTask;
    }

    public Task<PlanningWorkflowRecord?> GetAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(workflowId)) return Task.FromResult<PlanningWorkflowRecord?>(null);

        _workflows.TryGetValue(workflowId, out var record);
        return Task.FromResult(record);
    }

    public Task<IReadOnlyList<PlanningWorkflowRecord>> GetByPatientIdAsync(int patientId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var records = _workflows.Values
            .Where(w => w.PatientId == patientId)
            .OrderByDescending(w => w.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<PlanningWorkflowRecord>>(records);
    }

    public Task AddAuditEventAsync(string workflowId, string eventType, string description, string? metadata = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_workflows.TryGetValue(workflowId, out var record))
        {
            lock (record)
            {
                record.AuditEvents.Add(new PlanningAuditEvent
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    EventType = eventType,
                    Description = description,
                    Metadata = metadata
                });
                record.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(string workflowId, string status, string? completedStage = null, string? error = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_workflows.TryGetValue(workflowId, out var record))
        {
            lock (record)
            {
                record.Status = status;
                if (!string.IsNullOrWhiteSpace(completedStage) && !record.CompletedStages.Contains(completedStage))
                {
                    record.CompletedStages.Add(completedStage);
                }
                if (!string.IsNullOrWhiteSpace(error) && !record.Errors.Contains(error))
                {
                    record.Errors.Add(error);
                }
                record.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        return Task.CompletedTask;
    }
}
