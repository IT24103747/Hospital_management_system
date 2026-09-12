import 'dart:math';

import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
import 'package:smartcare_mobile/models/hospital_assistant.dart';

/// One patient conversation; routing, safety and approval authority stay on the server.
class HospitalAssistantScreen extends StatefulWidget {
  final bool embedded;
  const HospitalAssistantScreen({super.key, this.embedded = false});

  @override
  State<HospitalAssistantScreen> createState() =>
      _HospitalAssistantScreenState();
}

class _HospitalAssistantScreenState extends State<HospitalAssistantScreen> {
  final _input = TextEditingController();
  final _scroll = ScrollController();
  final _focus = FocusNode();
  AssistantConversation? _conversation;
  List<AssistantCapability> _capabilities = [];
  bool _busy = false;
  bool _initializing = true;
  bool _uncertainAction = false;
  String? _error;
  String? _outgoing;
  String? _retryMessage;
  String? _retryRequestId;
  int? _selectedSlotId;
  int? _selectedAppointmentId;

  @override
  void initState() {
    super.initState();
    _initialize();
  }

  @override
  void dispose() {
    _input.dispose();
    _scroll.dispose();
    _focus.dispose();
    super.dispose();
  }

  // A retry of the same message reuses this key; approval requests are never auto-retried.
  String _requestId() {
    final random = Random.secure();
    final bytes = List<int>.generate(16, (_) => random.nextInt(256));
    bytes[6] = (bytes[6] & 0x0f) | 0x40;
    bytes[8] = (bytes[8] & 0x3f) | 0x80;
    final hex = bytes.map((b) => b.toRadixString(16).padLeft(2, '0')).join();
    return '${hex.substring(0, 8)}-${hex.substring(8, 12)}-'
        '${hex.substring(12, 16)}-${hex.substring(16, 20)}-${hex.substring(20)}';
  }

  Future<void> _initialize() async {
    setState(() {
      _initializing = true;
      _error = null;
    });
    try {
      final capabilities = await ApiService.getAssistantCapabilities();
      if (!mounted) return;
      setState(() => _capabilities = capabilities);
      // Do not automatically reopen the latest conversation. It may contain a
      // previous urgent safety state, and a patient opening the assistant should
      // begin with a genuinely new conversation unless they explicitly choose one
      // from the History button.
    } catch (_) {
      if (mounted) {
        setState(() => _error =
            'Could not load the assistant. Check your connection and try again.');
      }
    } finally {
      if (mounted) setState(() => _initializing = false);
    }
  }

  void _accept(AssistantConversation conversation) {
    final changedAction =
        _conversation?.pendingAction?.id != conversation.pendingAction?.id;
    setState(() {
      _conversation = conversation;
      _capabilities = conversation.capabilities;
      _uncertainAction = false;
      if (changedAction) {
        _selectedSlotId = null;
        _selectedAppointmentId = null;
      }
      final action = conversation.pendingAction;
      if (action?.type == 'book') {
        if (action!.slots.length == 1) {
          _selectedSlotId =
              (action.slots.single['doctorTimeSlotId'] as num).toInt();
        } else if (!action.slots.any(
            (slot) => slot['doctorTimeSlotId'] == _selectedSlotId)) {
          _selectedSlotId = null;
        }
      }
    });
    _scrollToLatest();
  }

  void _scrollToLatest() => WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted && _scroll.hasClients) {
          _scroll.animateTo(_scroll.position.maxScrollExtent,
              duration: const Duration(milliseconds: 250),
              curve: Curves.easeOut);
        }
      });

  Future<void> _send() async {
    final text = _input.text.trim();
    if (_busy || _initializing || _uncertainAction || text.isEmpty) return;
    if (text.length > 4000) {
      setState(
          () => _error = 'Please keep your message under 4,000 characters.');
      return;
    }
    final requestId = _retryMessage == text && _retryRequestId != null
        ? _retryRequestId!
        : _requestId();
    setState(() {
      _busy = true;
      _error = null;
      _outgoing = text;
    });
    _input.clear();
    _focus.unfocus();
    _scrollToLatest();
    try {
      final result = await ApiService.sendAssistantMessage(
          conversationId: _conversation?.id,
          message: text,
          requestId: requestId);
      if (!mounted) return;
      _accept(result);
      _retryMessage = null;
      _retryRequestId = null;
    } catch (_) {
      if (!mounted) return;
      _input.text = text;
      _retryMessage = text;
      _retryRequestId = requestId;
      setState(() => _error =
          'Your message could not be completed. Try sending it again, or refresh the conversation.');
    } finally {
      if (mounted) {
        setState(() {
          _busy = false;
          _outgoing = null;
        });
      }
    }
  }

  Future<void> _decide(String decision) async {
    final conversation = _conversation;
    final action = conversation?.pendingAction;
    if (_busy || _uncertainAction || conversation == null || action == null) {
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final result = await ApiService.decideAssistantAction(
        conversationId: conversation.id,
        actionId: action.id,
        decision: decision,
        requestId: _requestId(),
        doctorTimeSlotId: _selectedSlotId,
        appointmentId: _selectedAppointmentId,
      );
      if (!mounted) return;
      _accept(result);
      if (decision == 'chooseAnother') _focus.requestFocus();
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _uncertainAction = true;
        _error = 'The result could not be verified. Refresh this conversation '
            'before trying again to avoid submitting the action twice.';
      });
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _refresh([String? conversationId]) async {
    final id = conversationId ?? _conversation?.id;
    if (_busy || id == null) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final result = await ApiService.getAssistantConversation(id);
      if (mounted) _accept(result);
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Could not refresh. Please try again.');
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _history() async {
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final history = await ApiService.getAssistantHistory();
      if (!mounted) return;
      setState(() => _busy = false);
      final id = await showModalBottomSheet<String>(
        context: context,
        showDragHandle: true,
        builder: (context) => SafeArea(
          child: ListView(
            shrinkWrap: true,
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
            children: [
              Text('Conversation history',
                  style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: 12),
              ListTile(
                leading: const Icon(Icons.health_and_safety_outlined),
                title: const Text('Earlier assessments'),
                subtitle: const Text('View saved patient guidance'),
                onTap: () => Navigator.pop(context, 'earlier-assessments'),
              ),
              if (history.isEmpty)
                const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('No conversations yet.')),
              ...history.map((item) => ListTile(
                    leading: const Icon(Icons.chat_bubble_outline),
                    title: Text(item['title']?.toString() ?? 'Conversation',
                        maxLines: 2),
                    subtitle: Text(_date(item['updatedAt'])),
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => Navigator.pop(
                        context, item['conversationId'] as String),
                  )),
            ],
          ),
        ),
      );
      if (id != null && mounted) {
        if (id == 'earlier-assessments') {
          await _earlierAssessments();
        } else {
          await _refresh(id);
        }
      }
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Could not load conversation history.');
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _earlierAssessments() async {
    setState(() => _busy = true);
    try {
      final workflows = await ApiService.getTriageWorkflowHistory();
      final care = await ApiService.getPatientCareHistory();
      if (!mounted) return;
      final entries = <({String title, String text})>[
        ...workflows.map((workflow) => (
          title: '${_date(workflow.createdAt?.toIso8601String())} · ${workflow.triageLevel}',
          text: [workflow.patientReportedSymptoms, workflow.patientMessage,
            workflow.guidance?.summary ?? '', 'Review: ${workflow.approvalStatus}']
              .where((part) => part.isNotEmpty).join('\n\n'),
        )),
        ...care.map((assessment) {
          final clinical = assessment['clinical'] as Map?;
          final extracted = clinical?['extractedFacts'] as Map?;
          final guidance = extracted?['guidance'] as Map?;
          return (
            title: '${_date(assessment['createdAt'])} · ${assessment['triageLevel'] ?? 'Saved assessment'}',
            text: [assessment['symptoms'], guidance?['summary'], clinical?['proposedRoute']]
                .where((part) => part != null && part.toString().isNotEmpty).join('\n\n'),
          );
        }),
      ];
      await showModalBottomSheet<void>(
        context: context,
        showDragHandle: true,
        builder: (context) => SafeArea(child: ListView(
          padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
          children: [
            Text('Earlier assessments', style: Theme.of(context).textTheme.titleLarge),
            const SizedBox(height: 12),
            const Text('Saved guidance reflects the information provided at that time.'),
            if (entries.isEmpty) const Padding(
              padding: EdgeInsets.all(16), child: Text('No earlier assessments.')),
            ...entries.map((entry) => ExpansionTile(
              title: Text(entry.title),
              children: [Padding(padding: const EdgeInsets.all(12), child: SelectableText(entry.text))],
            )),
          ],
        )),
      );
    } catch (_) {
      if (mounted) {
        setState(() => _error = 'Could not load earlier assessments. Please try again.');
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _newConversation() {
    setState(() {
      _conversation = null;
      _error = null;
      _selectedSlotId = null;
      _selectedAppointmentId = null;
      _retryMessage = null;
      _retryRequestId = null;
    });
    _input.clear();
    _focus.requestFocus();
  }

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final disabled = _busy || _initializing;
    final body = Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 860),
        child: Column(children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 8, 4),
            child: Row(children: [
              Expanded(
                  child: Text('How can I help?',
                      style: Theme.of(context)
                          .textTheme
                          .titleMedium
                          ?.copyWith(fontWeight: FontWeight.w700))),
              IconButton(
                  tooltip: 'Conversation history',
                  onPressed: disabled ? null : _history,
                  icon: const Icon(Icons.history)),
              IconButton(
                  tooltip: 'Refresh conversation',
                  onPressed: disabled || _conversation == null
                      ? null
                      : () => _refresh(),
                  icon: const Icon(Icons.refresh)),
              IconButton(
                  tooltip: 'New conversation',
                  onPressed: disabled ||
                          _uncertainAction ||
                          _conversation?.pendingAction != null
                      ? null
                      : _newConversation,
                  icon: const Icon(Icons.add_comment_outlined)),
            ]),
          ),
          if (_capabilities.isNotEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Align(
                  alignment: Alignment.centerLeft,
                  child: Wrap(
                    spacing: 8,
                    runSpacing: 0,
                    children: _capabilities
                        .map((capability) => ActionChip(
                              label: Text(capability.enabled
                                  ? capability.label
                                  : '${capability.label} · Coming soon'),
                              onPressed: !capability.enabled ||
                                      disabled ||
                                      _uncertainAction
                                  ? null
                                  : () {
                                      _input.text = capability.prompt;
                                      _focus.requestFocus();
                                    },
                            ))
                        .toList(),
                  )),
            ),
          if (_error != null)
            Padding(
              padding: const EdgeInsets.all(12),
              child: Material(
                color: colors.errorContainer,
                borderRadius: BorderRadius.circular(12),
                child: Padding(
                    padding: const EdgeInsets.all(12),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(_error!,
                            style: TextStyle(color: colors.onErrorContainer)),
                        TextButton(
                          onPressed: disabled
                              ? null
                              : _conversation != null
                                  ? () => _refresh()
                                  : _initialize,
                          child: Text(_conversation != null
                              ? 'Refresh conversation'
                              : 'Try again'),
                        ),
                      ],
                    )),
              ),
            ),
          Expanded(
              child: _initializing
                  ? const Center(child: CircularProgressIndicator())
                  : ListView(
                      controller: _scroll,
                      padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
                      children: [
                        if (_conversation == null) _welcome(),
                        ...?_conversation?.messages.map(_message),
                        if (_outgoing != null) _bubble(_outgoing!, user: true),
                        if (_busy)
                          Padding(
                            padding: const EdgeInsets.symmetric(vertical: 16),
                            child: Row(children: [
                              const SizedBox(
                                  width: 18,
                                  height: 18,
                                  child: CircularProgressIndicator(
                                      strokeWidth: 2)),
                              const SizedBox(width: 12),
                              Expanded(
                                  child: Text(_outgoing != null
                                      ? 'Working on your request…'
                                      : 'Checking your request…')),
                            ]),
                          ),
                        if (_conversation != null && !_busy) ...[
                          ..._conversation!.questions.map((question) => _bubble(
                              _naturalQuestion(question['prompt']?.toString() ?? ''),
                              user: false)),
                          ..._conversation!.doctors.map((doctor) => ListTile(
                                contentPadding: EdgeInsets.zero,
                                leading:
                                    const Icon(Icons.medical_services_outlined),
                                title: Text(doctor['name']?.toString() ?? ''),
                                subtitle:
                                    Text(doctor['specialty']?.toString() ?? ''),
                              )),
                          if (_conversation!.pendingAction == null)
                            ..._conversation!.slots.map(
                                (slot) => _detailsCard(slot, isSlot: true)),
                          if (_conversation!.pendingAction == null)
                            ..._conversation!.appointments.map(
                                _confirmedAppointmentCard),
                          if (_conversation!.pendingAction != null)
                            _approval(_conversation!.pendingAction!),
                        ],
                      ],
                    )),
          SafeArea(
              top: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(12, 8, 12, 12),
                child:
                    Row(crossAxisAlignment: CrossAxisAlignment.end, children: [
                  Expanded(
                      child: TextField(
                    controller: _input,
                    focusNode: _focus,
                    enabled: !disabled && !_uncertainAction,
                    minLines: 1,
                    maxLines: 4,
                    maxLength: 4000,
                    textCapitalization: TextCapitalization.sentences,
                    decoration: const InputDecoration(
                      hintText: 'Ask about care or appointments…',
                      counterText: '',
                      border: OutlineInputBorder(),
                    ),
                  )),
                  const SizedBox(width: 8),
                  IconButton.filled(
                    tooltip: 'Send message',
                    onPressed: disabled || _uncertainAction ? null : _send,
                    icon: const Icon(Icons.send_rounded),
                  ),
                ]),
              )),
        ]),
      ),
    );
    if (widget.embedded) return body;
    return Scaffold(
        appBar: AppBar(title: const Text('Hospital AI Assistant')), body: body);
  }

  Widget _welcome() => Padding(
        padding: const EdgeInsets.symmetric(vertical: 24),
        child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Icon(Icons.support_agent_rounded,
              size: 42, color: Theme.of(context).colorScheme.primary),
          const SizedBox(height: 16),
          Text('One place for care and appointments',
              style: Theme.of(context)
                  .textTheme
                  .titleLarge
                  ?.copyWith(fontWeight: FontWeight.w700)),
          const SizedBox(height: 10),
          const Text('Tell me what you need. I can help with patient guidance, '
              'find a doctor, or check your appointments. You confirm any booking or cancellation.'),
          const SizedBox(height: 12),
          Text(
              'Guidance does not replace a clinician. For an emergency, seek urgent care immediately.',
              style: Theme.of(context).textTheme.bodySmall),
        ]),
      );

  Widget _message(AssistantMessage message) => Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _bubble(message.text, user: message.role == 'user'),
          if (message.progress.isNotEmpty)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: ExpansionTile(
                tilePadding: EdgeInsets.zero,
                title: const Text('What was checked',
                    style: TextStyle(fontSize: 12)),
                children: message.progress
                    .map((step) => Padding(
                          padding: const EdgeInsets.only(bottom: 8),
                          child: Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                const Icon(Icons.check, size: 16),
                                const SizedBox(width: 8),
                                Expanded(
                                    child: Text(step,
                                        style: const TextStyle(fontSize: 12))),
                              ]),
                        ))
                    .toList(),
              ),
            ),
        ],
      );

  Widget _bubble(String text, {required bool user}) {
    final colors = Theme.of(context).colorScheme;
    return Align(
      alignment: user ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: EdgeInsets.only(
            bottom: 12, left: user ? 28 : 0, right: user ? 0 : 16),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: user ? colors.primaryContainer : colors.surface,
          borderRadius: BorderRadius.circular(16),
          border: Border.all(color: colors.outlineVariant),
        ),
        child: SelectableText(text,
            style: TextStyle(
                color: user ? colors.onPrimaryContainer : colors.onSurface,
                height: 1.45)),
      ),
    );
  }

  // The current assistant accepts a natural-language answer, rather than a
  // checkbox selection. Translate legacy template wording for clarity.
  String _naturalQuestion(String prompt) => prompt
      .replaceFirst(
          RegExp(r'^Select any red-flag respiratory warning signs:', caseSensitive: false),
          'Do you have any of these respiratory warning signs?')
      .replaceFirst(
          RegExp(r'^Select every warning sign that applies\\.?$', caseSensitive: false),
          'Do you have any of these warning signs?')
      .replaceFirst(
          RegExp(r'^Select every health context that applies\\.?$', caseSensitive: false),
          'Do any of these health conditions or situations apply to you?')
      .replaceFirst(RegExp(r'^Select any ', caseSensitive: false), 'Do you have any ')
      .replaceFirst(RegExp(r'^Select every ', caseSensitive: false), 'Do you have any ');

  Widget _approval(AssistantAction action) {
    final expired = action.expiresAt?.isBefore(DateTime.now()) ?? false;
    final isBooking = action.type == 'book';
    final canConfirm = !_busy &&
        !_uncertainAction &&
        !expired &&
        (isBooking ? _selectedSlotId != null : _selectedAppointmentId != null);
    return Card(
      margin: const EdgeInsets.symmetric(vertical: 8),
      child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(action.title,
                  style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              Text(action.description),
              const SizedBox(height: 8),
              ...(isBooking ? action.slots : action.appointments).map((item) {
                final id =
                    (item[isBooking ? 'doctorTimeSlotId' : 'appointmentId']
                            as num)
                        .toInt();
                final selected = isBooking
                    ? _selectedSlotId == id
                    : _selectedAppointmentId == id;
                return Semantics(
                  selected: selected,
                  button: true,
                  child: InkWell(
                    key: ValueKey('assistant-option-$id'),
                    onTap: _busy || _uncertainAction || expired
                        ? null
                        : () => setState(() {
                              if (isBooking) {
                                _selectedSlotId = id;
                              } else {
                                _selectedAppointmentId = id;
                              }
                            }),
                    borderRadius: BorderRadius.circular(12),
                    child: Container(
                      margin: const EdgeInsets.symmetric(vertical: 5),
                      padding: const EdgeInsets.all(12),
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(
                            color: selected
                                ? Theme.of(context).colorScheme.primary
                                : Theme.of(context).colorScheme.outlineVariant,
                            width: selected ? 2 : 1),
                      ),
                      child: Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Icon(
                                selected
                                    ? Icons.radio_button_checked
                                    : Icons.radio_button_off,
                                color: Theme.of(context).colorScheme.primary),
                            const SizedBox(width: 10),
                            Expanded(child: _details(item, isSlot: isBooking)),
                          ]),
                    ),
                  ),
                );
              }),
              const SizedBox(height: 10),
              if (expired)
                const Text(
                    'This option has expired. Choose another to search again.'),
              if (isBooking)
                const Text(
                    'The appointment number is assigned when booking is confirmed. '
                    'Options are not reserved.',
                    style: TextStyle(fontSize: 12)),
              const SizedBox(height: 12),
              SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    onPressed: canConfirm ? () => _decide('confirm') : null,
                    child: Text(isBooking
                        ? 'Confirm appointment'
                        : 'Confirm cancellation'),
                  )),
              Wrap(spacing: 8, children: [
                TextButton(
                    onPressed: _busy || _uncertainAction
                        ? null
                        : () => _decide('chooseAnother'),
                    child: const Text('Choose another')),
                TextButton(
                    onPressed: _busy || _uncertainAction
                        ? null
                        : () => _decide('cancel'),
                    child: const Text('Dismiss request')),
              ]),
            ],
          )),
    );
  }

  Widget _detailsCard(AssistantJson item, {required bool isSlot}) => Card(
        margin: const EdgeInsets.only(bottom: 12),
        child: Padding(
            padding: const EdgeInsets.all(14),
            child: _details(item, isSlot: isSlot)),
      );

  Widget _confirmedAppointmentCard(AssistantJson appointment) {
    final colors = Theme.of(context).colorScheme;
    final number = appointment['appointmentNumber'];
    final status = appointment['status']?.toString() ?? 'Confirmed';
    final cancelled = status.toLowerCase() == 'cancelled';
    final tone = cancelled ? colors.error : AppColors.success;
    return Container(
      margin: const EdgeInsets.only(top: 8, bottom: 14),
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: tone.withValues(alpha: .09),
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: tone.withValues(alpha: .35)),
      ),
      child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
        Row(children: [
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(color: tone.withValues(alpha: .14), shape: BoxShape.circle),
            child: Icon(cancelled ? Icons.cancel_outlined : Icons.check_circle_outline_rounded, color: tone),
          ),
          const SizedBox(width: 12),
          Expanded(child: Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
            Text(cancelled ? 'Appointment cancelled' : 'Appointment confirmed', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: tone)),
            Text(cancelled ? 'This appointment is no longer active.' : 'Your hospital visit has been saved.', style: const TextStyle(fontSize: 12)),
          ])),
        ]),
        const SizedBox(height: 16),
        _details(appointment, isSlot: false),
        if (!cancelled && number is num) ...[
          const SizedBox(height: 14),
          Container(
            width: double.infinity,
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(color: colors.surface, borderRadius: BorderRadius.circular(12)),
            child: Text('Please arrive before your session. Keep Appointment No. $number for check-in.', style: const TextStyle(fontWeight: FontWeight.w600, height: 1.35)),
          ),
        ],
      ]),
    );
  }

  Widget _details(AssistantJson item, {required bool isSlot}) {
    final number = item['appointmentNumber'];
    final location = item['location']?.toString() ??
        [item['roomNumber'], item['roomName'], item['floor']]
            .where((value) => value != null && value.toString().isNotEmpty)
            .join(', ');
    return Column(crossAxisAlignment: CrossAxisAlignment.start, children: [
      Text(item['doctorName']?.toString() ?? 'Appointment',
          style: const TextStyle(fontWeight: FontWeight.w700)),
      if ((item['specialty']?.toString() ?? '').isNotEmpty)
        Text(item['specialty'].toString()),
      const SizedBox(height: 6),
      Text(_date(item['startAt'])),
      Text(_timeRange(item['startAt'], item['endAt'])),
      if (number is num && number > 0)
        Padding(
          padding: const EdgeInsets.only(top: 6),
          child: Text(
              '${isSlot ? 'Expected appointment number' : 'Appointment No'}: $number',
              style: const TextStyle(fontWeight: FontWeight.w600)),
        ),
      if (location.isNotEmpty) Text(location),
      if (item['consultationFee'] is num)
        Text('LKR ${NumberFormat('#,##0.00').format(item['consultationFee'])}'),
      if (!isSlot && item['status'] != null) Text(item['status'].toString()),
    ]);
  }

  String _date(dynamic value) {
    final date = _serverDate(value);
    return date == null
        ? ''
        : DateFormat('EEE, d MMM yyyy').format(date.toLocal());
  }

  String _timeRange(dynamic start, dynamic end) {
    final startAt = _serverDate(start);
    final endAt = _serverDate(end);
    if (startAt == null) return '';
    final first = DateFormat('h:mm a').format(startAt.toLocal());
    return endAt == null
        ? first
        : '$first – ${DateFormat('h:mm a').format(endAt.toLocal())}';
  }

  DateTime? _serverDate(dynamic value) {
    final text = value?.toString() ?? '';
    if (text.isEmpty) return null;
    // Legacy PostgreSQL timestamp columns can serialize UTC without a suffix.
    return DateTime.tryParse(RegExp(r'(Z|[+-]\d{2}:\d{2})$', caseSensitive: false)
        .hasMatch(text) ? text : '${text}Z');
  }
}
