# Hospital AI Assistant

## Existing implementation and scope

The patient application is `Frontend-Mobile` (Flutter). `Frontend-Web` is the
Doctor/Admin application; its clinical-review page remains available for independent
doctor decisions. Combining patient assistance does not remove this staff workflow or
grant patients access to clinical-review APIs.

The two existing capabilities are:

- **Patient help / safety triage:** `ClinicalSafetyTriageAgent`, backed by the
  existing SafeTriage extraction and deterministic safety tools. The separate
  `TriageWorkflowService` also supports persisted follow-up questions and the
  doctor-review queue.
- **Appointment assistance:** the active `HospitalAppointmentProposalAgent`, backed
  by approved doctor discovery, real sessions and existing appointment services.
  `SafetyValidationApprovalAgent` handles patient confirmation. The older
  `AppointmentSchedulingAgent` is preserved but is not registered or exposed by an API.

These capabilities contain multiple internal specialists and controlled tools. A
single patient request can require safety assessment, appointment discovery and
approval validation in sequence. They are not separately numbered in the patient UI.

Doctor schedule automation and medical-report AI are future capabilities. Existing
manual doctor search and medical-record pages remain usable; their existence does not
mean a dedicated AI agent has been implemented.

## Existing APIs and business rules

| Area | Existing endpoint or service |
| --- | --- |
| Safety assessment plus optional appointment proposal | `POST /api/patient-care/triage-appointment-proposal` |
| Patient assessment history | `GET /api/patient-care/history` |
| Legacy proposal confirmation | `POST /api/appointment-proposals/{proposalId}/confirm` |
| Persisted triage and follow-up | `POST /api/triage-workflows`, `POST /api/triage-workflows/{id}/continue` |
| Triage history and refresh | `GET /api/triage-workflows/history`, `GET /api/triage-workflows/{id}` |
| Doctor clinical review | `GET /api/triage-workflows/clinical-review/pending`, `GET /api/triage-workflows/{id}/clinical-review`, `POST /api/triage-workflows/{id}/review` |
| Clinical audit | `GET /api/triage-workflows/{id}/audit-events` |
| Appointment reads and mutations | `IAppointmentService` and existing `/api/appointment` endpoints |
| Approved doctor discovery | `IDoctorService` |

The backend assigns the next available appointment number when saving a booking.
An expected number in a proposal is provisional. Session start/end times describe
the consultation session, not an individual queue-number consultation time.

Clinical guidance remains non-diagnostic. Deterministic emergency/urgent rules and
existing SafeTriage escalation policy take priority over routine booking assistance.
The web reviewer can inspect safety evidence; only an authorized doctor can record
a clinical decision. See [the existing safety evaluation](agentic-ai-evaluation.md).

## Verification baseline

Before the shared-assistant implementation, the Flutter suite had **5 passing and
7 failing tests**. Failures included an obsolete appointment-number picker
expectation, fixed September 2026 appointment fixtures, old booking widget finders,
and notification/card label expectations. These are baseline findings, not evidence
that the assistant passes its own approval and routing tests.

Run Flutter tests from `Frontend-Mobile` with `flutter test --no-pub`. In the inspected
Windows environment, the batch launcher stalled; the working equivalent was:

```powershell
& 'C:\Users\mchan\Downloads\flutter\bin\cache\dart-sdk\bin\dart.exe' 'C:\Users\mchan\Downloads\flutter\bin\cache\flutter_tools.snapshot' test --no-pub
```

The tool needs write access to its SDK cache. Flutter's Dart constraint is
`>=3.0.0 <4.0.0`; tests use `flutter_test`, mock HTTP clients, and mocked local storage.

## Implemented shared workflow

The dashboard sidebar and home shortcut now open `HospitalAssistantScreen`. The old
`AiTriageScreen` and `PatientCareFlowScreen` classes are deprecated compatibility
wrappers for the same screen. The normal UI has no numbered agents or test workflow.
History restores server conversations, including pending approval. Earlier triage
and Patient Care assessments remain accessible through the history sheet.

The backend `HospitalAssistantService` routes patient goals to the existing bounded
SafeTriage workflow (`ITriageWorkflowService`), the active appointment proposal
agent, and the safety/approval agent. SafeTriage already coordinates seven internal
specialists; its rules, extraction, questions, and clinician review are reused.
The separate direct `ClinicalSafetyTriageAgent` and its existing APIs remain intact.
The shared coordinator adapts the persisted SafeTriage result to the appointment
proposal contract instead of running duplicate clinical extraction.

A symptom-and-booking request can execute safety intake and follow-up, doctor/slot
search, and explicit approval validation across several conversation turns. Clinical
questions are asked one at a time using their existing stable question IDs. The
server only submits the collected answers to the existing continuation API after
all issued questions are answered. An emergency answer interrupts clarification.

Routing is deliberately bounded and deterministic: hospital doctor/specialty names,
common specialty aliases, booking/cancellation/list intents, ISO dates, today,
tomorrow, weekdays, next week, and morning/afternoon/evening. Dates use Asia/Colombo.
For ambiguous dates and exact-hour filters it asks for clarification instead of
inventing a matching session. Weekday interpretation is shown in the returned dated
options. Search checks up to five matching doctors and returns up to five options.
It is not an unrestricted natural-language planner.

Supported execution states are UNDERSTANDING, ROUTING, GATHERING_INFORMATION,
WAITING_FOR_HUMAN_APPROVAL, EXECUTING, COMPLETED, FAILED, and CANCELLED. The persisted
pending proposal represents PROPOSING_ACTION before awaiting approval. The UI shows
ordinary progress text and completed server checks, not simulated agent timers or
private model reasoning.

## Shared endpoints and storage

All endpoints require the Patient role and resolve the current patient from the
existing authenticated email/profile lookup. Requests cannot choose another patient.

| Endpoint | Purpose |
| --- | --- |
| `GET /api/hospital-assistant/capabilities` | Registry capabilities and enabled quick actions |
| `GET /api/hospital-assistant/conversations` | Patient-owned recent conversations |
| `GET /api/hospital-assistant/conversations/{id}` | Restore messages, pending action, latest review state |
| `POST /api/hospital-assistant/messages` | `{conversationId?, message, requestId}`; reads, clarification, proposals only |
| `POST /api/hospital-assistant/conversations/{id}/actions` | `{actionId, decision, requestId, doctorTimeSlotId?, appointmentId?}` |

The response contains `conversationId`, `title`, `state`, `messages`,
`pendingAction`, `questions`, `appointments`, `slots`, `doctors`, and `capabilities`.
The Flutter models and backend records define the exact typed contract.

Migration `20260911120000_AddHospitalAssistantConversations` adds the patient-owned
conversation table with JSON state, messages, preferences, clinical workflow ID,
questions/answers, pending action, and request fingerprints. The existing
AppointmentProposal table continues to hold verified candidate slots, expiry,
confirmation timestamp and the final appointment ID. The database migration runs
through the project's existing startup migration process when the backend restarts.
No production database or real booking was used during automated verification.

## Approval and recovery

- Search, doctor discovery and appointment reads do not authorize booking.
- Creating an appointment requires choosing a proposed session and pressing
  **Confirm appointment**. The final ID and queue number come from the saved backend
  appointment. Expected queue numbers are explicitly provisional.
- Cancellation requires a reason, selection of an owned upcoming appointment and
  **Confirm cancellation**. Dismissing a proposal does not cancel an appointment.
- Chat assent such as ?that looks good? never executes a mutation.
- New preferences invalidate the previous proposal; action IDs and session IDs
  must match the server's current pending action. Expired actions are rejected.
- Availability, ownership, clinician requirements and session details are checked
  again at confirmation. A changed date/time, doctor, location or fee requires new
  approval; a changed provisional queue number is allowed.
- Requests carry UUIDs and persisted fingerprints. Identical retries return the
  stored conversation; reusing an ID with different content is rejected.
- PostgreSQL patient advisory locks and a transaction serialize confirmations
  across server instances and commit the booking plus conversation state together.
  Appointment creation joins the outer transaction and retains its session lock and
  unique-number guard. The legacy confirmation endpoint shares the patient lock.
- The UI disables repeated submissions. If a confirmation response is lost, it
  requires a read-only refresh instead of automatically repeating the write.
- Unresolved safety assessments are checked across the patient's conversations.
  Starting a fresh conversation cannot bypass pending questions or clinician review.
  Emergency/urgent outcomes cannot be conversationally downgraded to routine booking.
- Clinical review uses the existing web queue and Doctor-only review endpoint.
  The patient refreshes to see review changes; no notification or clinician decision
  is fabricated. Legacy Patient Care proposal clinical approval remains pending
  where its pre-existing completion endpoint is absent.

Rescheduling remains unavailable in the mobile assistant, consistent with the prior
request to remove it. The assistant can offer separate booking and cancellation
workflows, each with its own confirmation; it does not claim to move an appointment.
Doctor-schedule changes, medical-record changes, deletion, notifications and other
unsupported writes have no assistant execution route.

## Extending the registry

Implement and register `IHospitalAssistantReadAgent` through dependency injection.
Provide a stable capability ID, display label, enabled flag, starter prompt, intent
match and authorized read operation. `AssistantAgentRegistry` includes registered
capabilities automatically; an implementation with ID `medical-reports` or
`doctor-schedules` replaces that disabled placeholder. The existing conversation UI
renders its text and capability without separate agent screens.

Future state-changing tools must add a typed pending action and a server-validated
approval handler in `DecideAsync`, together with ownership, expiry, idempotency and
safety tests. Do not perform writes from `ReadAsync` or from message routing. New
structured result-card types may extend the response/UI contract without replacing
the common conversation shell.

## Files

Created:
- `Backend/HospitalManagementSystem.Api/AgenticAI/HospitalAssistant/AssistantModels.cs`
- `Backend/HospitalManagementSystem.Api/AgenticAI/HospitalAssistant/AssistantPreferences.cs`
- `Backend/HospitalManagementSystem.Api/AgenticAI/HospitalAssistant/HospitalAssistantService.cs`
- `Backend/HospitalManagementSystem.Api/Controllers/HospitalAssistantController.cs`
- `Backend/HospitalManagementSystem.Api/Migrations/20260911120000_AddHospitalAssistantConversations.cs`
- `Backend/HospitalManagementSystem.Api.Tests/AgenticAI/HospitalAssistantTests.cs`
- `Frontend-Mobile/lib/features/assistant/screens/hospital_assistant_screen.dart`
- `Frontend-Mobile/lib/models/hospital_assistant.dart`
- `Frontend-Mobile/test/hospital_assistant_test.dart`
- This document.

Modified:
- Backend `Program.cs`, `Data/ApplicationDbContext.cs` and migration model snapshot
  register the coordinator and conversation storage.
- `HospitalAppointmentProposalAgent.cs` accepts optional clinical context for
  non-symptom discovery, rejects failed-safe assessments, and filters time/date ranges.
- `SafetyApprovalTools.cs` rejects materially changed session details.
- `AppointmentProposalConfirmationController.cs` shares confirmation locking and
  checks unresolved safety workflows.
- `AppointmentRepository.cs` joins an existing booking transaction.
- Flutter `api_service.dart`, `dashboard_layout.dart`, and `patient_home_screen.dart`
  expose and connect the common conversation.
- The appointment agent README now distinguishes its legacy implementation from
  the active shared endpoints.

Deprecated: the two former patient AI screen implementations, replaced with
compatibility wrappers. No working backend agent or staff review screen was removed.

## Manual example flows

1. ?I need a cardiologist next Friday afternoon.? Review the dated real options,
   select one, and confirm. Check that the displayed final number matches My Appointments.
2. ?I have a mild headache. Find a doctor tomorrow.? Answer the safety questions,
   provide a specialty if asked, then review the proposal. Booking must wait for the
   completed safety path and the explicit button.
3. ?Do I have any appointments tomorrow?? Confirm only the authenticated patient's
   appointments appear and no approval/write is triggered.
4. ?Cancel my appointment.? Provide a reason, select an appointment, then cancel the
   proposal first; the appointment should remain unchanged. Repeat and explicitly
   confirm cancellation to verify the persisted cancelled status.
5. Propose a booking and type ?that looks good.? No booking occurs. Choose another
   date; the old approval must no longer work.
6. Report severe chest pain/breathing difficulty, then request booking. The existing
   safety guidance and clinician-review workflow must take priority; no routine
   proposal should appear, including after reopening the assistant.
7. Lose the network immediately after confirming. Refresh the conversation and
   check the saved outcome; do not automatically retry the mutation.
8. Tap Medical Reports or Doctor Schedules: disabled, Coming soon. No fabricated
   backend result should appear.

## Final verification

- New backend assistant tests: **15 passed**. Covers actual safety/follow-up to
  proposal collaboration, explicit booking and cancellation, final automatic
  numbering, repeated requests, changed session details, patient ownership,
  emergency interruption, cross-conversation safety, alternatives and registry flags.
- New Flutter widget tests: **4 passed**. Covers proposal-only messaging, explicit
  confirmation, restored approval, disabled future actions, and recovery after an
  uncertain confirmation response.
- Dart analysis of all changed mobile implementation files and the new tests:
  **No issues found**.
- Backend build succeeded. Five existing nullable warnings remain in PatientService.
- The last full backend run had **124 passed / 3 failed**. The failures are the old
  `AppointmentAgentApiTests` still expecting the already removed
  `/api/appointment-agent` endpoint. The subsequently added alternative-request
  test also passes in the 15-test targeted run.
- The previously recorded full Flutter baseline has 7 failures from earlier mobile
  changes; the new assistant widget tests pass independently. No obsolete tests were
  weakened to hide those failures.
- `git diff --check` passes.

Backend tests use an isolated EF InMemory database and controlled tool/extraction
substitutes. PostgreSQL advisory-lock behavior and the migration were reviewed in
code but not exercised against a live database. No real hospital data was changed.
The UI was widget-tested, not manually exercised on the user's emulator. Restart
the backend to apply the new conversation-table migration through normal startup,
then hot restart Flutter to use the shared assistant.
