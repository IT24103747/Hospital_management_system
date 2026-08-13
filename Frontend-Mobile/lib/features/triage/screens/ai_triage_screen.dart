import 'package:flutter/material.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
import 'package:smartcare_mobile/features/clinic_finder/screens/emergency_clinic_screen.dart';
import 'package:smartcare_mobile/models/triage_workflow.dart';

class AiTriageScreen extends StatefulWidget {
  const AiTriageScreen({super.key});

  @override
  State<AiTriageScreen> createState() => _AiTriageScreenState();
}

class _AiTriageScreenState extends State<AiTriageScreen> {
  final _symptomController = TextEditingController();
  bool _isSubmitting = false;
  TriageWorkflow? _workflow;

  Future<void> _startTriage() async {
    if (_symptomController.text.trim().length < 3) {
      ScaffoldMessenger.of(context).showSnackBar(const SnackBar(
          content: Text('Please describe your symptoms before continuing.')));
      return;
    }
    setState(() => _isSubmitting = true);
    try {
      final workflow = await ApiService.startTriageWorkflow(
          symptoms: _symptomController.text);
      if (mounted) setState(() => _workflow = workflow);
    } catch (error) {
      if (mounted)
        ScaffoldMessenger.of(context).showSnackBar(SnackBar(
            content: Text(
                'SafeTriage could not safely process this request: $error'),
            backgroundColor: AppColors.danger));
    } finally {
      if (mounted) setState(() => _isSubmitting = false);
    }
  }

  Future<void> _refreshStatus() async {
    if (_workflow == null) return;
    setState(() => _isSubmitting = true);
    try {
      final workflow =
          await ApiService.getTriageWorkflow(_workflow!.workflowId);
      if (mounted) setState(() => _workflow = workflow);
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
    _symptomController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final isEmergency = _workflow?.triageLevel == 'Emergency';
    return Scaffold(
      appBar: AppBar(title: const Text('SafeTriage decision support')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20),
        child:
            Column(crossAxisAlignment: CrossAxisAlignment.stretch, children: [
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
                color: AppColors.warning.withValues(alpha: .12),
                borderRadius: BorderRadius.circular(14)),
            child: const Text(
                'SafeTriage is decision support, not a diagnosis or replacement for a clinician. If this may be an emergency, seek emergency care immediately.'),
          ),
          const SizedBox(height: 20),
          const Text('What symptoms are you experiencing?',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16)),
          const SizedBox(height: 8),
          TextFormField(
              controller: _symptomController,
              maxLines: 4,
              decoration: const InputDecoration(
                  hintText:
                      'Describe symptoms in your own words. Do not include passwords or account details.')),
          const SizedBox(height: 16),
          SizedBox(
              height: 52,
              child: ElevatedButton.icon(
                  onPressed: _isSubmitting ? null : _startTriage,
                  icon: const Icon(Icons.health_and_safety_outlined),
                  label: Text(_isSubmitting
                      ? 'Submitting safely…'
                      : 'Submit for SafeTriage'))),
          if (_workflow != null) ...[
            const SizedBox(height: 24),
            Card(
                child: Padding(
                    padding: const EdgeInsets.all(18),
                    child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(_workflow!.triageLevel,
                              style: TextStyle(
                                  fontSize: 20,
                                  fontWeight: FontWeight.bold,
                                  color: isEmergency
                                      ? AppColors.danger
                                      : AppColors.primary)),
                          const SizedBox(height: 8),
                          Text(_workflow!.patientMessage),
                          const SizedBox(height: 12),
                          Text('Workflow status: ${_workflow!.status}'),
                          Text('Clinical review: ${_workflow!.approvalStatus}'),
                          Text(
                              'Information state: ${_workflow!.uncertaintyState}'),
                          if (_workflow!.redFlags.isNotEmpty) ...[
                            const SizedBox(height: 12),
                            const Text('Configured safety flags',
                                style: TextStyle(fontWeight: FontWeight.bold)),
                            ..._workflow!.redFlags
                                .map((flag) => Text('• $flag'))
                          ],
                          if (_workflow!.missingInformation.isNotEmpty) ...[
                            const SizedBox(height: 12),
                            const Text('Important limitations',
                                style: TextStyle(fontWeight: FontWeight.bold)),
                            ..._workflow!.missingInformation
                                .map((item) => Text('• $item'))
                          ],
                          const SizedBox(height: 16),
                          OutlinedButton.icon(
                              onPressed: _isSubmitting ? null : _refreshStatus,
                              icon: const Icon(Icons.refresh),
                              label:
                                  const Text('Refresh clinical-review status')),
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
                                    icon:
                                        const Icon(Icons.warning_amber_rounded),
                                    label:
                                        const Text('Find emergency clinic'))),
                        ]))),
          ],
        ]),
      ),
    );
  }
}
