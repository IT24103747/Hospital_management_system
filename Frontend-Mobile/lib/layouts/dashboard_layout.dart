import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:smartcare_mobile/core/constants/app_colors.dart';
import 'package:smartcare_mobile/core/widgets/medicore_logo.dart';
import 'package:smartcare_mobile/features/triage/screens/ai_triage_screen.dart';
import 'package:smartcare_mobile/features/vitals/screens/vitals_logger_screen.dart';
import 'package:smartcare_mobile/features/clinic_finder/screens/emergency_clinic_screen.dart';
import 'package:smartcare_mobile/features/profile/screens/profile_screen.dart';
import 'package:smartcare_mobile/features/auth/screens/login_screen.dart';

class DashboardLayout extends StatefulWidget {
  final Widget body;
  final String title;
  final String subtitle;

  const DashboardLayout({
    super.key,
    required this.body,
    required this.title,
    required this.subtitle,
  });

  @override
  State<DashboardLayout> createState() => _DashboardLayoutState();
}

class _DashboardLayoutState extends State<DashboardLayout> {
  bool _isSidebarCollapsed = false;
  String _userName = 'Patient User';
  String _userInitials = 'P';

  @override
  void initState() {
    super.initState();
    _loadUserSession();
  }

  Future<void> _loadUserSession() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final name = prefs.getString('patient_full_name');
      if (name != null && name.trim().isNotEmpty) {
        final parts = name.trim().split(' ');
        String initials = parts.first[0].toUpperCase();
        if (parts.length > 1) {
          initials += parts.last[0].toUpperCase();
        }
        setState(() {
          _userName = name;
          _userInitials = initials;
        });
      }
    } catch (_) {}
  }

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final isDesktop = MediaQuery.of(context).size.width >= 900;

    Widget sidebarContent = Container(
      width: isDesktop ? (_isSidebarCollapsed ? 80.0 : 250.0) : 270.0,
      decoration: BoxDecoration(
        color: isDark ? AppColors.surfaceDark : AppColors.surfaceLight,
        border: Border(
          right: BorderSide(
            color: isDark ? AppColors.borderDark : AppColors.borderLight,
          ),
        ),
      ),
      child: Column(
        children: [
          // Sidebar Brand Header
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 20),
            decoration: BoxDecoration(
              border: Border(
                bottom: BorderSide(
                  color: isDark ? AppColors.borderDark : AppColors.borderLight,
                ),
              ),
            ),
            child: Row(
              children: [
                MediCoreLogo(
                  size: 38.0,
                  borderRadius: 10.0,
                  showText: !isDesktop || !_isSidebarCollapsed,
                  fontSize: 20.0,
                ),
                if (isDesktop) ...[
                  const Spacer(),
                  IconButton(
                    icon: Icon(
                      _isSidebarCollapsed
                          ? Icons.chevron_right_rounded
                          : Icons.chevron_left_rounded,
                      size: 20,
                    ),
                    onPressed: () {
                      setState(
                          () => _isSidebarCollapsed = !_isSidebarCollapsed);
                    },
                  ),
                ],
              ],
            ),
          ),

          // Navigation Links
          Expanded(
            child: ListView(
              padding: const EdgeInsets.symmetric(vertical: 16, horizontal: 10),
              children: [
                if (!isDesktop || !_isSidebarCollapsed)
                  const Padding(
                    padding: EdgeInsets.only(left: 12, bottom: 8, top: 4),
                    child: Text(
                      'MAIN MENU',
                      style: TextStyle(
                        fontSize: 10,
                        fontWeight: FontWeight.bold,
                        letterSpacing: 1.2,
                        color: AppColors.textSecondaryLight,
                      ),
                    ),
                  ),
                _buildNavItem(
                  icon: Icons.dashboard_outlined,
                  label: 'Dashboard',
                  isSelected: widget.title == 'Dashboard',
                  onTap: () {
                    Navigator.pushReplacement(
                      context,
                      MaterialPageRoute(
                        builder: (context) => const DashboardLayout(
                          title: 'Dashboard',
                          subtitle: 'Patient Overview & Analytics',
                          body: AiTriageScreen(),
                        ),
                      ),
                    );
                  },
                ),
                _buildNavItem(
                  icon: Icons.psychology_outlined,
                  label: 'AI Smart Triage',
                  isSelected: widget.title == 'AI Smart Triage',
                  onTap: () {
                    Navigator.pushReplacement(
                      context,
                      MaterialPageRoute(
                        builder: (context) => const DashboardLayout(
                          title: 'AI Smart Triage',
                          subtitle:
                              'Autonomous Symptom Analysis & Risk Classification',
                          body: AiTriageScreen(),
                        ),
                      ),
                    );
                  },
                ),
                _buildNavItem(
                  icon: Icons.favorite_outline,
                  label: 'Vitals Logger',
                  isSelected: widget.title == 'Vitals Logger',
                  onTap: () {
                    Navigator.pushReplacement(
                      context,
                      MaterialPageRoute(
                        builder: (context) => const DashboardLayout(
                          title: 'Vitals Logger',
                          subtitle: 'Daily Health Metrics & Indicators',
                          body: VitalsLoggerScreen(),
                        ),
                      ),
                    );
                  },
                ),
                _buildNavItem(
                  icon: Icons.near_me_outlined,
                  label: 'Emergency ER',
                  isSelected: widget.title == 'Emergency ER',
                  onTap: () {
                    Navigator.pushReplacement(
                      context,
                      MaterialPageRoute(
                        builder: (context) => const DashboardLayout(
                          title: 'Emergency ER',
                          subtitle: 'GPS ER Clinic Locator & Direct Hotline',
                          body: EmergencyClinicScreen(),
                        ),
                      ),
                    );
                  },
                ),
                _buildNavItem(
                  icon: Icons.person_outline,
                  label: 'Patient Profile',
                  isSelected: widget.title == 'Patient Profile',
                  onTap: () {
                    Navigator.pushReplacement(
                      context,
                      MaterialPageRoute(
                        builder: (context) => const DashboardLayout(
                          title: 'Patient Profile',
                          subtitle: 'Personal Records & ID Camera Scanner',
                          body: ProfileScreen(),
                        ),
                      ),
                    );
                  },
                ),
              ],
            ),
          ),

          // User Profile & Account Footer
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              border: Border(
                top: BorderSide(
                  color: isDark ? AppColors.borderDark : AppColors.borderLight,
                ),
              ),
            ),
            child: Column(
              children: [
                _buildNavItem(
                  icon: Icons.logout_rounded,
                  label: 'Logout',
                  isDanger: true,
                  onTap: () async {
                    // Clear session data so next login starts fresh
                    final prefs = await SharedPreferences.getInstance();
                    await prefs.remove('patient_user_id');
                    await prefs.remove('patient_full_name');
                    await prefs.remove('patient_email');
                    if (!context.mounted) return;
                    Navigator.pushReplacement(
                      context,
                      MaterialPageRoute(
                          builder: (context) => const LoginScreen()),
                    );
                  },
                ),
                if (!isDesktop || !_isSidebarCollapsed) ...[
                  const SizedBox(height: 8),
                  Container(
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: isDark
                          ? AppColors.surfaceDarkSecondary
                          : AppColors.bgLight,
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(
                        color: isDark
                            ? AppColors.borderDark
                            : AppColors.borderLight,
                      ),
                    ),
                    child: Row(
                      children: [
                        CircleAvatar(
                          radius: 16,
                          backgroundColor: AppColors.primary,
                          child: Text(
                            _userInitials,
                            style: const TextStyle(
                                fontSize: 12,
                                fontWeight: FontWeight.bold,
                                color: Colors.white),
                          ),
                        ),
                        const SizedBox(width: 10),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                _userName,
                                style: const TextStyle(
                                    fontWeight: FontWeight.bold, fontSize: 13),
                                overflow: TextOverflow.ellipsis,
                              ),
                              const Text(
                                'Patient Portal User',
                                style: TextStyle(
                                    fontSize: 11,
                                    color: AppColors.textSecondaryLight),
                              ),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );

    return Scaffold(
      drawer: isDesktop ? null : Drawer(child: sidebarContent),
      body: Row(
        children: [
          // Persistent Sidebar on Desktop/Tablet
          if (isDesktop) sidebarContent,

          // Main Body Content
          Expanded(
            child: Column(
              children: [
                // Topbar Header
                Container(
                  height: 70,
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  decoration: BoxDecoration(
                    color:
                        isDark ? AppColors.surfaceDark : AppColors.surfaceLight,
                    border: Border(
                      bottom: BorderSide(
                        color: isDark
                            ? AppColors.borderDark
                            : AppColors.borderLight,
                      ),
                    ),
                  ),
                  child: Row(
                    children: [
                      if (!isDesktop)
                        Builder(
                          builder: (context) => IconButton(
                            icon: const Icon(Icons.menu_rounded),
                            onPressed: () => Scaffold.of(context).openDrawer(),
                          ),
                        ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              widget.title,
                              style: TextStyle(
                                fontSize: 18,
                                fontWeight: FontWeight.bold,
                                color: isDark
                                    ? AppColors.textPrimaryDark
                                    : AppColors.textPrimaryLight,
                              ),
                            ),
                            Text(
                              widget.subtitle,
                              style: const TextStyle(
                                  fontSize: 12,
                                  color: AppColors.textSecondaryLight),
                            ),
                          ],
                        ),
                      ),
                      IconButton(
                        icon: const Icon(Icons.notifications_none_rounded),
                        onPressed: () {},
                      ),
                      const SizedBox(width: 8),
                      CircleAvatar(
                        radius: 18,
                        backgroundColor: AppColors.primary,
                        child: Text(
                          _userInitials,
                          style: const TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.bold,
                              color: Colors.white),
                        ),
                      ),
                    ],
                  ),
                ),

                // Main Page View
                Expanded(child: widget.body),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildNavItem({
    required IconData icon,
    required String label,
    required VoidCallback onTap,
    bool isSelected = false,
    bool isDanger = false,
  }) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final isDesktop = MediaQuery.of(context).size.width >= 900;

    Color iconColor = isDanger
        ? AppColors.danger
        : (isSelected
            ? AppColors.primary
            : (isDark
                ? AppColors.textSecondaryDark
                : AppColors.textSecondaryLight));

    Color textColor = isDanger
        ? AppColors.danger
        : (isSelected
            ? AppColors.primary
            : (isDark
                ? AppColors.textPrimaryDark
                : AppColors.textPrimaryLight));

    Color bgColor = isSelected
        ? (isDark
            ? AppColors.primary.withValues(alpha: 0.15)
            : AppColors.primary.withValues(alpha: 0.1))
        : Colors.transparent;

    return Container(
      margin: const EdgeInsets.only(bottom: 4),
      decoration: BoxDecoration(
        color: bgColor,
        borderRadius: BorderRadius.circular(10),
        border: isSelected
            ? const Border(left: BorderSide(color: AppColors.primary, width: 3))
            : null,
      ),
      child: ListTile(
        dense: true,
        contentPadding: EdgeInsets.symmetric(
          horizontal: (isDesktop && _isSidebarCollapsed) ? 12 : 12,
          vertical: 2,
        ),
        leading: Icon(icon, color: iconColor, size: 20),
        title: (isDesktop && _isSidebarCollapsed)
            ? null
            : Text(
                label,
                style: TextStyle(
                  color: textColor,
                  fontWeight: isSelected ? FontWeight.bold : FontWeight.w500,
                  fontSize: 14,
                ),
              ),
        onTap: onTap,
      ),
    );
  }
}
