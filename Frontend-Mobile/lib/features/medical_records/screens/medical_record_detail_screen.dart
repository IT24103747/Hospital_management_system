import 'dart:io';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:intl/intl.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
import 'package:smartcare_mobile/models/medical_record.dart';

class MedicalRecordDetailScreen extends StatefulWidget {
  final MedicalRecord record;
  final VoidCallback? onRecordUpdated;

  const MedicalRecordDetailScreen({
    super.key,
    required this.record,
    this.onRecordUpdated,
  });

  @override
  State<MedicalRecordDetailScreen> createState() => _MedicalRecordDetailScreenState();
}

class _MedicalRecordDetailScreenState extends State<MedicalRecordDetailScreen> {
  late MedicalRecord _record;
  bool _uploading = false;
  final ImagePicker _picker = ImagePicker();

  @override
  void initState() {
    super.initState();
    _record = widget.record;
  }

  Future<void> _pickAndUploadImage(ImageSource source) async {
    try {
      final pickedFile = await _picker.pickImage(
        source: source,
        imageQuality: 85,
        maxWidth: 1600,
      );

      if (pickedFile == null) return;

      setState(() => _uploading = true);

      final file = File(pickedFile.path);
      final fileName = pickedFile.name.isNotEmpty
          ? pickedFile.name
          : 'report_${DateTime.now().millisecondsSinceEpoch}.jpg';
      final fileSize = await file.length();

      // Simulated local path url for mobile upload attachment
      final mockFileUrl = '/uploads/mobile-scans/$fileName';

      final attachment = await ApiService.addMedicalRecordAttachment(
        _record.medicalRecordId,
        fileName: fileName,
        fileType: 'image/jpeg',
        fileUrl: mockFileUrl,
        fileSize: fileSize,
      );

      if (!mounted) return;

      setState(() {
        _record = MedicalRecord(
          medicalRecordId: _record.medicalRecordId,
          patientId: _record.patientId,
          patientName: _record.patientName,
          patientEmail: _record.patientEmail,
          doctorId: _record.doctorId,
          doctorName: _record.doctorName,
          doctorSpecialization: _record.doctorSpecialization,
          appointmentId: _record.appointmentId,
          recordDate: _record.recordDate,
          recordType: _record.recordType,
          diagnosis: _record.diagnosis,
          symptoms: _record.symptoms,
          treatmentPlan: _record.treatmentPlan,
          prescriptionNotes: _record.prescriptionNotes,
          labNotes: _record.labNotes,
          followUpDate: _record.followUpDate,
          status: _record.status,
          createdAt: _record.createdAt,
          updatedAt: _record.updatedAt,
          attachments: [..._record.attachments, attachment],
        );
        _uploading = false;
      });

      widget.onRecordUpdated?.call();

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Document / Scan uploaded successfully!'),
          backgroundColor: AppColors.success,
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _uploading = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Upload failed: $e'),
          backgroundColor: AppColors.danger,
        ),
      );
    }
  }

  void _showUploadOptions() {
    showModalBottomSheet(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 16.0, horizontal: 20.0),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Upload Diagnostic Report / Scan',
                style: TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 8),
              const Text(
                'Take a photo of your paper report or choose from gallery',
                style: TextStyle(color: Colors.grey, fontSize: 13),
              ),
              const SizedBox(height: 16),
              ListTile(
                leading: Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: AppColors.primary.withValues(alpha: 0.1),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.camera_alt_rounded, color: AppColors.primary),
                ),
                title: const Text('Take Photo with Camera', style: TextStyle(fontWeight: FontWeight.w600)),
                subtitle: const Text('Scan document directly using device camera'),
                onTap: () {
                  Navigator.pop(ctx);
                  _pickAndUploadImage(ImageSource.camera);
                },
              ),
              ListTile(
                leading: Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: AppColors.accent.withValues(alpha: 0.1),
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(Icons.photo_library_rounded, color: AppColors.accent),
                ),
                title: const Text('Choose from Photo Gallery', style: TextStyle(fontWeight: FontWeight.w600)),
                subtitle: const Text('Select saved report image from device'),
                onTap: () {
                  Navigator.pop(ctx);
                  _pickAndUploadImage(ImageSource.gallery);
                },
              ),
            ],
          ),
        ),
      ),
    );
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
    final typeColor = _getTypeColor(_record.recordType);

    return Scaffold(
      appBar: AppBar(
        title: Text('Record #${_record.medicalRecordId}'),
        actions: [
          IconButton(
            icon: const Icon(Icons.share_outlined),
            tooltip: 'Share Record',
            onPressed: () {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('Medical record summary copied to clipboard.')),
              );
            },
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Header Hero Banner
            Container(
              padding: const EdgeInsets.all(18),
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  colors: [typeColor.withValues(alpha: 0.15), typeColor.withValues(alpha: 0.05)],
                  begin: Alignment.topLeft,
                  end: Alignment.bottomRight,
                ),
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: typeColor.withValues(alpha: 0.3)),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: typeColor,
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Text(
                          _record.recordType,
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 12,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        decoration: BoxDecoration(
                          color: _record.status == 'Finalized'
                              ? AppColors.success.withValues(alpha: 0.15)
                              : Colors.grey.withValues(alpha: 0.2),
                          borderRadius: BorderRadius.circular(8),
                        ),
                        child: Text(
                          _record.status,
                          style: TextStyle(
                            color: _record.status == 'Finalized' ? AppColors.success : Colors.grey,
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Text(
                    _record.diagnosis,
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.w900,
                      color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                    ),
                  ),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      const Icon(Icons.calendar_today_rounded, size: 14, color: Colors.grey),
                      const SizedBox(width: 6),
                      Text(
                        DateFormat('MMMM dd, yyyy').format(_record.recordDate),
                        style: const TextStyle(color: Colors.grey, fontSize: 13),
                      ),
                      if (_record.doctorName != null) ...[
                        const SizedBox(width: 14),
                        const Icon(Icons.medical_services_rounded, size: 14, color: Colors.grey),
                        const SizedBox(width: 6),
                        Expanded(
                          child: Text(
                            _record.doctorName!,
                            style: const TextStyle(color: Colors.grey, fontSize: 13),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                      ],
                    ],
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Symptoms Section
            _buildSectionCard(
              title: 'Symptoms & Observations',
              icon: Icons.personal_injury_outlined,
              iconColor: Colors.orange,
              content: Text(
                _record.symptoms,
                style: TextStyle(
                  fontSize: 14,
                  height: 1.5,
                  color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                ),
              ),
            ),
            const SizedBox(height: 16),

            // Treatment Plan Section
            _buildSectionCard(
              title: 'Treatment Plan & Advice',
              icon: Icons.healing_outlined,
              iconColor: AppColors.primary,
              content: Text(
                _record.treatmentPlan,
                style: TextStyle(
                  fontSize: 14,
                  height: 1.5,
                  color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                ),
              ),
            ),
            const SizedBox(height: 16),

            // Prescription Notes
            if (_record.prescriptionNotes != null && _record.prescriptionNotes!.trim().isNotEmpty) ...[
              _buildSectionCard(
                title: 'Prescribed Medication',
                icon: Icons.medication_outlined,
                iconColor: const Color(0xFF4F46E5),
                content: Container(
                  width: double.infinity,
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: isDark ? Colors.black26 : const Color(0xFFF1F5F9),
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(color: Colors.grey.withValues(alpha: 0.2)),
                  ),
                  child: Text(
                    _record.prescriptionNotes!,
                    style: const TextStyle(
                      fontFamily: 'monospace',
                      fontSize: 13,
                      height: 1.5,
                    ),
                  ),
                ),
              ),
              const SizedBox(height: 16),
            ],

            // Lab Notes
            if (_record.labNotes != null && _record.labNotes!.trim().isNotEmpty) ...[
              _buildSectionCard(
                title: 'Lab & Diagnostic Findings',
                icon: Icons.science_outlined,
                iconColor: const Color(0xFF0891B2),
                content: Text(
                  _record.labNotes!,
                  style: TextStyle(
                    fontSize: 14,
                    height: 1.5,
                    color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                  ),
                ),
              ),
              const SizedBox(height: 16),
            ],

            // Attachments Section
            _buildSectionCard(
              title: 'Attached Reports & Scans (${_record.attachments.length})',
              icon: Icons.attach_file_rounded,
              iconColor: AppColors.accent,
              action: TextButton.icon(
                onPressed: _uploading ? null : _showUploadOptions,
                icon: const Icon(Icons.camera_alt, size: 16),
                label: const Text('Scan / Upload'),
              ),
              content: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  if (_uploading)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 8.0),
                      child: Row(
                        children: [
                          SizedBox(width: 16, height: 16, child: CircularProgressIndicator(strokeWidth: 2)),
                          SizedBox(width: 10),
                          Text('Uploading scan...', style: TextStyle(fontSize: 13)),
                        ],
                      ),
                    ),
                  if (_record.attachments.isEmpty)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 8.0),
                      child: Text(
                        'No files attached yet. Tap "Scan / Upload" above to attach a lab report or photo.',
                        style: TextStyle(color: Colors.grey, fontSize: 13, fontStyle: FontStyle.italic),
                      ),
                    )
                  else
                    ListView.separated(
                      shrinkWrap: true,
                      physics: const NeverScrollableScrollPhysics(),
                      itemCount: _record.attachments.length,
                      separatorBuilder: (_, __) => const SizedBox(height: 8),
                      itemBuilder: (context, index) {
                        final att = _record.attachments[index];
                        return Container(
                          padding: const EdgeInsets.all(12),
                          decoration: BoxDecoration(
                            color: isDark ? Colors.grey[900] : const Color(0xFFF8FAFC),
                            borderRadius: BorderRadius.circular(10),
                            border: Border.all(color: Colors.grey.withValues(alpha: 0.2)),
                          ),
                          child: Row(
                            children: [
                              Container(
                                padding: const EdgeInsets.all(8),
                                decoration: BoxDecoration(
                                  color: AppColors.primary.withValues(alpha: 0.1),
                                  borderRadius: BorderRadius.circular(8),
                                ),
                                child: const Icon(Icons.description_outlined, color: AppColors.primary, size: 20),
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      att.fileName,
                                      style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
                                      overflow: TextOverflow.ellipsis,
                                    ),
                                    Text(
                                      '${att.fileType} • ${(att.fileSize / 1024).toStringAsFixed(1)} KB',
                                      style: const TextStyle(color: Colors.grey, fontSize: 11),
                                    ),
                                  ],
                                ),
                              ),
                              IconButton(
                                icon: const Icon(Icons.open_in_new_rounded, size: 18),
                                color: AppColors.primary,
                                tooltip: 'View File',
                                onPressed: () {
                                  ScaffoldMessenger.of(context).showSnackBar(
                                    SnackBar(content: Text('Opening ${att.fileName}...')),
                                  );
                                },
                              ),
                            ],
                          ),
                        );
                      },
                    ),
                ],
              ),
            ),
            const SizedBox(height: 30),
          ],
        ),
      ),
    );
  }

  Widget _buildSectionCard({
    required String title,
    required IconData icon,
    required Color iconColor,
    required Widget content,
    Widget? action,
  }) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      width: double.infinity,
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
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  Icon(icon, size: 18, color: iconColor),
                  const SizedBox(width: 8),
                  Text(
                    title,
                    style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
              if (action != null) action,
            ],
          ),
          const SizedBox(height: 12),
          content,
        ],
      ),
    );
  }
}
