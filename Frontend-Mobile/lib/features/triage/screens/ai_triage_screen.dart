import 'dart:async';

import 'package:flutter/material.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
import 'package:smartcare_mobile/features/clinic_finder/screens/emergency_clinic_screen.dart';
import 'package:smartcare_mobile/models/triage_workflow.dart';

class AiTriageScreen extends StatefulWidget {
  final bool embedded;

  const AiTriageScreen({super.key, this.embedded = false});

  @override
  State<AiTriageScreen> createState() => _AiTriageScreenState();
}

class _AiTriageScreenState extends State<AiTriageScreen> {
  final _symptomController = TextEditingController();
  final _heartRateController = TextEditingController();
  final _temperatureController = TextEditingController();
  final _processingKey = GlobalKey();
  bool _isSubmitting = false;
  int _activeStage = 0;
  Timer? _progressTimer;
  TriageWorkflow? _workflow;
  List<TriageWorkflow> _assessmentHistory = [];
  int? _expandedHistoryWorkflowId;
  TriageWorkflow? _expandedHistoryWorkflow;
  final Map<String, String> _followUpAnswers = {};
  final Map<String, TextEditingController> _followUpControllers = {};
  bool _followUpRoundComplete = false;

  @override
  void initState() {
    super.initState();
    _loadAssessmentHistory();
  }

  Future<void> _loadAssessmentHistory() async {
    try {
      final history = await ApiService.getTriageWorkflowHistory();
      if (mounted) setState(() => _assessmentHistory = history);
    } catch (_) {
      // History is an enhancement; an assessment must still work offline/from a fresh account.
    }
  }

  static const _workflowStages = [
    (
      'Checking your information',
      'Making sure the details you entered can be processed safely.'
    ),
    (
      'Checking for urgent warning signs',
      'Looking for symptoms that need immediate or urgent care.'
    ),
    ('Understanding your symptoms', 'Organizing the details you entered.'),
    (
      'Preparing guidance',
      'Creating general information and helpful questions.'
    ),
    (
      'Verifying the response',
      'Making sure the response follows safety requirements.'
    ),
  ];

  Future<void> _startTriage({bool includeFollowUpAnswers = false}) async {
    if (!includeFollowUpAnswers) {
      final inputIssue = _symptomInputIssue(_symptomController.text);
      if (inputIssue != null) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(inputIssue)));
        return;
      }
    }
    final symptoms = _symptomController.text.trim();
    final answers = includeFollowUpAnswers ? _answersForSubmission() : null;
    if (includeFollowUpAnswers && answers == null) return;
    setState(() {
      if (!includeFollowUpAnswers) _followUpRoundComplete = false;
      _isSubmitting = true;
      _activeStage = 0;
    });
    if (includeFollowUpAnswers) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        final processingContext = _processingKey.currentContext;
        if (processingContext != null) {
          Scrollable.ensureVisible(processingContext,
              duration: const Duration(milliseconds: 350),
              curve: Curves.easeOutCubic,
              alignment: .15);
        }
      });
    }
    _progressTimer?.cancel();
    _progressTimer = Timer.periodic(const Duration(milliseconds: 900), (_) {
      if (mounted && _activeStage < _workflowStages.length - 1) {
        setState(() => _activeStage++);
      }
    });
    try {
      final workflow = includeFollowUpAnswers
          ? await ApiService.continueTriageWorkflow(
              workflowId: _workflow!.workflowId, answers: answers!)
          : await ApiService.startTriageWorkflow(
              symptoms: symptoms, vitals: _optionalVitals());
      if (mounted) {
        _followUpRoundComplete = includeFollowUpAnswers;
        _setWorkflow(workflow);
        if (!includeFollowUpAnswers) {
          _symptomController.clear();
          _heartRateController.clear();
          _temperatureController.clear();
        }
      }
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
            content: Text(
                'SafeTriage could not safely process this request: $error'),
            backgroundColor: AppColors.danger));
      }
    } finally {
      _progressTimer?.cancel();
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  String? _symptomInputIssue(String raw) {
    final value = raw.trim();
    if (value.length < 3 || !RegExp(r'[A-Za-z]').hasMatch(value)) {
      return 'Describe your symptom using a few words.';
    }
    if (RegExp(r'^(.)\1{2,}$').hasMatch(value.replaceAll(' ', ''))) {
      return 'Please avoid repeated characters and describe what you are feeling.';
    }
    if (RegExp(r'\b(password|passcode|cvv|card number|account number)\b',
                caseSensitive: false)
            .hasMatch(value) ||
        RegExp(r'[\w.+-]+@[\w.-]+\.[A-Za-z]{2,}|\+?\d[\d\s().-]{7,}\d')
            .hasMatch(value)) {
      return 'For your privacy, remove contact, account, and password details.';
    }
    return null;
  }

  List<Map<String, dynamic>>? _answersForSubmission() {
    final questions = _workflow?.guidance?.followUpItems ?? const [];
    final missingRequired = questions.where((question) =>
        question.required &&
        (_followUpAnswers[question.id]?.trim().isEmpty ?? true));
    if (missingRequired.isNotEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('Please answer every required follow-up question.')));
      return null;
    }
    return questions
        .where((question) =>
            _followUpAnswers[question.id]?.trim().isNotEmpty == true)
        .map((question) => {
              'questionId': question.id,
              'value': _followUpAnswers[question.id]!.trim(),
              if (question.unit != null) 'unit': question.unit,
            })
        .toList();
  }

  void _setWorkflow(TriageWorkflow workflow) {
    for (final controller in _followUpControllers.values) {
      controller.dispose();
    }
    _followUpControllers.clear();
    _followUpAnswers.clear();
    for (final question in workflow.guidance?.followUpItems ?? const []) {
      _followUpControllers[question.id] = TextEditingController();
    }
    setState(() {
      _workflow = workflow;
      _expandedHistoryWorkflowId = null;
      _expandedHistoryWorkflow = null;
      _assessmentHistory = [
        workflow,
        ..._assessmentHistory
            .where((item) => item.workflowId != workflow.workflowId),
      ];
    });
  }

  Map<String, dynamic>? _optionalVitals() {
    final heartRateText = _heartRateController.text.trim();
    final temperatureText = _temperatureController.text.trim();
    if (heartRateText.isEmpty && temperatureText.isEmpty) return null;

    final vitals = <String, dynamic>{'source': 'patient-reported'};
    if (heartRateText.isNotEmpty) {
      final heartRate = int.tryParse(heartRateText);
      if (heartRate == null) {
        throw const FormatException('Heart rate must be a whole number.');
      }
      vitals['heartRateBpm'] = heartRate;
    }
    if (temperatureText.isNotEmpty) {
      final temperature = double.tryParse(temperatureText);
      if (temperature == null) {
        throw const FormatException('Temperature must be a valid number.');
      }
      vitals['temperatureCelsius'] = temperature;
    }
    return vitals;
  }

  Future<void> _refreshStatus() async {
    if (_workflow == null) return;
    setState(() => _isSubmitting = true);
    try {
      final workflow =
          await ApiService.getTriageWorkflow(_workflow!.workflowId);
      if (mounted) _setWorkflow(workflow);
    } catch (error) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('Unable to refresh status: $error')));
      }
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  @override
  void dispose() {
    _progressTimer?.cancel();
    _symptomController.dispose();
    _heartRateController.dispose();
    _temperatureController.dispose();
    for (final controller in _followUpControllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isEmergency = _workflow?.triageLevel == 'Emergency';
    final isUrgent = _workflow?.triageLevel == 'Urgent';
    final isClinicalReview = _workflow?.triageLevel == 'ClinicalReview';
    final isAwaitingFollowUp =
        _workflow?.status == 'PendingPatientInput' && !_followUpRoundComplete;
    final needsEscalation = isEmergency || isUrgent || isClinicalReview;
    final resultColor = needsEscalation
        ? isClinicalReview
            ? AppColors.warning
            : AppColors.danger
        : isAwaitingFollowUp
            ? AppColors.accent
            : AppColors.primary;
    final resultLabel =
        isAwaitingFollowUp ? 'Assessment incomplete' : _workflow?.triageLevel;
    final content = SingleChildScrollView(
      padding: const EdgeInsets.all(20),
      child: Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
        Container(
          padding: const EdgeInsets.all(20),
          decoration: BoxDecoration(
            gradient: const LinearGradient(
              colors: [AppColors.primaryDark, AppColors.accent],
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
            ),
            borderRadius: BorderRadius.circular(22),
            boxShadow: [
              BoxShadow(
                  color: AppColors.primary.withValues(alpha: .25),
                  blurRadius: 22,
                  offset: const Offset(0, 10)),
            ],
          ),
          child: const Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                CircleAvatar(
                    radius: 25,
                    backgroundColor: Color(0x33FFFFFF),
                    child: Icon(Icons.auto_awesome_rounded,
                        color: Colors.white, size: 27)),
                SizedBox(width: 14),
                Expanded(
                    child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                      Text('SafeTriage AI',
                          style: TextStyle(
                              color: Colors.white,
                              fontSize: 21,
                              fontWeight: FontWeight.w800)),
                      SizedBox(height: 5),
                      Text(
                          'Guided symptom support with built-in safety checks.',
                          style: TextStyle(
                              color: Color(0xE6FFFFFF), height: 1.35)),
                      SizedBox(height: 11),
                      Text(
                          'Not a diagnosis · Emergency symptoms need immediate care',
                          style: TextStyle(
                              color: Color(0xCCFFFFFF), fontSize: 11)),
                    ])),
              ]),
        ),
        const SizedBox(height: 20),
        const Text('Start an assessment',
            style: TextStyle(fontWeight: FontWeight.w800, fontSize: 18)),
        const SizedBox(height: 3),
        const Text('Describe what you are feeling in your own words.',
            style: TextStyle(color: AppColors.textSecondaryLight)),
        const SizedBox(height: 8),
        TextFormField(
            controller: _symptomController,
            maxLines: 4,
            decoration: const InputDecoration(
                prefixIcon: Padding(
                    padding: EdgeInsets.only(bottom: 58),
                    child: Icon(Icons.edit_note_outlined)),
                hintText:
                    'Example: I have felt dizzy since this morning…\n\nDo not include passwords or account details.')),
        const SizedBox(height: 12),
        const Text('Optional patient-reported vitals',
            style: TextStyle(fontSize: 13, fontWeight: FontWeight.bold)),
        const SizedBox(height: 8),
        Row(children: [
          Expanded(
              child: TextFormField(
                  controller: _heartRateController,
                  keyboardType: TextInputType.number,
                  decoration: const InputDecoration(
                      prefixIcon: Icon(Icons.favorite_border),
                      labelText: 'Heart rate (bpm)'))),
          const SizedBox(width: 12),
          Expanded(
              child: TextFormField(
                  controller: _temperatureController,
                  keyboardType:
                      const TextInputType.numberWithOptions(decimal: true),
                  decoration: const InputDecoration(
                      prefixIcon: Icon(Icons.thermostat_outlined),
                      labelText: 'Temperature (°C)'))),
        ]),
        const SizedBox(height: 16),
        SizedBox(
            height: 52,
            child: ElevatedButton.icon(
                onPressed: _isSubmitting ? null : _startTriage,
                icon: const Icon(Icons.health_and_safety_outlined),
                label: Text(_isSubmitting
                    ? 'Submitting safely…'
                    : 'Submit for SafeTriage'))),
        if (_isSubmitting) ...[
          const SizedBox(height: 18),
          Container(key: _processingKey, child: _buildLiveWorkflowProgress()),
        ],
        if (_workflow != null) ...[
          const SizedBox(height: 24),
          _buildSubmittedReport(_workflow!),
          const SizedBox(height: 12),
          Card(
              margin: EdgeInsets.zero,
              elevation: 0,
              shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(20),
                  side: BorderSide(color: resultColor.withValues(alpha: .30))),
              child: Padding(
                  padding: const EdgeInsets.all(18),
                  child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(children: [
                          Container(
                            padding: const EdgeInsets.all(10),
                            decoration: BoxDecoration(
                                color: resultColor.withValues(alpha: .12),
                                borderRadius: BorderRadius.circular(12)),
                            child: Icon(
                                needsEscalation
                                    ? Icons.warning_amber_rounded
                                    : isAwaitingFollowUp
                                        ? Icons.pending_actions_outlined
                                        : Icons.verified_user_outlined,
                                color: resultColor),
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                              child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                const Text('Assessment result',
                                    style: TextStyle(
                                        fontSize: 12,
                                        color: AppColors.textSecondaryLight)),
                                Text(resultLabel ?? 'Assessment unavailable',
                                    style: TextStyle(
                                        fontSize: 20,
                                        fontWeight: FontWeight.w800,
                                        color: resultColor)),
                              ])),
                        ]),
                        const SizedBox(height: 8),
                        Text(_workflow!.patientMessage),
                        const SizedBox(height: 12),
                        Wrap(spacing: 8, runSpacing: 8, children: [
                          _statusPill(Icons.shield_outlined,
                              _workflow!.uncertaintyState),
                          _statusPill(
                              Icons.account_tree_outlined, _workflow!.status),
                          if (_workflow!.requiresHumanReview)
                            _statusPill(Icons.person_search_outlined,
                                'Clinical review needed'),
                        ]),
                        if (isEmergency) ...[
                          const SizedBox(height: 14),
                          Container(
                            padding: const EdgeInsets.all(14),
                            decoration: BoxDecoration(
                              color: AppColors.danger.withValues(alpha: .12),
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: const Text(
                              'Do not wait for clinical review. Seek immediate emergency care now. The clinician alert is for follow-up and audit only.',
                              style: TextStyle(fontWeight: FontWeight.bold),
                            ),
                          ),
                        ],
                        if (isUrgent) ...[
                          const SizedBox(height: 14),
                          Container(
                            padding: const EdgeInsets.all(14),
                            decoration: BoxDecoration(
                              color: AppColors.warning.withValues(alpha: .12),
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: const Text(
                              'Seek urgent medical assessment now. Do not wait for clinical review. If symptoms become severe, sudden, persistent, or you develop breathing difficulty, sweating, sickness, or light-headedness, seek emergency care immediately.',
                              style: TextStyle(fontWeight: FontWeight.bold),
                            ),
                          ),
                        ],
                        if (isClinicalReview) ...[
                          const SizedBox(height: 14),
                          Container(
                            padding: const EdgeInsets.all(14),
                            decoration: BoxDecoration(
                              color: AppColors.warning.withValues(alpha: .12),
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: const Row(children: [
                              Icon(Icons.medical_information_outlined,
                                  color: AppColors.warning),
                              SizedBox(width: 10),
                              Expanded(
                                  child: Text(
                                'CLINICAL REVIEW REQUIRED\nA qualified clinician must review this report. Contact the relevant care team. If symptoms become severe or rapidly worsen, seek urgent or emergency care.',
                                style: TextStyle(
                                    fontWeight: FontWeight.bold, height: 1.35),
                              )),
                            ]),
                          ),
                        ],
                        if (_workflow!.guidance != null) ...[
                          const SizedBox(height: 16),
                          _buildAgentResponse(_workflow!, isEmergency),
                        ],
                        const SizedBox(height: 12),
                        _buildTechnicalDetails(_workflow!),
                        if (_workflow!.requiresHumanReview) ...[
                          const SizedBox(height: 16),
                          OutlinedButton.icon(
                              onPressed: _isSubmitting ? null : _refreshStatus,
                              icon: const Icon(Icons.refresh),
                              label:
                                  const Text('Refresh clinical-review status')),
                        ],
                        if (isEmergency)
                          SizedBox(
                              width: double.infinity,
                              child: ElevatedButton.icon(
                                  style: ElevatedButton.styleFrom(
                                      backgroundColor: AppColors.danger),
                                  onPressed: () => Navigator.push(
                                      context,
                                      MaterialPageRoute(
                                          builder: (_) =>
                                              const EmergencyClinicScreen())),
                                  icon: const Icon(Icons.warning_amber_rounded),
                                  label: const Text('Find emergency clinic'))),
                      ]))),
        ],
        if (_assessmentHistory.isNotEmpty) ...[
          const SizedBox(height: 24),
          _buildAssessmentHistory(),
        ],
      ]),
    );
    if (widget.embedded) return content;
    return Scaffold(
      appBar: AppBar(title: const Text('SafeTriage decision support')),
      body: content,
    );
  }

  Widget _buildLiveWorkflowProgress() => Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: AppColors.primary.withValues(alpha: .07),
          border: Border.all(color: AppColors.primary.withValues(alpha: .22)),
          borderRadius: BorderRadius.circular(14),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Row(children: [
              SizedBox(
                  width: 17,
                  height: 17,
                  child: CircularProgressIndicator(strokeWidth: 2.5)),
              SizedBox(width: 10),
              Text('SafeTriage is processing',
                  style: TextStyle(fontWeight: FontWeight.bold)),
            ]),
            const SizedBox(height: 6),
            const Text('Please wait while we prepare a safe response.'),
            const SizedBox(height: 12),
            ...List.generate(_workflowStages.length, (index) {
              final isCurrent = index == _activeStage;
              final isDone = index < _activeStage;
              final color = isDone || isCurrent
                  ? AppColors.primary
                  : AppColors.textSecondaryLight;
              return Padding(
                padding: const EdgeInsets.only(bottom: 9),
                child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(
                          isDone
                              ? Icons.check_circle
                              : isCurrent
                                  ? Icons.sync
                                  : Icons.radio_button_unchecked,
                          size: 18,
                          color: color),
                      const SizedBox(width: 9),
                      Expanded(
                          child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                            Text(_workflowStages[index].$1,
                                style: TextStyle(
                                    fontWeight: isCurrent
                                        ? FontWeight.bold
                                        : FontWeight.w500,
                                    color: color)),
                            if (isCurrent)
                              Text(_workflowStages[index].$2,
                                  style: const TextStyle(fontSize: 12)),
                          ])),
                    ]),
              );
            }),
          ],
        ),
      );

  Widget _buildAgentResponse(TriageWorkflow workflow, bool isEmergency) {
    final guidance = workflow.guidance!;
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      _responsePanel(
        icon: Icons.auto_awesome_rounded,
        title: guidance.heading,
        color: AppColors.primary,
        child: Text(guidance.summary.isEmpty
            ? 'General information based on the details you reported.'
            : guidance.summary),
      ),
      const SizedBox(height: 10),
      _responsePanel(
        icon: Icons.check_circle_outline,
        title: 'Recommended next steps',
        color: AppColors.success,
        child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: guidance.actions
                .map((item) => Padding(
                      padding: const EdgeInsets.only(bottom: 6),
                      child: Text('• $item'),
                    ))
                .toList()),
      ),
      const SizedBox(height: 10),
      _responsePanel(
        icon: Icons.warning_amber_rounded,
        title: 'When to get help sooner',
        color: AppColors.warning,
        child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: guidance.seekHelpIf
                .map((item) => Padding(
                      padding: const EdgeInsets.only(bottom: 6),
                      child: Text('• $item'),
                    ))
                .toList()),
      ),
      if (!_followUpRoundComplete && guidance.followUpItems.isNotEmpty) ...[
        const SizedBox(height: 10),
        _responsePanel(
          icon: Icons.forum_outlined,
          title: 'A few details could improve this guidance',
          color: AppColors.accent,
          child: Column(children: [
            const Align(
              alignment: Alignment.centerLeft,
              child: Padding(
                padding: EdgeInsets.only(bottom: 12),
                child: Text(
                  'The assistant selected up to 3 relevant questions from what you reported. Reply naturally in your own words.',
                  style: TextStyle(color: AppColors.textSecondaryLight),
                ),
              ),
            ),
            ...guidance.followUpItems.map((question) => Padding(
                  padding: const EdgeInsets.only(bottom: 10),
                  child: Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: .68),
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(
                          color: AppColors.accent.withValues(alpha: .16)),
                    ),
                    child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(_followUpPrompt(question.prompt),
                              style:
                                  const TextStyle(fontWeight: FontWeight.w600)),
                          const SizedBox(height: 10),
                          _buildQuestionInput(question),
                        ]),
                  ),
                )),
            if (!isEmergency)
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  onPressed: _isSubmitting || !_allRequiredAnswersProvided
                      ? null
                      : () => _startTriage(includeFollowUpAnswers: true),
                  icon: const Icon(Icons.update_outlined),
                  label: const Text('Update with my answers'),
                ),
              ),
          ]),
        ),
      ],
      if (guidance.evidenceSource.isNotEmpty) ...[
        const SizedBox(height: 10),
        Text('Source & limitation: ${guidance.evidenceSource}',
            style: const TextStyle(
                fontSize: 11, color: AppColors.textSecondaryLight)),
      ],
    ]);
  }

  Widget _responsePanel({
    required IconData icon,
    required String title,
    required Color color,
    required Widget child,
  }) =>
      Container(
        width: double.infinity,
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: color.withValues(alpha: .065),
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: color.withValues(alpha: .18)),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            Icon(icon, size: 18, color: color),
            const SizedBox(width: 8),
            Expanded(
                child: Text(title,
                    style:
                        TextStyle(fontWeight: FontWeight.w800, color: color))),
          ]),
          const SizedBox(height: 9),
          child,
        ]),
      );

  Widget _buildQuestionInput(TriageFollowUpQuestion question) {
    return TextField(
      controller: _followUpControllers[question.id],
      keyboardType: TextInputType.text,
      textCapitalization: TextCapitalization.sentences,
      maxLines: 2,
      minLines: 1,
      onChanged: (value) =>
          setState(() => _followUpAnswers[question.id] = value),
      decoration: InputDecoration(
        hintText: _followUpHint(question),
        prefixIcon: const Icon(Icons.chat_bubble_outline_rounded, size: 19),
      ),
    );
  }

  String _followUpHint(TriageFollowUpQuestion question) {
    switch (question.type) {
      case 'yesNo':
        return 'For example: Yes, and it is still happening';
      case 'number':
        return question.unit == null
            ? 'Write your answer in your own words'
            : 'For example: About 3 ${question.unit}';
      case 'severityScale':
        final minimum = (question.minimum ?? 0).toStringAsFixed(0);
        final maximum = (question.maximum ?? 10).toStringAsFixed(0);
        return 'Describe it, or give a number from $minimum to $maximum';
      case 'multipleChoice':
        return 'Describe anything that applies, or write “none”';
      default:
        return 'Write your answer in your own words';
    }
  }

  String _followUpPrompt(String prompt) => prompt
      .replaceFirst(
          RegExp(r'^Select every ', caseSensitive: false), 'Describe any ')
      .replaceFirst(
          RegExp(r'^Select any ', caseSensitive: false), 'Describe any ');

  bool get _allRequiredAnswersProvided {
    final questions = _workflow?.guidance?.followUpItems ?? const [];
    return questions.isNotEmpty &&
        questions.every((question) =>
            !question.required ||
            (_followUpAnswers[question.id]?.trim().isNotEmpty ?? false));
  }

  Widget _statusPill(IconData icon, String label) => Container(
        padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 6),
        decoration: BoxDecoration(
          color: AppColors.primary.withValues(alpha: .08),
          borderRadius: BorderRadius.circular(20),
          border: Border.all(color: AppColors.primary.withValues(alpha: .14)),
        ),
        child: Row(mainAxisSize: MainAxisSize.min, children: [
          Icon(icon, size: 14, color: AppColors.primary),
          const SizedBox(width: 5),
          Text(label,
              style: const TextStyle(
                  color: AppColors.primary,
                  fontSize: 11,
                  fontWeight: FontWeight.w600)),
        ]),
      );

  Widget _buildSubmittedReport(TriageWorkflow workflow) => Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: AppColors.primary.withValues(alpha: .06),
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: AppColors.primary.withValues(alpha: .18)),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Row(children: [
            Icon(Icons.receipt_long_outlined,
                color: AppColors.primary, size: 19),
            SizedBox(width: 8),
            Text('Your submitted report',
                style: TextStyle(fontWeight: FontWeight.w800)),
          ]),
          const SizedBox(height: 8),
          Text(
              workflow.patientReportedSymptoms.isEmpty
                  ? _symptomController.text.trim()
                  : workflow.patientReportedSymptoms,
              style: const TextStyle(height: 1.4)),
          const SizedBox(height: 8),
          Text(
              'Saved as assessment #${workflow.workflowId} · ${_patientStatusLabel(workflow)}',
              style: const TextStyle(
                  fontSize: 11, color: AppColors.textSecondaryLight)),
        ]),
      );

  Widget _buildAssessmentHistory() => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('Previous assessments',
              style: TextStyle(fontWeight: FontWeight.w800, fontSize: 16)),
          const SizedBox(height: 4),
          const Text(
              'Your earlier reports are saved. Tap one to expand its latest status here.',
              style:
                  TextStyle(fontSize: 12, color: AppColors.textSecondaryLight)),
          const SizedBox(height: 9),
          ..._historyCards(),
        ],
      );

  List<Widget> _historyCards() {
    final cards = <Widget>[];
    for (final item in _assessmentHistory.take(6)) {
      final expanded = _expandedHistoryWorkflowId == item.workflowId;
      cards.add(Card(
        margin: const EdgeInsets.only(bottom: 8),
        elevation: 0,
        child: ListTile(
          onTap: _isSubmitting
              ? null
              : () async {
                  if (expanded) {
                    setState(() {
                      _expandedHistoryWorkflowId = null;
                      _expandedHistoryWorkflow = null;
                    });
                    return;
                  }
                  setState(() => _isSubmitting = true);
                  try {
                    final workflow =
                        await ApiService.getTriageWorkflow(item.workflowId);
                    if (mounted) {
                      setState(() {
                        _expandedHistoryWorkflowId = item.workflowId;
                        _expandedHistoryWorkflow = workflow;
                      });
                    }
                  } catch (_) {
                    if (mounted) {
                      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
                          content:
                              Text('This assessment could not be opened.')));
                    }
                  } finally {
                    if (mounted) setState(() => _isSubmitting = false);
                  }
                },
          leading: Icon(_historyIcon(item), color: _historyColor(item)),
          title: Text(
              item.patientReportedSymptoms.isEmpty
                  ? 'Assessment #${item.workflowId}'
                  : item.patientReportedSymptoms.split('\n').first,
              maxLines: 1,
              overflow: TextOverflow.ellipsis),
          subtitle: Text(_patientStatusLabel(item)),
          trailing: Icon(expanded ? Icons.expand_less : Icons.expand_more),
        ),
      ));
      if (expanded && _expandedHistoryWorkflow != null) {
        cards.add(_buildExpandedHistoryDetail(_expandedHistoryWorkflow!));
      }
    }
    return cards;
  }

  Widget _buildExpandedHistoryDetail(TriageWorkflow workflow) => Container(
        margin: const EdgeInsets.only(left: 8, right: 8, bottom: 14),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: AppColors.primary.withValues(alpha: .055),
          border: Border.all(color: AppColors.primary.withValues(alpha: .18)),
          borderRadius: BorderRadius.circular(12),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Saved assessment details',
              style: TextStyle(
                  fontWeight: FontWeight.w800, color: AppColors.primary)),
          const SizedBox(height: 8),
          Text(workflow.patientReportedSymptoms,
              style:
                  const TextStyle(fontWeight: FontWeight.w600, height: 1.35)),
          const SizedBox(height: 8),
          Text(_patientStatusLabel(workflow),
              style: TextStyle(
                  color: _historyColor(workflow), fontWeight: FontWeight.w700)),
          const SizedBox(height: 6),
          Text(workflow.patientMessage, style: const TextStyle(height: 1.4)),
          if (workflow.guidance?.actions.isNotEmpty == true) ...[
            const SizedBox(height: 10),
            const Text('Recorded next steps',
                style: TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 4),
            ...workflow.guidance!.actions.take(3).map((action) => Padding(
                  padding: const EdgeInsets.only(bottom: 3),
                  child: Text('• $action'),
                )),
          ],
        ]),
      );

  String _patientStatusLabel(TriageWorkflow workflow) {
    if (workflow.approvalStatus == 'Approved') {
      return 'Reviewed and accepted by a clinician';
    }
    if (workflow.approvalStatus == 'Rejected') {
      return 'Reviewed — recommendation not accepted';
    }
    if (workflow.approvalStatus == 'RevisionRequested') {
      return 'Clinician requested more information';
    }
    if (workflow.requiresHumanReview) {
      return 'Waiting for clinical review';
    }
    if (workflow.status == 'PendingPatientInput') {
      return 'More information needed from you';
    }
    return 'Guidance completed';
  }

  IconData _historyIcon(TriageWorkflow workflow) =>
      workflow.approvalStatus == 'Approved'
          ? Icons.verified_rounded
          : workflow.requiresHumanReview
              ? Icons.person_search_outlined
              : Icons.assignment_turned_in_outlined;

  Color _historyColor(TriageWorkflow workflow) =>
      workflow.approvalStatus == 'Approved'
          ? AppColors.success
          : workflow.requiresHumanReview
              ? AppColors.warning
              : AppColors.primary;

  Widget _buildTechnicalDetails(TriageWorkflow workflow) => Theme(
        data: Theme.of(context).copyWith(dividerColor: Colors.transparent),
        child: ExpansionTile(
          tilePadding: EdgeInsets.zero,
          childrenPadding: const EdgeInsets.only(bottom: 4),
          leading: const Icon(Icons.shield_outlined,
              color: AppColors.primary, size: 20),
          title: const Text('Technical safety details',
              style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700)),
          subtitle: const Text('Workflow and audit information',
              style: TextStyle(fontSize: 11)),
          children: [
            _technicalHeading('Assessment status'),
            _technicalItem('Workflow status', workflow.status),
            _technicalItem('Clinical review', workflow.approvalStatus),
            _technicalItem('Information state', workflow.uncertaintyState),
            const SizedBox(height: 7),
            _technicalHeading('Safety signals'),
            if (workflow.redFlags.isNotEmpty)
              _technicalList('Emergency red flags', workflow.redFlags),
            if (workflow.urgentFlags.isNotEmpty)
              _technicalList('Urgent warning signs', workflow.urgentFlags),
            if (workflow.clinicalReviewFlags.isNotEmpty)
              _technicalList(
                  'Serious or high-risk context', workflow.clinicalReviewFlags),
            if (workflow.missingInformation.isNotEmpty)
              _technicalList(
                  'Information limitations', workflow.missingInformation),
            const SizedBox(height: 7),
            _technicalHeading('Decision audit'),
            if (workflow.decisionBasis.isNotEmpty)
              _technicalList(
                  'Why this result was produced', workflow.decisionBasis),
            if (workflow.plan.isNotEmpty) ...[
              const SizedBox(height: 8),
              const Align(
                  alignment: Alignment.centerLeft,
                  child: Text('Workflow steps',
                      style: TextStyle(
                          fontSize: 12, fontWeight: FontWeight.w700))),
              const SizedBox(height: 6),
              ...workflow.plan.map((step) => Padding(
                    padding: const EdgeInsets.only(bottom: 6),
                    child: Row(children: [
                      const Icon(Icons.account_tree_outlined,
                          size: 15, color: AppColors.primary),
                      const SizedBox(width: 7),
                      Expanded(
                          child: Text(step.agent,
                              style: const TextStyle(
                                  fontSize: 12, fontWeight: FontWeight.w600))),
                      Text(step.status,
                          style: const TextStyle(
                              fontSize: 11,
                              color: AppColors.textSecondaryLight)),
                    ]),
                  )),
            ],
          ],
        ),
      );

  Widget _technicalItem(String label, String value) => Padding(
        padding: const EdgeInsets.only(bottom: 5),
        child: Row(children: [
          Expanded(
              child: Text(label,
                  style: const TextStyle(
                      fontSize: 12, color: AppColors.textSecondaryLight))),
          Text(value,
              style:
                  const TextStyle(fontSize: 12, fontWeight: FontWeight.w600)),
        ]),
      );

  Widget _technicalHeading(String label) => Padding(
        padding: const EdgeInsets.only(bottom: 5),
        child: Text(label.toUpperCase(),
            style: const TextStyle(
                fontSize: 10,
                fontWeight: FontWeight.w800,
                color: AppColors.primary)),
      );

  Widget _technicalList(String title, List<String> items) => Padding(
        padding: const EdgeInsets.only(top: 6),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(title,
              style:
                  const TextStyle(fontSize: 12, fontWeight: FontWeight.w700)),
          const SizedBox(height: 3),
          ...items.map(
              (item) => Text('• $item', style: const TextStyle(fontSize: 12))),
        ]),
      );
}
