import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
import 'package:smartcare_mobile/core/services/secure_token_storage.dart';
import 'package:smartcare_mobile/core/theme/theme_controller.dart';
import 'package:smartcare_mobile/core/widgets/medicore_logo.dart';
import 'package:smartcare_mobile/features/auth/screens/login_screen.dart';
import 'package:smartcare_mobile/features/medical_records/screens/medical_records_screen.dart';
import 'package:smartcare_mobile/features/profile/screens/profile_screen.dart';
import 'package:smartcare_mobile/features/triage/screens/ai_triage_screen.dart';
import 'package:smartcare_mobile/models/appointment.dart';
import 'package:smartcare_mobile/models/patient.dart';

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

enum _AppointmentListMode { upcoming, history }

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
  String? _appointmentAction;
  int _appointmentActionVersion = 0;
  Appointment? _nextAppointment;
  bool _loadingNextAppointment = true;
  String? _nextAppointmentError;

  @override
  void initState() {
    super.initState();
    _activeSection = _sectionFromTitle(widget.title);
    _loadUserSession();
    _loadNextAppointment();
  }

  Future<void> _loadUserSession() async {
    final prefs = await SharedPreferences.getInstance();
    final name = prefs.getString('patient_full_name');
    final email = prefs.getString('patient_email');
    if (!mounted) return;

    final resolvedName =
        name?.trim().isNotEmpty == true ? name!.trim() : _userName;
    setState(() {
      _userName = resolvedName;
      _userEmail =
          email?.trim().isNotEmpty == true ? email!.trim() : _userEmail;
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
        _PatientSection.appointments =>
          'Book, reschedule, cancel, and view visits',
        _PatientSection.doctors => 'Find specialists and available schedules',
        _PatientSection.records => 'History, reports, prescriptions, and notes',
        _PatientSection.assistant =>
          'Simple explanations and guided health questions',
        _PatientSection.notifications =>
          'Hospital updates and appointment reminders',
        _PatientSection.settings => 'Account, privacy, language, and security',
        _PatientSection.support => 'FAQs, hospital contact, and technical help',
      };

  Widget get _content => switch (_activeSection) {
        _PatientSection.home => _HomeSection(
            userName: _userName,
            nextAppointmentText: _nextAppointmentText,
            onBookAppointment: () => _openAppointments('book'),
            onViewAppointments: () => _openAppointments('upcoming'),
          ),
        _PatientSection.profile => const ProfileScreen(),
        _PatientSection.appointments => _AppointmentsSection(
            action: _appointmentAction,
            actionVersion: _appointmentActionVersion,
            onAppointmentsLoaded: _syncNextAppointment,
          ),
        _PatientSection.doctors => const _DoctorsSection(),
        _PatientSection.records => const MedicalRecordsScreen(embedded: true),
        _PatientSection.assistant => const AiTriageScreen(embedded: true),
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

  void _openAppointments(String action) {
    setState(() {
      _activeSection = _PatientSection.appointments;
      _appointmentAction = action;
      _appointmentActionVersion++;
    });
  }

  Future<void> _loadNextAppointment() async {
    try {
      final appointments = await ApiService.getMyAppointments();
      if (!mounted) return;
      setState(() {
        _nextAppointment = _findNextAppointment(appointments);
        _nextAppointmentError = null;
        _loadingNextAppointment = false;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() {
        _nextAppointmentError = 'Unable to load your next appointment right now.';
        _loadingNextAppointment = false;
      });
    }
  }

  void _syncNextAppointment(List<Appointment> appointments) {
    setState(() {
      _nextAppointment = _findNextAppointment(appointments);
      _nextAppointmentError = null;
      _loadingNextAppointment = false;
    });
  }

  String get _nextAppointmentText {
    if (_loadingNextAppointment) {
      return 'Checking your upcoming appointments...';
    }

    if (_nextAppointmentError != null) {
      return _nextAppointmentError!;
    }

    final appointment = _nextAppointment;
    if (appointment == null) {
      return 'You have no upcoming appointments. Book a visit when you are ready.';
    }

    final doctorName = appointment.doctorName.trim().isEmpty
        ? 'your doctor'
        : appointment.doctorName.trim();
    final estimatedStartAt = appointment.estimatedStartAt.toLocal();
    final date = DateFormat('MMM d').format(estimatedStartAt);
    final time = DateFormat('h:mm a').format(estimatedStartAt);
    return 'Your next appointment is $doctorName on $date at $time.';
  }

  Future<void> _logout() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove('patient_user_id');
    await prefs.remove('patient_full_name');
    await prefs.remove('patient_email');
    await SecureTokenStorage.clearToken();
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
      drawer: isDesktop
          ? null
          : Drawer(width: 286, child: _sidebar(isDesktop: false)),
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
          bottom: BorderSide(
              color: isDark ? AppColors.borderDark : AppColors.borderLight),
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
                    color: isDark
                        ? AppColors.textPrimaryDark
                        : AppColors.textPrimaryLight,
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
                    color: isDark
                        ? AppColors.textMutedDark
                        : AppColors.textMutedLight,
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
              style: const TextStyle(
                  color: Colors.white,
                  fontSize: 12,
                  fontWeight: FontWeight.w800),
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
            Divider(
                color: isDark ? AppColors.borderDark : AppColors.borderLight),
            Expanded(
              child: ListView(
                physics: const BouncingScrollPhysics(),
                padding: listPadding,
                children: [
                  _sectionLabel('Main Menu'),
                  _navItem(Icons.home_rounded, 'Home', _PatientSection.home),
                  _navItem(Icons.person_outline_rounded, 'My Profile',
                      _PatientSection.profile),
                  _navItem(Icons.calendar_month_outlined, 'Appointments',
                      _PatientSection.appointments),
                  _navItem(Icons.medical_services_outlined, 'Doctors',
                      _PatientSection.doctors),
                  _navItem(Icons.folder_copy_outlined, 'Medical Records',
                      _PatientSection.records),
                  _navItem(Icons.psychology_alt_outlined, 'AI Health Assistant',
                      _PatientSection.assistant),
                  const SizedBox(height: 12),
                  _sectionLabel('Account'),
                  _navItem(Icons.settings_outlined, 'Settings',
                      _PatientSection.settings),
                  _navItem(Icons.help_outline_rounded, 'Help & Support',
                      _PatientSection.support),
                ],
              ),
            ),
            Divider(
                color: isDark ? AppColors.borderDark : AppColors.borderLight),
            Padding(
              padding: footerPadding,
              child: Column(
                children: [
                  _logoutItem(),
                  SizedBox(height: isDesktop ? 12 : 8),
                  Container(
                    padding: EdgeInsets.all(isDesktop ? 12 : 10),
                    decoration: BoxDecoration(
                      color: isDark
                          ? AppColors.surfaceDarkSecondary
                          : AppColors.bgLightCard,
                      borderRadius: BorderRadius.circular(14),
                      border: Border.all(
                          color: isDark
                              ? AppColors.borderDark
                              : AppColors.borderLight),
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
                                  color: isDark
                                      ? AppColors.textPrimaryDark
                                      : AppColors.textPrimaryLight,
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
                                  color: isDark
                                      ? AppColors.textMutedDark
                                      : AppColors.textMutedLight,
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
      padding:
          EdgeInsets.fromLTRB(isDesktop ? 14 : 12, 0, 12, isDesktop ? 8 : 5),
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
        color: isSelected
            ? AppColors.primary.withValues(alpha: isDark ? 0.18 : 0.12)
            : Colors.transparent,
        borderRadius: BorderRadius.circular(isDesktop ? 12 : 10),
        child: InkWell(
          borderRadius: BorderRadius.circular(isDesktop ? 12 : 10),
          onTap: () => _selectSection(section, closeDrawer: true),
          child: Container(
            constraints: BoxConstraints(minHeight: isDesktop ? 52 : 42),
            padding: EdgeInsets.symmetric(
                horizontal: isDesktop ? 14 : 12, vertical: isDesktop ? 10 : 7),
            decoration: BoxDecoration(
              border: isSelected
                  ? Border(
                      left: BorderSide(
                          color: AppColors.primary, width: isDesktop ? 4 : 3))
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
                          : (isDark
                              ? AppColors.textPrimaryDark
                              : AppColors.textPrimaryLight),
                      fontSize: isDesktop ? 15 : 13.5,
                      fontWeight:
                          isSelected ? FontWeight.w800 : FontWeight.w600,
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
          padding: EdgeInsets.symmetric(
              horizontal: isDesktop ? 14 : 12, vertical: isDesktop ? 10 : 7),
          child: Row(
            children: [
              Icon(Icons.logout_rounded,
                  color: AppColors.danger, size: isDesktop ? 22 : 19),
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
  final String nextAppointmentText;
  final VoidCallback onBookAppointment;
  final VoidCallback onViewAppointments;

  const _HomeSection({
    required this.userName,
    required this.nextAppointmentText,
    required this.onBookAppointment,
    required this.onViewAppointments,
  });

  @override
  Widget build(BuildContext context) {
    final firstName = userName.trim().split(' ').first;
    return _PageScaffold(
      children: [
        _HeroCard(
          title: 'Good morning, $firstName',
          subtitle: nextAppointmentText,
          icon: Icons.waving_hand_rounded,
          actions: const ['View Appointment', 'Book Appointment'],
          onAction: (label) {
            if (label == 'Book Appointment') {
              onBookAppointment();
            } else {
              onViewAppointments();
            }
          },
        ),
        const SizedBox(height: 16),
        const _SectionTitle('Today'),
        const SizedBox(height: 12),
        const _FeatureGrid(
          tiles: [
            _FeatureTileData(
                Icons.medical_information_outlined,
                'Doctor information',
                'Cardiology consultation',
                AppColors.primary),
            _FeatureTileData(Icons.description_outlined, 'Recent report',
                'Blood test uploaded', AppColors.success),
            _FeatureTileData(Icons.notifications_active_outlined,
                'Notifications', 'Appointment updates', AppColors.warning),
            _FeatureTileData(Icons.psychology_alt_outlined, 'AI Assistant',
                'Ask about reports', AppColors.accent),
          ],
        ),
        const SizedBox(height: 16),
        const _InfoPanel(
          icon: Icons.health_and_safety_outlined,
          title: 'Important health reminder',
          subtitle:
              'Take prescribed medicine after breakfast and keep your appointment documents ready.',
          trailing: 'Today',
        ),
      ],
    );
  }
}

class _AppointmentsSection extends StatefulWidget {
  final String? action;
  final int actionVersion;
  final ValueChanged<List<Appointment>>? onAppointmentsLoaded;

  const _AppointmentsSection({
    this.action,
    this.actionVersion = 0,
    this.onAppointmentsLoaded,
  });

  @override
  State<_AppointmentsSection> createState() => _AppointmentsSectionState();
}

class _AppointmentsSectionState extends State<_AppointmentsSection> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _phoneController = TextEditingController();
  final _emailController = TextEditingController();
  final _ageController = TextEditingController();
  String _appointmentType = 'Consultation';
  String? _selectedSpecialty;
  String? _selectedDoctorName;
  int? _selectedSlotId;
  int? _selectedAppointmentNumber;
  bool _showBookingForm = false;
  _AppointmentListMode _listMode = _AppointmentListMode.upcoming;
  bool _loading = true;
  bool _saving = false;
  String? _error;
  List<Appointment> _appointments = [];
  List<DoctorTimeSlot> _slots = [];
  List<DoctorLookup> _doctors = [];
  List<String> _specializations = [];

  @override
  void initState() {
    super.initState();
    _loadAppointments();
  }

  @override
  void dispose() {
    _nameController.dispose();
    _phoneController.dispose();
    _emailController.dispose();
    _ageController.dispose();
    super.dispose();
  }

  @override
  void didUpdateWidget(covariant _AppointmentsSection oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.actionVersion != oldWidget.actionVersion) {
      _applyAction(widget.action);
    }
  }

  void _applyAction(String? action) {
    if (action == 'book') {
      setState(() {
        _showBookingForm = true;
        _listMode = _AppointmentListMode.upcoming;
      });
    } else if (action == 'upcoming') {
      setState(() {
        _showBookingForm = false;
        _listMode = _AppointmentListMode.upcoming;
      });
    } else if (action == 'history') {
      setState(() {
        _showBookingForm = false;
        _listMode = _AppointmentListMode.history;
      });
    }
  }

  Future<void> _loadAppointments() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final results = await Future.wait([
        ApiService.getMyProfile(),
        ApiService.getMyAppointments(),
        ApiService.getAvailableAppointmentSlots(),
        ApiService.getAppointmentDoctors(),
        ApiService.getAppointmentSpecializations(),
      ]);
      final profile = results[0] as Patient?;
      final appointments = results[1] as List<Appointment>;
      final slots = results[2] as List<DoctorTimeSlot>;
      final doctors = results[3] as List<DoctorLookup>;
      final specializations =
          _specialties(results[4] as List<String>, doctors);
      final futureSlots = slots.where(_isFutureSlot).toList();
      if (!mounted) return;
      setState(() {
        _appointments = appointments;
        _slots = futureSlots;
        _doctors = doctors;
        _specializations = specializations;
        _selectedSpecialty = specializations.contains(_selectedSpecialty)
            ? _selectedSpecialty
            : null;
        _selectedDoctorName = _doctorNamesForSpecialty(doctors,
                    _selectedSpecialty)
                .contains(_selectedDoctorName)
            ? _selectedDoctorName
            : null;
        _selectedSlotId = futureSlots.any((slot) =>
                slot.doctorTimeSlotId == _selectedSlotId &&
                slot.doctorName == _selectedDoctorName &&
                _sameText(slot.specialty, _selectedSpecialty))
            ? _selectedSlotId
            : null;
        if (_selectedSlot == null ||
            !_availableNumbers(_selectedSlot!)
                .contains(_selectedAppointmentNumber)) {
          _selectedAppointmentNumber = null;
        }
      });
      widget.onAppointmentsLoaded?.call(appointments);
      _prefillProfile(profile);
      if (widget.action != null) _applyAction(widget.action);
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = _friendlyError(e));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  void _prefillProfile(Patient? profile) {
    if (profile == null) return;
    if (_nameController.text.trim().isEmpty) {
      _nameController.text = profile.fullName;
    }
    if (_phoneController.text.trim().isEmpty) {
      _phoneController.text = profile.phoneNumber;
    }
    if (_emailController.text.trim().isEmpty) {
      _emailController.text = profile.email ?? '';
    }
    if (_ageController.text.trim().isEmpty) {
      _ageController.text = _ageFromDateOfBirth(profile.dateOfBirth).toString();
    }
  }

  Future<void> _bookAppointment() async {
    if (!_formKey.currentState!.validate()) return;
    final slot = _selectedSlot;
    if (slot == null || !_isFutureSlot(slot)) {
      setState(() => _error = 'Select an upcoming appointment slot.');
      return;
    }

    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ApiService.bookAppointment(
        doctorTimeSlotId: _selectedSlotId!,
        appointmentNumber: _selectedAppointmentNumber!,
        patientName: _nameController.text,
        patientPhone: _phoneController.text,
        patientEmail: _emailController.text,
        appointmentType: _appointmentType,
      );
      if (!mounted) return;
      _selectedSpecialty = null;
      _selectedSlotId = null;
      _selectedDoctorName = null;
      _selectedAppointmentNumber = null;
      _showBookingForm = false;
      _listMode = _AppointmentListMode.upcoming;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Appointment booked successfully.')),
      );
      await _loadAppointments();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = _friendlyError(e));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _reschedule(Appointment appointment) async {
    final slotId = await _pickSlot(
      title: 'Reschedule appointment',
      excludedSlotId: appointment.doctorTimeSlotId,
    );
    if (slotId == null) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ApiService.rescheduleAppointment(
        appointmentId: appointment.appointmentId,
        doctorTimeSlotId: slotId,
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Appointment rescheduled.')),
      );
      await _loadAppointments();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = _friendlyError(e));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _cancel(Appointment appointment) async {
    final reason = await _cancelReason();
    if (reason == null) return;
    setState(() {
      _saving = true;
      _error = null;
    });
    try {
      await ApiService.cancelAppointment(
        appointmentId: appointment.appointmentId,
        reason: reason,
      );
      if (!mounted) return;
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Appointment cancelled.')),
      );
      await _loadAppointments();
    } catch (e) {
      if (!mounted) return;
      setState(() => _error = _friendlyError(e));
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<int?> _pickSlot({required String title, int? excludedSlotId}) async {
    final choices = _slots
        .where((slot) =>
            slot.doctorTimeSlotId != excludedSlotId && _isFutureSlot(slot))
        .toList();
    if (choices.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('No alternate time slots are available.')),
      );
      return null;
    }

    return showModalBottomSheet<int>(
      context: context,
      showDragHandle: true,
      builder: (context) {
        return SafeArea(
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 24),
            children: [
              Text(title,
                  style: Theme.of(context)
                      .textTheme
                      .titleMedium
                      ?.copyWith(fontWeight: FontWeight.w900)),
              const SizedBox(height: 12),
              ...choices.map(
                (slot) => ListTile(
                  leading: const CircleAvatar(
                    backgroundColor: Color(0xFFE0F2FE),
                    child:
                        Icon(Icons.schedule_rounded, color: AppColors.primary),
                  ),
                  title: Text(slot.doctorName,
                      maxLines: 1, overflow: TextOverflow.ellipsis),
                  subtitle: Text('${slot.specialty} - ${_formatSlot(slot)}'),
                  trailing: Text('${slot.availableCount} left'),
                  onTap: () => Navigator.pop(context, slot.doctorTimeSlotId),
                ),
              ),
            ],
          ),
        );
      },
    );
  }

  Future<String?> _cancelReason() async {
    var reason = '';
    final formKey = GlobalKey<FormState>();
    final result = await showDialog<String>(
      context: context,
      builder: (context) {
        return AlertDialog(
          title: const Text('Cancel appointment'),
          content: Form(
            key: formKey,
            child: TextFormField(
              onChanged: (value) => reason = value,
              minLines: 2,
              maxLines: 4,
              decoration: const InputDecoration(
                labelText: 'Reason',
                hintText: 'Tell us why you need to cancel',
              ),
              validator: (value) => value == null || value.trim().length < 3
                  ? 'Enter a cancellation reason.'
                  : null,
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Keep'),
            ),
            FilledButton(
              onPressed: () {
                if (formKey.currentState!.validate()) {
                  Navigator.pop(context, reason.trim());
                }
              },
              child: const Text('Cancel appointment'),
            ),
          ],
        );
      },
    );
    return result;
  }

  @override
  Widget build(BuildContext context) {
    final upcomingAppointments =
        _appointments.where(_isUpcomingAppointment).toList();
    final historicalAppointments =
        _appointments.where(_isHistoricalAppointment).toList();
    final showingHistory = _listMode == _AppointmentListMode.history;
    final visibleAppointments =
        showingHistory ? historicalAppointments : upcomingAppointments;

    return _PageScaffold(
      children: [
        _HeroCard(
          title: 'Book a doctor visit',
          subtitle:
              'Select Doctor, choose Date, pick an Available Time, then Confirm Appointment.',
          icon: Icons.calendar_month_outlined,
          actions: const [
            'Book Appointment',
            'View Appointment',
            'View History'
          ],
          onAction: (label) {
            setState(() {
              _showBookingForm = label == 'Book Appointment';
              _listMode = label == 'View History'
                  ? _AppointmentListMode.history
                  : _AppointmentListMode.upcoming;
            });
          },
        ),
        const SizedBox(height: 16),
        if (_error != null) ...[
          _ErrorPanel(message: _error!, onRetry: _loadAppointments),
          const SizedBox(height: 16),
        ],
        if (_loading)
          const _AppointmentLoadingState()
        else ...[
          if (_showBookingForm) ...[
            _BookingFormCard(
              formKey: _formKey,
              slots: _slots,
              doctors: _doctors,
              specializations: _specializations,
              selectedSpecialty: _selectedSpecialty,
              selectedDoctorName: _selectedDoctorName,
              selectedSlotId: _selectedSlotId,
              selectedAppointmentNumber: _selectedAppointmentNumber,
              appointmentType: _appointmentType,
              nameController: _nameController,
              phoneController: _phoneController,
              emailController: _emailController,
              ageController: _ageController,
              saving: _saving,
              onSpecialtyChanged: _selectSpecialty,
              onDoctorChanged: _selectDoctor,
              onAppointmentNumberChanged: _selectAppointmentNumber,
              onTypeChanged: (value) =>
                  setState(() => _appointmentType = value ?? 'Consultation'),
              onClose: () => setState(() => _showBookingForm = false),
              onSubmit: _bookAppointment,
            ),
            const SizedBox(height: 16),
          ],
          _SectionTitle(
              showingHistory ? 'Appointment history' : 'Upcoming appointments'),
          const SizedBox(height: 12),
          if (visibleAppointments.isEmpty)
            _EmptyStateCard(
              icon: showingHistory
                  ? Icons.history_rounded
                  : Icons.event_available_outlined,
              title: showingHistory
                  ? 'No completed or cancelled appointments'
                  : 'No upcoming appointments',
              subtitle: showingHistory
                  ? 'Completed and cancelled appointments will appear here.'
                  : 'Booked upcoming appointments will appear here.',
            )
          else ...[
            _SectionLabel(
                showingHistory ? 'Completed and cancelled' : 'Upcoming'),
            const SizedBox(height: 8),
            ...visibleAppointments.map((appointment) => Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: _AppointmentCard(
                    appointment: appointment,
                    compact: showingHistory,
                    onReschedule: showingHistory || _saving
                        ? null
                        : () => _reschedule(appointment),
                    onCancel: showingHistory || _saving
                        ? null
                        : () => _cancel(appointment),
                  ),
                )),
          ],
        ],
      ],
    );
  }

  bool _isUpcomingAppointment(Appointment appointment) {
    if (_isHistoricalAppointment(appointment)) {
      return false;
    }

    return appointment.endAt.isAfter(DateTime.now());
  }

  bool _isHistoricalAppointment(Appointment appointment) =>
      appointment.status == 'Cancelled' || appointment.status == 'Completed';

  String _friendlyError(Object error) {
    final message = error.toString().replaceFirst('Exception: ', '');
    return message.isEmpty
        ? 'Unable to load appointment information.'
        : message;
  }

  DoctorTimeSlot? get _selectedSlot {
    for (final slot in _slots) {
      if (slot.doctorTimeSlotId == _selectedSlotId) return slot;
    }
    return null;
  }

  void _selectSpecialty(String? specialty) {
    setState(() {
      _selectedSpecialty = specialty;
      _selectedDoctorName = null;
      _selectedSlotId = null;
      _selectedAppointmentNumber = null;
    });
  }

  void _selectDoctor(String? doctorName) {
    setState(() {
      _selectedDoctorName = doctorName;
      _selectedSlotId = null;
      _selectedAppointmentNumber = null;
    });
  }

  void _selectAppointmentNumber(DoctorTimeSlot slot, int number) {
    if (!_isFutureSlot(slot)) return;

    setState(() {
      _selectedSpecialty = slot.specialty;
      _selectedDoctorName = slot.doctorName;
      _selectedSlotId = slot.doctorTimeSlotId;
      _selectedAppointmentNumber = number;
    });
  }

  List<int> _availableNumbers(DoctorTimeSlot slot) {
    if (!_isFutureSlot(slot)) return [];

    final booked = slot.bookedAppointmentNumbers.toSet();
    return List.generate(slot.capacity, (index) => index + 1)
        .where((number) => !booked.contains(number))
        .toList();
  }

  List<String> _doctorNamesForSpecialty(
      List<DoctorLookup> doctors, String? specialty) {
    final names = <String>{};
    for (final doctor in doctors) {
      if (doctor.doctorName.trim().isNotEmpty &&
          _sameText(doctor.specialty, specialty)) {
        names.add(doctor.doctorName);
      }
    }
    return names.toList()..sort();
  }

  List<String> _specialties(
      List<String> loadedSpecializations, List<DoctorLookup> doctors) {
    final values = <String>{};
    for (final specialty in loadedSpecializations) {
      if (specialty.trim().isNotEmpty) values.add(specialty.trim());
    }
    for (final doctor in doctors) {
      if (doctor.specialty.trim().isNotEmpty) values.add(doctor.specialty);
    }
    return values.toList()..sort();
  }

  int _ageFromDateOfBirth(DateTime dateOfBirth) {
    final today = DateTime.now();
    var age = today.year - dateOfBirth.year;
    final birthdayThisYear =
        DateTime(today.year, dateOfBirth.month, dateOfBirth.day);
    if (birthdayThisYear.isAfter(today)) age--;
    return age < 0 ? 0 : age;
  }

  bool _isFutureSlot(DoctorTimeSlot slot) =>
      slot.startAt.isAfter(DateTime.now());
}

class _BookingFormCard extends StatelessWidget {
  final GlobalKey<FormState> formKey;
  final List<DoctorTimeSlot> slots;
  final List<DoctorLookup> doctors;
  final List<String> specializations;
  final String? selectedSpecialty;
  final String? selectedDoctorName;
  final int? selectedSlotId;
  final int? selectedAppointmentNumber;
  final String appointmentType;
  final TextEditingController nameController;
  final TextEditingController phoneController;
  final TextEditingController emailController;
  final TextEditingController ageController;
  final bool saving;
  final ValueChanged<String?> onSpecialtyChanged;
  final ValueChanged<String?> onDoctorChanged;
  final void Function(DoctorTimeSlot slot, int number)
      onAppointmentNumberChanged;
  final ValueChanged<String?> onTypeChanged;
  final VoidCallback onClose;
  final VoidCallback onSubmit;

  const _BookingFormCard({
    required this.formKey,
    required this.slots,
    required this.doctors,
    required this.specializations,
    required this.selectedSpecialty,
    required this.selectedDoctorName,
    required this.selectedSlotId,
    required this.selectedAppointmentNumber,
    required this.appointmentType,
    required this.nameController,
    required this.phoneController,
    required this.emailController,
    required this.ageController,
    required this.saving,
    required this.onSpecialtyChanged,
    required this.onDoctorChanged,
    required this.onAppointmentNumberChanged,
    required this.onTypeChanged,
    required this.onClose,
    required this.onSubmit,
  });

  @override
  Widget build(BuildContext context) {
    final selectedSlot = _slotById(selectedSlotId);
    final filteredDoctors = selectedSpecialty == null
        ? <DoctorLookup>[]
        : doctors
            .where((doctor) => _sameText(doctor.specialty, selectedSpecialty))
            .toList();
    final doctorSlots = selectedDoctorName == null
        ? <DoctorTimeSlot>[]
        : slots
            .where((slot) =>
                slot.doctorName == selectedDoctorName &&
                _sameText(slot.specialty, selectedSpecialty))
            .toList();
    return _SurfaceCard(
      child: Form(
        key: formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                const Icon(Icons.add_circle_outline, color: AppColors.primary),
                const SizedBox(width: 8),
                Expanded(
                  child: Text('Book appointment',
                      style: Theme.of(context)
                          .textTheme
                          .titleMedium
                          ?.copyWith(fontWeight: FontWeight.w900)),
                ),
                IconButton(
                  onPressed: saving ? null : onClose,
                  icon: const Icon(Icons.close_rounded),
                  tooltip: 'Close',
                ),
              ],
            ),
            const SizedBox(height: 14),
            if (specializations.isEmpty)
              const _InlineNotice(
                icon: Icons.medical_information_outlined,
                message: 'No approved doctor specializations are available.',
              )
            else
              DropdownButtonFormField<String>(
                initialValue: selectedSpecialty,
                isExpanded: true,
                decoration:
                    const InputDecoration(labelText: 'Specialization'),
                items: specializations
                    .map((specialty) => DropdownMenuItem(
                          value: specialty,
                          child: Text(specialty,
                              overflow: TextOverflow.ellipsis),
                        ))
                    .toList(),
                validator: (value) =>
                    value == null ? 'Choose a specialization.' : null,
                onChanged: saving ? null : onSpecialtyChanged,
              ),
            const SizedBox(height: 12),
            if (selectedSpecialty != null && filteredDoctors.isEmpty)
              const _InlineNotice(
                icon: Icons.person_search_outlined,
                message: 'No approved doctors are available for this specialization.',
              )
            else
              DropdownButtonFormField<String>(
                initialValue: selectedDoctorName,
                isExpanded: true,
                decoration:
                    const InputDecoration(labelText: 'Search or Select Doctor'),
                items: filteredDoctors
                    .map((doctor) => DropdownMenuItem(
                          value: doctor.doctorName,
                          child: Text(doctor.doctorName,
                              overflow: TextOverflow.ellipsis),
                        ))
                    .toList(),
                validator: (value) => value == null ? 'Choose a doctor.' : null,
                onChanged: saving || selectedSpecialty == null
                    ? null
                    : onDoctorChanged,
              ),
            const SizedBox(height: 12),
            _AppointmentNumberSelector(
              slots: doctorSlots,
              enabled: !saving &&
                  selectedSpecialty != null &&
                  selectedDoctorName != null,
              selectedSlotId: selectedSlotId,
              selectedAppointmentNumber: selectedAppointmentNumber,
              onSelected: onAppointmentNumberChanged,
            ),
            if (selectedDoctorName != null && doctorSlots.isEmpty) ...[
              const SizedBox(height: 10),
              const _InlineNotice(
                icon: Icons.event_busy_outlined,
                message: 'No upcoming appointment numbers are available for this doctor.',
              ),
            ],
            if (selectedSlot != null && selectedAppointmentNumber != null) ...[
              const SizedBox(height: 12),
              _AppointmentEstimatePanel(
                slot: selectedSlot,
                appointmentNumber: selectedAppointmentNumber,
              ),
            ],
            const SizedBox(height: 12),
            TextFormField(
              controller: nameController,
              decoration: const InputDecoration(labelText: 'Patient name'),
              textInputAction: TextInputAction.next,
              validator: (value) => value == null || value.trim().length < 2
                  ? 'Enter the patient name.'
                  : null,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: ageController,
              decoration: const InputDecoration(labelText: 'Patient age'),
              keyboardType: TextInputType.number,
              textInputAction: TextInputAction.next,
              validator: (value) {
                final age = int.tryParse(value?.trim() ?? '');
                if (age == null || age < 0 || age > 130) {
                  return 'Enter a valid age.';
                }
                return null;
              },
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: phoneController,
              decoration: const InputDecoration(labelText: 'Phone number'),
              keyboardType: TextInputType.phone,
              textInputAction: TextInputAction.next,
              validator: (value) => value == null || value.trim().length < 6
                  ? 'Enter a valid phone number.'
                  : null,
            ),
            const SizedBox(height: 12),
            TextFormField(
              controller: emailController,
              decoration: const InputDecoration(labelText: 'Email'),
              keyboardType: TextInputType.emailAddress,
              textInputAction: TextInputAction.next,
              validator: (value) {
                final text = value?.trim() ?? '';
                if (text.isEmpty) return null;
                return text.contains('@') ? null : 'Enter a valid email.';
              },
            ),
            const SizedBox(height: 12),
            DropdownButtonFormField<String>(
              initialValue: appointmentType,
              decoration: const InputDecoration(labelText: 'Appointment type'),
              items: const [
                DropdownMenuItem(
                    value: 'Consultation', child: Text('Consultation')),
                DropdownMenuItem(value: 'Follow-up', child: Text('Follow-up')),
                DropdownMenuItem(value: 'Review', child: Text('Review')),
              ],
              onChanged: saving ? null : onTypeChanged,
            ),
            const SizedBox(height: 14),
            Row(
              children: [
                Expanded(
                  child: OutlinedButton(
                    onPressed: saving ? null : onClose,
                    child: const Text('Close'),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: FilledButton.icon(
                    onPressed: saving || selectedSlotId == null ? null : onSubmit,
                    icon: saving
                        ? const SizedBox(
                            width: 16,
                            height: 16,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Icon(Icons.check_rounded),
                    label: Text(saving ? 'Booking...' : 'Confirm'),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  DoctorTimeSlot? _slotById(int? slotId) {
    for (final slot in slots) {
      if (slot.doctorTimeSlotId == slotId) return slot;
    }
    return null;
  }
}

class _AppointmentNumberSelector extends StatefulWidget {
  final List<DoctorTimeSlot> slots;
  final bool enabled;
  final int? selectedSlotId;
  final int? selectedAppointmentNumber;
  final void Function(DoctorTimeSlot slot, int number) onSelected;

  const _AppointmentNumberSelector({
    required this.slots,
    required this.enabled,
    required this.selectedSlotId,
    required this.selectedAppointmentNumber,
    required this.onSelected,
  });

  @override
  State<_AppointmentNumberSelector> createState() =>
      _AppointmentNumberSelectorState();
}

class _AppointmentNumberSelectorState
    extends State<_AppointmentNumberSelector> {
  bool _open = false;

  @override
  void didUpdateWidget(covariant _AppointmentNumberSelector oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (!widget.enabled && _open) {
      _open = false;
    }
  }

  @override
  Widget build(BuildContext context) {
    final selectedSlot = _selectedSlot();
    final selectedNumber = widget.selectedAppointmentNumber;
    final selectedLabel = selectedSlot != null && selectedNumber != null
        ? '#$selectedNumber - ${_formatEstimatedTime(selectedSlot, selectedNumber)}'
        : 'Select appointment no';

    return FormField<int>(
      key: ValueKey(
          '${widget.selectedSlotId}-${widget.selectedAppointmentNumber}-${widget.slots.length}'),
      initialValue: widget.selectedAppointmentNumber,
      validator: (_) => widget.selectedAppointmentNumber == null
          ? 'Choose an appointment number.'
          : null,
      builder: (field) {
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Select Appointment No',
              style: TextStyle(
                color: Theme.of(context).brightness == Brightness.dark
                    ? AppColors.textSecondaryDark
                    : AppColors.textSecondaryLight,
                fontSize: 14,
                fontWeight: FontWeight.w900,
              ),
            ),
            const SizedBox(height: 10),
            Material(
              color: Colors.transparent,
              child: InkWell(
                onTap: widget.enabled
                    ? () => setState(() => _open = !_open)
                    : null,
                borderRadius: BorderRadius.circular(8),
                child: Container(
                  width: double.infinity,
                  padding:
                      const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
                  decoration: BoxDecoration(
                    color: Theme.of(context).brightness == Brightness.dark
                        ? AppColors.surfaceDarkSecondary
                        : AppColors.bgLightCard,
                    borderRadius: BorderRadius.circular(8),
                    border: Border.all(
                      color: field.hasError
                          ? AppColors.danger
                          : AppColors.borderLight,
                    ),
                  ),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          widget.enabled
                              ? selectedLabel
                              : 'Select doctor first',
                          style: TextStyle(
                            color: widget.enabled
                                ? AppColors.textPrimaryLight
                                : AppColors.textMutedLight,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                      Icon(
                        _open
                            ? Icons.keyboard_arrow_up_rounded
                            : Icons.keyboard_arrow_down_rounded,
                        color: AppColors.textMutedLight,
                      ),
                    ],
                  ),
                ),
              ),
            ),
            if (field.hasError) ...[
              const SizedBox(height: 6),
              Text(
                field.errorText!,
                style: const TextStyle(color: AppColors.danger, fontSize: 12),
              ),
            ],
            if (_open) ...[
              const SizedBox(height: 8),
              _AppointmentNumberPanel(
                slots: widget.slots,
                selectedSlotId: widget.selectedSlotId,
                selectedAppointmentNumber: widget.selectedAppointmentNumber,
                onSelected: (slot, number) {
                  field.didChange(number);
                  widget.onSelected(slot, number);
                  setState(() => _open = false);
                },
              ),
            ],
          ],
        );
      },
    );
  }

  DoctorTimeSlot? _selectedSlot() {
    for (final slot in widget.slots) {
      if (slot.doctorTimeSlotId == widget.selectedSlotId) return slot;
    }
    return null;
  }
}

class _AppointmentNumberPanel extends StatelessWidget {
  final List<DoctorTimeSlot> slots;
  final int? selectedSlotId;
  final int? selectedAppointmentNumber;
  final void Function(DoctorTimeSlot slot, int number) onSelected;

  const _AppointmentNumberPanel({
    required this.slots,
    required this.selectedSlotId,
    required this.selectedAppointmentNumber,
    required this.onSelected,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.fromLTRB(12, 12, 8, 12),
      decoration: BoxDecoration(
        color: Theme.of(context).brightness == Brightness.dark
            ? AppColors.surfaceDark
            : AppColors.bgLightCard,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: AppColors.borderLight),
      ),
      child: slots.isEmpty
          ? const _InlineNotice(
              icon: Icons.event_busy_outlined,
              message: 'No appointment numbers are available for this doctor.',
            )
          : Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                for (var index = 0; index < slots.length; index++) ...[
                  _AppointmentNumberSlotGroup(
                    slot: slots[index],
                    selectedSlotId: selectedSlotId,
                    selectedAppointmentNumber: selectedAppointmentNumber,
                    onSelected: onSelected,
                  ),
                  if (index != slots.length - 1)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 8),
                      child: Divider(height: 1, color: AppColors.borderLight),
                    ),
                ],
              ],
            ),
    );
  }
}

class _AppointmentNumberSlotGroup extends StatelessWidget {
  final DoctorTimeSlot slot;
  final int? selectedSlotId;
  final int? selectedAppointmentNumber;
  final void Function(DoctorTimeSlot slot, int number) onSelected;

  const _AppointmentNumberSlotGroup({
    required this.slot,
    required this.selectedSlotId,
    required this.selectedAppointmentNumber,
    required this.onSelected,
  });

  @override
  Widget build(BuildContext context) {
    final booked = slot.bookedAppointmentNumbers.toSet();
    final numbers = List.generate(slot.capacity, (index) => index + 1);
    final location = _formatSlotLocation(slot);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          _formatSlotHeading(slot),
          style: const TextStyle(
            color: AppColors.textSecondaryLight,
            fontSize: 13,
            fontWeight: FontWeight.w900,
          ),
        ),
        if (location.isNotEmpty) ...[
          const SizedBox(height: 3),
          Text(
            location,
            style: const TextStyle(
              color: AppColors.textMutedLight,
              fontSize: 12,
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
        const SizedBox(height: 8),
        Wrap(
          spacing: 6,
          runSpacing: 6,
          children: numbers.map((number) {
            final isBooked = booked.contains(number);
            final isSelected = selectedSlotId == slot.doctorTimeSlotId &&
                selectedAppointmentNumber == number;
            return _AppointmentNumberButton(
              number: number,
              selected: isSelected,
              disabled: isBooked,
              onTap: isBooked ? null : () => onSelected(slot, number),
            );
          }).toList(),
        ),
      ],
    );
  }
}

class _AppointmentNumberButton extends StatelessWidget {
  final int number;
  final bool selected;
  final bool disabled;
  final VoidCallback? onTap;

  const _AppointmentNumberButton({
    required this.number,
    required this.selected,
    required this.disabled,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final borderColor = selected ? AppColors.primary : Colors.transparent;
    final backgroundColor =
        disabled ? AppColors.surfaceLight : const Color(0xFFD7E2EF);
    return Material(
      color: backgroundColor,
      borderRadius: BorderRadius.circular(8),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(8),
        child: Container(
          width: 42,
          height: 42,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(8),
            border: Border.all(color: borderColor, width: selected ? 1.5 : 1),
          ),
          child: Text(
            '#$number',
            style: TextStyle(
              color: disabled
                  ? AppColors.textMutedLight
                  : AppColors.textPrimaryLight,
              fontSize: 13,
              fontWeight: FontWeight.w900,
            ),
          ),
        ),
      ),
    );
  }
}

class _AppointmentEstimatePanel extends StatelessWidget {
  final DoctorTimeSlot slot;
  final int? appointmentNumber;

  const _AppointmentEstimatePanel({
    required this.slot,
    required this.appointmentNumber,
  });

  @override
  Widget build(BuildContext context) {
    final location = _formatSlotLocation(slot);
    final estimatedTime = appointmentNumber == null
        ? 'Select a number to see estimated patient time.'
        : _formatEstimatedDateTime(slot, appointmentNumber!);
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.primary.withValues(alpha: 0.08),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.primary.withValues(alpha: 0.18)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Icon(Icons.event_available_outlined,
                  color: AppColors.primary),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  'Appointment #${appointmentNumber ?? '-'}',
                  style: const TextStyle(
                      fontSize: 14, fontWeight: FontWeight.w900),
                ),
              ),
              Container(
                padding:
                    const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                decoration: BoxDecoration(
                  color: AppColors.primary.withValues(alpha: 0.1),
                  borderRadius: BorderRadius.circular(999),
                ),
                child: Text(
                  _formatFee(slot.consultationFee),
                  style: const TextStyle(
                      color: AppColors.primary,
                      fontSize: 12,
                      fontWeight: FontWeight.w900),
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          _AppointmentDetailRow(
            icon: Icons.schedule_rounded,
            label: 'Estimated time',
            value: estimatedTime,
          ),
          if (location.isNotEmpty) ...[
            const SizedBox(height: 8),
            _AppointmentDetailRow(
              icon: Icons.meeting_room_outlined,
              label: 'Location',
              value: location,
            ),
          ],
        ],
      ),
    );
  }
}

class _AppointmentDetailRow extends StatelessWidget {
  final IconData icon;
  final String label;
  final String value;

  const _AppointmentDetailRow({
    required this.icon,
    required this.label,
    required this.value,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 16, color: AppColors.textMutedLight),
        const SizedBox(width: 8),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: const TextStyle(
                  color: AppColors.textMutedLight,
                  fontSize: 11,
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                value,
                style: const TextStyle(
                  color: AppColors.textSecondaryLight,
                  fontSize: 13,
                  height: 1.35,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _AppointmentLoadingState extends StatelessWidget {
  const _AppointmentLoadingState();

  @override
  Widget build(BuildContext context) {
    return const _SurfaceCard(
      child: Padding(
        padding: EdgeInsets.symmetric(vertical: 18),
        child: Row(
          children: [
            SizedBox(
              width: 22,
              height: 22,
              child: CircularProgressIndicator(strokeWidth: 2.4),
            ),
            SizedBox(width: 12),
            Expanded(
              child: Text(
                'Loading appointments and available time slots...',
                style: TextStyle(fontWeight: FontWeight.w700),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorPanel extends StatelessWidget {
  final String message;
  final VoidCallback onRetry;

  const _ErrorPanel({required this.message, required this.onRetry});

  @override
  Widget build(BuildContext context) {
    return _SurfaceCard(
      child: Row(
        children: [
          const CircleAvatar(
            backgroundColor: Color(0xFFFEE2E2),
            child: Icon(Icons.error_outline_rounded, color: AppColors.danger),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              message,
              style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w700),
            ),
          ),
          TextButton.icon(
            onPressed: onRetry,
            icon: const Icon(Icons.refresh_rounded),
            label: const Text('Retry'),
          ),
        ],
      ),
    );
  }
}

class _EmptyStateCard extends StatelessWidget {
  final IconData icon;
  final String title;
  final String subtitle;

  const _EmptyStateCard({
    required this.icon,
    required this.title,
    required this.subtitle,
  });

  @override
  Widget build(BuildContext context) {
    return _SurfaceCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          CircleAvatar(
            backgroundColor: AppColors.primary.withValues(alpha: 0.12),
            child: Icon(icon, color: AppColors.primary),
          ),
          const SizedBox(height: 12),
          Text(title,
              style:
                  const TextStyle(fontSize: 15, fontWeight: FontWeight.w900)),
          const SizedBox(height: 4),
          Text(subtitle,
              style: const TextStyle(
                  color: AppColors.textMutedLight, fontSize: 12, height: 1.35)),
        ],
      ),
    );
  }
}

class _InlineNotice extends StatelessWidget {
  final IconData icon;
  final String message;

  const _InlineNotice({required this.icon, required this.message});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.warning.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.warning.withValues(alpha: 0.24)),
      ),
      child: Row(
        children: [
          Icon(icon, color: AppColors.warning),
          const SizedBox(width: 10),
          Expanded(
            child: Text(message,
                style:
                    const TextStyle(fontSize: 12, fontWeight: FontWeight.w700)),
          ),
        ],
      ),
    );
  }
}

class _SectionLabel extends StatelessWidget {
  final String label;

  const _SectionLabel(this.label);

  @override
  Widget build(BuildContext context) {
    return Text(
      label,
      style: const TextStyle(
        color: AppColors.textMutedLight,
        fontSize: 12,
        fontWeight: FontWeight.w900,
      ),
    );
  }
}

String _formatSlot(DoctorTimeSlot slot) {
  final date = DateFormat('MMM d, yyyy').format(slot.startAt.toLocal());
  final start = DateFormat('h:mm a').format(slot.startAt.toLocal());
  final end = DateFormat('h:mm a').format(slot.endAt.toLocal());
  return '$date, $start - $end';
}

String _formatSlotHeading(DoctorTimeSlot slot) {
  final date = DateFormat('MMM d').format(slot.startAt.toLocal());
  final start = DateFormat('hh:mm a').format(slot.startAt.toLocal());
  final end = DateFormat('hh:mm a').format(slot.endAt.toLocal());
  return '$date, $start-$end';
}

String _formatSlotLocation(DoctorTimeSlot slot) {
  return [slot.roomNumber, slot.roomName, slot.floor]
      .map((value) => value.trim())
      .where((value) => value.isNotEmpty)
      .join(', ');
}

String _formatAppointmentLocation(Appointment appointment) {
  return [appointment.roomNumber, appointment.roomName, appointment.floor]
      .map((value) => value.trim())
      .where((value) => value.isNotEmpty)
      .join(', ');
}

Appointment? _findNextAppointment(List<Appointment> appointments) {
  final now = DateTime.now();
  final upcoming = appointments
      .where((appointment) {
        final status = appointment.status.trim().toLowerCase();
        return !_isClosedAppointmentStatus(status) &&
            status != 'no-show' &&
            appointment.endAt.toLocal().isAfter(now);
      })
      .toList()
    ..sort((a, b) => a.estimatedStartAt.compareTo(b.estimatedStartAt));

  return upcoming.isEmpty ? null : upcoming.first;
}

bool _isClosedAppointmentStatus(String status) =>
    status == 'cancelled' || status == 'completed';

String _formatAppointmentDate(Appointment appointment) =>
    DateFormat('MMM d, yyyy').format(appointment.startAt.toLocal());

String _formatAppointmentTime(Appointment appointment) =>
    DateFormat('h:mm a').format(appointment.estimatedStartAt.toLocal());

String _formatEstimatedTime(DoctorTimeSlot slot, int appointmentNumber) =>
    DateFormat('h:mm a')
        .format(_estimatedStartAt(slot, appointmentNumber).toLocal());

String _formatEstimatedDateTime(DoctorTimeSlot slot, int appointmentNumber) {
  return DateFormat('MMM d, yyyy, h:mm a')
      .format(_estimatedStartAt(slot, appointmentNumber).toLocal());
}

DateTime _estimatedStartAt(DoctorTimeSlot slot, int appointmentNumber) {
  if (slot.capacity <= 0) return slot.startAt;
  final duration = slot.endAt.difference(slot.startAt);
  final intervalMs = duration.inMilliseconds ~/ slot.capacity;
  return slot.startAt
      .add(Duration(milliseconds: intervalMs * (appointmentNumber - 1)));
}

String _formatFee(double value) {
  if (value <= 0) return 'Fee pending';
  return 'LKR ${NumberFormat('#,##0.00').format(value)}';
}

bool _sameText(String? a, String? b) =>
    (a ?? '').trim().toLowerCase() == (b ?? '').trim().toLowerCase();

class _DoctorsSection extends StatelessWidget {
  const _DoctorsSection();

  @override
  Widget build(BuildContext context) {
    return const _PageScaffold(
      children: [
        _SearchBarCard(hint: 'Search doctors or specialization'),
        SizedBox(height: 14),
        _FilterChips(labels: [
          'All',
          'Cardiology',
          'General',
          'Pediatrics',
          'Orthopedics'
        ]),
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
          subtitle:
              'Medical history, reports, prescriptions, doctor notes, and uploaded documents stay together.',
          icon: Icons.folder_copy_outlined,
          actions: ['Upload Document', 'Share Summary'],
        ),
        SizedBox(height: 16),
        _RecordCategory(
            icon: Icons.history_edu_outlined,
            title: 'Medical History',
            items: [
              'Previous diagnoses',
              'Previous treatments',
              'Past visits'
            ]),
        _RecordCategory(
            icon: Icons.science_outlined,
            title: 'Lab Reports',
            items: ['Blood tests', 'X-rays', 'Scan reports', 'Results']),
        _RecordCategory(
            icon: Icons.medication_outlined,
            title: 'Prescriptions',
            items: [
              'Current medicines',
              'Dosage',
              'Instructions',
              'Previous prescriptions'
            ]),
        _RecordCategory(
            icon: Icons.note_alt_outlined,
            title: 'Doctor Notes',
            items: ['Consultation notes', 'Treatment recommendations']),
        _RecordCategory(
            icon: Icons.file_copy_outlined,
            title: 'Documents',
            items: ['Uploaded documents', 'Medical certificates']),
      ],
    );
  }
}

class _NotificationsSection extends StatefulWidget {
  const _NotificationsSection();

  @override
  State<_NotificationsSection> createState() => _NotificationsSectionState();
}

class _NotificationsSectionState extends State<_NotificationsSection> {
  late Future<List<Map<String, dynamic>>> _notifications;

  @override
  void initState() {
    super.initState();
    _notifications = ApiService.getAppointmentNotifications();
  }

  void _refresh() {
    setState(() => _notifications = ApiService.getAppointmentNotifications());
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<List<Map<String, dynamic>>>(
      future: _notifications,
      builder: (context, snapshot) {
        return _PageScaffold(children: [
          Align(alignment: Alignment.centerRight,
            child: TextButton.icon(onPressed: _refresh,
              icon: const Icon(Icons.refresh), label: const Text('Refresh'))),
          if (snapshot.connectionState == ConnectionState.waiting)
            const Center(child: CircularProgressIndicator())
          else if (snapshot.hasError)
            const Text('Unable to load notifications. Please try again.')
          else if (snapshot.data!.isEmpty)
            const Text('No appointment updates yet.')
          else
            ...snapshot.data!.map((notification) => _NotificationTile(
              Icons.calendar_month_outlined,
              'Appointment Updated',
              notification['message'] as String,
              DateFormat('MMM d, h:mm a').format(
                DateTime.parse(notification['createdAt'] as String).toLocal()),
            )),
        ]);
      },
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
      _notifyAppointments =
          prefs.getBool('patient_notify_appointments') ?? true;
      _notifyLabReports = prefs.getBool('patient_notify_lab_reports') ?? true;
      _notifyAnnouncements =
          prefs.getBool('patient_notify_announcements') ?? false;
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
                style: const TextStyle(
                    color: Colors.white, fontWeight: FontWeight.w600),
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
              color: isDark
                  ? AppColors.textPrimaryDark
                  : AppColors.textPrimaryLight,
            );

            InputDecoration customInputDecoration({
              required String label,
              required IconData prefixIcon,
              Widget? suffixIcon,
            }) {
              return InputDecoration(
                labelText: label,
                labelStyle: TextStyle(
                    color: isDark
                        ? AppColors.textMutedDark
                        : AppColors.textSecondaryLight),
                prefixIcon: Icon(prefixIcon, color: AppColors.primary),
                suffixIcon: suffixIcon,
                enabledBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: BorderSide(
                      color: isDark
                          ? AppColors.borderDark
                          : AppColors.borderLight),
                ),
                focusedBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: BorderSide(
                      color:
                          isDark ? AppColors.primaryLight : AppColors.primary),
                ),
                errorBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide: const BorderSide(color: AppColors.danger),
                ),
                focusedErrorBorder: OutlineInputBorder(
                  borderRadius: BorderRadius.circular(12),
                  borderSide:
                      const BorderSide(color: AppColors.danger, width: 2),
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
                        color: isDark
                            ? Colors.grey.shade700
                            : Colors.grey.shade300,
                        borderRadius: BorderRadius.circular(2),
                      ),
                    ),
                    const SizedBox(height: 10),
                    Text(
                      'Change Password',
                      style: TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.bold,
                        color: isDark
                            ? AppColors.textPrimaryDark
                            : AppColors.textPrimaryLight,
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
                            obscureCurrent
                                ? Icons.visibility_off_outlined
                                : Icons.visibility_outlined,
                            color: isDark
                                ? AppColors.textMutedDark
                                : AppColors.textSecondaryLight,
                          ),
                          onPressed: () => setSheetState(
                              () => obscureCurrent = !obscureCurrent),
                        ),
                      ),
                      validator: (v) => v == null || v.isEmpty
                          ? 'Please enter current password'
                          : null,
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
                            obscureNew
                                ? Icons.visibility_off_outlined
                                : Icons.visibility_outlined,
                            color: isDark
                                ? AppColors.textMutedDark
                                : AppColors.textSecondaryLight,
                          ),
                          onPressed: () =>
                              setSheetState(() => obscureNew = !obscureNew),
                        ),
                      ),
                      validator: (v) {
                        if (v == null || v.isEmpty) {
                          return 'Please enter new password';
                        }
                        if (v.length < 8) {
                          return 'Password must be at least 8 characters';
                        }
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
                            obscureConfirm
                                ? Icons.visibility_off_outlined
                                : Icons.visibility_outlined,
                            color: isDark
                                ? AppColors.textMutedDark
                                : AppColors.textSecondaryLight,
                          ),
                          onPressed: () => setSheetState(
                              () => obscureConfirm = !obscureConfirm),
                        ),
                      ),
                      validator: (v) {
                        if (v == null || v.isEmpty) {
                          return 'Please confirm new password';
                        }
                        if (v != newPasswordController.text) {
                          return 'Passwords do not match';
                        }
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
                          shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(12)),
                        ),
                        onPressed: isSaving
                            ? null
                            : () async {
                                if (formKey.currentState?.validate() == true) {
                                  setSheetState(() => isSaving = true);
                                  try {
                                    await ApiService.changePassword(
                                      email: _userEmail,
                                      currentPassword:
                                          currentPasswordController.text,
                                      newPassword: newPasswordController.text,
                                    );
                                    if (context.mounted) {
                                      Navigator.pop(context);
                                    }
                                    _showSnackBar(
                                        'Password changed successfully!',
                                        AppColors.success);
                                  } catch (e) {
                                    _showSnackBar(
                                        e
                                            .toString()
                                            .replaceAll('Exception: ', ''),
                                        AppColors.danger);
                                  } finally {
                                    setSheetState(() => isSaving = false);
                                  }
                                }
                              },
                        child: isSaving
                            ? const SizedBox(
                                width: 24,
                                height: 24,
                                child: CircularProgressIndicator(
                                    color: Colors.white, strokeWidth: 2),
                              )
                            : const Text(
                                'Update Password',
                                style: TextStyle(
                                    color: Colors.white,
                                    fontSize: 15,
                                    fontWeight: FontWeight.bold),
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
              color: isDark
                  ? AppColors.textPrimaryDark
                  : AppColors.textPrimaryLight,
            );
            final subtitleStyle = TextStyle(
              fontSize: 12,
              color: isDark
                  ? AppColors.textMutedDark
                  : AppColors.textSecondaryLight,
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
                      color:
                          isDark ? Colors.grey.shade700 : Colors.grey.shade300,
                      borderRadius: BorderRadius.circular(2),
                    ),
                  ),
                  Text(
                    'Notification Settings',
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                      color: isDark
                          ? AppColors.textPrimaryDark
                          : AppColors.textPrimaryLight,
                    ),
                  ),
                  const SizedBox(height: 16),
                  SwitchListTile(
                    activeThumbColor: AppColors.primary,
                    title: Text('Appointment Reminders', style: textStyle),
                    subtitle: Text(
                        'Receive alerts for upcoming consultations and schedule updates',
                        style: subtitleStyle),
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
                    subtitle: Text(
                        'Get notified as soon as diagnostics/reports are published',
                        style: subtitleStyle),
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
                    subtitle: Text(
                        'Stay informed about holiday closures, camps, and clinic details',
                        style: subtitleStyle),
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
          subtitle:
              'Get help with the app, hospital services, emergencies, and account access.',
          icon: Icons.help_outline_rounded,
          actions: ['Contact Hospital', 'Report Problem'],
        ),
        SizedBox(height: 16),
        _SettingsTile(Icons.question_answer_outlined, 'FAQs',
            'Common patient portal questions'),
        _SettingsTile(Icons.support_agent_rounded, 'Technical support',
            'App issues and login problems'),
        _SettingsTile(Icons.emergency_outlined, 'Emergency information',
            'Urgent contact and hospital details'),
        _SettingsTile(Icons.info_outline_rounded, 'About application',
            'MediCore Patient Portal'),
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
  final ValueChanged<String>? onAction;

  const _HeroCard({
    required this.title,
    required this.subtitle,
    required this.icon,
    required this.actions,
    this.onAction,
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
            style: const TextStyle(
                color: Colors.white,
                fontSize: 24,
                fontWeight: FontWeight.w900,
                height: 1.1),
          ),
          const SizedBox(height: 8),
          Text(
            subtitle,
            style: const TextStyle(
                color: Colors.white70,
                fontSize: 13,
                fontWeight: FontWeight.w600,
                height: 1.35),
          ),
          const SizedBox(height: 16),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: actions
                .map((label) => _WhitePill(
                      label: label,
                      onTap: onAction == null ? null : () => onAction!(label),
                    ))
                .toList(),
          ),
        ],
      ),
    );
  }
}

class _WhitePill extends StatelessWidget {
  final String label;
  final VoidCallback? onTap;

  const _WhitePill({required this.label, this.onTap});

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.white,
      borderRadius: BorderRadius.circular(12),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
          child: Text(label,
              style: const TextStyle(
                  color: AppColors.primary,
                  fontSize: 12,
                  fontWeight: FontWeight.w800)),
        ),
      ),
    );
  }
}

class _SectionTitle extends StatelessWidget {
  final String title;

  const _SectionTitle(this.title);

  @override
  Widget build(BuildContext context) {
    return Text(title,
        style: Theme.of(context)
            .textTheme
            .titleMedium
            ?.copyWith(fontWeight: FontWeight.w900));
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
          Text(tile.title,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style:
                  const TextStyle(fontSize: 13, fontWeight: FontWeight.w900)),
          const SizedBox(height: 2),
          Text(tile.subtitle,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                  color: AppColors.textMutedLight, fontSize: 11)),
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
        border: Border.all(
            color: isDark ? AppColors.borderDark : AppColors.borderLight),
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
          CircleAvatar(
              backgroundColor: AppColors.primary.withValues(alpha: 0.12),
              child: Icon(icon, color: AppColors.primary)),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title,
                    style: const TextStyle(
                        fontSize: 14, fontWeight: FontWeight.w900)),
                const SizedBox(height: 3),
                Text(subtitle,
                    style: const TextStyle(
                        color: AppColors.textMutedLight,
                        fontSize: 12,
                        height: 1.35)),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Text(trailing,
              style: const TextStyle(
                  color: AppColors.primary,
                  fontSize: 11,
                  fontWeight: FontWeight.w900)),
        ],
      ),
    );
  }
}

class _AppointmentCard extends StatelessWidget {
  final Appointment appointment;
  final bool compact;
  final VoidCallback? onReschedule;
  final VoidCallback? onCancel;

  const _AppointmentCard({
    required this.appointment,
    this.compact = false,
    this.onReschedule,
    this.onCancel,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final reason = appointment.reason.trim();
    final cancellationReason = appointment.cancellationReason.trim();
    final location = _formatAppointmentLocation(appointment);
    return _SurfaceCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const CircleAvatar(
                  backgroundColor: Color(0xFFE0F2FE),
                  child: Icon(Icons.medical_services_outlined,
                      color: AppColors.primary)),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      appointment.doctorName,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(
                        fontWeight: FontWeight.w900,
                        color: isDark
                            ? AppColors.textPrimaryDark
                            : AppColors.textPrimaryLight,
                      ),
                    ),
                    Text(appointment.specialty,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: TextStyle(
                            color: isDark
                                ? AppColors.textMutedDark
                                : AppColors.textMutedLight,
                            fontSize: 12)),
                  ],
                ),
              ),
              _StatusBadge(label: appointment.status),
            ],
          ),
          if (location.isNotEmpty) ...[
            const SizedBox(height: 8),
            Row(
              children: [
                const Icon(Icons.meeting_room_outlined,
                    size: 15, color: AppColors.textMutedLight),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    location,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                        color: AppColors.textMutedLight,
                        fontSize: 12,
                        fontWeight: FontWeight.w700),
                  ),
                ),
              ],
            ),
          ],
          if (reason.isNotEmpty || cancellationReason.isNotEmpty) ...[
            const SizedBox(height: 10),
            Text(
              cancellationReason.isNotEmpty ? cancellationReason : reason,
              maxLines: compact ? 1 : 2,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: AppColors.textMutedLight,
                fontSize: 12,
                height: 1.35,
              ),
            ),
          ],
          const SizedBox(height: 14),
          Row(
            children: [
              Expanded(
                  child: _MiniMetric(
                      label: 'Appointment number',
                      value: '#${appointment.appointmentNumber}')),
              const SizedBox(width: 8),
              Expanded(
                  child: _MiniMetric(
                      label: 'Estimated time',
                      value: _formatAppointmentTime(appointment))),
            ],
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              Expanded(
                  child: _MiniMetric(
                      label: 'Date',
                      value: _formatAppointmentDate(appointment))),
              const SizedBox(width: 8),
              Expanded(
                  child: _MiniMetric(
                      label: 'Fee',
                      value: _formatFee(appointment.consultationFee))),
            ],
          ),
          if (onReschedule != null || onCancel != null) ...[
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                    child: _OutlineAction(
                        label: 'Reschedule', onTap: onReschedule)),
                const SizedBox(width: 8),
                Expanded(
                    child: _OutlineAction(
                        label: 'Cancel', danger: true, onTap: onCancel)),
              ],
            ),
          ],
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
              color:
                  isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
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
              color: isDark
                  ? AppColors.textPrimaryDark
                  : AppColors.textPrimaryLight,
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
    final normalized = label.toLowerCase();
    final color = normalized == 'cancelled'
        ? AppColors.danger
        : normalized == 'requested'
            ? AppColors.warning
            : normalized == 'completed'
                ? AppColors.accent
                : AppColors.success;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 5),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(label,
          style: TextStyle(
              color: color, fontSize: 11, fontWeight: FontWeight.w900)),
    );
  }
}

class _OutlineAction extends StatelessWidget {
  final String label;
  final bool danger;
  final VoidCallback? onTap;

  const _OutlineAction({required this.label, this.danger = false, this.onTap});

  @override
  Widget build(BuildContext context) {
    final enabled = onTap != null;
    final color = enabled
        ? (danger ? AppColors.danger : AppColors.primary)
        : AppColors.textMutedLight;
    return Material(
      color: Colors.transparent,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Container(
          alignment: Alignment.center,
          padding: const EdgeInsets.symmetric(vertical: 10),
          decoration: BoxDecoration(
            border: Border.all(color: color.withValues(alpha: 0.32)),
            borderRadius: BorderRadius.circular(12),
          ),
          child: Text(label,
              style: TextStyle(
                  color: color, fontSize: 12, fontWeight: FontWeight.w900)),
        ),
      ),
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
          Icon(Icons.search_rounded,
              color:
                  isDark ? AppColors.textMutedDark : AppColors.textMutedLight),
          const SizedBox(width: 10),
          Expanded(
            child: Text(
              hint,
              style: TextStyle(
                color:
                    isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
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
          backgroundColor:
              selected ? AppColors.primary.withValues(alpha: 0.12) : null,
          labelStyle: TextStyle(
            color: selected
                ? AppColors.primary
                : (isDark
                    ? AppColors.textSecondaryDark
                    : AppColors.textSecondaryLight),
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
              const CircleAvatar(
                  radius: 24,
                  backgroundColor: Color(0xFFE0F2FE),
                  child: Icon(Icons.person_rounded, color: AppColors.primary)),
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
                        color: isDark
                            ? AppColors.textPrimaryDark
                            : AppColors.textPrimaryLight,
                      ),
                    ),
                    Text(specialty,
                        style: const TextStyle(
                            color: AppColors.primary,
                            fontSize: 12,
                            fontWeight: FontWeight.w800)),
                  ],
                ),
              ),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.star_rounded,
                      color: AppColors.warning, size: 16),
                  const SizedBox(width: 2),
                  Text(
                    rating,
                    style: const TextStyle(
                        color: AppColors.warning,
                        fontSize: 12,
                        fontWeight: FontWeight.w900),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            details,
            style: TextStyle(
              color: isDark
                  ? AppColors.textSecondaryDark
                  : AppColors.textSecondaryLight,
              fontSize: 12,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            schedule,
            style: TextStyle(
              color:
                  isDark ? AppColors.textMutedDark : AppColors.textMutedLight,
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
                CircleAvatar(
                    backgroundColor: AppColors.primary.withValues(alpha: 0.12),
                    child: Icon(icon, color: AppColors.primary)),
                const SizedBox(width: 12),
                Text(title,
                    style: const TextStyle(
                        fontSize: 15, fontWeight: FontWeight.w900)),
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
      child: _InfoPanel(
          icon: icon, title: title, subtitle: message, trailing: tag),
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
                      color: isDarkMode
                          ? AppColors.textPrimaryDark
                          : AppColors.textPrimaryLight,
                    ),
                  ),
                  Text(
                    'Choose the mode you want',
                    style: TextStyle(
                      color: isDarkMode
                          ? AppColors.textMutedDark
                          : AppColors.textMutedLight,
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
              color: selected
                  ? AppColors.primary
                  : (isDark ? AppColors.borderDark : AppColors.borderLight),
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
                    color: selected
                        ? AppColors.primary
                        : (isDark
                            ? AppColors.textMutedDark
                            : AppColors.textMutedLight),
                    size: 16,
                  ),
                  const SizedBox(width: 5),
                  Text(
                    label,
                    maxLines: 1,
                    style: TextStyle(
                      color: selected
                          ? AppColors.primary
                          : (isDark
                              ? AppColors.textSecondaryDark
                              : AppColors.textSecondaryLight),
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
              border: Border.all(
                  color: isDark ? AppColors.borderDark : AppColors.borderLight),
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
                          color: isDark
                              ? AppColors.textPrimaryDark
                              : AppColors.textPrimaryLight,
                        ),
                      ),
                      Text(
                        subtitle,
                        style: TextStyle(
                          color: isDark
                              ? AppColors.textMutedDark
                              : AppColors.textMutedLight,
                          fontSize: 12,
                        ),
                      ),
                    ],
                  ),
                ),
                Icon(
                  Icons.chevron_right_rounded,
                  color: isDark
                      ? AppColors.textMutedDark
                      : AppColors.textMutedLight,
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
  final parts = name
      .trim()
      .split(RegExp(r'\s+'))
      .where((part) => part.isNotEmpty)
      .toList();
  if (parts.isEmpty) return 'PU';
  final first = parts.first[0].toUpperCase();
  final second = parts.length > 1 ? parts.last[0].toUpperCase() : '';
  return '$first$second';
}
