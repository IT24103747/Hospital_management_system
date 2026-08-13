# SafeTriage safety design

SafeTriage is a prototype clinical decision-support workflow. It is not clinically validated, does not diagnose disease, prescribe medication, or replace a qualified clinician.

## Workflow

1. Authenticate the patient and obtain their profile only from the API.
2. Validate symptoms and optional patient-reported vitals.
3. Apply versioned, deterministic red-flag rules before any generative component.
4. Persist the structured workflow plan, safety outcome, and audit events.
5. Require an authorised Doctor or Admin review for emergency escalation.
6. Return a safe fallback when information is insufficient, conflicting, invalid, or outside scope.

## Safety controls

- The backend, not an LLM, enforces authorization, data validation, triage enums, emergency escalation, approval, and audit logging.
- Red-flag rules are identified by `safetriage-rules-v1`; clinical content must be reviewed and versioned before any production use.
- Audit APIs expose stage/event/timestamp only, avoiding symptom text in the clinical-review event summary.
- JWTs are stored in Flutter secure storage. Non-sensitive display preferences remain in shared preferences.
- Patient endpoints scope workflow reads to the signed-in patient. Clinical queue and audit access require Doctor or Admin roles.

## Limitations and evaluation

No confidence percentage is shown because no clinical calibration or validation study has been performed. Invalid inputs produce a safe failure response. A configured emergency phrase produces emergency escalation and clinical review; the patient UI directs users to seek emergency care immediately.

Automated tests cover emergency escalation, invalid vital values, conservative non-diagnostic fallback, cross-patient isolation, invalid approval decisions, prompt-injection text, audit persistence, and review-queue selection. Future changes to rules, prompts, tools, or models must retain and extend these regression tests.
