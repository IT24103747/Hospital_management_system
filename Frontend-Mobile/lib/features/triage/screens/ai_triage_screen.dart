import 'package:flutter/material.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
import 'package:smartcare_mobile/core/widgets/medicore_logo.dart';
import 'package:smartcare_mobile/models/triage_result.dart';
import 'package:smartcare_mobile/features/vitals/screens/vitals_logger_screen.dart';
import 'package:smartcare_mobile/features/clinic_finder/screens/emergency_clinic_screen.dart';
import 'package:smartcare_mobile/features/profile/screens/profile_screen.dart';

class AiTriageScreen extends StatefulWidget {
  const AiTriageScreen({super.key});

  @override
  State<AiTriageScreen> createState() => _AiTriageScreenState();
}

class _AiTriageScreenState extends State<AiTriageScreen> {
  final _symptomController = TextEditingController();
  bool _isAnalyzing = false;
  TriageResult? _triageResult;

  void _runAiTriage() async {
    if (_symptomController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Please describe your symptoms before running AI triage.')),
      );
      return;
    }

    setState(() {
      _isAnalyzing = true;
      _triageResult = null;
    });

    final result = await ApiService.performAiTriage(
      patientId: 1,
      symptoms: _symptomController.text.trim(),
    );

    if (mounted) {
      setState(() {
        _isAnalyzing = false;
        _triageResult = result;
      });
    }
  }

  Color _getRiskColor(TriageRiskLevel level) {
    switch (level) {
      case TriageRiskLevel.critical:
        return AppColors.danger;
      case TriageRiskLevel.urgent:
        return AppColors.warning;
      case TriageRiskLevel.routine:
        return AppColors.success;
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      appBar: AppBar(
        title: const MediCoreLogo(
          size: 32.0,
          borderRadius: 8.0,
          showText: true,
          fontSize: 18.0,
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.favorite_outline),
            tooltip: 'Log Vitals',
            onPressed: () {
              Navigator.push(
                context,
                MaterialPageRoute(builder: (context) => const VitalsLoggerScreen()),
              );
            },
          ),
          IconButton(
            icon: const Icon(Icons.near_me_outlined),
            tooltip: 'Emergency Locator',
            onPressed: () {
              Navigator.push(
                context,
                MaterialPageRoute(builder: (context) => const EmergencyClinicScreen()),
              );
            },
          ),
          IconButton(
            icon: const Icon(Icons.person_outline),
            tooltip: 'Profile',
            onPressed: () {
              Navigator.push(
                context,
                MaterialPageRoute(builder: (context) => const ProfileScreen()),
              );
            },
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              padding: const EdgeInsets.all(20),
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  colors: isDark
                      ? [AppColors.primaryDark, AppColors.accent]
                      : [AppColors.primary, AppColors.accent],
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                ),
                borderRadius: BorderRadius.circular(20),
                boxShadow: [
                  BoxShadow(
                    color: AppColors.primary.withValues(alpha: 0.3),
                    blurRadius: 12,
                    offset: const Offset(0, 4),
                  )
                ],
              ),
              child: const Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Icon(Icons.auto_awesome, color: Colors.white, size: 24),
                      SizedBox(width: 8),
                      Text(
                        'AI Patient Triage Subsystem',
                        style: TextStyle(
                          color: Colors.white,
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ],
                  ),
                  SizedBox(height: 8),
                  Text(
                    'Describe your current symptoms or pain. Our autonomous Agentic AI analyzes severity, vitals, and routes you to the right care department.',
                    style: TextStyle(color: Colors.white70, fontSize: 13, height: 1.4),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),

            Text(
              'What symptoms are you experiencing?',
              style: TextStyle(
                fontSize: 16,
                fontWeight: FontWeight.bold,
                color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
              ),
            ),
            const SizedBox(height: 10),
            TextFormField(
              controller: _symptomController,
              maxLines: 4,
              decoration: const InputDecoration(
                hintText: 'e.g. Sharp chest pain radiating to left arm, high fever of 39°C, and shortness of breath...',
              ),
            ),
            const SizedBox(height: 16),

            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                'Chest Pain & Breathlessness',
                'High Fever & Chills',
                'Severe Headache & Dizziness',
                'Stomach Cramps',
              ].map((tag) {
                return ActionChip(
                  label: Text(tag, style: const TextStyle(fontSize: 12)),
                  backgroundColor: isDark ? AppColors.surfaceDarkSecondary : Colors.grey.shade100,
                  onPressed: () {
                    _symptomController.text = tag;
                  },
                );
              }).toList(),
            ),
            const SizedBox(height: 20),

            SizedBox(
              height: 52,
              child: ElevatedButton.icon(
                onPressed: _isAnalyzing ? null : _runAiTriage,
                icon: const Icon(Icons.psychology_rounded),
                label: Text(_isAnalyzing ? 'Analyzing Symptoms via AI Agent...' : 'Analyze & Determine Triage'),
              ),
            ),
            const SizedBox(height: 24),

            if (_isAnalyzing)
              Center(
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 24.0),
                  child: Column(
                    children: [
                      const CircularProgressIndicator(color: AppColors.primary),
                      const SizedBox(height: 16),
                      Text(
                        'AI Agent is evaluating risk factors & department availability...',
                        style: TextStyle(
                          color: isDark ? AppColors.textSecondaryDark : AppColors.textSecondaryLight,
                          fontSize: 13,
                        ),
                      ),
                    ],
                  ),
                ),
              ),

            if (_triageResult != null && !_isAnalyzing) ...[
              Card(
                elevation: 4,
                child: Padding(
                  padding: const EdgeInsets.all(20.0),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
                            decoration: BoxDecoration(
                              color: _getRiskColor(_triageResult!.riskLevel).withValues(alpha: 0.15),
                              borderRadius: BorderRadius.circular(20),
                              border: Border.all(color: _getRiskColor(_triageResult!.riskLevel)),
                            ),
                            child: Text(
                              _triageResult!.riskLabel,
                              style: TextStyle(
                                color: _getRiskColor(_triageResult!.riskLevel),
                                fontWeight: FontWeight.bold,
                                fontSize: 13,
                              ),
                            ),
                          ),
                          Text(
                            'Urgency: ${_triageResult!.urgencyScore}/10',
                            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                          ),
                        ],
                      ),
                      const SizedBox(height: 16),
                      const Text(
                        'Recommended Department:',
                        style: TextStyle(fontSize: 12, color: AppColors.textSecondaryLight),
                      ),
                      Text(
                        _triageResult!.recommendedDepartment,
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                          color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                        ),
                      ),
                      const Divider(height: 24),
                      const Text(
                        'AI Agent Assessment Summary:',
                        style: TextStyle(fontSize: 12, fontWeight: FontWeight.bold),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        _triageResult!.aiSummary,
                        style: const TextStyle(fontSize: 13, height: 1.4),
                      ),
                      const SizedBox(height: 16),
                      const Text(
                        'Recommended Next Actions:',
                        style: TextStyle(fontSize: 12, fontWeight: FontWeight.bold),
                      ),
                      const SizedBox(height: 8),
                      ..._triageResult!.recommendedActions.map(
                        (action) => Padding(
                          padding: const EdgeInsets.only(bottom: 6.0),
                          child: Row(
                            children: [
                              const Icon(Icons.check_circle_outline, size: 16, color: AppColors.success),
                              const SizedBox(width: 8),
                              Expanded(child: Text(action, style: const TextStyle(fontSize: 13))),
                            ],
                          ),
                        ),
                      ),
                      const SizedBox(height: 16),
                      if (_triageResult!.riskLevel == TriageRiskLevel.critical)
                        SizedBox(
                          width: double.infinity,
                          child: ElevatedButton.icon(
                            style: ElevatedButton.styleFrom(backgroundColor: AppColors.danger),
                            onPressed: () {
                              Navigator.push(
                                context,
                                MaterialPageRoute(builder: (context) => const EmergencyClinicScreen()),
                              );
                            },
                            icon: const Icon(Icons.warning_amber_rounded),
                            label: const Text('Find Nearest Emergency ER Clinic'),
                          ),
                        ),
                    ],
                  ),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}
