# SmartCare — Assignment Gap and Improvement Plan

**Course:** SE3090 — Software Engineering Frameworks, Assignment 1  
**Prepared:** 8 September 2026  
**Updated after pull:** 8 September 2026  
**Purpose:** evidence-based implementation and submission checklist, assessed against the supplied Assignment 1 specification and the repository's current code.

## Executive assessment

SmartCare already has a credible healthcare domain, a shared ASP.NET Core API, PostgreSQL/EF Core migrations, React staff/administrator views, a Flutter patient app, JWT roles, appointment/room/medical-record workflows, and a safety-focused SafeTriage feature. This is a solid foundation.

The latest pull also adds an **Appointment Scheduling Agent**. This is a meaningful improvement: it implements a bounded Ollama `qwen2.5:3b` decision loop, uses a strict JSON decision schema, executes only allow-listed doctor/slot/booking tools, validates model references against request-local tool results, rechecks availability before booking, and applies model-turn/overall time limits. It is substantially closer to the Lab 05 tool-loop concept than the original SafeTriage implementation.

The project is **not submission-ready yet**. The biggest mark risks are not ordinary CRUD features. They are:

1. SafeTriage must become an explicitly evidenced multi-agent workflow, rather than a single service with agent-labelled stages.
2. Secrets and deployment configuration must be secured.
3. GitHub Actions CI, deployment, ADRs, diagrams, testing evidence, performance evidence, and the consolidated report must be completed.
4. Every group member must be able to prove an owned component and a distinct Agentic AI contribution.

Do not replace the existing ASP.NET Core/Ollama design with the Lab 05 Python service unless the lecturer requires it. A custom C# orchestration is permitted by the specification and is a better fit for the existing system, provided it meets every required agentic behaviour below.

## Current strengths to retain

- **Full-stack integration:** React and Flutter both use the ASP.NET Core API; the backend uses EF Core and PostgreSQL.
- **Roles and security foundation:** Patient, Doctor, and Admin routes use JWT authentication and role-based authorization.
- **Business components:** patient management, doctor management/scheduling, appointments, rooms, and medical records are implemented with business operations beyond CRUD.
- **Mobile device feature:** medical-record image capture/gallery upload is a meaningful device feature.
- **SafeTriage safety design:** deterministic vital-sign/red-flag validation happens before the model; the local model is constrained to non-diagnostic structured extraction; clinician review is available; safe fallback exists.
- **Appointment Scheduling Agent:** bounded ReAct-style tool loop for doctor discovery, slot search, and guarded appointment booking, with structured Ollama output and no direct repository/DbContext access from the agent.
- **Operational support:** an in-app appointment-notification record/workflow and a `/health` endpoint are now present.
- **Existing tests:** backend service/API tests include appointments, patients, rooms, error handling, and useful SafeTriage safety cases; React and Flutter have a small starting test suite.

## Pull update — what changed in this plan

| Plan item | Updated status | What remains |
|---|---|---|
| Bounded agent loop and allow-listed tools | **Partly implemented** by `AppointmentSchedulingAgent` | Persist an execution trace/state, expose it in a client workflow, and add approval before any defined high-impact action. |
| Distinct AI contribution for appointment component | **Strong starting evidence** | Add frontend integration, tests/evaluation evidence, and ensure its owner can explain/modify it in the viva. |
| Health endpoint | **Implemented** (`/health`) | Verify it after deployment and document the live URL. |
| In-app appointment notifications | **Implemented as application data** | This is useful functionality, but it is not an external third-party integration such as email/SMS/calendar/cloud storage. |
| SafeTriage multi-agent workflow | **Unchanged** | Still requires explicit agent classes/contracts, tool registry, accurate trace/state, and complete assessed-workflow evidence. |

The Appointment Scheduling Agent should be retained. It is a good reusable reference implementation for the tool registry, model validation, iteration cap, request-local state, and fail-safe behaviour required elsewhere in the system.

## Priority 0 — fix before any deployment or public repository push

### P0.1 Remove committed credentials and secure configuration

**Observed:** `Backend/HospitalManagementSystem.Api/appsettings.json` contains a PostgreSQL connection string with a password. The development CORS policy also allows any origin, method, and header.

**Required action:**

1. Rotate the exposed database password immediately.
2. Move connection strings, JWT signing keys, Ollama URL/model settings, and future third-party credentials into environment variables, User Secrets for local development, or the deployment platform's secret store.
3. Commit only an `appsettings.example.json` with placeholder values.
4. Add local secret/config files to `.gitignore` and check Git history before publishing.
5. Replace unrestricted CORS with an allow-list of deployed React origins; use a separate development policy only locally.

**Why it matters:** the specification requires secret protection and secure configuration. A database password in Git is also a serious real-world security issue.

### P0.2 Authorisation audit for every ownership-sensitive endpoint

**Required action:** verify every patient, medical-record attachment, appointment, and workflow endpoint checks that the signed-in user owns the resource where the role is `Patient`. Add integration tests for cross-patient access denial.

**Acceptance evidence:** a patient cannot read, upload to, change, or infer another patient's appointments, records, attachments, or triage workflows.

## Priority 1 — complete the assessed Agentic AI workflow

### Required target workflow

The following should be the project’s assessed cross-platform scenario:

```text
Flutter patient submits symptoms and optional vitals
        ↓
ASP.NET Core creates workflow state and a structured plan in PostgreSQL
        ↓
Coordinator delegates bounded steps to specialist agents
        ↓
Deterministic safety validation + allow-listed tool calls
        ↓
High-impact escalation proposal pauses for Doctor/Admin approval in React
        ↓
Decision, trace, and final outcome are persisted
        ↓
Flutter patient refreshes and sees the approved/rejected/revision status
```

**Important:** keep SafeTriage as the main assessed workflow unless the group intentionally decides to upgrade the Appointment Scheduling Agent to have durable state, Doctor/Admin approval, React review, and Flutter status updates. The appointment agent is technically strong, but as currently implemented it is request-local and does not yet persist its agent trajectory or pause a booking for human approval. It cannot by itself prove every item in the minimum assessed workflow.

### P1.1 Make at least four agents real and distinct

The assignment counts an agent as distinct only when it has a unique responsibility, input/output contract, controlled tool permissions, and visible workflow participation. Current SafeTriage stage labels alone are risky because most logic is inside `TriageWorkflowService`.

Implement these C# classes/interfaces:

| Agent | Input | Controlled responsibility / allowed tool | Structured output |
|---|---|---|---|
| `TriageWorkflowCoordinator` | workflow objective and patient submission | Creates plan; invokes only registered agents in bounded order | plan, current state, stop/approval decision |
| `IntakeValidationAgent` | symptoms and vitals | Deterministic validation only | validated data or validation errors |
| `SafetyRedFlagAgent` | validated symptoms/vitals | Versioned deterministic red-flag rules only | emergency/urgent/non-red-flag result and rule version |
| `ClinicalInformationExtractionAgent` | non-red-flag symptom text | Read-only `OllamaStructuredExtractionTool` only | schema-validated extracted facts, missing information, safe general guidance |
| `CareRoutingAgent` | validated safety/extraction results | Controlled routing policy only; cannot book, prescribe, or diagnose | proposed route and whether approval is required |
| `SafetyValidationAgent` | complete proposed outcome | Deterministic schema/business-rule validation | accepted, rejected, or revision-required result |

The existing Ollama model (`qwen2.5:3b`) should remain limited to extraction/general non-diagnostic wording. It must never make the emergency decision, access PostgreSQL, prescribe, diagnose, or perform approvals.

### P1.2 Add explicit controlled tools and a bounded executor

The lab’s central idea is useful even without copying its Python stack: model/action capability must be bounded by application code.

The newly added `AppointmentSchedulingAgent` already demonstrates this pattern for `find_doctors`, `find_slots`, and `book_appointment`. Reuse its design principles; do not duplicate its code or give the triage model permission to call booking tools.

Create a tool registry with clear names, input validation, output schemas, permissions, timeout, and error handling. Suitable SafeTriage tools are:

- `ValidateVitalsTool` — deterministic C# validation; no external access.
- `EvaluateRedFlagsTool` — versioned rule set; no LLM access.
- `OllamaStructuredExtractionTool` — read-only local model call, strict JSON schema, 75-second maximum timeout.
- `CreateEscalationProposalTool` — creates a *proposed* escalation only; no direct notification/action until approval.
- `RecordWorkflowEventTool` — writes a minimal structured audit event; never stores hidden reasoning or unnecessary sensitive text.

The coordinator must use a hard maximum number of workflow steps/retries. A prompt must not be the only control. Tool failure should become a structured safe-failure result, not an unhandled exception.

### P1.3 Persist a complete execution summary

Extend the workflow/event schema or introduce a `TriageWorkflowStep` table. For each step, persist:

- workflow ID, objective, plan-step ID and agent name
- permitted tool name and safe input/output summaries
- start/end timestamps and duration
- status: planned, running, completed, failed-safely, skipped, awaiting-approval
- schema/business-rule validation result
- error code, bounded retry count, and safe-failure reason
- approval decision, reviewer, timestamp, and resulting action

Never persist chain-of-thought, raw hidden reasoning, access tokens, passwords, or unneeded clinical text.

### P1.4 Define the high-impact action precisely

The proposal to escalate must remain `PendingApproval` until an authorised Doctor/Admin approves, rejects, or requests revision. On approval, perform and record one clear controlled action—for example create a clinician escalation/priority-review record or send a configured notification through the approved third-party provider. The patient-facing emergency instruction can still tell the patient to seek immediate help; it must not wait on staff approval.

### P1.5 Upgrade the React and Flutter evidence

- **React:** display plan, agent steps, tool/result summaries, validation, timing, errors, and Approve/Reject/Request revision controls. Do not display raw hidden reasoning or unnecessary symptom data in broad queues.
- **Flutter:** submit triage workflow, show submission status and safe guidance, display “awaiting clinical review” where relevant, and refresh/poll the final clinician decision.
- Use the real persisted trace; do not animate hard-coded stages that are not tied to server state.

### P1.6 Add Agentic AI evaluation evidence

Create a golden-case test/evaluation matrix. At minimum include:

| Scenario | Required assertion |
|---|---|
| Emergency phrase | red-flag agent routes to approval queue; extraction is skipped |
| Urgent phrase | urgent route and clinician review are required |
| Valid low-risk input | plan/delegation occurs; output meets schema and safety rules |
| Invalid/impossible vitals | safe failure, no model/tool call |
| Ollama timeout/invalid JSON | safe fallback, logged structured failure |
| Prompt injection | cannot change tool permissions, routing, or safety rules |
| Invalid tool/agent output | validator rejects or requests revision |
| Unauthorised review | action is denied |
| Approval/rejection/revision | correct state transition and audit record |

Record results, expected versus actual outputs, latency, and screenshots in the Agentic AI evaluation report.

## Priority 2 — close mandatory system gaps

### P2.1 Meaningful third-party integration

Local Ollama may be presented as the AI runtime, but it is not a strong substitute for the required third-party business-service integration. `image_picker` is a device feature, not a third-party service.

The new `AppointmentNotifications` database feature is an in-app notification mechanism, not a third-party API/service. Recommended option: add an email/SMS/notification integration that is triggered only after authorised workflow events, appointment changes, or doctor-registration decisions. Route it through ASP.NET Core, keep credentials server-side, minimise personal data, use timeouts, and record safe delivery failure.

Other viable options: calendar integration for appointment reminders, cloud file storage for medical attachments, or a map service for hospital/clinic routing. Choose **one** with a clear user benefit and complete it well.

### P2.2 Reporting and analytics

The specification requires reporting or analytics. Add an Admin dashboard with server-side aggregates and date filtering, for example:

- appointments by status, doctor, speciality, and date
- room utilisation / availability
- pending doctor registrations and triage reviews
- SafeTriage workflow totals by status, safe failures, approval turnaround time, and model latency
- medical-record count/upload activity by period

Do not expose patient-identifying information in aggregate charts. Add a backend reporting endpoint rather than calculating everything only in React.

### P2.3 Pagination, filters, sorting, and error states

Verify each major component has server-backed search, filtering, sorting, and pagination where datasets can grow: patients, doctors, appointments, rooms/schedules, medical records, triage review queue. Ensure React and Flutter show loading, empty, validation, success, and failure states.

### P2.4 Consistent API quality

- `/health` is now implemented; verify it in the deployed environment and document the public URL.
- Confirm Swagger is available outside development only where safely protected/appropriate.
- Standardise validation error responses and pagination response models.
- Add structured logging with workflow/correlation IDs; never log secrets or sensitive symptom content unnecessarily.
- Apply database transactions to multi-write business operations such as approval/action/notification workflows.

## Priority 3 — testing, CI, deployment, and documentation

### P3.1 Testing plan

Current automated coverage is a useful start but insufficient for the specification.

| Layer | Current evidence | Required upgrade |
|---|---|---|
| Backend | service and some API tests | add authorisation, validation, controller/API, PostgreSQL integration, transaction, and agent trace tests |
| PostgreSQL | migrations exist | run integration tests against PostgreSQL, not only EF InMemory; test constraints/indexes/migration behaviour |
| React | one appointments test found | add form validation, protected route, API success/failure, triage review and trace tests |
| Flutter | small widget/API test base | add auth, forms, triage workflow submission/status, navigation, API failure, and attachment tests |
| End-to-end | no evidence found | script and record the Flutter → API → PostgreSQL → agent → React approval → Flutter update scenario |
| Performance | no evidence found | measure concurrent API requests, database response, agent latency, success/failure rate, and document results |

### P3.2 GitHub Actions CI

No GitHub Actions workflow was found. Add `.github/workflows/ci.yml` that runs on pushes and pull requests to `main`:

1. restore/build/test the .NET solution
2. run `npm ci`, lint, test, and build for React
3. run `flutter pub get`, `flutter analyze`, and Flutter tests

Make the GitHub Actions badge and successful runs part of the report/demo evidence. Use feature branches, pull requests, code review, and meaningful commits from now onward.

### P3.3 Deployment readiness

Required deliverables are a working ASP.NET Core health URL and Swagger URL, deployed PostgreSQL evidence, a React live URL using the deployed API, and a runnable Android APK. The local `/health` endpoint is already implemented.

Complete:

- deployment platform decision and ADR
- environment-specific backend configuration and CORS allow-list
- PostgreSQL managed instance, migrations, restricted credentials, backup/recovery note
- health/readiness endpoint
- React production environment variable for API base URL
- Android release APK build and installation test on a physical device/emulator
- Ollama install/model/startup order and fallback behaviour in evaluator instructions

If Ollama will run locally in the viva, prepare a repeatable pre-demo check: `ollama serve`, model availability, API health, and a safe fallback demonstration.

### P3.4 Documentation deliverables

The current root and component READMEs are too short, and `Backend/README.md` describes project names/structure that do not match the repository. Replace them with accurate documents.

Create or complete:

- root README: business problem, roles, features, architecture, setup, environment variables, test accounts, test commands, deployed URLs, startup order
- ER diagram and relational schema
- system architecture and sequence diagram for the assessed cross-platform workflow
- Agentic AI architecture/tool-permission diagram
- API contract summary and Swagger URL
- testing report, Agentic AI evaluation report, performance report, deployment report
- security/privacy section, limitations/disclaimer, backup/error-handling approach
- consolidated group report and individual report sections

Create at least these ADRs:

1. React state-management choice
2. Flutter state-management choice
3. Custom ASP.NET Core + Ollama agent orchestration choice
4. Agent workflow-state database schema
5. Cloud/deployment platform

### P3.5 Individual contribution and AI-use evidence

For each member, maintain a one-page contribution section containing owned component, relevant issues/PRs/commits, tests, challenges, and a distinct Agentic AI contribution. Maintain individual AI usage logs: date, tool/model, task, output used/rejected, and verification. Each student must write their own reflection and be able to explain, change, test, and debug their implementation without external AI during the viva.

## Recommended feature improvements

These are optional only after all Priority 0–3 requirements are complete.

1. **Notification centre:** in-app and email/SMS notifications for appointment changes, doctor approval, and approved triage escalation.
2. **Appointment reminders:** schedule reminders and show delivery status; this can also satisfy the third-party integration requirement.
3. **Accessible UX pass:** semantic labels, keyboard navigation for React, sufficient colour contrast, readable error text, and responsive layout tests.
4. **Audit dashboard:** role-restricted admin view for workflow success/failure/approval turnaround metrics without exposing private clinical content.
5. **Attachment hardening:** file type/size checks, malware-scanning strategy if cloud-hosted, authorisation tests, secure file storage, and signed download links.
6. **Availability intelligence:** deterministic appointment slot recommendations based on doctor schedule, room capacity, and patient constraints. Keep any high-impact booking change approval-controlled.

## Recommended implementation order

### Week 1 — protect and make the agent workflow assessable

1. Rotate/remove secrets; create environment templates.
2. Implement the coordinator, distinct agents, tool registry, bounded execution, and detailed persisted trace.
3. Wire real trace data into React review and Flutter status screens.
4. Write golden-case Agentic AI tests.

### Week 2 — complete required quality evidence

1. Add third-party notification/calendar/cloud-storage integration.
2. Add analytics endpoints/dashboard.
3. Expand backend, React, Flutter, PostgreSQL, and end-to-end tests.
4. Add GitHub Actions CI and fix all errors/warnings that affect quality.

### Week 3 — deploy, document, rehearse

1. Deploy backend, database, and React; produce Flutter APK.
2. Write accurate README, diagrams, ADRs, reports, AI usage logs, and contribution statements.
3. Capture passing CI, test, performance, and Agentic AI evaluation evidence.
4. Rehearse the 10-minute demo and each person’s viva questions, including a safe Ollama failure scenario.

## Definition of submission-ready

The project is ready only when the group can demonstrate, without external coding assistants:

- a Flutter patient starts a persisted SafeTriage workflow;
- the backend displays an actual structured plan, four-plus distinct agent executions, allow-listed tools, validation, safe trace, and bounded failure handling;
- React Doctor/Admin reviews and approves/rejects/revises the paused high-impact action;
- Flutter shows the final updated state;
- tests, CI, deployed links, Swagger/health, APK, documentation, ADRs, diagrams, AI declarations, and individual Git evidence are all available;
- every member can modify and explain their own backend, database, React, Flutter, tests, and distinct agent contribution.
