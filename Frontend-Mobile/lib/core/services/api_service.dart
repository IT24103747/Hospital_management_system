import 'dart:convert';
import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;
import 'package:smartcare_mobile/core/services/secure_token_storage.dart';
import 'package:smartcare_mobile/models/appointment.dart';
import 'package:smartcare_mobile/models/medical_record.dart';
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
  static http.Client _client = http.Client();

  @visibleForTesting
  static void setHttpClientForTesting(http.Client client) {
    _client = client;
  }

  @visibleForTesting
  static void resetHttpClientForTesting() {
    _client.close();
    _client = http.Client();
  }

  static Future<Map<String, String>> _authHeaders() async {
    final token = await SecureTokenStorage.readToken();
    return {
      'Content-Type': 'application/json',
      if (token != null && token.isNotEmpty) 'Authorization': 'Bearer $token',
    };
  }

  static Future<List<Map<String, dynamic>>>
      getAppointmentNotifications() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/appointment/notifications'),
      headers: await _authHeaders(),
    );
    if (response.statusCode != 200) {
      throw Exception('Unable to load notifications.');
    }
    return (jsonDecode(response.body) as List<dynamic>)
        .map((item) => item as Map<String, dynamic>)
        .toList();
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
    final response = await _client.get(uri, headers: await _authHeaders());
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
    final response = await _client.post(
      Uri.parse('$baseUrl/auth/login'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'email': email.trim(), 'password': password}),
    );
    if (response.statusCode == 200) {
      return jsonDecode(response.body) as Map<String, dynamic>;
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<void> register(Map<String, dynamic> patient) async {
    final response = await _client.post(Uri.parse('$baseUrl/auth/register'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode(patient));
    if (response.statusCode != 201) {
      throw Exception(_errorMessage(response.body));
    }
  }

  static Future<void> changePassword({
    required String email,
    required String currentPassword,
    required String newPassword,
  }) async {
    final response = await _client.post(
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
        if (decoded.containsKey('message')) {
          return decoded['message'].toString();
        }
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
      final response = await _client.get(Uri.parse('$baseUrl/patient/me'),
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
      final response = await _client.get(Uri.parse('$baseUrl/patient/$id'),
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
      final response = await _client.post(
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
        ? await _client.post(uri,
            headers: await _authHeaders(), body: jsonEncode(data))
        : await _client.put(uri,
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
    final response = await _client.delete(
        Uri.parse('$baseUrl/patient/$patientId'),
        headers: await _authHeaders());
    if (response.statusCode != 204) {
      throw Exception('Unable to delete patient.');
    }
  }

  static Future<Patient> updateMyProfile(Map<String, dynamic> data) async {
    final response = await _client.put(
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

  static Future<List<Appointment>> getMyAppointments() async {
    final uri = Uri.parse('$baseUrl/appointment').replace(queryParameters: {
      'page': '1',
      'pageSize': '50',
      'sortBy': 'startAt',
      'sortDirection': 'desc',
    });
    final response = await _client.get(uri, headers: await _authHeaders());
    if (response.statusCode == 200) {
      final body = jsonDecode(response.body);
      final items = body is Map<String, dynamic>
          ? body['data'] as List<dynamic>? ?? []
          : body as List<dynamic>? ?? [];
      return items
          .map((item) => Appointment.fromJson(item as Map<String, dynamic>))
          .toList();
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<List<DoctorTimeSlot>> getAvailableAppointmentSlots() async {
    final uri = Uri.parse('$baseUrl/appointment/available-slots')
        .replace(queryParameters: {'onlyAvailable': 'true'});
    final response = await _client.get(uri, headers: await _authHeaders());
    if (response.statusCode == 200) {
      final items = jsonDecode(response.body) as List<dynamic>? ?? [];
      return items
          .map((item) => DoctorTimeSlot.fromJson(item as Map<String, dynamic>))
          .where((slot) => slot.isActive && slot.availableCount > 0)
          .toList();
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<List<DoctorLookup>> getAppointmentDoctors() async {
    final response = await _client.get(
        Uri.parse('$baseUrl/appointment/doctors'),
        headers: await _authHeaders());
    if (response.statusCode == 200) {
      final items = jsonDecode(response.body) as List<dynamic>? ?? [];
      return items
          .map((item) => DoctorLookup.fromJson(item as Map<String, dynamic>))
          .toList();
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<List<String>> getAppointmentSpecializations() async {
    final response = await _client.get(
        Uri.parse('$baseUrl/appointment/specializations'),
        headers: await _authHeaders());
    if (response.statusCode == 404) {
      return [];
    }
    if (response.statusCode == 200) {
      final items = jsonDecode(response.body) as List<dynamic>? ?? [];
      return items
          .map((item) => item.toString().trim())
          .where((item) => item.isNotEmpty)
          .toSet()
          .toList()
        ..sort();
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<Appointment> bookAppointment({
    required int doctorTimeSlotId,
    required String patientName,
    required String patientPhone,
    String? patientEmail,
    required String appointmentType,
  }) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/appointment'),
      headers: await _authHeaders(),
      body: jsonEncode({
        'doctorTimeSlotId': doctorTimeSlotId,
        'patientName': patientName.trim(),
        'patientPhone': patientPhone.trim(),
        if (patientEmail != null && patientEmail.trim().isNotEmpty)
          'patientEmail': patientEmail.trim().toLowerCase(),
        'appointmentType': appointmentType.trim(),
      }),
    );
    if (response.statusCode == 201 || response.statusCode == 200) {
      return Appointment.fromJson(jsonDecode(response.body));
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<Appointment> rescheduleAppointment({
    required int appointmentId,
    required int doctorTimeSlotId,
  }) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/appointment/$appointmentId/reschedule'),
      headers: await _authHeaders(),
      body: jsonEncode({'doctorTimeSlotId': doctorTimeSlotId}),
    );
    if (response.statusCode == 200) {
      return Appointment.fromJson(jsonDecode(response.body));
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<Appointment> cancelAppointment({
    required int appointmentId,
    required String reason,
  }) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/appointment/$appointmentId/cancel'),
      headers: await _authHeaders(),
      body: jsonEncode({'reason': reason.trim()}),
    );
    if (response.statusCode == 200) {
      return Appointment.fromJson(jsonDecode(response.body));
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
    final response = await _client.get(
      Uri.parse('$baseUrl/triage-workflows/$workflowId'),
      headers: await _authHeaders(),
    );
    if (response.statusCode == 200) {
      return TriageWorkflow.fromJson(jsonDecode(response.body));
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<List<TriageWorkflow>> getTriageWorkflowHistory() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/triage-workflows/history'),
      headers: await _authHeaders(),
    );
    if (response.statusCode == 200) {
      return (jsonDecode(response.body) as List<dynamic>)
          .map((item) => TriageWorkflow.fromJson(item as Map<String, dynamic>))
          .toList();
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<TriageWorkflow> continueTriageWorkflow({
    required int workflowId,
    required List<Map<String, dynamic>> answers,
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

  // ---------- Patient Care Agent Workflow ----------

  static Future<Map<String, dynamic>> createTriageAppointmentProposal({
    required String symptoms,
    String? specialty,
    bool requestAppointmentProposal = false,
    Map<String, dynamic>? vitals,
  }) async {
    final response = await _client
        .post(
          Uri.parse('$baseUrl/patient-care/triage-appointment-proposal'),
          headers: await _authHeaders(),
          body: jsonEncode({
            'symptoms': symptoms.trim(),
            'requestAppointmentProposal': requestAppointmentProposal,
            if (specialty != null && specialty.trim().isNotEmpty)
              'specialty': specialty.trim(),
            if (vitals != null) 'vitals': vitals,
          }),
        )
        .timeout(const Duration(seconds: 85));
    if (response.statusCode == 200) {
      return jsonDecode(response.body) as Map<String, dynamic>;
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<Map<String, dynamic>> confirmAppointmentProposal({
    required int proposalId,
    required int doctorTimeSlotId,
  }) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/appointment-proposals/$proposalId/confirm'),
      headers: await _authHeaders(),
      body: jsonEncode({'doctorTimeSlotId': doctorTimeSlotId}),
    );
    if (response.statusCode == 200) {
      return jsonDecode(response.body) as Map<String, dynamic>;
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<List<Map<String, dynamic>>> getPatientCareHistory() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/patient-care/history'),
      headers: await _authHeaders(),
    );
    if (response.statusCode == 200) {
      return (jsonDecode(response.body) as List<dynamic>)
          .map((item) => item as Map<String, dynamic>)
          .toList();
    }
    throw Exception(_errorMessage(response.body));
  }

  // ---------- Medical Records Endpoints ----------

  static Future<List<MedicalRecord>> getMyMedicalRecords() async {
    final response = await _client.get(
      Uri.parse('$baseUrl/medicalrecord/me'),
      headers: await _authHeaders(),
    );
    if (response.statusCode == 200) {
      final list = jsonDecode(response.body) as List<dynamic>;
      return list
          .map((item) => MedicalRecord.fromJson(item as Map<String, dynamic>))
          .toList();
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<MedicalRecord> getMedicalRecordById(int id) async {
    final response = await _client.get(
      Uri.parse('$baseUrl/medicalrecord/$id'),
      headers: await _authHeaders(),
    );
    if (response.statusCode == 200) {
      return MedicalRecord.fromJson(jsonDecode(response.body));
    }
    throw Exception(_errorMessage(response.body));
  }

  static Future<MedicalRecordAttachment> addMedicalRecordAttachment(
    int recordId, {
    required String fileName,
    required String fileType,
    required String fileUrl,
    required int fileSize,
  }) async {
    final response = await _client.post(
      Uri.parse('$baseUrl/medicalrecord/$recordId/attachments'),
      headers: await _authHeaders(),
      body: jsonEncode({
        'fileName': fileName,
        'fileType': fileType,
        'fileUrl': fileUrl,
        'fileSize': fileSize,
      }),
    );
    if (response.statusCode == 201) {
      return MedicalRecordAttachment.fromJson(jsonDecode(response.body));
    }
    throw Exception(_errorMessage(response.body));
  }
}
