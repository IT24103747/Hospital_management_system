class TriageWorkflow {
  final int workflowId;
  final String status;
  final String approvalStatus;
  final String triageLevel;
  final String uncertaintyState;
  final bool requiresHumanReview;
  final String patientMessage;
  final List<String> redFlags;
  final List<String> missingInformation;

  const TriageWorkflow({
    required this.workflowId,
    required this.status,
    required this.approvalStatus,
    required this.triageLevel,
    required this.uncertaintyState,
    required this.requiresHumanReview,
    required this.patientMessage,
    required this.redFlags,
    required this.missingInformation,
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
        redFlags: List<String>.from(json['redFlags'] ?? const []),
        missingInformation:
            List<String>.from(json['missingInformation'] ?? const []),
      );
}
