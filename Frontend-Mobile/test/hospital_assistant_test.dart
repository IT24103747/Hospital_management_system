import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:smartcare_mobile/core/services/api_service.dart';
import 'package:smartcare_mobile/core/services/secure_token_storage.dart';
import 'package:smartcare_mobile/features/assistant/screens/hospital_assistant_screen.dart';

const capabilities = [
  {
    'id': 'patient-help',
    'label': 'Patient Help',
    'enabled': true,
    'prompt': 'I need help with my symptoms'
  },
  {
    'id': 'find-doctor',
    'label': 'Find Doctor',
    'enabled': true,
    'prompt': 'Find a doctor'
  },
  {
    'id': 'appointments',
    'label': 'Appointments',
    'enabled': true,
    'prompt': 'Show my appointments'
  },
  {
    'id': 'medical-reports',
    'label': 'Medical Reports',
    'enabled': false,
    'prompt': ''
  },
];

Map<String, dynamic> conversation(
        {bool pending = false, bool booked = false}) =>
    {
      'conversationId': '11111111-1111-4111-8111-111111111111',
      'title': 'Find a cardiologist',
      'state': pending ? 'WAITING_FOR_HUMAN_APPROVAL' : 'COMPLETED',
      'messages': [
        {
          'id': '1',
          'role': 'user',
          'text': 'Find a cardiologist',
          'progress': []
        },
        {
          'id': '2',
          'role': 'assistant',
          'text':
              booked ? 'Appointment confirmed.' : 'Please review your options.',
          'progress': []
        },
      ],
      'pendingAction': pending
          ? {
              'actionId': '22222222-2222-4222-8222-222222222222',
              'type': 'book',
              'title': 'Confirm an appointment',
              'description': 'Choose a session to confirm.',
              'expiresAt': DateTime.now()
                  .toUtc()
                  .add(const Duration(minutes: 30))
                  .toIso8601String(),
              'slots': [slot()],
              'appointments': [],
            }
          : null,
      'questions': [],
      'slots': [],
      'doctors': [],
      'appointments': booked
          ? [
              {
                ...slot(),
                'appointmentId': 501,
                'appointmentNumber': 9,
                'status': 'Confirmed'
              }
            ]
          : [],
      'capabilities': capabilities,
    };

Map<String, dynamic> slot() => {
      'doctorTimeSlotId': 11,
      'doctorName': 'Dr. Silva',
      'specialty': 'Cardiology',
      'startAt': '2030-10-08T15:00:00+05:30',
      'endAt': '2030-10-08T17:00:00+05:30',
      'appointmentNumber': 8,
      'consultationFee': 2500,
      'location': 'Room A-01',
    };

http.Response jsonResponse(Object value) =>
    http.Response(jsonEncode(value), 200,
        headers: {'content-type': 'application/json'});

void main() {
  tearDown(() {
    ApiService.resetHttpClientForTesting();
    SecureTokenStorage.resetTokenForTesting();
  });

  Future<void> pump(WidgetTester tester,
      Future<http.Response> Function(http.Request) handler) async {
    await tester.binding.setSurfaceSize(const Size(420, 1000));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    SecureTokenStorage.setTokenForTesting('patient-token');
    ApiService.setHttpClientForTesting(MockClient((request) async {
      expect(request.headers['Authorization'], 'Bearer patient-token');
      if (request.url.path.endsWith('/capabilities')) {
        return jsonResponse(capabilities);
      }
      return handler(request);
    }));
    await tester.pumpWidget(const MaterialApp(home: HospitalAssistantScreen()));
    await tester.pumpAndSettle();
  }

  testWidgets(
      'single option is selected automatically; explicit confirmation displays final number',
      (tester) async {
    final posts = <http.Request>[];
    await pump(tester, (request) async {
      if (request.method == 'GET') return jsonResponse([]);
      posts.add(request);
      if (request.url.path.endsWith('/messages')) {
        return jsonResponse(conversation(pending: true));
      }
      return jsonResponse(conversation(booked: true));
    });
    await tester.enterText(find.byType(TextField), 'Find a cardiologist');
    await tester.tap(find.byTooltip('Send message'));
    await tester.pumpAndSettle();
    expect(posts.length, 1);
    expect(posts.single.url.path, '/api/hospital-assistant/messages');
    expect(tester.widget<FilledButton>(
        find.widgetWithText(FilledButton, 'Confirm appointment')).onPressed,
        isNotNull);
    expect(posts.length, 1);
    await tester.ensureVisible(find.text('Confirm appointment'));
    await tester.tap(find.text('Confirm appointment'));
    await tester.pumpAndSettle();
    expect(posts.length, 2);
    final action = jsonDecode(posts.last.body) as Map<String, dynamic>;
    expect(action['decision'], 'confirm');
    expect(action['doctorTimeSlotId'], 11);
    expect(action.containsKey('appointmentNumber'), isFalse);
    expect(action.containsKey('patientId'), isFalse);
    expect(find.text('Appointment No: 9'), findsOneWidget);
    expect(find.text('Confirm appointment'), findsNothing);
    expect(tester.takeException(), isNull);
  });

  testWidgets(
      'multiple options require a selection before confirmation',
      (tester) async {
    final posts = <http.Request>[];
    await pump(tester, (request) async {
      posts.add(request);
      final response = conversation(pending: true);
      (response['pendingAction'] as Map)['slots'] = [
        slot(), {...slot(), 'doctorTimeSlotId': 12}
      ];
      return jsonResponse(response);
    });
    await tester.enterText(find.byType(TextField), 'Find a cardiologist');
    await tester.tap(find.byTooltip('Send message'));
    await tester.pumpAndSettle();
    final confirm = find.widgetWithText(FilledButton, 'Confirm appointment');
    expect(tester.widget<FilledButton>(confirm).onPressed, isNull);
    await tester.ensureVisible(find.byKey(const ValueKey('assistant-option-12')));
    await tester.tap(find.byKey(const ValueKey('assistant-option-12')));
    await tester.pumpAndSettle();
    expect(tester.widget<FilledButton>(confirm).onPressed, isNotNull);
    expect(posts.length, 1);
  });

  testWidgets(
      'restores pending server approval; future capability stays disabled',
      (tester) async {
    var writes = 0;
    await pump(tester, (request) async {
      if (request.method != 'GET') writes++;
      return jsonResponse(request.url.path.endsWith('/conversations')
          ? [
              {
                'conversationId': conversation()['conversationId'],
                'title': 'Find a cardiologist'
              }
            ]
          : conversation(pending: true));
    });
    await tester.tap(find.byTooltip('Conversation history'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Find a cardiologist'));
    await tester.pumpAndSettle();
    expect(find.text('Confirm appointment'), findsOneWidget);
    final chip = tester.widget<ActionChip>(
        find.widgetWithText(ActionChip, 'Medical Reports · Coming soon'));
    expect(chip.onPressed, isNull);
    expect(writes, 0);
    expect(tester.takeException(), isNull);
  });

  testWidgets(
      'uncertain confirmation is not retried and refresh restores actual result',
      (tester) async {
    var actionWrites = 0;
    await pump(tester, (request) async {
      if (request.url.path.endsWith('/conversations')) {
        return jsonResponse([
          {
            'conversationId': conversation()['conversationId'],
            'title': 'Find a cardiologist'
          }
        ]);
      }
      if (request.method == 'POST') {
        actionWrites++;
        throw http.ClientException('Connection lost after submission');
      }
      return jsonResponse(
          conversation(pending: actionWrites == 0, booked: actionWrites > 0));
    });
    await tester.tap(find.byTooltip('Conversation history'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Find a cardiologist'));
    await tester.pumpAndSettle();
    await tester
        .ensureVisible(find.byKey(const ValueKey('assistant-option-11')));
    await tester.tap(find.byKey(const ValueKey('assistant-option-11')));
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Confirm appointment'));
    await tester.tap(find.text('Confirm appointment'));
    await tester.pumpAndSettle();
    expect(actionWrites, 1);
    expect(
        tester
            .widget<FilledButton>(
                find.widgetWithText(FilledButton, 'Confirm appointment'))
            .onPressed,
        isNull);
    await tester.tap(find.byTooltip('Refresh conversation'));
    await tester.pumpAndSettle();
    expect(actionWrites, 1);
    expect(find.text('Appointment No: 9'), findsOneWidget);
  });

  testWidgets('sending a prompt is separate from tapping a quick action',
      (tester) async {
    final posts = <http.Request>[];
    await pump(tester, (request) async {
      if (request.method == 'POST') posts.add(request);
      return jsonResponse([]);
    });
    await tester.tap(find.widgetWithText(ActionChip, 'Find Doctor'));
    await tester.pumpAndSettle();
    expect(tester.widget<TextField>(find.byType(TextField)).controller!.text,
        'Find a doctor');
    expect(posts, isEmpty);
  });
}
