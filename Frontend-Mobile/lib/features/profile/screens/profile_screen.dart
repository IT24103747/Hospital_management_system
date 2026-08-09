import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
import 'package:smartcare_mobile/models/patient.dart';
import 'dart:io';

class ProfileScreen extends StatefulWidget {
  const ProfileScreen({super.key});

  @override
  State<ProfileScreen> createState() => _ProfileScreenState();
}

class _ProfileScreenState extends State<ProfileScreen> {
  final ImagePicker _picker = ImagePicker();
  XFile? _idCardImage;
  Patient? _patient;
  bool _isLoading = true;

  String _userName = 'Patient';
  String _userEmail = '';
  String _userInitials = 'P';

  @override
  void initState() {
    super.initState();
    _loadProfileData();
  }

  Future<void> _loadProfileData() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final name = prefs.getString('patient_full_name');
      final email = prefs.getString('patient_email');

      // Derive initials & display name from session
      if (name != null && name.trim().isNotEmpty) {
        final parts = name.trim().split(' ');
        String initials = parts.first[0].toUpperCase();
        if (parts.length > 1) {
          initials += parts.last[0].toUpperCase();
        }
        setState(() {
          _userName = name;
          _userEmail = email ?? '';
          _userInitials = initials;
        });
      }

      // Fetch correct patient record from backend by email (email is unique per patient)
      if (email != null && email.trim().isNotEmpty) {
        final fetched = await ApiService.getPatientByEmail(email);
        if (fetched != null && mounted) {
          final parts = '${fetched.firstName} ${fetched.lastName}'.trim().split(' ');
          String initials = parts.first[0].toUpperCase();
          if (parts.length > 1) initials += parts.last[0].toUpperCase();
          setState(() {
            _patient = fetched;
            _userName = '${fetched.firstName} ${fetched.lastName}';
            _userInitials = initials;
          });
        }
      }
    } catch (_) {
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  void _pickIdCardImage(ImageSource source) async {
    try {
      final picked = await _picker.pickImage(source: source);
      if (picked != null) {
        setState(() {
          _idCardImage = picked;
        });
        if (mounted) {
          ScaffoldMessenger.of(context).showSnackBar(
            SnackBar(
              content: const Row(
                children: [
                  Icon(Icons.check_circle_rounded, color: Colors.white),
                  SizedBox(width: 10),
                  Text('ID/Insurance Card captured successfully!'),
                ],
              ),
              backgroundColor: AppColors.success,
              behavior: SnackBarBehavior.floating,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            ),
          );
        }
      }
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(content: Text('Camera picker info: $e')),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: AppColors.primary))
          : SingleChildScrollView(
              physics: const BouncingScrollPhysics(),
              padding: const EdgeInsets.all(20.0),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  // Profile Header Avatar Box
                  Container(
                    padding: const EdgeInsets.all(4),
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      border: Border.all(color: AppColors.primary, width: 3),
                    ),
                    child: CircleAvatar(
                      radius: 46,
                      backgroundColor: AppColors.primary,
                      child: Text(
                        _userInitials,
                        style: const TextStyle(
                          fontSize: 32,
                          fontWeight: FontWeight.bold,
                          color: Colors.white,
                        ),
                      ),
                    ),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    _patient != null ? '${_patient!.firstName} ${_patient!.lastName}' : _userName,
                    style: TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.bold,
                      color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    _userEmail.isNotEmpty ? _userEmail : 'Patient Portal User',
                    style: const TextStyle(color: AppColors.textSecondaryLight, fontSize: 13),
                  ),
                  const SizedBox(height: 12),

                  // Pill Badges for NIC & Blood Group
                  Row(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                        decoration: BoxDecoration(
                          color: AppColors.primary.withValues(alpha: 0.12),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Row(
                          children: [
                            const Icon(Icons.badge_outlined, size: 16, color: AppColors.primary),
                            const SizedBox(width: 6),
                            Text(
                              'NIC: ${_patient?.nic ?? '199512345678'}',
                              style: const TextStyle(
                                color: AppColors.primary,
                                fontWeight: FontWeight.bold,
                                fontSize: 12,
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 8),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                        decoration: BoxDecoration(
                          color: AppColors.danger.withValues(alpha: 0.12),
                          borderRadius: BorderRadius.circular(20),
                        ),
                        child: Row(
                          children: [
                            const Icon(Icons.water_drop_rounded, size: 16, color: AppColors.danger),
                            const SizedBox(width: 6),
                            Text(
                              'Blood: ${_patient?.bloodGroup ?? 'O+'}',
                              style: const TextStyle(
                                color: AppColors.danger,
                                fontWeight: FontWeight.bold,
                                fontSize: 12,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 24),

                  // Personal Medical Records Card
                  Card(
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                      side: BorderSide(color: isDark ? AppColors.borderDark : AppColors.borderLight),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Row(
                            children: [
                              Icon(Icons.folder_shared_outlined, color: AppColors.primary, size: 20),
                              SizedBox(width: 8),
                              Text(
                                'Personal Identity & Details',
                                style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15),
                              ),
                            ],
                          ),
                          const Divider(height: 24),
                          _buildProfileRow('Phone Number:', _patient?.phoneNumber ?? '+94 77 123 4567', isDark),
                          const SizedBox(height: 10),
                          _buildProfileRow('Gender:', _patient?.gender ?? 'Male', isDark),
                          const SizedBox(height: 10),
                          _buildProfileRow('Address:', _patient?.address ?? '45 Galle Rd, Colombo 03', isDark),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // Emergency Contact Card
                  Card(
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                      side: BorderSide(color: isDark ? AppColors.borderDark : AppColors.borderLight),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Row(
                            children: [
                              Icon(Icons.phone_in_talk_rounded, color: AppColors.danger, size: 20),
                              SizedBox(width: 8),
                              Text(
                                'Emergency Contact',
                                style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15),
                              ),
                            ],
                          ),
                          const Divider(height: 24),
                          _buildProfileRow(
                            'Contact Person:',
                            (_patient?.emergencyContactName != null && _patient!.emergencyContactName!.isNotEmpty)
                                ? _patient!.emergencyContactName!
                                : 'Primary Emergency Contact',
                            isDark,
                          ),
                          const SizedBox(height: 10),
                          _buildProfileRow(
                            'Phone:',
                            (_patient?.emergencyContactPhone != null && _patient!.emergencyContactPhone!.isNotEmpty)
                                ? _patient!.emergencyContactPhone!
                                : '+94 71 234 5678',
                            isDark,
                            isPrimaryText: true,
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 16),

                  // ID / Insurance Camera Scanner Card
                  Card(
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(16),
                      side: BorderSide(color: isDark ? AppColors.borderDark : AppColors.borderLight),
                    ),
                    child: Padding(
                      padding: const EdgeInsets.all(16.0),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Row(
                            children: [
                              Icon(Icons.camera_alt_outlined, color: AppColors.primary, size: 20),
                              SizedBox(width: 8),
                              Text(
                                'Camera ID & Insurance Scanner',
                                style: TextStyle(fontWeight: FontWeight.bold, fontSize: 15),
                              ),
                            ],
                          ),
                          const SizedBox(height: 6),
                          const Text(
                            'Scan your NIC or Health Insurance card using your phone camera for direct hospital verification.',
                            style: TextStyle(fontSize: 12, color: AppColors.textSecondaryLight),
                          ),
                          const SizedBox(height: 16),

                          if (_idCardImage != null)
                            ClipRRect(
                              borderRadius: BorderRadius.circular(12),
                              child: Image.file(
                                File(_idCardImage!.path),
                                height: 160,
                                width: double.infinity,
                                fit: BoxFit.cover,
                              ),
                            )
                          else
                            Container(
                              height: 110,
                              width: double.infinity,
                              decoration: BoxDecoration(
                                color: isDark ? AppColors.surfaceDarkSecondary : Colors.grey.shade100,
                                borderRadius: BorderRadius.circular(12),
                                border: Border.all(
                                  color: isDark ? AppColors.borderDark : AppColors.borderLight,
                                ),
                              ),
                              child: const Column(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  Icon(Icons.badge_outlined, size: 36, color: AppColors.textSecondaryLight),
                                  SizedBox(height: 6),
                                  Text(
                                    'No ID card scanned yet',
                                    style: TextStyle(fontSize: 12, color: AppColors.textSecondaryLight),
                                  ),
                                ],
                              ),
                            ),
                          const SizedBox(height: 16),

                          Row(
                            children: [
                              Expanded(
                                child: OutlinedButton.icon(
                                  onPressed: () => _pickIdCardImage(ImageSource.camera),
                                  icon: const Icon(Icons.photo_camera),
                                  label: const Text('Camera'),
                                  style: OutlinedButton.styleFrom(
                                    padding: const EdgeInsets.symmetric(vertical: 12),
                                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                                  ),
                                ),
                              ),
                              const SizedBox(width: 12),
                              Expanded(
                                child: OutlinedButton.icon(
                                  onPressed: () => _pickIdCardImage(ImageSource.gallery),
                                  icon: const Icon(Icons.photo_library),
                                  label: const Text('Gallery'),
                                  style: OutlinedButton.styleFrom(
                                    padding: const EdgeInsets.symmetric(vertical: 12),
                                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 24),
                ],
              ),
            ),
    );
  }

  Widget _buildProfileRow(String label, String value, bool isDark, {bool isPrimaryText = false}) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(color: AppColors.textSecondaryLight, fontSize: 13),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: Text(
            value,
            textAlign: TextAlign.right,
            style: TextStyle(
              fontWeight: FontWeight.w600,
              fontSize: 13,
              color: isPrimaryText
                  ? AppColors.primary
                  : (isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight),
            ),
          ),
        ),
      ],
    );
  }
}
