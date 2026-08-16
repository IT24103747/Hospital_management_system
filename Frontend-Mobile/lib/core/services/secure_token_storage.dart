import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Stores credentials separately from non-sensitive UI preferences.
class SecureTokenStorage {
  static const _tokenKey = 'hms_token';
  static const FlutterSecureStorage _storage = FlutterSecureStorage();
  static bool _hasTokenOverride = false;
  static String? _tokenOverride;

  static Future<String?> readToken() => _hasTokenOverride
      ? Future.value(_tokenOverride)
      : _storage.read(key: _tokenKey);
  static Future<void> saveToken(String token) =>
      _storage.write(key: _tokenKey, value: token);
  static Future<void> clearToken() => _storage.delete(key: _tokenKey);

  @visibleForTesting
  static void setTokenForTesting(String? token) {
    _hasTokenOverride = true;
    _tokenOverride = token;
  }

  @visibleForTesting
  static void resetTokenForTesting() {
    _hasTokenOverride = false;
    _tokenOverride = null;
  }
}
