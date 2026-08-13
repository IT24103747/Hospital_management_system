import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
import 'package:smartcare_mobile/core/theme/theme_controller.dart';
import 'package:smartcare_mobile/core/widgets/medicore_logo.dart';
import 'package:smartcare_mobile/features/auth/screens/login_screen.dart';
import 'package:smartcare_mobile/features/profile/screens/profile_screen.dart';

enum _PatientSection {
  home,
  profile,
  appointments,
  doctors,
  records,
  assistant,
  notifications,
  settings,
  support,
}

class DashboardLayout extends StatefulWidget {
  final Widget? body;
  final String? title;
  final String? subtitle;

  const DashboardLayout({
    super.key,
    this.body,
    this.title,
    this.subtitle,
  });

  @override
  State<DashboardLayout> createState() => _DashboardLayoutState();
}

class _DashboardLayoutState extends State<DashboardLayout> {
  _PatientSection _activeSection = _PatientSection.home;
  String _userName = 'Patient User';
  String _userEmail = 'patient@medicore.lk';
  String _userInitials = 'PU';

  @override
  void initState() {
    super.initState();
    _activeSection = _sectionFromTitle(widget.title);
    _loadUserSession();
  }

  Future<void> _loadUserSession() async {
    final prefs = await SharedPreferences.getInstance();
    final name = prefs.getString('patient_full_name');
    final email = prefs.getString('patient_email');
    if (!mounted) return;

    final resolvedName = name?.trim().isNotEmpty == true ? name!.trim() : _userName;
    setState(() {
      _userName = resolvedName;
      _userEmail = email?.trim().isNotEmpty == true ? email!.trim() : _userEmail;
      _userInitials = _initialsFor(resolvedName);
    });
  }

  _PatientSection _sectionFromTitle(String? title) {
    switch (title) {
      case 'My Profile':
      case 'Patient Profile':
        return _PatientSection.profile;
      case 'Appointments':
        return _PatientSection.appointments;
      case 'Doctors':
        return _PatientSection.doctors;
      case 'Medical Records':
        return _PatientSection.records;
      case 'AI Health Assistant':
      case 'AI Smart Triage':
        return _PatientSection.assistant;
      case 'Notifications':
        return _PatientSection.notifications;
      case 'Settings':
        return _PatientSection.settings;
      case 'Help & Support':
        return _PatientSection.support;
      default:
        return _PatientSection.home;
    }
  }

  String get _title => switch (_activeSection) {
        _PatientSection.home => 'Home',
        _PatientSection.profile => 'My Profile',
        _PatientSection.appointments => 'Appointments',
        _PatientSection.doctors => 'Doctors',
        _PatientSection.records => 'Medical Records',
        _PatientSection.assistant => 'AI Health Assistant',
        _PatientSection.notifications => 'Notifications',
        _PatientSection.settings => 'Settings',
        _PatientSection.support => 'Help & Support',
      };

  String get _subtitle => switch (_activeSection) {
        _PatientSection.home => 'Your patient dashboard',
        _PatientSection.profile => 'Personal and emergency information',
        _PatientSection.appointments => 'Book, reschedule, cancel, and view visits',
        _PatientSection.doctors => 'Find specialists and available schedules',
        _PatientSection.records => 'History, reports, prescriptions, and notes',
        _PatientSection.assistant => 'Simple explanations and guided health questions',
        _PatientSection.notifications => 'Hospital updates and appointment reminders',
        _PatientSection.settings => 'Account, privacy, language, and security',
        _PatientSection.support => 'FAQs, hospital contact, and technical help',
      };

  Widget get _content => switch (_activeSection) {
        _PatientSection.home => _HomeSection(userName: _userName),
        _PatientSection.profile => const ProfileScreen(),
        _PatientSection.appointments => const _AppointmentsSection(),
        _PatientSection.doctors => const _DoctorsSection(),
        _PatientSection.records => const _MedicalRecordsSection(),
        _PatientSection.assistant => const _AssistantSection(),
        _PatientSection.notifications => const _NotificationsSection(),
        _PatientSection.settings => const _SettingsSection(),
        _PatientSection.support => const _SupportSection(),
      };

  void _selectSection(_PatientSection section, {bool closeDrawer = false}) {
    setState(() => _activeSection = section);
    if (closeDrawer) {
      Navigator.maybePop(context);
    }
  }

  Future<void> _logout() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('patient_user_id');
    await prefs.remove('patient_full_name');
    await prefs.remove('patient_email');
    await prefs.remove('hms_token');
    if (!mounted) return;
    Navigator.pushReplacement(
      context,
      MaterialPageRoute(builder: (context) => const LoginScreen()),
    );
  }

  @override
  Widget build(BuildContext context) {
    final size = MediaQuery.of(context).size;
    final isDesktop = size.width >= 900;
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      backgroundColor: isDark ? AppColors.bgDark : AppColors.bgLight,
      drawer: isDesktop ? null : Drawer(width: 286, child: _sidebar(isDesktop: false)),
      body: Row(
        children: [
          if (isDesktop) _sidebar(isDesktop: true),
          Expanded(
            child: Column(
              children: [
                _topBar(isDesktop: isDesktop),
                Expanded(child: _content),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _topBar({required bool isDesktop}) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      height: 76,
      padding: const EdgeInsets.symmetric(horizontal: 16),
      decoration: BoxDecoration(
        color: isDark ? AppColors.surfaceDark : AppColors.bgLightCard,
        border: Border(
          bottom: BorderSide(color: isDark ? AppColors.borderDark : AppColors.borderLight),
        ),
      ),
      child: Row(
        children: [
          if (!isDesktop)
            Builder(
              builder: (context) => IconButton.filledTonal(
                onPressed: () => Scaffold.of(context).openDrawer(),
                icon: const Icon(Icons.menu_rounded),
              ),
            ),
          if (!isDesktop) const SizedBox(width: 10),
          Expanded(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  _title,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                    fontSize: 18,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  _subtitle,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ),
          _HeaderIconButton(
            icon: Icons.notifications_none_rounded,
            onTap: () => _selectSection(_PatientSection.notifications),
          ),
          const SizedBox(width: 8),
          CircleAvatar(
            radius: 18,
            backgroundColor: AppColors.primary,
            child: Text(
              _userInitials,
              style: const TextStyle(color: Colors.white, fontSize: 12, fontWeight: FontWeight.w800),
            ),
          ),
        ],
      ),
    );
  }

  Widget _sidebar({required bool isDesktop}) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final headerPadding = isDesktop
        ? const EdgeInsets.fromLTRB(18, 18, 18, 20)
        : const EdgeInsets.fromLTRB(14, 12, 12, 12);
    final listPadding = isDesktop
        ? const EdgeInsets.fromLTRB(14, 18, 14, 18)
        : const EdgeInsets.fromLTRB(10, 10, 10, 10);
    final footerPadding = isDesktop
        ? const EdgeInsets.fromLTRB(14, 12, 14, 14)
        : const EdgeInsets.fromLTRB(10, 8, 10, 10);
    return Container(
      width: isDesktop ? 300 : null,
      color: isDark ? AppColors.surfaceDark : const Color(0xFFEAF1F8),
      child: SafeArea(
        child: Column(
          children: [
            Padding(
              padding: headerPadding,
              child: Row(
                children: [
                  MediCoreLogo(
                    size: isDesktop ? 48 : 40,
                    borderRadius: isDesktop ? 12 : 10,
                    showText: true,
                    fontSize: isDesktop ? 23 : 20,
                  ),
                  const Spacer(),
                  if (!isDesktop)
                    IconButton(
                      visualDensity: VisualDensity.compact,
                      onPressed: () => Navigator.pop(context),
                      icon: const Icon(Icons.close_rounded, size: 22),
                    ),
                ],
              ),
            ),
            Divider(color: isDark ? AppColors.borderDark : AppColors.borderLight),
            Expanded(
              child: ListView(
                physics: const BouncingScrollPhysics(),
                padding: listPadding,
                children: [
                  _sectionLabel('Main Menu'),
                  _navItem(Icons.home_rounded, 'Home', _PatientSection.home),
                  _navItem(Icons.person_outline_rounded, 'My Profile', _PatientSection.profile),
                  _navItem(Icons.calendar_month_outlined, 'Appointments', _PatientSection.appointments),
                  _navItem(Icons.medical_services_outlined, 'Doctors', _PatientSection.doctors),
                  _navItem(Icons.folder_copy_outlined, 'Medical Records', _PatientSection.records),
                  _navItem(Icons.psychology_alt_outlined, 'AI Health Assistant', _PatientSection.assistant),
                  const SizedBox(height: 12),
                  _sectionLabel('Account'),
                  _navItem(Icons.settings_outlined, 'Settings', _PatientSection.settings),
                  _navItem(Icons.help_outline_rounded, 'Help & Support', _PatientSection.support),
                ],
              ),
            ),
            Divider(color: isDark ? AppColors.borderDark : AppColors.borderLight),
            Padding(
              padding: footerPadding,
              child: Column(
                children: [
                  _logoutItem(),
                  SizedBox(height: isDesktop ? 12 : 8),
                  Container(
                    padding: EdgeInsets.all(isDesktop ? 12 : 10),
                    decoration: BoxDecoration(
                      color: isDark ? AppColors.surfaceDarkSecondary : AppColors.bgLightCard,
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(color: isDark ? AppColors.borderDark : AppColors.borderLight),
                    ),
                    child: Row(
                      children: [
                        CircleAvatar(
                          radius: isDesktop ? 20 : 17,
                          backgroundColor: AppColors.primary,
                          child: Text(
                            _userInitials,
                            style: TextStyle(
                              color: Colors.white,
                              fontSize: isDesktop ? 12 : 11,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                        ),
                        SizedBox(width: isDesktop ? 12 : 10),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                _userName,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(
                                  color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                                  fontSize: isDesktop ? 13 : 12,
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                              const SizedBox(height: 2),
                              Text(
                                _userEmail,
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: TextStyle(
                                  color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
                                  fontSize: isDesktop ? 11 : 10,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _sectionLabel(String label) {
    final isDesktop = MediaQuery.of(context).size.width >= 900;
    return Padding(
      padding: EdgeInsets.fromLTRB(isDesktop ? 14 : 12, 0, 12, isDesktop ? 8 : 5),
      child: Text(
        label.toUpperCase(),
        style: TextStyle(
          color: AppColors.textMutedLight,
          fontSize: isDesktop ? 11 : 10,
          fontWeight: FontWeight.w800,
          letterSpacing: 1.2,
        ),
      ),
    );
  }

  Widget _navItem(IconData icon, String label, _PatientSection section) {
    final isSelected = _activeSection == section;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final isDesktop = MediaQuery.of(context).size.width >= 900;
    final color = isSelected
        ? AppColors.primary
        : (isDark ? AppColors.textSecondaryDark : AppColors.textSecondaryLight);
    return Padding(
      padding: EdgeInsets.only(bottom: isDesktop ? 6 : 3),
      child: Material(
        color: isSelected ? AppColors.primary.withValues(alpha: isDark ? 0.18 : 0.12) : Colors.transparent,
        borderRadius: BorderRadius.circular(isDesktop ? 12 : 10),
        child: InkWell(
          borderRadius: BorderRadius.circular(isDesktop ? 12 : 10),
          onTap: () => _selectSection(section, closeDrawer: true),
          child: Container(
            constraints: BoxConstraints(minHeight: isDesktop ? 52 : 42),
            padding: EdgeInsets.symmetric(horizontal: isDesktop ? 14 : 12, vertical: isDesktop ? 10 : 7),
            decoration: BoxDecoration(
              border: isSelected
                  ? Border(left: BorderSide(color: AppColors.primary, width: isDesktop ? 4 : 3))
                  : null,
            ),
            child: Row(
              children: [
                Icon(icon, color: color, size: isDesktop ? 22 : 19),
                SizedBox(width: isDesktop ? 16 : 12),
                Expanded(
                  child: Text(
                    label,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: isSelected
                          ? AppColors.primary
                          : (isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight),
                      fontSize: isDesktop ? 15 : 13.5,
                      fontWeight: isSelected ? FontWeight.w800 : FontWeight.w600,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _logoutItem() {
    final isDesktop = MediaQuery.of(context).size.width >= 900;
    return Material(
      color: Colors.transparent,
      borderRadius: BorderRadius.circular(isDesktop ? 12 : 10),
      child: InkWell(
        borderRadius: BorderRadius.circular(isDesktop ? 12 : 10),
        onTap: _logout,
        child: Container(
          constraints: BoxConstraints(minHeight: isDesktop ? 52 : 42),
          padding: EdgeInsets.symmetric(horizontal: isDesktop ? 14 : 12, vertical: isDesktop ? 10 : 7),
          child: Row(
            children: [
              Icon(Icons.logout_rounded, color: AppColors.danger, size: isDesktop ? 22 : 19),
              SizedBox(width: isDesktop ? 16 : 12),
              Expanded(
                child: Text(
                  'Logout',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: AppColors.danger,
                    fontSize: isDesktop ? 15 : 13.5,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _HomeSection extends StatelessWidget {
  final String userName;

  const _HomeSection({required this.userName});

  @override
  Widget build(BuildContext context) {
    final firstName = userName.trim().split(' ').first;
    return _PageScaffold(
      children: [
        _HeroCard(
          title: 'Good morning, $firstName',
          subtitle: 'Your next appointment is Dr. Kasun Silva on Aug 15 at 10:30 AM.',
          icon: Icons.waving_hand_rounded,
          actions: const ['View Appointment', 'Book Appointment'],
        ),
        const SizedBox(height: 16),
        const _SectionTitle('Today'),
        const SizedBox(height: 12),
        const _FeatureGrid(
          tiles: [
            _FeatureTileData(Icons.medical_information_outlined, 'Doctor information', 'Cardiology consultation', AppColors.primary),
            _FeatureTileData(Icons.description_outlined, 'Recent report', 'Blood test uploaded', AppColors.success),
            _FeatureTileData(Icons.notifications_active_outlined, 'Notifications', '3 new updates', AppColors.warning),
            _FeatureTileData(Icons.psychology_alt_outlined, 'AI Assistant', 'Ask about reports', AppColors.accent),
          ],
        ),
        const SizedBox(height: 16),
        const _InfoPanel(
          icon: Icons.health_and_safety_outlined,
          title: 'Important health reminder',
          subtitle: 'Take prescribed medicine after breakfast and keep your appointment documents ready.',
          trailing: 'Today',
        ),
      ],
    );
  }
}

class _AppointmentsSection extends StatelessWidget {
  const _AppointmentsSection();

  @override
  Widget build(BuildContext context) {
    return const _PageScaffold(
      children: [
        _HeroCard(
          title: 'Book a doctor visit',
          subtitle: 'Select Doctor, choose Date, pick an Available Time, then Confirm Appointment.',
          icon: Icons.calendar_month_outlined,
          actions: ['Book Appointment', 'View History'],
        ),
        SizedBox(height: 16),
        _SectionTitle('Upcoming'),
        SizedBox(height: 12),
        _AppointmentCard(
          doctor: 'Dr. Kasun Silva',
          specialty: 'Cardiologist',
          date: 'Aug 15, 2026',
          time: '10:30 AM',
          status: 'Confirmed',
        ),
        SizedBox(height: 12),
        _AppointmentCard(
          doctor: 'Dr. Nilani Perera',
          specialty: 'General Medicine',
          date: 'Aug 22, 2026',
          time: '2:00 PM',
          status: 'Pending',
        ),
        SizedBox(height: 16),
        _SectionTitle('Appointment tools'),
        SizedBox(height: 12),
        _FeatureGrid(
          tiles: [
            _FeatureTileData(Icons.edit_calendar_outlined, 'Reschedule', 'Change date or time', AppColors.primary),
            _FeatureTileData(Icons.cancel_outlined, 'Cancel', 'Cancel safely', AppColors.danger),
            _FeatureTileData(Icons.person_search_outlined, 'View doctor', 'Profile and schedule', AppColors.accent),
            _FeatureTileData(Icons.history_rounded, 'History', 'Past visits', AppColors.success),
          ],
        ),
      ],
    );
  }
}

class _DoctorsSection extends StatelessWidget {
  const _DoctorsSection();

  @override
  Widget build(BuildContext context) {
    return const _PageScaffold(
      children: [
        _SearchBarCard(hint: 'Search doctors or specialization'),
        SizedBox(height: 14),
        _FilterChips(labels: ['All', 'Cardiology', 'General', 'Pediatrics', 'Orthopedics']),
        SizedBox(height: 16),
        _DoctorCard(
          name: 'Dr. Kasun Silva',
          specialty: 'Cardiologist',
          details: 'MBBS, MD Cardiology | 12 years experience',
          schedule: 'Available: Mon, Wed, Fri',
          rating: '4.8',
        ),
        SizedBox(height: 12),
        _DoctorCard(
          name: 'Dr. Nilani Perera',
          specialty: 'General Physician',
          details: 'MBBS, Family Medicine | 9 years experience',
          schedule: 'Available: Tue, Thu, Sat',
          rating: '4.7',
        ),
      ],
    );
  }
}

class _MedicalRecordsSection extends StatelessWidget {
  const _MedicalRecordsSection();

  @override
  Widget build(BuildContext context) {
    return const _PageScaffold(
      children: [
        _HeroCard(
          title: 'Your health record hub',
          subtitle: 'Medical history, reports, prescriptions, doctor notes, and uploaded documents stay together.',
          icon: Icons.folder_copy_outlined,
          actions: ['Upload Document', 'Share Summary'],
        ),
        SizedBox(height: 16),
        _RecordCategory(icon: Icons.history_edu_outlined, title: 'Medical History', items: ['Previous diagnoses', 'Previous treatments', 'Past visits']),
        _RecordCategory(icon: Icons.science_outlined, title: 'Lab Reports', items: ['Blood tests', 'X-rays', 'Scan reports', 'Results']),
        _RecordCategory(icon: Icons.medication_outlined, title: 'Prescriptions', items: ['Current medicines', 'Dosage', 'Instructions', 'Previous prescriptions']),
        _RecordCategory(icon: Icons.note_alt_outlined, title: 'Doctor Notes', items: ['Consultation notes', 'Treatment recommendations']),
        _RecordCategory(icon: Icons.file_copy_outlined, title: 'Documents', items: ['Uploaded documents', 'Medical certificates']),
      ],
    );
  }
}

class _AssistantSection extends StatelessWidget {
  const _AssistantSection();

  @override
  Widget build(BuildContext context) {
    return const _PageScaffold(
      children: [
        _HeroCard(
          title: 'AI Health Assistant',
          subtitle: 'Ask questions, understand reports, prepare doctor questions, and get appointment help.',
          icon: Icons.psychology_alt_outlined,
          actions: ['Ask AI', 'Explain Report'],
        ),
        SizedBox(height: 16),
        _ChatBubble(text: 'What does my blood test report mean?', isPatient: true),
        _ChatBubble(
          text: 'I can explain common values in simple language. Abnormal findings should be discussed with your doctor.',
          isPatient: false,
        ),
        SizedBox(height: 12),
        _InfoPanel(
          icon: Icons.verified_user_outlined,
          title: 'Medical safety',
          subtitle: 'AI guidance is informational and does not replace diagnosis or treatment from a doctor.',
          trailing: 'Important',
        ),
      ],
    );
  }
}

class _NotificationsSection extends StatelessWidget {
  const _NotificationsSection();

  @override
  Widget build(BuildContext context) {
    return const _PageScaffold(
      children: [
        _NotificationTile(Icons.alarm_on_outlined, 'Appointment Reminder', 'You have an appointment with Dr. Silva tomorrow at 10:30 AM.', 'New'),
        _NotificationTile(Icons.check_circle_outline_rounded, 'Appointment Confirmed', 'Your cardiology visit has been confirmed.', 'Today'),
        _NotificationTile(Icons.science_outlined, 'New Lab Report', 'A blood test report is available in Medical Records.', 'Yesterday'),
        _NotificationTile(Icons.campaign_outlined, 'Hospital Announcement', 'The outpatient desk closes early on public holidays.', 'Info'),
      ],
    );
  }
}

class _SettingsSection extends StatefulWidget {
  const _SettingsSection();

  @override
  State<_SettingsSection> createState() => _SettingsSectionState();
}

class _SettingsSectionState extends State<_SettingsSection> {
  String _userEmail = '';
  bool _notifyAppointments = true;
  bool _notifyLabReports = true;
  bool _notifyAnnouncements = false;

  @override
  void initState() {
    super.initState();
    _loadSettings();
  }

  Future<void> _loadSettings() async {
    final prefs = await SharedPreferences.getInstance();
    setState(() {
      _userEmail = prefs.getString('patient_email') ?? 'patient@medicore.lk';
      _notifyAppointments = prefs.getBool('patient_notify_appointments') ?? true;
      _notifyLabReports = prefs.getBool('patient_notify_lab_reports') ?? true;
      _notifyAnnouncements = prefs.getBool('patient_notify_announcements') ?? false;
    });
  }

  Future<void> _updateSetting(String key, dynamic value) async {
    final prefs = await SharedPreferences.getInstance();
    if (value is bool) {
      await prefs.setBool(key, value);
    } else if (value is String) {
      await prefs.setString(key, value);
    }
    _loadSettings();
  }

  void _showSnackBar(String message, Color bgColor) {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Row(
          children: [
            Icon(
              bgColor == AppColors.success
                  ? Icons.check_circle_rounded
                  : Icons.error_outline_rounded,
              color: Colors.white,
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                message,
                style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600),
              ),
            ),
          ],
        ),
        backgroundColor: bgColor,
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
        margin: const EdgeInsets.all(16),
      ),
    );
  }

  void _showChangePasswordSheet() {
    final currentPasswordController = TextEditingController();
    final newPasswordController = TextEditingController();
    final confirmPasswordController = TextEditingController();
    final formKey = GlobalKey<FormState>();
    bool obscureCurrent = true;
    bool obscureNew = true;
    bool obscureConfirm = true;
    bool isSaving = false;

    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Theme.of(context).brightness == Brightness.dark
          ? AppColors.surfaceDark
          : Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (context) {
        return StatefulBuilder(
          builder: (context, setSheetState) {
            final isDark = Theme.of(context).brightness == Brightness.dark;
            final textStyle = TextStyle(
              color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
            );

            InputDecoration customInputDecoration({
              required String label,
              required IconData prefixIcon,
              Widget? suffixIcon,
            }) {
              return InputDecoration(
                labelText: label,
                labelStyle: TextStyle(color: isDark ? AppColors.textMutedDark : AppColors.textSecondaryLight),
                prefixIcon: Icon(prefixIcon, color: AppColors.primary),
                suffixIcon: suffixIcon,
                enabledBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: BorderSide(color: isDark ? AppColors.borderDark : AppColors.borderLight),
                ),
                focusedBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: BorderSide(color: isDark ? AppColors.primaryLight : AppColors.primary),
                ),
                errorBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: const BorderSide(color: AppColors.danger),
                ),
                focusedErrorBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: const BorderSide(color: AppColors.danger, width: 2),
                ),
              );
            }

            return Padding(
              padding: EdgeInsets.only(
                left: 20,
                right: 20,
                top: 10,
                bottom: MediaQuery.of(context).viewInsets.bottom + 24,
              ),
              child: Form(
                key: formKey,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Container(
                      width: 40,
                      height: 4,
                      margin: const EdgeInsets.symmetric(vertical: 10),
                      decoration: BoxDecoration(
                        color: isDark ? Colors.grey.shade700 : Colors.grey.shade300,
                        borderRadius: BorderRadius.circular(2),
                      ),
                    ),
                    const SizedBox(height: 10),
                    Text(
                      'Change Password',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                      ),
                    ),
                    const SizedBox(height: 20),
                    TextFormField(
                      controller: currentPasswordController,
                      obscureText: obscureCurrent,
                      style: textStyle,
                      decoration: customInputDecoration(
                        label: 'Current Password',
                        prefixIcon: Icons.lock_outline_rounded,
                        suffixIcon: IconButton(
                          icon: Icon(
                            obscureCurrent ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                            color: isDark ? AppColors.textMutedDark : AppColors.textSecondaryLight,
                          ),
                          onPressed: () => setSheetState(() => obscureCurrent = !obscureCurrent),
                        ),
                      ),
                      validator: (v) => v == null || v.isEmpty ? 'Please enter current password' : null,
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: newPasswordController,
                      obscureText: obscureNew,
                      style: textStyle,
                      decoration: customInputDecoration(
                        label: 'New Password',
                        prefixIcon: Icons.lock_reset_rounded,
                        suffixIcon: IconButton(
                          icon: Icon(
                            obscureNew ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                            color: isDark ? AppColors.textMutedDark : AppColors.textSecondaryLight,
                          ),
                          onPressed: () => setSheetState(() => obscureNew = !obscureNew),
                        ),
                      ),
                      validator: (v) {
                        if (v == null || v.isEmpty) return 'Please enter new password';
                        if (v.length < 8) return 'Password must be at least 8 characters';
                        return null;
                      },
                    ),
                    const SizedBox(height: 16),
                    TextFormField(
                      controller: confirmPasswordController,
                      obscureText: obscureConfirm,
                      style: textStyle,
                      decoration: customInputDecoration(
                        label: 'Confirm New Password',
                        prefixIcon: Icons.lock_clock_outlined,
                        suffixIcon: IconButton(
                          icon: Icon(
                            obscureConfirm ? Icons.visibility_off_outlined : Icons.visibility_outlined,
                            color: isDark ? AppColors.textMutedDark : AppColors.textSecondaryLight,
                          ),
                          onPressed: () => setSheetState(() => obscureConfirm = !obscureConfirm),
                        ),
                      ),
                      validator: (v) {
                        if (v == null || v.isEmpty) return 'Please confirm new password';
                        if (v != newPasswordController.text) return 'Passwords do not match';
                        return null;
                      },
                    ),
                    const SizedBox(height: 24),
                    SizedBox(
                      width: double.infinity,
                      height: 50,
                      child: ElevatedButton(
                        style: ElevatedButton.styleFrom(
                          backgroundColor: AppColors.primary,
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                        ),
                        onPressed: isSaving
                            ? null
                            : () async {
                                if (formKey.currentState?.validate() == true) {
                                  setSheetState(() => isSaving = true);
                                  try {
                                    await ApiService.changePassword(
                                      email: _userEmail,
                                      currentPassword: currentPasswordController.text,
                                      newPassword: newPasswordController.text,
                                    );
                                    if (context.mounted) {
                                      Navigator.pop(context);
                                    }
                                    _showSnackBar('Password changed successfully!', AppColors.success);
                                  } catch (e) {
                                    _showSnackBar(e.toString().replaceAll('Exception: ', ''), AppColors.danger);
                                  } finally {
                                    setSheetState(() => isSaving = false);
                                  }
                                }
                              },
                        child: isSaving
                            ? const SizedBox(
                                width: 24,
                                height: 24,
                                child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                              )
                            : const Text(
                                'Update Password',
                                style: TextStyle(color: Colors.white, fontSize: 15, fontWeight: FontWeight.bold),
                              ),
                      ),
                    ),
                  ],
                ),
              ),
            );
          },
        );
      },
    );
  }

  void _showNotificationSettingsSheet() {
    showModalBottomSheet(
      context: context,
      backgroundColor: Theme.of(context).brightness == Brightness.dark
          ? AppColors.surfaceDark
          : Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      builder: (context) {
        return StatefulBuilder(
          builder: (context, setSheetState) {
            final isDark = Theme.of(context).brightness == Brightness.dark;
            final textStyle = TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w600,
              color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
            );
            final subtitleStyle = TextStyle(
              fontSize: 12,
              color: isDark ? AppColors.textMutedDark : AppColors.textSecondaryLight,
            );

            return Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: 40,
                    height: 4,
                    margin: const EdgeInsets.only(bottom: 16),
                    decoration: BoxDecoration(
                      color: isDark ? Colors.grey.shade700 : Colors.grey.shade300,
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                  Text(
                    'Notification Settings',
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                      color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                    ),
                  ),
                  const SizedBox(height: 16),
                  SwitchListTile(
                    activeThumbColor: AppColors.primary,
                    title: Text('Appointment Reminders', style: textStyle),
                    subtitle: Text('Receive alerts for upcoming consultations and schedule updates', style: subtitleStyle),
                    value: _notifyAppointments,
                    onChanged: (val) {
                      setSheetState(() => _notifyAppointments = val);
                      _updateSetting('patient_notify_appointments', val);
                    },
                  ),
                  const Divider(),
                  SwitchListTile(
                    activeThumbColor: AppColors.primary,
                    title: Text('Lab Reports Alert', style: textStyle),
                    subtitle: Text('Get notified as soon as diagnostics/reports are published', style: subtitleStyle),
                    value: _notifyLabReports,
                    onChanged: (val) {
                      setSheetState(() => _notifyLabReports = val);
                      _updateSetting('patient_notify_lab_reports', val);
                    },
                  ),
                  const Divider(),
                  SwitchListTile(
                    activeThumbColor: AppColors.primary,
                    title: Text('Hospital Announcements', style: textStyle),
                    subtitle: Text('Stay informed about holiday closures, camps, and clinic details', style: subtitleStyle),
                    value: _notifyAnnouncements,
                    onChanged: (val) {
                      setSheetState(() => _notifyAnnouncements = val);
                      _updateSetting('patient_notify_announcements', val);
                    },
                  ),
                  const SizedBox(height: 20),
                ],
              ),
            );
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return _PageScaffold(
      children: [
        _SettingsTile(
          Icons.lock_outline_rounded,
          'Change password',
          'Update your account password',
          onTap: _showChangePasswordSheet,
        ),
        _SettingsTile(
          Icons.notifications_none_rounded,
          'Notification settings',
          'Appointment, report, and system alerts',
          onTap: _showNotificationSettingsSheet,
        ),
        ValueListenableBuilder<ThemeMode>(
          valueListenable: ThemeController.mode,
          builder: (context, themeMode, _) {
            final isDarkMode = themeMode == ThemeMode.dark;
            return _ThemeModeTile(isDarkMode: isDarkMode);
          },
        ),
      ],
    );
  }
}

class _SupportSection extends StatelessWidget {
  const _SupportSection();

  @override
  Widget build(BuildContext context) {
    return const _PageScaffold(
      children: [
        _HeroCard(
          title: 'Help & Support',
          subtitle: 'Get help with the app, hospital services, emergencies, and account access.',
          icon: Icons.help_outline_rounded,
          actions: ['Contact Hospital', 'Report Problem'],
        ),
        SizedBox(height: 16),
        _SettingsTile(Icons.question_answer_outlined, 'FAQs', 'Common patient portal questions'),
        _SettingsTile(Icons.support_agent_rounded, 'Technical support', 'App issues and login problems'),
        _SettingsTile(Icons.emergency_outlined, 'Emergency information', 'Urgent contact and hospital details'),
        _SettingsTile(Icons.info_outline_rounded, 'About application', 'MediCore Patient Portal'),
      ],
    );
  }
}

class _PageScaffold extends StatelessWidget {
  final List<Widget> children;

  const _PageScaffold({required this.children});

  @override
  Widget build(BuildContext context) {
    return ListView(
      physics: const BouncingScrollPhysics(),
      padding: const EdgeInsets.fromLTRB(18, 18, 18, 28),
      children: children,
    );
  }
}

class _HeroCard extends StatelessWidget {
  final String title;
  final String subtitle;
  final IconData icon;
  final List<String> actions;

  const _HeroCard({
    required this.title,
    required this.subtitle,
    required this.icon,
    required this.actions,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        gradient: const LinearGradient(
          colors: [Color(0xFF0369A1), Color(0xFF0284C7), Color(0xFF4F46E5)],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(24),
        boxShadow: [
          BoxShadow(
            color: AppColors.primary.withValues(alpha: 0.24),
            blurRadius: 26,
            offset: const Offset(0, 14),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          CircleAvatar(
            backgroundColor: Colors.white.withValues(alpha: 0.16),
            child: Icon(icon, color: Colors.white),
          ),
          const SizedBox(height: 16),
          Text(
            title,
            style: const TextStyle(color: Colors.white, fontSize: 24, fontWeight: FontWeight.w900, height: 1.1),
          ),
          const SizedBox(height: 8),
          Text(
            subtitle,
            style: const TextStyle(color: Colors.white70, fontSize: 13, fontWeight: FontWeight.w600, height: 1.35),
          ),
          const SizedBox(height: 16),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: actions.map((label) => _WhitePill(label: label)).toList(),
          ),
        ],
      ),
    );
  }
}

class _WhitePill extends StatelessWidget {
  final String label;

  const _WhitePill({required this.label});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Text(label, style: const TextStyle(color: AppColors.primary, fontSize: 12, fontWeight: FontWeight.w800)),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  final String title;

  const _SectionTitle(this.title);

  @override
  Widget build(BuildContext context) {
    return Text(title, style: Theme.of(context).textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w900));
  }
}

class _FeatureTileData {
  final IconData icon;
  final String title;
  final String subtitle;
  final Color color;

  const _FeatureTileData(this.icon, this.title, this.subtitle, this.color);
}

class _FeatureGrid extends StatelessWidget {
  final List<_FeatureTileData> tiles;

  const _FeatureGrid({required this.tiles});

  @override
  Widget build(BuildContext context) {
    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      crossAxisSpacing: 12,
      mainAxisSpacing: 12,
      childAspectRatio: 1.06,
      children: tiles.map((tile) => _FeatureTile(tile: tile)).toList(),
    );
  }
}

class _FeatureTile extends StatelessWidget {
  final _FeatureTileData tile;

  const _FeatureTile({required this.tile});

  @override
  Widget build(BuildContext context) {
    return _SurfaceCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          CircleAvatar(
            backgroundColor: tile.color.withValues(alpha: 0.12),
            child: Icon(tile.icon, color: tile.color, size: 20),
          ),
          const Spacer(),
          Text(tile.title, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w900)),
          const SizedBox(height: 2),
          Text(tile.subtitle, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(color: AppColors.textMutedLight, fontSize: 11)),
        ],
      ),
    );
  }
}

class _SurfaceCard extends StatelessWidget {
  final Widget child;

  const _SurfaceCard({required this.child});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: isDark ? AppColors.surfaceDark : AppColors.bgLightCard,
        borderRadius: BorderRadius.circular(18),
        border: Border.all(color: isDark ? AppColors.borderDark : AppColors.borderLight),
      ),
      child: child,
    );
  }
}

class _InfoPanel extends StatelessWidget {
  final IconData icon;
  final String title;
  final String subtitle;
  final String trailing;

  const _InfoPanel({
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    return _SurfaceCard(
      child: Row(
        children: [
          CircleAvatar(backgroundColor: AppColors.primary.withValues(alpha: 0.12), child: Icon(icon, color: AppColors.primary)),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: const TextStyle(fontSize: 14, fontWeight: FontWeight.w900)),
                const SizedBox(height: 3),
                Text(subtitle, style: const TextStyle(color: AppColors.textMutedLight, fontSize: 12, height: 1.35)),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Text(trailing, style: const TextStyle(color: AppColors.primary, fontSize: 11, fontWeight: FontWeight.w900)),
        ],
      ),
    );
  }
}

class _AppointmentCard extends StatelessWidget {
  final String doctor;
  final String specialty;
  final String date;
  final String time;
  final String status;

  const _AppointmentCard({
    required this.doctor,
    required this.specialty,
    required this.date,
    required this.time,
    required this.status,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return _SurfaceCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const CircleAvatar(backgroundColor: Color(0xFFE0F2FE), child: Icon(Icons.medical_services_outlined, color: AppColors.primary)),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      doctor,
                      style: TextStyle(
                        fontWeight: FontWeight.w900,
                        color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                      ),
                    ),
                    Text(specialty, style: TextStyle(color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight, fontSize: 12)),
                  ],
                ),
              ),
              _StatusBadge(label: status),
            ],
          ),
          const SizedBox(height: 14),
          Row(
            children: [
              Expanded(child: _MiniMetric(label: 'Date', value: date)),
              const SizedBox(width: 8),
              Expanded(child: _MiniMetric(label: 'Time', value: time)),
            ],
          ),
          const SizedBox(height: 12),
          const Row(
            children: [
              Expanded(child: _OutlineAction(label: 'Reschedule')),
              SizedBox(width: 8),
              Expanded(child: _OutlineAction(label: 'Cancel', danger: true)),
            ],
          ),
        ],
      ),
    );
  }
}

class _MiniMetric extends StatelessWidget {
  final String label;
  final String value;

  const _MiniMetric({required this.label, required this.value});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Container(
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: isDark ? AppColors.surfaceDarkSecondary : AppColors.bgLight,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: TextStyle(
              color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
              fontSize: 10,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 2),
          Text(
            value,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w800,
              color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
            ),
          ),
        ],
      ),
    );
  }
}

class _StatusBadge extends StatelessWidget {
  final String label;

  const _StatusBadge({required this.label});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
      decoration: BoxDecoration(
        color: AppColors.success.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(label, style: const TextStyle(color: AppColors.success, fontSize: 11, fontWeight: FontWeight.w900)),
    );
  }
}

class _OutlineAction extends StatelessWidget {
  final String label;
  final bool danger;

  const _OutlineAction({required this.label, this.danger = false});

  @override
  Widget build(BuildContext context) {
    final color = danger ? AppColors.danger : AppColors.primary;
    return Container(
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(vertical: 10),
      decoration: BoxDecoration(
        border: Border.all(color: color.withValues(alpha: 0.32)),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Text(label, style: TextStyle(color: color, fontSize: 12, fontWeight: FontWeight.w900)),
    );
  }
}

class _SearchBarCard extends StatelessWidget {
  final String hint;

  const _SearchBarCard({required this.hint});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return _SurfaceCard(
      child: Row(
        children: [
          Icon(Icons.search_rounded, color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              hint,
              style: TextStyle(
                color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
                fontSize: 13,
              ),
            ),
          ),
          const Icon(Icons.tune_rounded, color: AppColors.primary),
        ],
      ),
    );
  }
}

class _FilterChips extends StatelessWidget {
  final List<String> labels;

  const _FilterChips({required this.labels});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Wrap(
      spacing: 8,
      runSpacing: 8,
      children: labels.map((label) {
        final selected = label == labels.first;
        return Chip(
          label: Text(label),
          backgroundColor: selected ? AppColors.primary.withValues(alpha: 0.12) : null,
          labelStyle: TextStyle(
            color: selected ? AppColors.primary : (isDark ? AppColors.textSecondaryDark : AppColors.textSecondaryLight),
            fontWeight: FontWeight.w700,
          ),
        );
      }).toList(),
    );
  }
}

class _DoctorCard extends StatelessWidget {
  final String name;
  final String specialty;
  final String details;
  final String schedule;
  final String rating;

  const _DoctorCard({
    required this.name,
    required this.specialty,
    required this.details,
    required this.schedule,
    required this.rating,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return _SurfaceCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const CircleAvatar(radius: 24, backgroundColor: Color(0xFFE0F2FE), child: Icon(Icons.person_rounded, color: AppColors.primary)),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      name,
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w900,
                        color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                      ),
                    ),
                    Text(specialty, style: const TextStyle(color: AppColors.primary, fontSize: 12, fontWeight: FontWeight.w800)),
                  ],
                ),
              ),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.star_rounded, color: AppColors.warning, size: 16),
                  const SizedBox(width: 2),
                  Text(
                    rating,
                    style: const TextStyle(color: AppColors.warning, fontSize: 12, fontWeight: FontWeight.w900),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            details,
            style: TextStyle(
              color: isDark ? AppColors.textSecondaryDark : AppColors.textSecondaryLight,
              fontSize: 12,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            schedule,
            style: TextStyle(
              color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
              fontSize: 12,
            ),
          ),
          const SizedBox(height: 12),
          const Row(
            children: [
              Expanded(child: _OutlineAction(label: 'View Profile')),
              SizedBox(width: 8),
              Expanded(child: _OutlineAction(label: 'Book')),
            ],
          ),
        ],
      ),
    );
  }
}

class _RecordCategory extends StatelessWidget {
  final IconData icon;
  final String title;
  final List<String> items;

  const _RecordCategory({
    required this.icon,
    required this.title,
    required this.items,
  });

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: _SurfaceCard(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                CircleAvatar(backgroundColor: AppColors.primary.withValues(alpha: 0.12), child: Icon(icon, color: AppColors.primary)),
                const SizedBox(width: 12),
                Text(title, style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w900)),
              ],
            ),
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: items.map((item) => Chip(label: Text(item))).toList(),
            ),
          ],
        ),
      ),
    );
  }
}

class _ChatBubble extends StatelessWidget {
  final String text;
  final bool isPatient;

  const _ChatBubble({required this.text, required this.isPatient});

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: isPatient ? Alignment.centerRight : Alignment.centerLeft,
      child: Container(
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.all(13),
        constraints: const BoxConstraints(maxWidth: 300),
        decoration: BoxDecoration(
          color: isPatient ? AppColors.primary : AppColors.bgLightCard,
          borderRadius: BorderRadius.circular(16),
          border: isPatient ? null : Border.all(color: AppColors.borderLight),
        ),
        child: Text(
          text,
          style: TextStyle(color: isPatient ? Colors.white : AppColors.textPrimaryLight, fontSize: 13, height: 1.35),
        ),
      ),
    );
  }
}

class _NotificationTile extends StatelessWidget {
  final IconData icon;
  final String title;
  final String message;
  final String tag;

  const _NotificationTile(this.icon, this.title, this.message, this.tag);

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: _InfoPanel(icon: icon, title: title, subtitle: message, trailing: tag),
    );
  }
}

class _ThemeModeTile extends StatelessWidget {
  final bool isDarkMode;

  const _ThemeModeTile({required this.isDarkMode});

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: _SurfaceCard(
        child: Row(
          children: [
            CircleAvatar(
              backgroundColor: AppColors.accent.withValues(alpha: 0.12),
              child: Icon(
                isDarkMode ? Icons.dark_mode_rounded : Icons.light_mode_rounded,
                color: AppColors.accent,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'App appearance',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w900,
                      color: isDarkMode ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                    ),
                  ),
                  Text(
                    'Choose the mode you want',
                    style: TextStyle(
                      color: isDarkMode ? AppColors.textMutedDark : AppColors.textMutedLight,
                      fontSize: 12,
                    ),
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Expanded(
                        child: _ThemeChoiceButton(
                          icon: Icons.light_mode_rounded,
                          label: 'Light',
                          selected: !isDarkMode,
                          onTap: () => ThemeController.setMode(ThemeMode.light),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: _ThemeChoiceButton(
                          icon: Icons.dark_mode_rounded,
                          label: 'Dark',
                          selected: isDarkMode,
                          onTap: () => ThemeController.setMode(ThemeMode.dark),
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ThemeChoiceButton extends StatelessWidget {
  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  const _ThemeChoiceButton({
    required this.icon,
    required this.label,
    required this.selected,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Material(
      color: selected
          ? AppColors.primary.withValues(alpha: isDark ? 0.22 : 0.12)
          : (isDark ? AppColors.surfaceDarkSecondary : AppColors.bgLight),
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 10),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(
              color: selected ? AppColors.primary : (isDark ? AppColors.borderDark : AppColors.borderLight),
            ),
          ),
          child: Center(
            child: FittedBox(
              fit: BoxFit.scaleDown,
              child: Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Icon(
                    icon,
                    color: selected ? AppColors.primary : (isDark ? AppColors.textMutedDark : AppColors.textMutedLight),
                    size: 16,
                  ),
                  const SizedBox(width: 5),
                  Text(
                    label,
                    maxLines: 1,
                    style: TextStyle(
                      color: selected ? AppColors.primary : (isDark ? AppColors.textSecondaryDark : AppColors.textSecondaryLight),
                      fontSize: 12,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _SettingsTile extends StatelessWidget {
  final IconData icon;
  final String title;
  final String subtitle;
  final VoidCallback? onTap;

  const _SettingsTile(this.icon, this.title, this.subtitle, {this.onTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Material(
        color: isDark ? AppColors.surfaceDark : AppColors.bgLightCard,
        borderRadius: BorderRadius.circular(18),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          onTap: onTap,
          child: Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(18),
              border: Border.all(color: isDark ? AppColors.borderDark : AppColors.borderLight),
            ),
            child: Row(
              children: [
                CircleAvatar(
                  backgroundColor: AppColors.primary.withValues(alpha: 0.12),
                  child: Icon(icon, color: AppColors.primary),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        title,
                        style: TextStyle(
                          fontSize: 14,
                          fontWeight: FontWeight.w900,
                          color: isDark ? AppColors.textPrimaryDark : AppColors.textPrimaryLight,
                        ),
                      ),
                      Text(
                        subtitle,
                        style: TextStyle(
                          color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ),
                ),
                Icon(
                  Icons.chevron_right_rounded,
                  color: isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _HeaderIconButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onTap;

  const _HeaderIconButton({required this.icon, required this.onTap});

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(12),
      child: Container(
        width: 40,
        height: 40,
        decoration: BoxDecoration(
          color: AppColors.primary.withValues(alpha: 0.1),
          borderRadius: BorderRadius.circular(12),
        ),
        child: Icon(icon, color: AppColors.primary, size: 21),
      ),
    );
  }
}

String _initialsFor(String name) {
  final parts = name.trim().split(RegExp(r'\s+')).where((part) => part.isNotEmpty).toList();
  if (parts.isEmpty) return 'PU';
  final first = parts.first[0].toUpperCase();
  final second = parts.length > 1 ? parts.last[0].toUpperCase() : '';
  return '$first$second';
}
