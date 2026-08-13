class TriageWorkflow {
  final int workflowId;
  final String status;
  final String approvalStatus;
  final String triageLevel;
  final String uncertaintyState;
  final bool requiresHumanReview;
  final String patientMessage;
  final TriageGuidance? guidance;
  final List<String> redFlags;
  final List<String> missingInformation;
  final List<TriagePlanStep> plan;

  const TriageWorkflow({
    required this.workflowId,
    required this.status,
    required this.approvalStatus,
    required this.triageLevel,
    required this.uncertaintyState,
    required this.requiresHumanReview,
    required this.patientMessage,
    required this.guidance,
    required this.redFlags,
    required this.missingInformation,
    required this.plan,
  });

  factory TriageWorkflow.fromJson(Map<String, dynamic> json) => TriageWorkflow(
        workflowId: json['workflowId'] as int,
        status: json['status']?.toString() ?? 'Unknown',
        approvalStatus: json['approvalStatus']?.toString() ?? 'Unknown',
        triageLevel:
            json['triageLevel']?.toString() ?? 'InsufficientInformation',
        uncertaintyState:
            json['uncertaintyState']?.toString() ?? 'LimitedInformation',
        requiresHumanReview: json['requiresHumanReview'] == true,
        patientMessage: json['patientMessage']?.toString() ??
            'The system cannot safely assess this situation.',
        guidance: json['guidance'] is Map<String, dynamic>
            ? TriageGuidance.fromJson(json['guidance'] as Map<String, dynamic>)
            : null,
        redFlags: List<String>.from(json['redFlags'] ?? const []),
        missingInformation:
            List<String>.from(json['missingInformation'] ?? const []),
        plan: (json['plan'] as List<dynamic>? ?? const [])
            .map(
                (item) => TriagePlanStep.fromJson(item as Map<String, dynamic>))
            .toList(),
      );
}

class TriageGuidance {
  final String heading;
  final String summary;
  final List<String> actions;
  final List<String> seekHelpIf;
  final List<String> followUpQuestions;
  final String evidenceSource;

  const TriageGuidance(
      {required this.heading,
      required this.summary,
      required this.actions,
      required this.seekHelpIf,
      required this.followUpQuestions,
      required this.evidenceSource});

  factory TriageGuidance.fromJson(Map<String, dynamic> json) => TriageGuidance(
        heading: json['heading']?.toString() ?? 'General guidance',
        summary: json['summary']?.toString() ?? '',
        actions: List<String>.from(json['actions'] ?? const []),
        seekHelpIf: List<String>.from(json['seekHelpIf'] ?? const []),
        followUpQuestions:
            List<String>.from(json['followUpQuestions'] ?? const []),
        evidenceSource: json['evidenceSource']?.toString() ?? '',
      );
}

class TriagePlanStep {
  final String agent;
  final String status;
  final String purpose;
  const TriagePlanStep(
      {required this.agent, required this.status, required this.purpose});
  factory TriagePlanStep.fromJson(Map<String, dynamic> json) => TriagePlanStep(
        agent: json['agent']?.toString() ?? 'Workflow agent',
        status: json['status']?.toString() ?? 'Unknown',
        purpose: json['purpose']?.toString() ?? '',
      );
}
