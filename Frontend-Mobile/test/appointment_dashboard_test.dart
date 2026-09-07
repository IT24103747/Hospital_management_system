import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
import 'package:smartcare_mobile/core/services/secure_token_storage.dart';
import 'package:smartcare_mobile/layouts/dashboard_layout.dart';

void main() {
  tearDown(() {
    ApiService.resetHttpClientForTesting();
    SecureTokenStorage.resetTokenForTesting();
  });

  testWidgets('patient sees saved schedule changes in notifications', (tester) async {
    SharedPreferences.setMockInitialValues({});
    SecureTokenStorage.setTokenForTesting('patient-token');
    ApiService.setHttpClientForTesting(MockClient((request) async {
      if (request.url.path == '/api/appointment/notifications') {
        expect(request.headers['Authorization'], 'Bearer patient-token');
        return jsonResponse([{
          'message': 'Your appointment time has changed to 11:00 AM.',
          'createdAt': '2026-09-07T08:00:00Z',
        }]);
      }
      return jsonResponse({'data': []});
    }));
    await tester.pumpWidget(const MaterialApp(home: DashboardLayout(title: 'Notifications')));
    await tester.pumpAndSettle();
    expect(find.text('Appointment Updated'), findsOneWidget);
    expect(find.text('Your appointment time has changed to 11:00 AM.'), findsOneWidget);
    expect(find.text('Your cardiology visit has been confirmed.'), findsNothing);
  });

  testWidgets('patient can complete the appointment booking form',
      (tester) async {
    final requests = <http.Request>[];
    await pumpAppointmentsDashboard(tester, requests: requests);

    await tester.tap(find.text('Book Appointment'));
    await tester.pumpAndSettle();
    await selectDropdownValue(tester, 0, 'Cardiology');
    await selectDropdownValue(tester, 1, 'Dr. Ada Lovelace');
    await tester.tap(find.text('Select appointment no'));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('#1').last);
    await tester.pumpAndSettle();
    await tester.tap(find.text('#1').last);
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Confirm'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Confirm'));
    await tester.pumpAndSettle();

    expect(
      requests.any((request) =>
          request.method == 'POST' && request.url.path == '/api/appointment'),
      isTrue,
    );
    expect(find.text('Appointment booked successfully.'), findsOneWidget);
  });

  testWidgets('patient can switch between upcoming appointments and history',
      (tester) async {
    await pumpAppointmentsDashboard(tester);

    expect(find.text('Upcoming appointments'), findsOneWidget);
    expect(find.text('Dr. Ada Lovelace'), findsWidgets);

    await tester.tap(find.text('View History'));
    await tester.pumpAndSettle();

    expect(find.text('Appointment history'), findsOneWidget);
    expect(find.text('Dr. Grace Hopper'), findsWidgets);
  });

  testWidgets('patient can submit a cancellation reason', (tester) async {
    final requests = <http.Request>[];
    await pumpAppointmentsDashboard(tester, requests: requests);

    await tester.ensureVisible(find.text('Cancel'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cancel').first);
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextFormField).last, 'Patient unavailable');
    await tester.tap(find.widgetWithText(FilledButton, 'Cancel appointment'));
    await tester.pumpAndSettle();

    expect(
      requests.any((request) =>
          request.method == 'POST' &&
          request.url.path == '/api/appointment/10/cancel' &&
          request.body.contains('Patient unavailable')),
      isTrue,
    );
    expect(find.text('Appointment cancelled.'), findsOneWidget);
  });

  testWidgets('patient can reschedule by picking another slot', (tester) async {
    final requests = <http.Request>[];
    await pumpAppointmentsDashboard(tester, requests: requests);

    await tester.ensureVisible(find.text('Reschedule'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Reschedule').first);
    await tester.pumpAndSettle();
    expect(find.text('Reschedule appointment'), findsOneWidget);

    await tester.tap(find.text('Dr. Alan Turing').last);
    await tester.pumpAndSettle();

    expect(
      requests.any((request) =>
          request.method == 'POST' &&
          request.url.path == '/api/appointment/10/reschedule' &&
          request.body.contains('"doctorTimeSlotId":12')),
      isTrue,
    );
    expect(find.text('Appointment rescheduled.'), findsOneWidget);
  });

  testWidgets('appointment API failures show a retryable error state',
      (tester) async {
    await pumpAppointmentsDashboard(tester, failAppointments: true);

    expect(find.text('Unable to load appointments.'), findsOneWidget);
    expect(find.text('Retry'), findsOneWidget);
  });
}

Future<void> pumpAppointmentsDashboard(
  WidgetTester tester, {
  List<http.Request>? requests,
  bool failAppointments = false,
}) async {
  SharedPreferences.setMockInitialValues({
    'patient_full_name': 'Amal Perera',
    'patient_email': 'amal.perera@email.com',
  });
  SecureTokenStorage.setTokenForTesting('test-token');
  ApiService.setHttpClientForTesting(MockClient((request) async {
    requests?.add(request);
    if (failAppointments && request.url.path == '/api/appointment') {
      return jsonResponse({'message': 'Unable to load appointments.'}, 500);
    }

    if (request.method == 'GET' && request.url.path == '/api/patient/me') {
      return jsonResponse(patientJson());
    }

    if (request.method == 'GET' && request.url.path == '/api/appointment') {
      return jsonResponse({'data': appointmentJson()});
    }

    if (request.method == 'GET' &&
        request.url.path == '/api/appointment/available-slots') {
      return jsonResponse(slotJson());
    }

    if (request.method == 'GET' &&
        request.url.path == '/api/appointment/doctors') {
      return jsonResponse([
        {'doctorId': 1, 'doctorName': 'Dr. Ada Lovelace', 'specialty': 'Cardiology'},
        {'doctorId': 2, 'doctorName': 'Dr. Alan Turing', 'specialty': 'Cardiology'},
      ]);
    }

    if (request.method == 'GET' &&
        request.url.path == '/api/appointment/specializations') {
      return jsonResponse(['Cardiology']);
    }

    if (request.method == 'POST' && request.url.path == '/api/appointment') {
      return jsonResponse({...appointmentJson().first, 'appointmentId': 99}, 200);
    }

    if (request.method == 'POST' &&
        request.url.path == '/api/appointment/10/reschedule') {
      return jsonResponse({
        ...appointmentJson().first,
        'doctorTimeSlotId': 12,
        'doctorName': 'Dr. Alan Turing',
        'status': 'Confirmed',
      });
    }

    if (request.method == 'POST' &&
        request.url.path == '/api/appointment/10/cancel') {
      return jsonResponse({
        ...appointmentJson().first,
        'status': 'Cancelled',
        'cancellationReason': 'Patient unavailable',
      });
    }

    return jsonResponse({'message': 'Not found'}, 404);
  }));

  await tester.pumpWidget(const MaterialApp(
    home: DashboardLayout(title: 'Appointments'),
  ));
  await tester.pumpAndSettle();
}

Future<void> selectDropdownValue(
    WidgetTester tester, int dropdownIndex, String value) async {
  await tester.tap(find.byType(DropdownButtonFormField<String>).at(dropdownIndex));
  await tester.pumpAndSettle();
  await tester.tap(find.text(value).last);
  await tester.pumpAndSettle();
}

http.Response jsonResponse(Object body, [int status = 200]) => http.Response(
      jsonEncode(body),
      status,
      headers: {'content-type': 'application/json'},
    );

Map<String, dynamic> patientJson() => {
      'patientId': 1,
      'firstName': 'Amal',
      'lastName': 'Perera',
      'dateOfBirth': '1985-03-14T00:00:00Z',
      'gender': 'Male',
      'nic': '850314123V',
      'phoneNumber': '0771234567',
      'email': 'amal.perera@email.com',
      'address': 'Colombo',
    };

List<Map<String, dynamic>> appointmentJson() => [
      {
        'appointmentId': 10,
        'doctorTimeSlotId': 11,
        'doctorId': 1,
        'patientId': 1,
        'appointmentNumber': 1,
        'estimatedStartAt': '2026-09-10T08:00:00Z',
        'patientName': 'Amal Perera',
        'patientPhone': '0771234567',
        'patientEmail': 'amal.perera@email.com',
        'doctorName': 'Dr. Ada Lovelace',
        'specialty': 'Cardiology',
        'roomId': 3,
        'roomNumber': 'C-101',
        'roomName': 'Consultation Room',
        'floor': 'First',
        'startAt': '2026-09-10T08:00:00Z',
        'endAt': '2026-09-10T09:00:00Z',
        'consultationFee': 4500,
        'appointmentType': 'Consultation',
        'reason': 'Checkup',
        'status': 'Confirmed',
        'cancellationReason': '',
        'notes': '',
        'createdAt': '2026-09-01T08:00:00Z',
        'updatedAt': '2026-09-01T08:00:00Z',
      },
      {
        'appointmentId': 20,
        'doctorTimeSlotId': 21,
        'doctorId': 3,
        'patientId': 1,
        'appointmentNumber': 2,
        'estimatedStartAt': '2026-08-01T08:30:00Z',
        'patientName': 'Amal Perera',
        'patientPhone': '0771234567',
        'patientEmail': 'amal.perera@email.com',
        'doctorName': 'Dr. Grace Hopper',
        'specialty': 'Neurology',
        'roomId': 4,
        'roomNumber': 'N-101',
        'roomName': 'Neuro Room',
        'floor': 'Second',
        'startAt': '2026-08-01T08:00:00Z',
        'endAt': '2026-08-01T09:00:00Z',
        'consultationFee': 5000,
        'appointmentType': 'Review',
        'reason': 'Review',
        'status': 'Completed',
        'cancellationReason': '',
        'notes': '',
        'createdAt': '2026-07-20T08:00:00Z',
        'updatedAt': '2026-08-01T09:00:00Z',
      },
    ];

List<Map<String, dynamic>> slotJson() => [
      {
        'doctorTimeSlotId': 11,
        'doctorId': 1,
        'doctorName': 'Dr. Ada Lovelace',
        'specialty': 'Cardiology',
        'roomId': 3,
        'roomNumber': 'C-101',
        'roomName': 'Consultation Room',
        'floor': 'First',
        'startAt': '2026-09-10T08:00:00Z',
        'endAt': '2026-09-10T09:00:00Z',
        'capacity': 2,
        'bookedCount': 0,
        'bookedAppointmentNumbers': [],
        'nextAppointmentNumber': 1,
        'nextEstimatedStartAt': '2026-09-10T08:00:00Z',
        'isActive': true,
        'consultationFee': 4500,
      },
      {
        'doctorTimeSlotId': 12,
        'doctorId': 2,
        'doctorName': 'Dr. Alan Turing',
        'specialty': 'Cardiology',
        'roomId': 5,
        'roomNumber': 'C-102',
        'roomName': 'Consultation Room 2',
        'floor': 'First',
        'startAt': '2026-09-11T08:00:00Z',
        'endAt': '2026-09-11T09:00:00Z',
        'capacity': 2,
        'bookedCount': 0,
        'bookedAppointmentNumbers': [],
        'nextAppointmentNumber': 1,
        'nextEstimatedStartAt': '2026-09-11T08:00:00Z',
        'isActive': true,
        'consultationFee': 4500,
      },
    ];
