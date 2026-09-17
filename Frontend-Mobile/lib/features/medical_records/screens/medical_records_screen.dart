import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
import 'package:smartcare_mobile/features/medical_records/screens/add_medical_record_dialog.dart';
import 'package:smartcare_mobile/features/medical_records/screens/medical_record_detail_screen.dart';
import 'package:smartcare_mobile/models/medical_record.dart';

class MedicalRecordsScreen extends StatefulWidget {
  final bool embedded;

  const MedicalRecordsScreen({super.key, this.embedded = false});

  @override
  State<MedicalRecordsScreen> createState() => _MedicalRecordsScreenState();
}

class _MedicalRecordsScreenState extends State<MedicalRecordsScreen> {
  List<MedicalRecord> _records = [];
  bool _loading = true;
  String? _error;
  String _searchQuery = '';
  String _selectedType = 'All';
  bool _isAdminOrDoctor = false;

  final List<String> _types = [
    'All',
    'Consultation',
    'LabReport',
    'Prescription',
    'DischargeSummary',
    'GeneralNote',
  ];

  @override
  void initState() {
    super.initState();
    _checkRoleAndFetch();
  }

  Future<void> _checkRoleAndFetch() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final role = prefs.getString('user_role');
      final email = (prefs.getString('patient_email') ?? '').toLowerCase();

      final isAdminOrDoctor = role == 'Admin' ||
          role == 'Doctor' ||
          email.contains('admin') ||
          email.contains('doctor');

      if (mounted) {
        setState(() => _isAdminOrDoctor = isAdminOrDoctor);
      }
    } catch (_) {}

    await _fetchRecords();
  }

  Future<void> _fetchRecords() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final data = await ApiService.getMedicalRecords();
      if (!mounted) return;
      setState(() {
        _records = data;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = 'Unable to load medical records. Please pull down or click try again.';
        _loading = false;
      });
    }
  }

  void _openAddRecordDialog() {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => AddMedicalRecordDialog(
        onRecordCreated: _fetchRecords,
      ),
    );
  }

  List<MedicalRecord> get _filteredRecords {
    return _records.where((rec) {
      final matchesType = _selectedType == 'All' || rec.recordType == _selectedType;
      final query = _searchQuery.trim().toLowerCase();
      final matchesSearch = query.isEmpty ||
          rec.diagnosis.toLowerCase().contains(query) ||
          rec.symptoms.toLowerCase().contains(query) ||
          (rec.doctorName != null && rec.doctorName!.toLowerCase().contains(query)) ||
          rec.patientName.toLowerCase().contains(query);
      return matchesType && matchesSearch;
    }).toList();
  }

  Color _getTypeColor(String type) {
    switch (type) {
      case 'Consultation':
        return AppColors.primary;
      case 'LabReport':
        return const Color(0xFF0891B2);
      case 'DischargeSummary':
        return const Color(0xFFD97706);
      case 'Prescription':
        return const Color(0xFF4F46E5);
      case 'GeneralNote':
        return const Color(0xFF10B981);
      default:
        return Colors.grey;
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    final content = RefreshIndicator(
      onRefresh: _fetchRecords,
      color: AppColors.primary,
      child: SingleChildScrollView(
        physics: const AlwaysScrollableScrollPhysics(),
        padding: const EdgeInsets.symmetric(horizontal: 16.0, vertical: 12.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Top Search & Add Button Bar
            Row(
              children: [
                Expanded(
                  child: TextField(
                    decoration: InputDecoration(
                      hintText: 'Search diagnoses, symptoms, patients...',
                      prefixIcon: const Icon(Icons.search, size: 20),
                      contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                      filled: true,
                      fillColor: isDark ? AppColors.surfaceDark : const Color(0xFFF1F5F9),
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                        borderSide: BorderSide.none,
                      ),
                    ),
                    onChanged: (val) => setState(() => _searchQuery = val),
                  ),
                ),
                if (_isAdminOrDoctor) ...[
                  const SizedBox(width: 10),
                  ElevatedButton.icon(
                    onPressed: _openAddRecordDialog,
                    icon: const Icon(Icons.add_rounded, size: 18),
                    label: const Text('Add Record'),
                    style: ElevatedButton.styleFrom(
                      backgroundColor: AppColors.primary,
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                      elevation: 1,
                    ),
                  ),
                ],
              ],
            ),
            const SizedBox(height: 12),

            // Type Filter Chips
            SizedBox(
              height: 38,
              child: ListView.separated(
                scrollDirection: Axis.horizontal,
                itemCount: _types.length,
                separatorBuilder: (_, __) => const SizedBox(width: 8),
                itemBuilder: (context, i) {
                  final t = _types[i];
                  final isSelected = _selectedType == t;
                  return ChoiceChip(
                    label: Text(
                      t == 'LabReport'
                          ? 'Lab Reports'
                          : t == 'DischargeSummary'
                              ? 'Discharge'
                              : t == 'GeneralNote'
                                  ? 'Notes'
                                  : t,
                    ),
                    selected: isSelected,
                    selectedColor: AppColors.primary,
                    labelStyle: TextStyle(
                      color: isSelected ? Colors.white : (isDark ? Colors.white70 : Colors.black87),
                      fontSize: 12,
                      fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                    ),
                    backgroundColor: isDark ? AppColors.surfaceDark : const Color(0xFFE2E8F0),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
                    onSelected: (_) => setState(() => _selectedType = t),
                  );
                },
              ),
            ),
            const SizedBox(height: 16),

            // Body State Rendering
            if (_loading) ...[
              const Center(
                child: Padding(
                  padding: EdgeInsets.symmetric(vertical: 40.0),
                  child: CircularProgressIndicator(),
                ),
              ),
            ] else if (_error != null) ...[
              Center(
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 40.0, horizontal: 20.0),
                  child: Column(
                    children: [
                      const Icon(Icons.error_outline_rounded, size: 40, color: AppColors.danger),
                      const SizedBox(height: 12),
                      Text(
                        _error!,
                        textAlign: TextAlign.center,
                        style: const TextStyle(color: Colors.grey, fontSize: 13),
                      ),
                      const SizedBox(height: 16),
                      ElevatedButton.icon(
                        onPressed: _fetchRecords,
                        icon: const Icon(Icons.refresh, size: 16),
                        label: const Text('Try Again'),
                      ),
                    ],
                  ),
                ),
              ),
            ] else if (_filteredRecords.isEmpty) ...[
              Center(
                child: Padding(
                  padding: const EdgeInsets.symmetric(vertical: 60.0),
                  child: Column(
                    children: [
                      Icon(Icons.folder_open_rounded, size: 48, color: Colors.grey.withValues(alpha: 0.5)),
                      const SizedBox(height: 12),
                      const Text(
                        'No medical records found',
                        style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        _searchQuery.isNotEmpty
                            ? 'No matches for "$_searchQuery"'
                            : 'No medical records have been recorded yet.',
                        style: const TextStyle(color: Colors.grey, fontSize: 13),
                      ),
                      if (_isAdminOrDoctor) ...[
                        const SizedBox(height: 16),
                        ElevatedButton.icon(
                          onPressed: _openAddRecordDialog,
                          icon: const Icon(Icons.add_rounded, size: 18),
                          label: const Text('Create First Record'),
                          style: ElevatedButton.styleFrom(
                            backgroundColor: AppColors.primary,
                            foregroundColor: Colors.white,
                            padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 12),
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
            ] else ...[
              // Summary counter
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    '${_filteredRecords.length} ${_filteredRecords.length == 1 ? 'Record' : 'Records'} Found',
                    style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Colors.grey),
                  ),
                  if (_isAdminOrDoctor)
                    TextButton.icon(
                      onPressed: _openAddRecordDialog,
                      icon: const Icon(Icons.add_circle_outline_rounded, size: 16),
                      label: const Text('New Record', style: TextStyle(fontSize: 12, fontWeight: FontWeight.bold)),
                    ),
                ],
              ),
              const SizedBox(height: 8),

              // Cards List
              ListView.separated(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: _filteredRecords.length,
                separatorBuilder: (_, __) => const SizedBox(height: 12),
                itemBuilder: (context, index) {
                  final rec = _filteredRecords[index];
                  final typeColor = _getTypeColor(rec.recordType);

                  return InkWell(
                    borderRadius: BorderRadius.circular(14),
                    onTap: () {
                      Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (_) => MedicalRecordDetailScreen(
                            record: rec,
                            onRecordUpdated: _fetchRecords,
                          ),
                        ),
                      );
                    },
                    child: Container(
                      padding: const EdgeInsets.all(16),
                      decoration: BoxDecoration(
                        color: isDark ? AppColors.surfaceDark : Colors.white,
                        borderRadius: BorderRadius.circular(14),
                        border: Border.all(
                          color: isDark ? AppColors.borderDark : AppColors.borderLight,
                        ),
                        boxShadow: [
                          BoxShadow(
                            color: Colors.black.withValues(alpha: 0.03),
                            blurRadius: 8,
                            offset: const Offset(0, 3),
                          ),
                        ],
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                                decoration: BoxDecoration(
                                  color: typeColor.withValues(alpha: 0.12),
                                  borderRadius: BorderRadius.circular(6),
                                ),
                                child: Text(
                                  rec.recordType,
                                  style: TextStyle(
                                    color: typeColor,
                                    fontSize: 11,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ),
                              Text(
                                DateFormat('MMM dd, yyyy').format(rec.recordDate),
                                style: const TextStyle(color: Colors.grey, fontSize: 12),
                              ),
                            ],
                          ),
                          const SizedBox(height: 10),
                          if (_isAdminOrDoctor && rec.patientName.isNotEmpty) ...[
                            Row(
                              children: [
                                const Icon(Icons.person_rounded, size: 14, color: AppColors.primary),
                                const SizedBox(width: 4),
                                Text(
                                  rec.patientName,
                                  style: const TextStyle(
                                    fontSize: 13,
                                    fontWeight: FontWeight.bold,
                                    color: AppColors.primary,
                                  ),
                                ),
                              ],
                            ),
                            const SizedBox(height: 4),
                          ],
                          Text(
                            rec.diagnosis,
                            style: TextStyle(
                              fontSize: 15,
                              fontWeight: FontWeight.w800,
                              color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                            ),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            rec.treatmentPlan,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              fontSize: 13,
                              color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
                              height: 1.3,
                            ),
                          ),
                          const SizedBox(height: 12),
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Row(
                                children: [
                                  const Icon(Icons.medical_services_outlined, size: 15, color: Colors.grey),
                                  const SizedBox(width: 4),
                                  Text(
                                    rec.doctorName ?? 'Hospital Clinician',
                                    style: const TextStyle(fontSize: 12, color: Colors.grey),
                                  ),
                                ],
                              ),
                              if (rec.attachments.isNotEmpty)
                                Row(
                                  children: [
                                    const Icon(Icons.attach_file_rounded, size: 14, color: AppColors.primary),
                                    const SizedBox(width: 2),
                                    Text(
                                      '${rec.attachments.length} file${rec.attachments.length == 1 ? '' : 's'}',
                                      style: const TextStyle(
                                        fontSize: 11,
                                        fontWeight: FontWeight.bold,
                                        color: AppColors.primary,
                                      ),
                                    ),
                                  ],
                                ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  );
                },
              ),
            ],
            const SizedBox(height: 80),
          ],
        ),
      ),
    );

    return Scaffold(
      backgroundColor: Colors.transparent,
      appBar: widget.embedded
          ? null
          : AppBar(
              title: const Text('Medical Records'),
              actions: [
                if (_isAdminOrDoctor)
                  IconButton(
                    icon: const Icon(Icons.add_rounded),
                    onPressed: _openAddRecordDialog,
                    tooltip: 'Add Medical Record',
                  ),
              ],
            ),
      body: content,
      floatingActionButton: _isAdminOrDoctor
          ? FloatingActionButton.extended(
              onPressed: _openAddRecordDialog,
              backgroundColor: AppColors.primary,
              icon: const Icon(Icons.add_rounded, color: Colors.white),
              label: const Text(
                'Add Record',
                style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold),
              ),
            )
          : null,
    );
  }
}
