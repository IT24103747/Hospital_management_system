import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
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

  final List<String> _types = ['All', 'Consultation', 'LabReport', 'Prescription', 'DischargeSummary'];

  @override
  void initState() {
    super.initState();
    _fetchRecords();
  }

  Future<void> _fetchRecords() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final data = await ApiService.getMyMedicalRecords();
      if (!mounted) return;
      setState(() {
        _records = data;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error = 'Unable to load your medical records. Please pull down to refresh.';
        _loading = false;
      });
    }
  }

  List<MedicalRecord> get _filteredRecords {
    return _records.where((rec) {
      final matchesType = _selectedType == 'All' || rec.recordType == _selectedType;
      final query = _searchQuery.trim().toLowerCase();
      final matchesSearch = query.isEmpty ||
          rec.diagnosis.toLowerCase().contains(query) ||
          rec.symptoms.toLowerCase().contains(query) ||
          (rec.doctorName != null && rec.doctorName!.toLowerCase().contains(query));
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
            // Search Input
            TextField(
              decoration: InputDecoration(
                hintText: 'Search diagnoses, symptoms, doctors...',
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
                    label: Text(t == 'LabReport' ? 'Lab Reports' : t == 'DischargeSummary' ? 'Discharge' : t),
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
                      const Icon(Icons.error_outline_rounded, size: 40, color: AppColors.error),
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
                            : 'You do not have any recorded medical history yet.',
                        style: const TextStyle(color: Colors.grey, fontSize: 13),
                      ),
                    ],
                  ),
                ),
              ),
            ] else ...[
              // Summary counter
              Text(
                '${_filteredRecords.length} ${_filteredRecords.length == 1 ? 'Record' : 'Records'} Found',
                style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Colors.grey),
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
                                  const Icon(Icons.person_outline_rounded, size: 15, color: Colors.grey),
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
            const SizedBox(height: 20),
          ],
        ),
      ),
    );

    if (widget.embedded) {
      return content;
    }

    return Scaffold(
      appBar: AppBar(
        title: const Text('My Medical Records'),
      ),
      body: content,
    );
  }
}
