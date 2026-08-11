import 'package:flutter/material.dart';
import 'package:smartcare_mobile/core/theme/app_theme.dart';
import 'package:smartcare_mobile/core/theme/theme_controller.dart';
import 'package:smartcare_mobile/features/auth/screens/login_screen.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await ThemeController.load();
  runApp(const MediCoreMobileApp());
}

class MediCoreMobileApp extends StatelessWidget {
  const MediCoreMobileApp({super.key});

  @override
  Widget build(BuildContext context) {
    return ValueListenableBuilder<ThemeMode>(
      valueListenable: ThemeController.mode,
      builder: (context, themeMode, _) {
        return MaterialApp(
          title: 'MediCore',
          debugShowCheckedModeBanner: false,
          theme: AppTheme.lightTheme,
          darkTheme: AppTheme.darkTheme,
          themeMode: themeMode,
          home: const LoginScreen(),
        );
      },
    );
  }
}
