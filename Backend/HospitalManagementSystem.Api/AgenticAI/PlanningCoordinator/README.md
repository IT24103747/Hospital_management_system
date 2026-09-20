> Updated architecture: see [the unified implementation report](../../../../Docs/unified-agentic-ai.md).
> PlanningCoordinatorAgent now owns the active assistant execution and uses a scoped PostgreSQL-backed store.
> The original planning-only design notes below are historical; they do not describe the current runtime architecture.

# Planning / Coordinator Agent (Member 1)

> **Subsystem**: Planning & Coordinator Agent for SmartCare Hospital Management System  
> **Course**: SE3090 — Software Engineering Frameworks  
> **Component Ownership**: Member 1 — Patient Management & Planning / Coordinator Agent  
> **Backend Platform**: ASP.NET Core 8 Web API & Gemini AI (Server-Side Only)

---

## 1. Overview & Architecture

The **Planning/Coordinator Agent** serves as the front-line orchestrator for all patient healthcare intents within the SmartCare backend. When a patient submits an objective (via web or mobile), the Planning/Coordinator Agent analyzes the intent using a strict Gemini structured-output JSON protocol and constructs an allow-listed execution plan.

```
                  ┌─────────────────────────────────┐
                  │ Patient Objective Submission    │
                  │ (via ASP.NET Core Web API)      │
                  └────────────────┬────────────────┘
                                   │
                                   ▼
                  ┌─────────────────────────────────┐
                  │ Heuristic & Anti-Injection      │
                  │ Pre-Screening Filter            │
                  └────────────────┬────────────────┘
                                   │
                                   ▼
                  ┌─────────────────────────────────┐
                  │ Gemini Structured Model Client  │
                  │ (Strict JSON Decision Schema)   │
                  └────────────────┬────────────────┘
                                   │
                                   ▼
                  ┌─────────────────────────────────┐
                  │ Post-Processing & Guardrails:   │
                  │ 1. Never Infer Booking Consent  │
                  │ 2. Allow-Listed Workflows/Steps │
                  │ 3. Bound Follow-Up Qs (<= 3)    │
                  │ 4. Require Patient Confirmation │
                  └────────────────┬────────────────┘
                                   │
                                   ▼
                  ┌─────────────────────────────────┐
                  │ Durable Workflow Persistence &  │
                  │ Audit Trail Logging             │
                  └─────────────────────────────────┘
```

---

## 2. Supported Workflows

The Planning/Coordinator Agent recognizes exactly **four predefined, allow-listed workflows**:

| Workflow Type | Criteria & Intent | Allow-Listed Steps |
|---|---|---|
| **`TriageThenAppointmentProposal`** | Patient reports clinical symptoms, pain, injuries, discomfort, or general health concerns. | `SafetyCheck` → `SymptomExtraction` → `TriageAssessment` → `AppointmentProposal` → `PatientConfirmation` |
| **`AppointmentProposal`** | Patient explicitly requests to find, consult, or book a doctor/specialist without describing acute illness. | `DoctorLookup` → `SlotSearch` → `AppointmentProposal` → `PatientConfirmation` |
| **`AppointmentStatus`** | Patient inquires about existing appointment status, scheduled date/time, or queue. | `AppointmentLookup` → `StatusNotification` |
| **`Unsupported`** | Off-topic requests (e.g. recipes, programming), diagnostic inquiries, or adversarial prompts. | `SafeControlledResponse` |

---

## 3. Strict Safety & Ethical Guardrails

1. **Server-Side Gemini Access Only**:
   - The Gemini API key is stored securely in backend configuration/secrets.
   - The frontend (React / Flutter) never receives the API key or talks to Gemini directly.

2. **No Direct Database or Action Authority**:
   - Gemini cannot execute SQL/PostgreSQL queries, modify database records, or book appointments directly.
   - Gemini cannot invent tools or execute unverified actions.

3. **Explicit Consent Rule (Zero-Assumption Booking)**:
   - **Rule**: *Never infer consent to book an appointment from symptom descriptions alone.*
   - If a patient describes symptoms (e.g., *"I have a sore throat and fever"*), `appointmentRequested` is set to `false`.
   - `appointmentRequested` is only set to `true` when the patient explicitly requests booking (e.g., *"I want to book an appointment for my back pain"*).

4. **Mandatory Patient Confirmation**:
   - `patientConfirmationRequired` is strictly enforced as `true` for all proposal and scheduling workflows.

5. **Bounded Follow-Up Questions (<= 3)**:
   - To prevent patient overwhelm and cognitive fatigue, follow-up clarifying questions are deduplicated and strictly capped at **maximum three (3) questions**.

6. **Adversarial & Prompt Injection Defense**:
   - Heuristic pre-screening catches attempts to override system instructions, bypass safety checks, or escalate privileges.
   - Adversarial inputs are immediately routed to `Unsupported` with safety audit events recorded.

7. **Graceful Fallback on Malformed JSON / Model Outage**:
   - If Gemini is unreachable or returns malformed JSON, a deterministic rule-based safety planner engages seamlessly without crashing or revealing internal stack traces.

---

## 4. Strict Gemini JSON Schema

```json
{
  "type": "object",
  "additionalProperties": false,
  "required": [
    "workflowType",
    "appointmentRequested",
    "patientConfirmationRequired",
    "requiredSteps",
    "followUpQuestions",
    "rationale",
    "safeResponse"
  ],
  "properties": {
    "workflowType": {
      "type": "string",
      "enum": [
        "TriageThenAppointmentProposal",
        "AppointmentProposal",
        "AppointmentStatus",
        "Unsupported"
      ]
    },
    "appointmentRequested": { "type": "boolean" },
    "patientConfirmationRequired": { "type": "boolean" },
    "requiredSteps": {
      "type": "array",
      "items": { "type": "string" },
      "maxItems": 6
    },
    "preferredDate": { "type": "string" },
    "preferredTime": { "type": "string" },
    "followUpQuestions": {
      "type": "array",
      "items": { "type": "string" },
      "maxItems": 3
    },
    "rationale": { "type": "string", "maxLength": 500 },
    "safeResponse": { "type": "string", "maxLength": 1000 }
  }
}
```

---

## 5. API Endpoints

### `POST /api/planning-coordinator/plan`
Analyzes a patient objective and creates an allow-listed execution plan.

**Request Payload:**
```json
{
  "objective": "I have had a throbbing headache and light sensitivity for two days.",
  "patientId": 12,
  "preferredSpecialty": "Neurology",
  "preferredDate": "2026-09-15"
}
```

**Response Payload:**
```json
{
  "workflowId": "4a73b889e49c4f0f8a846c4302685713",
  "status": "Planned",
  "objective": "I have had a throbbing headache and light sensitivity for two days.",
  "plan": {
    "workflowType": "TriageThenAppointmentProposal",
    "appointmentRequested": false,
    "patientConfirmationRequired": true,
    "requiredSteps": [
      "SafetyCheck",
      "SymptomExtraction",
      "TriageAssessment",
      "AppointmentProposal",
      "PatientConfirmation"
    ],
    "preferredDate": "2026-09-15",
    "preferredTime": null,
    "followUpQuestions": [
      "How severe is your headache on a scale from 1 to 10?",
      "Are you experiencing any numbness or weakness?"
    ],
    "rationale": "Patient reported headache with neurological symptoms requiring triage evaluation.",
    "safeResponse": "Your symptoms will be evaluated through our clinical safety triage protocol."
  },
  "completedStages": ["PlanningStage"],
  "errors": [],
  "auditEvents": [
    {
      "timestamp": "2026-09-12T14:20:00Z",
      "eventType": "WorkflowInitialized",
      "description": "Planning workflow record created for patient objective.",
      "metadata": "PatientId: 12"
    },
    {
      "timestamp": "2026-09-12T14:20:01Z",
      "eventType": "PlanFinalized",
      "description": "Allow-listed plan finalized with workflow type: TriageThenAppointmentProposal.",
      "metadata": "AppointmentRequested: False, PatientConfirmationRequired: True"
    }
  ],
  "createdAt": "2026-09-12T14:20:00Z"
}
```

---

## 6. Verification & Test Suite

The Planning/Coordinator Agent is thoroughly verified in `HospitalManagementSystem.Api.Tests.AgenticAI.PlanningCoordinatorTests`:

| Test Case | Description | Verified Outcome |
|---|---|---|
| `ValidPlan_TriageThenAppointmentProposal` | Verifies full plan generation for symptomatic inputs. | Plan created with `TriageThenAppointmentProposal`, `appointmentRequested = false`, and audit events persisted. |
| `ValidPlan_ExplicitAppointmentRequest` | Verifies explicit booking requests set consent flag. | Plan created with `AppointmentProposal`, `appointmentRequested = true`. |
| `ConsentRule_NeverInferConsentToBookFromSymptoms` | Verifies guardrail when model hallucinates consent for symptoms. | Deterministic guardrail strictly overrides `appointmentRequested` to `false`. |
| `UnsupportedRequest_ReturnsSafeControlledResponse` | Verifies handling of non-medical queries. | Returns `Unsupported` workflow with predefined safe message. |
| `MalformedGeminiJson_EngagesFallbackSafely` | Verifies system resiliency during model parser failures. | Fallback planner engages seamlessly without 500 error or crash. |
| `PromptInjection_TrappedSafelyWithSecurityAudit` | Verifies defense against adversarial prompt injection attempts. | Trapped before model call, flagged as `Unsupported` with audit log. |
| `MaximumThreeQuestions_TruncatesExcessQuestions` | Verifies question limit bounding. | Output contains at most 3 questions; audit event logged. |
