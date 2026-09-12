# Appointment Scheduling Agent

This feature lives inside the existing ASP.NET Core API. It adds no database tables,
doctor-management logic, SMS integration, or frontend routes. The existing triage agent
is unchanged.

## Dependencies and data flow

`AppointmentAgentController` resolves the patient through `IPatientService`, following
the current JWT email/profile convention. Patients can act only for themselves;
admins may supply an existing `patientId`. Doctors have no access to this booking API,
matching the current appointment-booking roles.

`AppointmentSchedulingAgent` asks Gemini for a JSON decision, executes its selected
tool, adds the result to the local conversation, and repeats (default 8 steps, total
3-minute deadline). It uses Gemini's `generateContent` endpoint with a structured-output
schema. Gemini can select only the allow-listed decision protocol; it never receives
database access.

The tools reuse:

- `IDoctorService.GetRegistrationsAsync("Approved")` and `GetByIdAsync` for doctor discovery and eligibility.
- `IAppointmentService.GetSlotsAsync(..., onlyAvailable: true, doctorId)` for the existing
  shared availability data. `IDoctorScheduleService` manages a doctor's own schedules;
  it is intentionally not called with a patient's identity.
- `IAppointmentService.CreateAppointmentAsync` for booking, capacity validation,
  assignment of appointment numbers and persistence. Appointment times use the scheduled session start; queue numbers do not imply individual consultation times.

No agent class queries a repository or `ApplicationDbContext`. Search uses the next
available appointment number/time supplied by the existing service, not model arithmetic.
Local date filtering uses Asia/Colombo because the existing service's date filter is UTC.

The LLM only selects references observed during the current request. Tool results omit
patient details and private doctor registration fields. Unknown references, unsupported
tools, extra JSON fields, and invalid arguments cannot become writes. API response facts
and booking messages are assembled from service results, not unrestricted model prose.
Recommendations and bookings recheck observed slots. A successful booking ends the loop,
so a repeated model action cannot book twice in one request. Creation locks the selected
session while assigning the next free appointment number and saving the booking. The
existing unique index remains a final guard; conflicts do not trigger write retries.

## Run locally

```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_GEMINI_API_KEY" --project Backend/HospitalManagementSystem.Api
dotnet run --project Backend/HospitalManagementSystem.Api
```

Configure `Gemini:ApiKey` only through User Secrets locally and deployment secrets in
production. The default model is `gemini-2.5-flash`; it can be changed with
`AppointmentAgent__Model`. React and Flutter never receive the Gemini API key.

## API

The former `POST /api/appointment-agent` endpoint has been removed. Patient-facing
appointment proposals now use the Patient Care workflow endpoints instead.

Find options (no write):

```json
{
  "message": "Find a cardiology appointment tomorrow morning. Suggest alternatives if none are available.",
  "allowBooking": false
}
```

Book for the authenticated patient:

```json
{
  "message": "Book the earliest available consultation with Dr. Silva on 2026-10-07.",
  "allowBooking": true
}
```

For an admin booking, also supply the selected existing `patientId`. Use a future date
and a real doctor name. `allowBooking` is an explicit client intent and defaults to false;
the model cannot change it. The patient's name/contact details come from their profile.
This minimal feature books consultations and does not change/cancel existing appointments.

Responses contain `status`, `message`, `doctors`, `slots`, and optional `booking`.
Slot objects include the real slot/doctor IDs, fee, room, next available appointment number,
with a `+05:30` offset. `Booked` includes the ID returned
by the existing appointment service. Recommendations are not reservations.

This endpoint is stateless: for a follow-up, send the full doctor/date/time preference
again. It does not trust client-supplied chat history or tool results. A new booking HTTP
request is a new operation; clients must prevent duplicate submissions and must not
automatically retry a booking after an uncertain network response. Use existing appointment
history to check the result first. No cross-request idempotency infrastructure is added.

Malformed/unavailable Gemini responses return 503; unknown patient profiles return 404;
cross-patient access is forbidden; raced appointment numbers return 409. Business-rule
rejections return `BookingUnavailable`, and an exhausted loop returns `NeedsDetails`.

## Verification

```powershell
dotnet test Backend/HospitalManagementSystem.Api.Tests --configuration Verify
```

Tests substitute model decisions/HTTP responses and use the actual existing services with
the project's EF InMemory provider. No tests book against the development database.
