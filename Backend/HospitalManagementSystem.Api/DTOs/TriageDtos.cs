using System.ComponentModel.DataAnnotations;

namespace HospitalManagementSystem.Api.DTOs;

public class StartTriageWorkflowDto
{
    [Required, MinLength(3), MaxLength(4000)] public string Symptoms { get; set; } = string.Empty;
    public TriageVitalsDto? Vitals { get; set; }
}

public class TriageVitalsDto
{
    public decimal? TemperatureCelsius { get; set; }
    public int? HeartRateBpm { get; set; }
    public int? SystolicBloodPressure { get; set; }
    public int? DiastolicBloodPressure { get; set; }
    public int? OxygenSaturationPercent { get; set; }
    public DateTime? ObservedAt { get; set; }
    public string? Source { get; set; }
}

public class ReviewTriageWorkflowDto
{
    [Required] public string Decision { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Note { get; set; }
}

public class TriageWorkflowDto
{
    public int WorkflowId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ApprovalStatus { get; set; } = string.Empty;
    public string TriageLevel { get; set; } = string.Empty;
    public string UncertaintyState { get; set; } = string.Empty;
    public bool RequiresHumanReview { get; set; }
    public string PatientMessage { get; set; } = string.Empty;
    public IReadOnlyList<string> RiskFactors { get; set; } = [];
    public IReadOnlyList<string> RedFlags { get; set; } = [];
    public IReadOnlyList<string> MissingInformation { get; set; } = [];
    public IReadOnlyList<TriagePlanStepDto> Plan { get; set; } = [];
    public string RuleSetVersion { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TriagePlanStepDto { public string Agent { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; public string Purpose { get; set; } = string.Empty; }

public class TriageWorkflowEventDto
{
    public string Stage { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
