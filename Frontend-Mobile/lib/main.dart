import 'package:flutter/material.dart';
import 'package:smartcare_mobile/core/theme/app_theme.dart';
import 'package:smartcare_mobile/features/auth/screens/login_screen.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const MediCoreMobileApp());
}

class MediCoreMobileApp extends StatelessWidget {
  const MediCoreMobileApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'MediCore',
      debugShowCheckedModeBanner: false,
      theme: AppTheme.lightTheme,
      darkTheme: AppTheme.darkTheme,
      // Force the web's light theme — matches web app's default :root {} palette
      themeMode: ThemeMode.light,
      home: const LoginScreen(),
    );
  }
}
