import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:smartcare_mobile/models/patient.dart';
import 'package:smartcare_mobile/models/vitals.dart';
import 'package:smartcare_mobile/models/triage_result.dart';

class ApiService {
  // Android emulators use 10.0.2.2 to reach the development machine.
  // For a physical device, replace this with your computer's LAN IP address.
  static final String baseUrl = kIsWeb
      ? 'http://localhost:5000/api'
      : defaultTargetPlatform == TargetPlatform.android
          ? 'http://10.0.2.2:5000/api'
          : 'http://localhost:5000/api';

  static Future<List<Patient>> getPatients({String search = ''}) async {
    final query = <String, String>{
      'page': '1',
      'pageSize': '50',
      'sortBy': 'name',
      'sortDirection': 'asc',
      if (search.trim().isNotEmpty) 'search': search.trim(),
    };
    final uri = Uri.parse('$baseUrl/patient').replace(queryParameters: query);
    final response = await http.get(uri);
    if (response.statusCode != 200) {
      throw Exception('Unable to load patient records.');
    }
    final body = jsonDecode(response.body) as Map<String, dynamic>;
    final items = body['data'] as List<dynamic>? ?? [];
    return items
        .map((item) => Patient.fromJson(item as Map<String, dynamic>))
        .toList();
  }

  static Future<Map<String, dynamic>> login({required String email, required String password}) async {
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
    final response = await http.post(Uri.parse('$baseUrl/auth/register'), headers: {'Content-Type': 'application/json'}, body: jsonEncode(patient));
    if (response.statusCode != 201) throw Exception(_errorMessage(response.body));
  }

  static String _errorMessage(String body) {
    try {
      final decoded = jsonDecode(body);
      if (decoded is Map<String, dynamic>) {
        if (decoded.containsKey('message')) return decoded['message'].toString();
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

  static Future<Patient?> getPatientByEmail(String email) async {
    try {
      final uri = Uri.parse('$baseUrl/patient/me')
          .replace(queryParameters: {'email': email.trim().toLowerCase()});
      final response = await http.get(uri);
      if (response.statusCode == 200) {
        return Patient.fromJson(jsonDecode(response.body));
      }
    } catch (e) {
      debugPrint('ApiService getPatientByEmail Error: $e');
    }
    return null;
  }

  static Future<Patient?> getPatientById(int id) async {
    try {
      final response = await http.get(Uri.parse('$baseUrl/patient/$id'));
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
        headers: {'Content-Type': 'application/json'},
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
    final uri = Uri.parse(patientId == null
        ? '$baseUrl/patient'
        : '$baseUrl/patient/$patientId');
    final response = patientId == null
        ? await http.post(uri,
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode(data))
        : await http.put(uri,
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode(data));
    if (response.statusCode == 200 || response.statusCode == 201) {
      return Patient.fromJson(
          jsonDecode(response.body) as Map<String, dynamic>);
    }
    final message =
        response.body.isEmpty ? 'Unable to save patient.' : response.body;
    throw Exception(message);
  }

  static Future<void> deletePatient(int patientId) async {
    final response =
        await http.delete(Uri.parse('$baseUrl/patient/$patientId'));
    if (response.statusCode != 204) {
      throw Exception('Unable to delete patient.');
    }
  }

  static Future<TriageResult> performAiTriage({
    required int patientId,
    required String symptoms,
    Vitals? recentVitals,
  }) async {
    await Future.delayed(const Duration(seconds: 2));

    final text = symptoms.toLowerCase();
    TriageRiskLevel level = TriageRiskLevel.routine;
    int score = 3;
    String department = 'General OPD';
    String summary =
        'Patient presents mild symptoms. Standard OPD consultation advised.';
    List<String> actions = [
      'Schedule a routine OPD appointment within 48 hours.',
      'Stay hydrated and monitor vitals daily.',
    ];

    if (text.contains('chest pain') ||
        text.contains('breath') ||
        text.contains('unconscious') ||
        text.contains('stroke')) {
      level = TriageRiskLevel.critical;
      score = 9;
      department = 'Cardiology & Emergency ER';
      summary =
          'CRITICAL: Symptoms suggest acute cardiovascular or respiratory distress. Immediate triage required.';
      actions = [
        'Proceed immediately to the nearest Emergency Clinic.',
        'Alert Duty ER Officer for priority bed allocation.',
        'Avoid physical exertion.',
      ];
    } else if (text.contains('fever') ||
        text.contains('vomiting') ||
        text.contains('bleeding') ||
        text.contains('fracture')) {
      level = TriageRiskLevel.urgent;
      score = 7;
      department = 'Urgent Care / Internal Medicine';
      summary =
          'URGENT: Patient exhibits moderate risk requiring same-day clinical evaluation.';
      actions = [
        'Visit Urgent Care clinic within 4 hours.',
        'Keep record of temperature spikes.',
      ];
    }

    return TriageResult(
      patientId: patientId,
      symptomsText: symptoms,
      riskLevel: level,
      urgencyScore: score,
      recommendedDepartment: department,
      aiSummary: summary,
      recommendedActions: actions,
    );
  }
}
