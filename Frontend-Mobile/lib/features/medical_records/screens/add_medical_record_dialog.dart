import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
import 'package:smartcare_mobile/models/doctor.dart';
import 'package:smartcare_mobile/models/patient.dart';

class AddMedicalRecordDialog extends StatefulWidget {
  final VoidCallback onRecordCreated;

  const AddMedicalRecordDialog({super.key, required this.onRecordCreated});

  @override
  State<AddMedicalRecordDialog> createState() => _AddMedicalRecordDialogState();
}

class _AddMedicalRecordDialogState extends State<AddMedicalRecordDialog> {
  final _formKey = GlobalKey<FormState>();

  final _diagnosisController = TextEditingController();
  final _symptomsController = TextEditingController();
  final _treatmentPlanController = TextEditingController();
  final _prescriptionController = TextEditingController();
  final _labNotesController = TextEditingController();

  DateTime _recordDate = DateTime.now();
  DateTime? _followUpDate;
  String _selectedRecordType = 'Consultation';
  String _selectedStatus = 'Finalized';

  int? _selectedPatientId;
  int? _selectedDoctorId;

  List<Patient> _patients = [];
  List<DoctorSearchResult> _doctors = [];
  bool _loadingDependencies = true;
  bool _submitting = false;
  String? _errorMessage;

  final List<String> _recordTypes = [
    'Consultation',
    'LabReport',
    'DischargeSummary',
    'Prescription',
    'GeneralNote',
  ];

  final List<String> _statuses = [
    'Finalized',
    'Draft',
    'Archived',
  ];

  @override
  void initState() {
    super.initState();
    _loadDependencies();
  }

  @override
  void dispose() {
    _diagnosisController.dispose();
    _symptomsController.dispose();
    _treatmentPlanController.dispose();
    _prescriptionController.dispose();
    _labNotesController.dispose();
    super.dispose();
  }

  Future<void> _loadDependencies() async {
    try {
      final results = await Future.wait([
        ApiService.getAllPatients(),
        ApiService.searchDoctors(),
      ]);

      if (!mounted) return;
      setState(() {
        _patients = results[0] as List<Patient>;
        _doctors = results[1] as List<DoctorSearchResult>;
        if (_patients.isNotEmpty) {
          _selectedPatientId = _patients.first.patientId;
        }
        if (_doctors.isNotEmpty) {
          _selectedDoctorId = _doctors.first.doctorId;
        }
        _loadingDependencies = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => _loadingDependencies = false);
    }
  }

  Future<void> _pickDate({required bool isFollowUp}) async {
    final initialDate = isFollowUp
        ? (_followUpDate ?? DateTime.now().add(const Duration(days: 7)))
        : _recordDate;

    final picked = await showDatePicker(
      context: context,
      initialDate: initialDate,
      firstDate: DateTime(2020),
      lastDate: DateTime(2035),
    );

    if (picked != null && mounted) {
      setState(() {
        if (isFollowUp) {
          _followUpDate = picked;
        } else {
          _recordDate = picked;
        }
      });
    }
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    if (_selectedPatientId == null) {
      setState(() => _errorMessage = 'Please select a patient.');
      return;
    }

    setState(() {
      _submitting = true;
      _errorMessage = null;
    });

    try {
      final data = {
        'patientId': _selectedPatientId,
        if (_selectedDoctorId != null) 'doctorId': _selectedDoctorId,
        'recordDate': _recordDate.toIso8601String(),
        'recordType': _selectedRecordType,
        'diagnosis': _diagnosisController.text.trim(),
        'symptoms': _symptomsController.text.trim(),
        'treatmentPlan': _treatmentPlanController.text.trim(),
        if (_prescriptionController.text.trim().isNotEmpty)
          'prescriptionNotes': _prescriptionController.text.trim(),
        if (_labNotesController.text.trim().isNotEmpty)
          'labNotes': _labNotesController.text.trim(),
        if (_followUpDate != null)
          'followUpDate': _followUpDate!.toIso8601String(),
        'status': _selectedStatus,
      };

      await ApiService.createMedicalRecord(data);

      if (!mounted) return;
      Navigator.of(context).pop();
      widget.onRecordCreated();

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Medical record created successfully!'),
          backgroundColor: AppColors.success,
          behavior: SnackBarBehavior.floating,
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _submitting = false;
        _errorMessage = e.toString().replaceAll('Exception: ', '');
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final size = MediaQuery.of(context).size;
    final isDesktop = size.width >= 700;

    return Dialog(
      backgroundColor: isDark ? AppColors.surfaceDark : Colors.white,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
      insetPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 24),
      child: Container(
        width: isDesktop ? 640 : size.width,
        constraints: BoxConstraints(maxHeight: size.height * 0.88),
        child: Column(
          children: [
            // Header
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
              decoration: BoxDecoration(
                border: Border(
                  bottom: BorderSide(
                    color: isDark ? AppColors.borderDark : AppColors.borderLight,
                  ),
                ),
              ),
              child: Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha: 0.12),
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: const Icon(Icons.note_add_rounded, color: AppColors.primary, size: 22),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Add Medical Record',
                          style: TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.bold,
                            color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                          ),
                        ),
                        const SizedBox(height: 2),
                        const Text(
                          'Create a clinical consultation, report, or prescription note',
                          style: TextStyle(fontSize: 12, color: Colors.grey),
                        ),
                      ],
                    ),
                  ),
                  IconButton(
                    onPressed: () => Navigator.of(context).pop(),
                    icon: const Icon(Icons.close_rounded, size: 20),
                    tooltip: 'Close',
                  ),
                ],
              ),
            ),

            // Form Body
            Expanded(
              child: _loadingDependencies
                  ? const Center(child: CircularProgressIndicator())
                  : SingleChildScrollView(
                      padding: const EdgeInsets.all(20),
                      child: Form(
                        key: _formKey,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            if (_errorMessage != null) ...[
                              Container(
                                padding: const EdgeInsets.all(12),
                                margin: const EdgeInsets.only(bottom: 16),
                                decoration: BoxDecoration(
                                  color: AppColors.danger.withValues(alpha: 0.1),
                                  borderRadius: BorderRadius.circular(10),
                                  border: Border.all(color: AppColors.danger.withValues(alpha: 0.3)),
                                ),
                                child: Row(
                                  children: [
                                    const Icon(Icons.error_outline_rounded, color: AppColors.danger, size: 20),
                                    const SizedBox(width: 10),
                                    Expanded(
                                      child: Text(
                                        _errorMessage!,
                                        style: const TextStyle(color: AppColors.danger, fontSize: 13),
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],

                            // Patient Selector
                            _buildSectionLabel('Patient *', isDark),
                            if (_patients.isNotEmpty)
                              DropdownButtonFormField<int>(
                                initialValue: _selectedPatientId,
                                decoration: _inputDecoration(isDark, prefixIcon: Icons.person_outline_rounded),
                                items: _patients.map((p) {
                                  return DropdownMenuItem<int>(
                                    value: p.patientId,
                                    child: Text(
                                      '${p.fullName} (ID: ${p.patientId}${p.phoneNumber.isNotEmpty ? ' • ${p.phoneNumber}' : ''})',
                                      overflow: TextOverflow.ellipsis,
                                      style: const TextStyle(fontSize: 13),
                                    ),
                                  );
                                }).toList(),
                                onChanged: (val) => setState(() => _selectedPatientId = val),
                                validator: (val) => val == null ? 'Please select a patient' : null,
                              )
                            else
                              TextFormField(
                                initialValue: _selectedPatientId?.toString() ?? '1',
                                keyboardType: TextInputType.number,
                                decoration: _inputDecoration(isDark, hint: 'Enter Patient ID (e.g. 1)'),
                                onChanged: (val) => _selectedPatientId = int.tryParse(val),
                                validator: (val) => (val == null || val.trim().isEmpty) ? 'Required' : null,
                              ),
                            const SizedBox(height: 16),

                            // Doctor & Record Type Row
                            Row(
                              children: [
                                // Doctor Selector
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      _buildSectionLabel('Doctor', isDark),
                                      if (_doctors.isNotEmpty)
                                        DropdownButtonFormField<int?>(
                                          initialValue: _selectedDoctorId,
                                          decoration: _inputDecoration(isDark, prefixIcon: Icons.medication_liquid_rounded),
                                          items: [
                                            const DropdownMenuItem<int?>(
                                              value: null,
                                              child: Text('Unassigned / Clinician', style: TextStyle(fontSize: 13)),
                                            ),
                                            ..._doctors.map((d) => DropdownMenuItem<int?>(
                                                  value: d.doctorId,
                                                  child: Text(
                                                    '${d.fullName} (${d.specialization})',
                                                    overflow: TextOverflow.ellipsis,
                                                    style: const TextStyle(fontSize: 13),
                                                  ),
                                                )),
                                          ],
                                          onChanged: (val) => setState(() => _selectedDoctorId = val),
                                        )
                                      else
                                        TextFormField(
                                          decoration: _inputDecoration(isDark, hint: 'Doctor ID (optional)'),
                                          keyboardType: TextInputType.number,
                                          onChanged: (val) => _selectedDoctorId = int.tryParse(val),
                                        ),
                                    ],
                                  ),
                                ),
                                const SizedBox(width: 14),

                                // Record Type
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      _buildSectionLabel('Record Type *', isDark),
                                      DropdownButtonFormField<String>(
                                        initialValue: _selectedRecordType,
                                        decoration: _inputDecoration(isDark),
                                        items: _recordTypes.map((type) {
                                          return DropdownMenuItem<String>(
                                            value: type,
                                            child: Text(type, style: const TextStyle(fontSize: 13)),
                                          );
                                        }).toList(),
                                        onChanged: (val) {
                                          if (val != null) setState(() => _selectedRecordType = val);
                                        },
                                      ),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 16),

                            // Record Date & Status Row
                            Row(
                              children: [
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      _buildSectionLabel('Record Date', isDark),
                                      InkWell(
                                        onTap: () => _pickDate(isFollowUp: false),
                                        borderRadius: BorderRadius.circular(10),
                                        child: Container(
                                          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
                                          decoration: BoxDecoration(
                                            color: isDark ? AppColors.surfaceDark : const Color(0xFFF8FAFC),
                                            borderRadius: BorderRadius.circular(10),
                                            border: Border.all(
                                              color: isDark ? AppColors.borderDark : AppColors.borderLight,
                                            ),
                                          ),
                                          child: Row(
                                            children: [
                                              const Icon(Icons.calendar_today_rounded, size: 16, color: AppColors.primary),
                                              const SizedBox(width: 8),
                                              Text(
                                                DateFormat('MMM dd, yyyy').format(_recordDate),
                                                style: const TextStyle(fontSize: 13),
                                              ),
                                            ],
                                          ),
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                                const SizedBox(width: 14),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      _buildSectionLabel('Status', isDark),
                                      DropdownButtonFormField<String>(
                                        initialValue: _selectedStatus,
                                        decoration: _inputDecoration(isDark),
                                        items: _statuses.map((s) {
                                          return DropdownMenuItem<String>(
                                            value: s,
                                            child: Text(s, style: const TextStyle(fontSize: 13)),
                                          );
                                        }).toList(),
                                        onChanged: (val) {
                                          if (val != null) setState(() => _selectedStatus = val);
                                        },
                                      ),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 16),

                            // Diagnosis (Required)
                            _buildSectionLabel('Diagnosis *', isDark),
                            TextFormField(
                              controller: _diagnosisController,
                              decoration: _inputDecoration(
                                isDark,
                                hint: 'e.g. Acute Bronchitis, Type 2 Diabetes, Hypertension...',
                                prefixIcon: Icons.medical_services_outlined,
                              ),
                              validator: (val) =>
                                  (val == null || val.trim().isEmpty) ? 'Please enter a diagnosis' : null,
                            ),
                            const SizedBox(height: 16),

                            // Symptoms (Required)
                            _buildSectionLabel('Clinical Symptoms *', isDark),
                            TextFormField(
                              controller: _symptomsController,
                              maxLines: 2,
                              decoration: _inputDecoration(
                                isDark,
                                hint: 'e.g. Cough for 5 days, mild chest tightness, fatigue...',
                              ),
                              validator: (val) =>
                                  (val == null || val.trim().isEmpty) ? 'Please describe clinical symptoms' : null,
                            ),
                            const SizedBox(height: 16),

                            // Treatment Plan (Required)
                            _buildSectionLabel('Treatment Plan *', isDark),
                            TextFormField(
                              controller: _treatmentPlanController,
                              maxLines: 2,
                              decoration: _inputDecoration(
                                isDark,
                                hint: 'e.g. Prescribed bronchodilator inhaler, hydration, warm fluids...',
                              ),
                              validator: (val) =>
                                  (val == null || val.trim().isEmpty) ? 'Please enter a treatment plan' : null,
                            ),
                            const SizedBox(height: 16),

                            // Prescription Notes (Optional)
                            _buildSectionLabel('Prescription Notes (Optional)', isDark),
                            TextFormField(
                              controller: _prescriptionController,
                              maxLines: 2,
                              decoration: _inputDecoration(
                                isDark,
                                hint: 'e.g. Salbutamol 100mcg 2 puffs TDS, Paracetamol 500mg PRN...',
                              ),
                            ),
                            const SizedBox(height: 16),

                            // Lab Notes & Follow-up Date Row
                            Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Expanded(
                                  flex: 3,
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      _buildSectionLabel('Lab / Diagnostic Notes (Optional)', isDark),
                                      TextFormField(
                                        controller: _labNotesController,
                                        maxLines: 2,
                                        decoration: _inputDecoration(
                                          isDark,
                                          hint: 'e.g. Chest X-ray clear, normal inflammatory markers...',
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                                const SizedBox(width: 14),
                                Expanded(
                                  flex: 2,
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      _buildSectionLabel('Follow-up Date', isDark),
                                      InkWell(
                                        onTap: () => _pickDate(isFollowUp: true),
                                        borderRadius: BorderRadius.circular(10),
                                        child: Container(
                                          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
                                          decoration: BoxDecoration(
                                            color: isDark ? AppColors.surfaceDark : const Color(0xFFF8FAFC),
                                            borderRadius: BorderRadius.circular(10),
                                            border: Border.all(
                                              color: isDark ? AppColors.borderDark : AppColors.borderLight,
                                            ),
                                          ),
                                          child: Row(
                                            children: [
                                              const Icon(Icons.event_repeat_rounded, size: 16, color: AppColors.primary),
                                              const SizedBox(width: 8),
                                              Expanded(
                                                child: Text(
                                                  _followUpDate != null
                                                      ? DateFormat('MMM dd, yyyy').format(_followUpDate!)
                                                      : 'None',
                                                  overflow: TextOverflow.ellipsis,
                                                  style: TextStyle(
                                                    fontSize: 13,
                                                    color: _followUpDate != null ? null : Colors.grey,
                                                  ),
                                                ),
                                              ),
                                              if (_followUpDate != null)
                                                GestureDetector(
                                                  onTap: () => setState(() => _followUpDate = null),
                                                  child: const Icon(Icons.clear, size: 14, color: Colors.grey),
                                                ),
                                            ],
                                          ),
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ),
            ),

            // Actions Footer
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
              decoration: BoxDecoration(
                border: Border(
                  top: BorderSide(
                    color: isDark ? AppColors.borderDark : AppColors.borderLight,
                  ),
                ),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  OutlinedButton(
                    onPressed: _submitting ? null : () => Navigator.of(context).pop(),
                    style: OutlinedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    child: const Text('Cancel'),
                  ),
                  const SizedBox(width: 12),
                  ElevatedButton.icon(
                    onPressed: _submitting ? null : _submit,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 12),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    icon: _submitting
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                          )
                        : const Icon(Icons.check_circle_outline_rounded, size: 18),
                    label: Text(_submitting ? 'Saving...' : 'Create Record'),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildSectionLabel(String label, bool isDark) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 6),
      child: Text(
        label,
        style: TextStyle(
          fontSize: 12,
          fontWeight: FontWeight.w700,
          color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
        ),
      ),
    );
  }

  InputDecoration _inputDecoration(bool isDark, {String? hint, IconData? prefixIcon}) {
    return InputDecoration(
      hintText: hint,
      hintStyle: const TextStyle(color: Colors.grey, fontSize: 13),
      prefixIcon: prefixIcon != null ? Icon(prefixIcon, size: 18, color: Colors.grey) : null,
      filled: true,
      fillColor: isDark ? AppColors.surfaceDark : const Color(0xFFF8FAFC),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: BorderSide(
          color: isDark ? AppColors.borderDark : AppColors.borderLight,
        ),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: BorderSide(
          color: isDark ? AppColors.borderDark : AppColors.borderLight,
        ),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: const BorderSide(color: AppColors.primary, width: 1.5),
      ),
    );
  }
}
