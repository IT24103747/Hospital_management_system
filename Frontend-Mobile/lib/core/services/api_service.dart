import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:smartcare_mobile/core/services/secure_token_storage.dart';
import 'package:smartcare_mobile/models/patient.dart';
import 'package:smartcare_mobile/models/triage_workflow.dart';

class ApiService {
  // Android emulators use 10.0.2.2 to reach the development machine.
  // For a physical device, replace this with your computer's LAN IP address.
  static final String baseUrl = kIsWeb
      ? 'http://localhost:5000/api'
      : defaultTargetPlatform == TargetPlatform.android
          ? 'http://10.0.2.2:5000/api'
          : 'http://localhost:5000/api';

  static Future<Map<String, String>> _authHeaders() async {
    final token = await SecureTokenStorage.readToken();
    return {
      'Content-Type': 'application/json',
      if (token != null && token.isNotEmpty) 'Authorization': 'Bearer $token',
    };
  }

  static Future<List<Patient>> getPatients({String search = ''}) async {
    final query = <String, String>{
      'page': '1',
      'pageSize': '50',
      'sortBy': 'name',
      'sortDirection': 'asc',
      if (search.trim().isNotEmpty) 'search': search.trim(),
    };
    final uri = Uri.parse('$baseUrl/patient').replace(queryParameters: query);
    final response = await http.get(uri, headers: await _authHeaders());
    if (response.statusCode != 200) {
      throw Exception('Unable to load patient records.');
    }
    final body = jsonDecode(response.body) as Map<String, dynamic>;
    final items = body['data'] as List<dynamic>? ?? [];
    return items
        .map((item) => Patient.fromJson(item as Map<String, dynamic>))
        .toList();
  }

  static Future<Map<String, dynamic>> login(
      {required String email, required String password}) async {
    final response = await http.post(
      Uri.parse('$baseUrl/auth/login'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'email': email.trim(), 'password': password}),
    );
    if (response.statusCode == 200) {
      return jsonDecode(response.body) as Map<String, dynamic>;
    }
    throw Exception('Invalid email or password.');
  }

  static Future<void> register(Map<String, dynamic> patient) async {
    final response = await http.post(Uri.parse('$baseUrl/auth/register'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(patient));
    if (response.statusCode != 201)
      throw Exception(_errorMessage(response.body));
  }

  static Future<void> changePassword({
    required String email,
    required String currentPassword,
    required String newPassword,
  }) async {
    final response = await http.post(
      Uri.parse('$baseUrl/auth/change-password'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'email': email.trim().toLowerCase(),
        'currentPassword': currentPassword,
        'newPassword': newPassword,
      }),
    );
    if (response.statusCode != 200) {
      throw Exception(_errorMessage(response.body));
    }
  }

  static String _errorMessage(String body) {
    try {
      final decoded = jsonDecode(body);
      if (decoded is Map<String, dynamic>) {
        if (decoded.containsKey('message'))
          return decoded['message'].toString();
        if (decoded.containsKey('title')) return decoded['title'].toString();
        if (decoded.containsKey('errors')) {
          final errors = decoded['errors'];
          if (errors is Map<String, dynamic>) {
            final firstKey = errors.keys.first;
            final firstList = errors[firstKey];
            if (firstList is List && firstList.isNotEmpty) {
              return firstList.first.toString();
            }
          }
        }
      }
      return 'Request failed. Please check your inputs.';
    } catch (_) {
      return body.isNotEmpty ? body : 'Server error occurred.';
    }
  }

  static Future<Patient?> getMyProfile() async {
    try {
      final response = await http.get(Uri.parse('$baseUrl/patient/me'),
          headers: await _authHeaders());
      if (response.statusCode == 200) {
        return Patient.fromJson(jsonDecode(response.body));
      }
    } catch (e) {
      debugPrint('ApiService getMyProfile Error: $e');
    }
    return null;
  }

  static Future<Patient?> getPatientById(int id) async {
    try {
      final response = await http.get(Uri.parse('$baseUrl/patient/$id'),
          headers: await _authHeaders());
      if (response.statusCode == 200) {
        return Patient.fromJson(jsonDecode(response.body));
      }
    } catch (e) {
      debugPrint('ApiService Error: $e');
    }
    return null;
  }

  static Future<Patient?> createPatient(Map<String, dynamic> data) async {
    try {
      final response = await http.post(
        Uri.parse('$baseUrl/patient'),
        headers: await _authHeaders(),
        body: jsonEncode(data),
      );
      if (response.statusCode == 201 || response.statusCode == 200) {
        return Patient.fromJson(jsonDecode(response.body));
      }
    } catch (e) {
      debugPrint('ApiService Create Error: $e');
    }
    return null;
  }

  static Future<Patient> savePatient(Map<String, dynamic> data,
      {int? patientId}) async {
    final uri = Uri.parse(
        patientId == null ? '$baseUrl/patient' : '$baseUrl/patient/$patientId');
    final response = patientId == null
        ? await http.post(uri,
            headers: await _authHeaders(), body: jsonEncode(data))
        : await http.put(uri,
            headers: await _authHeaders(), body: jsonEncode(data));
    if (response.statusCode == 200 || response.statusCode == 201) {
      return Patient.fromJson(
          jsonDecode(response.body) as Map<String, dynamic>);
    }
    final message =
        response.body.isEmpty ? 'Unable to save patient.' : response.body;
    throw Exception(message);
  }

  static Future<void> deletePatient(int patientId) async {
    final response = await http.delete(Uri.parse('$baseUrl/patient/$patientId'),
        headers: await _authHeaders());
    if (response.statusCode != 204) {
      throw Exception('Unable to delete patient.');
    }
  }

  static Future<Patient> updateMyProfile(Map<String, dynamic> data) async {
    final response = await http.put(
      Uri.parse('$baseUrl/patient/me'),
      headers: await _authHeaders(),
      body: jsonEncode(data),
    );
    if (response.statusCode == 200) {
      return Patient.fromJson(
          jsonDecode(response.body) as Map<String, dynamic>);
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<TriageWorkflow> startTriageWorkflow({
    required String symptoms,
    Map<String, dynamic>? vitals,
    bool isFollowUp = false,
  }) async {
    final response = await http
        .post(
          Uri.parse('$baseUrl/triage-workflows'),
          headers: await _authHeaders(),
          body: jsonEncode({
            'symptoms': symptoms.trim(),
            'isFollowUp': isFollowUp,
            if (vitals != null) 'vitals': vitals,
          }),
        )
        .timeout(const Duration(seconds: 85));
    if (response.statusCode == 201) {
      return TriageWorkflow.fromJson(jsonDecode(response.body));
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<TriageWorkflow> getTriageWorkflow(int workflowId) async {
    final response = await http.get(
      Uri.parse('$baseUrl/triage-workflows/$workflowId'),
      headers: await _authHeaders(),
    );
    if (response.statusCode == 200) {
      return TriageWorkflow.fromJson(jsonDecode(response.body));
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<TriageWorkflow> continueTriageWorkflow({
    required int workflowId,
    required String answers,
  }) async {
    final response = await http
        .post(Uri.parse('$baseUrl/triage-workflows/$workflowId/continue'),
            headers: await _authHeaders(),
            body: jsonEncode({'answers': answers}))
        .timeout(const Duration(seconds: 85));
    if (response.statusCode == 200) {
      return TriageWorkflow.fromJson(jsonDecode(response.body));
    }
    throw Exception(_errorMessage(response.body));
  }
}
