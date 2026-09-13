# Appointment agents and the Hospital AI Assistant

The active patient experience is the shared Hospital AI Assistant in Flutter.
See [the implementation and verification guide](../../../../Docs/hospital-ai-assistant.md).

The active backend coordinator is `HospitalAssistantService`. It reuses the
persisted SafeTriage workflow, `HospitalAppointmentProposalAgent`, the controlled
appointment tools, and `SafetyValidationApprovalAgent`. Appointment numbers are
assigned by the existing appointment service when a confirmed booking is saved.

`AppointmentSchedulingAgent` and its Gemini structured-decision implementation
remain in this folder for compatibility and existing unit tests. They are not
registered in Program.cs. The former `/api/appointment-agent` endpoint was removed
before the shared assistant; its old `allowBooking` request examples are not an
active API contract. Do not re-enable them as a substitute for explicit proposal
approval.

Use `POST /api/hospital-assistant/messages` to gather information or propose an
action, and `POST /api/hospital-assistant/conversations/{id}/actions` only after the
patient explicitly confirms a currently proposed option. Both require a Patient
JWT. The shared service owns conversation state, safety continuity and retry keys;
the frontend never selects a final appointment number or receives a model API key.

Existing `/api/patient-care/*`, `/api/triage-workflows/*` and
`/api/appointment-proposals/{id}/confirm` endpoints remain for compatibility.
Clinical review remains in the staff web app with its existing authorization.
