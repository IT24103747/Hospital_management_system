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
  bool _isSubmitting = false;
  int _activeStage = 0;
  Timer? _progressTimer;
  TriageWorkflow? _workflow;
  List<bool?> _followUpAnswers = [];
  bool _followUpRoundComplete = false;

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
    if (_symptomController.text.trim().length < 3) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('Please describe your symptoms before continuing.')));
      return;
    }
    final symptoms = _symptomsForSubmission(includeFollowUpAnswers);
    if (symptoms == null) return;
    setState(() {
      if (!includeFollowUpAnswers) _followUpRoundComplete = false;
      _isSubmitting = true;
      _activeStage = 0;
    });
    _progressTimer?.cancel();
    _progressTimer = Timer.periodic(const Duration(milliseconds: 900), (_) {
      if (mounted && _activeStage < _workflowStages.length - 1) {
        setState(() => _activeStage++);
      }
    });
    try {
      final workflow = includeFollowUpAnswers
          ? await ApiService.continueTriageWorkflow(
              workflowId: _workflow!.workflowId, answers: symptoms)
          : await ApiService.startTriageWorkflow(
              symptoms: symptoms, vitals: _optionalVitals());
      if (mounted) {
        _followUpRoundComplete = includeFollowUpAnswers;
        _setWorkflow(workflow);
      }
    } catch (error) {
      if (mounted)
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
            content: Text(
                'SafeTriage could not safely process this request: $error'),
            backgroundColor: AppColors.danger));
    } finally {
      _progressTimer?.cancel();
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  String? _symptomsForSubmission(bool includeFollowUpAnswers) {
    if (!includeFollowUpAnswers) return _symptomController.text.trim();
    final guidance = _workflow?.guidance;
    if (guidance == null) return _symptomController.text.trim();
    final answered = <String>[];
    for (var index = 0; index < _followUpAnswers.length; index++) {
      final answer = _followUpAnswers[index];
      if (answer != null) {
        answered.add(
            '${guidance.followUpQuestions[index]} Answer: ${answer ? 'Yes' : 'No'}');
      }
    }
    if (answered.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text(
              'Answer at least one follow-up question before updating the assessment.')));
      return null;
    }
    return answered.join('\n');
  }

  void _setWorkflow(TriageWorkflow workflow) {
    _followUpAnswers = List<bool?>.filled(
        workflow.guidance?.followUpQuestions.length ?? 0, null);
    setState(() => _workflow = workflow);
  }

  Map<String, dynamic>? _optionalVitals() {
    final heartRateText = _heartRateController.text.trim();
    final temperatureText = _temperatureController.text.trim();
    if (heartRateText.isEmpty && temperatureText.isEmpty) return null;

    final vitals = <String, dynamic>{'source': 'patient-reported'};
    if (heartRateText.isNotEmpty) {
      final heartRate = int.tryParse(heartRateText);
      if (heartRate == null)
        throw const FormatException('Heart rate must be a whole number.');
      vitals['heartRateBpm'] = heartRate;
    }
    if (temperatureText.isNotEmpty) {
      final temperature = double.tryParse(temperatureText);
      if (temperature == null)
        throw const FormatException('Temperature must be a valid number.');
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
      if (mounted)
        ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(content: Text('Unable to refresh status: $error')));
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
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isEmergency = _workflow?.triageLevel == 'Emergency';
    final isUrgent = _workflow?.triageLevel == 'Urgent';
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
          _buildLiveWorkflowProgress(),
        ],
        if (_workflow != null) ...[
          const SizedBox(height: 24),
          Card(
              margin: EdgeInsets.zero,
              elevation: 0,
              shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(20),
                  side: BorderSide(
                      color: isEmergency || isUrgent
                          ? AppColors.danger.withValues(alpha: .35)
                          : AppColors.primary.withValues(alpha: .20))),
              child: Padding(
                  padding: const EdgeInsets.all(18),
                  child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(children: [
                          Container(
                            padding: const EdgeInsets.all(10),
                            decoration: BoxDecoration(
                                color: (isEmergency || isUrgent
                                        ? AppColors.danger
                                        : AppColors.primary)
                                    .withValues(alpha: .12),
                                borderRadius: BorderRadius.circular(12)),
                            child: Icon(
                                isEmergency || isUrgent
                                    ? Icons.warning_amber_rounded
                                    : Icons.verified_user_outlined,
                                color: isEmergency || isUrgent
                                    ? AppColors.danger
                                    : AppColors.primary),
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
                                Text(_workflow!.triageLevel,
                                    style: TextStyle(
                                        fontSize: 20,
                                        fontWeight: FontWeight.w800,
                                        color: isEmergency || isUrgent
                                            ? AppColors.danger
                                            : AppColors.primary)),
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
      if (!_followUpRoundComplete && guidance.followUpQuestions.isNotEmpty) ...[
        const SizedBox(height: 10),
        _responsePanel(
          icon: Icons.forum_outlined,
          title: 'A few details could improve this guidance',
          color: AppColors.accent,
          child: Column(children: [
            ...List.generate(
                guidance.followUpQuestions.length,
                (index) => Padding(
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
                              Text(guidance.followUpQuestions[index],
                                  style: const TextStyle(
                                      fontWeight: FontWeight.w600)),
                              const SizedBox(height: 10),
                              Row(children: [
                                _answerButton(index, true),
                                const SizedBox(width: 8),
                                _answerButton(index, false),
                              ]),
                            ]),
                      ),
                    )),
            if (!isEmergency)
              SizedBox(
                width: double.infinity,
                child: OutlinedButton.icon(
                  onPressed: _isSubmitting || !_hasFollowUpAnswer
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

  Widget _answerButton(int index, bool answer) {
    final isSelected =
        _followUpAnswers.length > index && _followUpAnswers[index] == answer;
    final color = answer ? AppColors.success : AppColors.textSecondaryLight;
    return Expanded(
      child: ChoiceChip(
        selected: isSelected,
        onSelected: (_) => setState(() => _followUpAnswers[index] = answer),
        showCheckmark: false,
        avatar: Icon(answer ? Icons.check_rounded : Icons.close_rounded,
            size: 17, color: isSelected ? Colors.white : color),
        label: Center(child: Text(answer ? 'Yes' : 'No')),
        labelStyle: TextStyle(
            color: isSelected ? Colors.white : color,
            fontWeight: FontWeight.w700),
        selectedColor: color,
        backgroundColor: Colors.transparent,
        side: BorderSide(color: color.withValues(alpha: isSelected ? 1 : .45)),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(9)),
      ),
    );
  }

  bool get _hasFollowUpAnswer =>
      _followUpAnswers.any((answer) => answer != null);

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
            _technicalItem('Workflow status', workflow.status),
            _technicalItem('Clinical review', workflow.approvalStatus),
            _technicalItem('Information state', workflow.uncertaintyState),
            if (workflow.redFlags.isNotEmpty)
              _technicalList('Configured safety flags', workflow.redFlags),
            if (workflow.missingInformation.isNotEmpty)
              _technicalList(
                  'Information limitations', workflow.missingInformation),
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
