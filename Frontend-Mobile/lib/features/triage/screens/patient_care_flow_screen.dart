import 'dart:async';

import 'package:flutter/material.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';

/// Patient-facing four-agent clinical safety and appointment workflow.
class PatientCareFlowScreen extends StatefulWidget {
  const PatientCareFlowScreen({super.key});

  @override
  State<PatientCareFlowScreen> createState() => _PatientCareFlowScreenState();
}

class _PatientCareFlowScreenState extends State<PatientCareFlowScreen> {
  final _symptoms = TextEditingController();
  final _specialty = TextEditingController(text: 'Cardiology');
  Map<String, dynamic>? _result;
  bool _loading = false;
  bool _findAppointment = false;
  List<Map<String, dynamic>> _history = [];
  int? _expandedAssessmentId;
  int _activeStage = 0;
  Timer? _progressTimer;

  static const _stages = [
    'Clinical Safety Agent checks input and warning signs',
    'Gemini extracts grounded non-diagnostic facts',
    'Appointment Proposal Agent finds verified options',
    'Safety Validation Agent protects confirmation',
  ];

  @override
  void initState() {
    super.initState();
    _loadHistory();
  }

  Future<void> _loadHistory() async {
    try {
      final history = await ApiService.getPatientCareHistory();
      if (mounted) setState(() => _history = history);
    } catch (_) {
      // A first-time user has no history yet; the current assessment still works.
    }
  }

  @override
  void dispose() {
    _progressTimer?.cancel();
    _symptoms.dispose();
    _specialty.dispose();
    super.dispose();
  }

  Future<void> _runWorkflow() async {
    if (_symptoms.text.trim().length < 3 ||
        (_findAppointment && _specialty.text.trim().length < 2)) {
      _show(_findAppointment
          ? 'Enter symptoms and a specialty.'
          : 'Enter a short symptom description.');
      return;
    }
    setState(() {
      _loading = true;
      _activeStage = 0;
    });
    _progressTimer?.cancel();
    _progressTimer = Timer.periodic(const Duration(milliseconds: 900), (_) {
      if (mounted && _activeStage < _stages.length - 1) {
        setState(() => _activeStage++);
      }
    });
    try {
      final result = await ApiService.createTriageAppointmentProposal(
        symptoms: _symptoms.text,
        specialty: _findAppointment ? _specialty.text : null,
        requestAppointmentProposal: _findAppointment,
      );
      if (mounted) setState(() => _result = result);
      await _loadHistory();
    } catch (error) {
      _show('Workflow could not be completed: $error');
    } finally {
      _progressTimer?.cancel();
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _confirmSlot(int proposalId, int slotId) async {
    setState(() => _loading = true);
    try {
      final result = await ApiService.confirmAppointmentProposal(
        proposalId: proposalId,
        doctorTimeSlotId: slotId,
      );
      if (mounted) {
        _show(result['message']?.toString() ?? 'Your selection was processed.');
        setState(() => _result = {
              ...?_result,
              'confirmation': result,
            });
      }
    } catch (error) {
      _show('Unable to confirm this slot: $error');
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _show(String message) => ScaffoldMessenger.of(context)
      .showSnackBar(SnackBar(content: Text(message)));

  @override
  Widget build(BuildContext context) {
    final clinical = _result?['clinical'] as Map<String, dynamic>?;
    final proposal = _result?['proposal'] as Map<String, dynamic>?;
    final slots = proposal?['slots'] as List<dynamic>? ?? const [];
    final proposalId = proposal?['proposalId'] as int?;
    final confirmation = _result?['confirmation'] as Map<String, dynamic>?;
    final extracted = clinical?['extractedFacts'] as Map<String, dynamic>?;
    final guidance = extracted?['guidance'] as Map<String, dynamic>?;

    return Scaffold(
      appBar: AppBar(title: const Text('Patient Care Assistant')),
      body: ListView(
          padding: const EdgeInsets.fromLTRB(20, 16, 20, 28),
          children: [
            _hero(),
            const SizedBox(height: 20),
            _sectionTitle(Icons.edit_note_outlined, 'Tell us how you feel',
                'The assistant provides guidance, not a diagnosis.'),
            const SizedBox(height: 10),
            _inputCard(),
            if (_loading) ...[
              const SizedBox(height: 18),
              _workflowProgress(),
            ],
            if (clinical != null) ...[
              const SizedBox(height: 22),
              _panel(
                  'Clinical Safety assessment',
                  [
                    'Triage: ${clinical['triageLevel']}',
                    'Route: ${clinical['proposedRoute']}',
                    if (clinical['requiresClinicalReview'] == true)
                      'Clinical review is required.',
                  ],
                  clinical['triageLevel'] == 'Urgent' ||
                          clinical['triageLevel'] == 'Emergency'
                      ? AppColors.danger
                      : AppColors.primary),
            ],
            if (guidance != null) ...[
              const SizedBox(height: 14),
              _panel(
                  'Your safe guidance',
                  [
                    guidance['summary']?.toString() ?? '',
                    ...(guidance['generalActions'] as List<dynamic>? ??
                            const [])
                        .map((item) => '• $item'),
                    ...(guidance['safetyNetting'] as List<dynamic>? ?? const [])
                        .map((item) => 'Safety: $item'),
                  ],
                  AppColors.primary),
            ],
            if (proposal == null && clinical != null) ...[
              const SizedBox(height: 14),
              Text(_findAppointment
                  ? 'No normal appointment options are shown because the safety route requires urgent care.'
                  : 'Guidance-only request completed. Turn on “Find appointment options” if you also want to search for a slot.'),
            ],
            if (proposal != null) ...[
              const SizedBox(height: 14),
              _panel(
                  'Verified appointment options',
                  [
                    proposal['message']?.toString() ?? '',
                    'Status: ${proposal['status']}',
                  ],
                  AppColors.accent),
              const SizedBox(height: 10),
              if (slots.isEmpty)
                Text(
                    (proposal['suggestedActions'] as List<dynamic>? ?? const [])
                        .join('\n'))
              else ...[
                const Text('Select one verified appointment option:',
                    style: TextStyle(fontWeight: FontWeight.w700)),
                const SizedBox(height: 8),
                ...slots.map((value) {
                  final slot = value as Map<String, dynamic>;
                  final slotId = slot['doctorTimeSlotId'] as int;
                  return Card(
                    elevation: 0,
                    margin: const EdgeInsets.only(bottom: 10),
                    shape: RoundedRectangleBorder(
                        borderRadius: BorderRadius.circular(16),
                        side: BorderSide(
                            color: AppColors.accent.withValues(alpha: .22))),
                    child: ListTile(
                      contentPadding: const EdgeInsets.fromLTRB(16, 10, 12, 10),
                      leading: Container(
                        padding: const EdgeInsets.all(9),
                        decoration: BoxDecoration(
                            color: AppColors.accent.withValues(alpha: .1),
                            borderRadius: BorderRadius.circular(12)),
                        child: const Icon(Icons.event_available_outlined,
                            color: AppColors.accent),
                      ),
                      title:
                          Text('${slot['doctorName']} · ${slot['specialty']}'),
                      subtitle: Text(
                          '${slot['startAt']}\n${slot['location']} · LKR ${slot['consultationFee']}'),
                      isThreeLine: true,
                      trailing: FilledButton(
                        onPressed: _loading || proposalId == null
                            ? null
                            : () => _confirmSlot(proposalId, slotId),
                        child: const Text('Choose'),
                      ),
                    ),
                  );
                }),
              ],
            ],
            if (confirmation != null) ...[
              const SizedBox(height: 14),
              _panel(
                  'Booking safety status',
                  [
                    'Status: ${confirmation['status']}',
                    confirmation['message']?.toString() ?? '',
                  ],
                  AppColors.primary),
            ],
            if (_history.isNotEmpty) ...[
              const SizedBox(height: 26),
              _sectionTitle(Icons.history_rounded, 'Previous assessments',
                  'Your recent Patient Care workflow results.'),
              const SizedBox(height: 8),
              ..._history.map(_historyCard),
            ],
          ]),
    );
  }

  Widget _hero() => Container(
        padding: const EdgeInsets.all(20),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(22),
          gradient: const LinearGradient(
            colors: [AppColors.primaryDark, AppColors.accent],
            begin: Alignment.topLeft,
            end: Alignment.bottomRight,
          ),
          boxShadow: [
            BoxShadow(
                color: AppColors.primary.withValues(alpha: .25),
                blurRadius: 22,
                offset: const Offset(0, 10))
          ],
        ),
        child: const Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(children: [
                CircleAvatar(
                    backgroundColor: Color(0x26FFFFFF),
                    child: Icon(Icons.health_and_safety_outlined,
                        color: Colors.white)),
                SizedBox(width: 11),
                Expanded(
                    child: Text('Patient Care AI',
                        style: TextStyle(
                            color: Colors.white,
                            fontSize: 21,
                            fontWeight: FontWeight.w800))),
              ]),
              SizedBox(height: 14),
              Text('Safe symptom guidance with verified appointment support.',
                  style: TextStyle(color: Color(0xE6FFFFFF), height: 1.4)),
              SizedBox(height: 10),
              Text(
                  'Clinical Safety  •  Appointment Proposal  •  Safety Approval',
                  style: TextStyle(
                      color: Color(0xCCFFFFFF),
                      fontSize: 11,
                      fontWeight: FontWeight.w600)),
            ]),
      );

  Widget _sectionTitle(IconData icon, String title, String subtitle) =>
      Row(children: [
        Container(
          padding: const EdgeInsets.all(9),
          decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: .1),
              borderRadius: BorderRadius.circular(11)),
          child: Icon(icon, color: AppColors.primary, size: 20),
        ),
        const SizedBox(width: 10),
        Expanded(
            child:
                Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Text(title,
              style:
                  const TextStyle(fontSize: 17, fontWeight: FontWeight.w800)),
          const SizedBox(height: 2),
          Text(subtitle,
              style: const TextStyle(
                  fontSize: 12, color: AppColors.textSecondaryLight)),
        ])),
      ]);

  Widget _inputCard() => Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: AppColors.bgLightCard,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(color: AppColors.borderLight),
        ),
        child: Column(children: [
          TextField(
            controller: _symptoms,
            maxLines: 4,
            textCapitalization: TextCapitalization.sentences,
            decoration: const InputDecoration(
              prefixIcon: Padding(
                  padding: EdgeInsets.only(bottom: 58),
                  child: Icon(Icons.edit_note_outlined)),
              labelText: 'Symptoms or concern',
              hintText: 'Example: I have had a cough for two days.',
            ),
          ),
          const SizedBox(height: 8),
          SwitchListTile.adaptive(
            contentPadding: EdgeInsets.zero,
            activeThumbColor: AppColors.accent,
            title: const Text('Find appointment options',
                style: TextStyle(fontWeight: FontWeight.w700)),
            subtitle: const Text('Only when the safety route permits it.',
                style: TextStyle(fontSize: 12)),
            value: _findAppointment,
            onChanged: _loading
                ? null
                : (value) => setState(() => _findAppointment = value),
          ),
          if (_findAppointment) ...[
            const SizedBox(height: 4),
            TextField(
              controller: _specialty,
              decoration: const InputDecoration(
                  prefixIcon: Icon(Icons.medical_services_outlined),
                  labelText: 'Requested specialty',
                  hintText: 'Example: Cardiology'),
            ),
          ],
          const SizedBox(height: 12),
          SizedBox(
            width: double.infinity,
            height: 50,
            child: ElevatedButton.icon(
              onPressed: _loading ? null : _runWorkflow,
              icon: Icon(_findAppointment
                  ? Icons.calendar_month_outlined
                  : Icons.health_and_safety_outlined),
              label: Text(_loading
                  ? 'Processing safely…'
                  : _findAppointment
                      ? 'Get guidance and appointment options'
                      : 'Get safe guidance'),
            ),
          ),
          const SizedBox(height: 10),
          const Text(
              'Not for emergencies. Severe symptoms require immediate medical care.',
              textAlign: TextAlign.center,
              style: TextStyle(fontSize: 11, color: AppColors.textMutedLight)),
        ]),
      );

  Widget _workflowProgress() => Container(
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
            color: AppColors.primary.withValues(alpha: .06),
            borderRadius: BorderRadius.circular(16),
            border: Border.all(color: AppColors.primary.withValues(alpha: .2))),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          const Text('Patient Care workflow in progress',
              style: TextStyle(fontWeight: FontWeight.w800)),
          const SizedBox(height: 14),
          ..._stages.asMap().entries.map((entry) {
            final isCurrent = entry.key == _activeStage;
            final isDone = entry.key < _activeStage;
            final color = isCurrent || isDone
                ? AppColors.primary
                : AppColors.textSecondaryLight;
            return Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child:
                  Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
                Icon(
                  isDone
                      ? Icons.check_circle
                      : isCurrent
                          ? Icons.sync
                          : Icons.radio_button_unchecked,
                  color: color,
                  size: 22,
                ),
                const SizedBox(width: 10),
                Expanded(
                    child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                      Text(entry.value,
                          style: TextStyle(
                              height: 1.3,
                              color: color,
                              fontWeight: isCurrent
                                  ? FontWeight.bold
                                  : FontWeight.w500)),
                      if (isCurrent)
                        const Text('Working on this stage now…',
                            style: TextStyle(
                                fontSize: 12,
                                color: AppColors.textSecondaryLight)),
                    ])),
              ]),
            );
          }),
        ]),
      );

  Widget _panel(String title, List<String> lines, Color color) => Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: color.withValues(alpha: .08),
          borderRadius: BorderRadius.circular(14),
          border: Border.all(color: color.withValues(alpha: .3)),
        ),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Row(children: [
            Icon(Icons.verified_outlined, size: 18, color: color),
            const SizedBox(width: 8),
            Expanded(
                child: Text(title,
                    style:
                        TextStyle(fontWeight: FontWeight.w800, color: color))),
          ]),
          const SizedBox(height: 6),
          ...lines.where((line) => line.isNotEmpty).map((line) => Padding(
              padding: const EdgeInsets.only(bottom: 3), child: Text(line))),
        ]),
      );

  Widget _historyCard(Map<String, dynamic> assessment) {
    final id = assessment['assessmentId'] as int;
    final expanded = _expandedAssessmentId == id;
    final clinical = assessment['clinical'] as Map<String, dynamic>?;
    final created =
        assessment['createdAt']?.toString().replaceFirst('T', ' ') ?? '';
    final level = assessment['triageLevel']?.toString() ?? 'Assessment';
    final color = level == 'Emergency' || level == 'Urgent'
        ? AppColors.danger
        : level == 'ClinicalReview'
            ? AppColors.warning
            : AppColors.success;
    final extracted = clinical?['extractedFacts'] as Map<String, dynamic>?;
    final guidance = extracted?['guidance'] as Map<String, dynamic>?;
    final actions = guidance?['generalActions'] as List<dynamic>? ?? const [];
    return Column(children: [
      Card(
        elevation: 0,
        margin: const EdgeInsets.only(bottom: 8),
        shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(16),
            side: BorderSide(color: color.withValues(alpha: .25))),
        child: ListTile(
          onTap: _loading
              ? null
              : () =>
                  setState(() => _expandedAssessmentId = expanded ? null : id),
          leading: Container(
            padding: const EdgeInsets.all(8),
            decoration: BoxDecoration(
                color: color.withValues(alpha: .1),
                borderRadius: BorderRadius.circular(10)),
            child: Icon(
                level == 'ClinicalReview'
                    ? Icons.person_search_outlined
                    : Icons.assignment_turned_in_outlined,
                color: color),
          ),
          title: Text(
              assessment['symptoms']?.toString().split('\n').first ??
                  'Assessment #$id',
              maxLines: 1,
              overflow: TextOverflow.ellipsis),
          subtitle: Text('$level · ${assessment['status'] ?? 'Completed'}'),
          trailing: Icon(expanded ? Icons.expand_less : Icons.expand_more),
        ),
      ),
      if (expanded)
        Container(
          width: double.infinity,
          margin: const EdgeInsets.fromLTRB(8, 0, 8, 14),
          padding: const EdgeInsets.all(14),
          decoration: BoxDecoration(
              color: color.withValues(alpha: .055),
              border: Border.all(color: color.withValues(alpha: .18)),
              borderRadius: BorderRadius.circular(12)),
          child:
              Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text('Saved assessment details',
                style: TextStyle(fontWeight: FontWeight.w800, color: color)),
            const SizedBox(height: 8),
            Text(assessment['symptoms']?.toString() ?? '',
                style:
                    const TextStyle(fontWeight: FontWeight.w600, height: 1.35)),
            const SizedBox(height: 8),
            Text(clinical?['proposedRoute']?.toString() ?? '',
                style: TextStyle(color: color, fontWeight: FontWeight.w700)),
            const SizedBox(height: 6),
            Text(guidance?['summary']?.toString() ?? 'Workflow #$id · $created',
                style: const TextStyle(height: 1.4)),
            if (actions.isNotEmpty) ...[
              const SizedBox(height: 10),
              const Text('Recorded next steps',
                  style: TextStyle(fontWeight: FontWeight.w700)),
              const SizedBox(height: 4),
              ...actions.take(3).map((item) => Padding(
                  padding: const EdgeInsets.only(bottom: 3),
                  child: Text('• $item'))),
            ],
          ]),
        ),
    ]);
  }
}
