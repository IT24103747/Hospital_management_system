# SafeTriage safety design

SafeTriage is a prototype clinical decision-support workflow. It is not clinically validated, does not diagnose disease, prescribe medication, or replace a qualified clinician.

## Workflow

1. Authenticate the patient and obtain their profile only from the API.
2. `TriageWorkflowCoordinator` creates a persisted, bounded seven-step plan.
3. `IntakeValidationAgent` validates symptoms and optional patient-reported vitals through `ValidateVitalsTool`.
4. `SafetyRedFlagAgent` applies versioned deterministic emergency, urgent, and serious/high-risk-context rules through `EvaluateRedFlagsTool` before any generative component.
5. Only when no configured escalation or mandatory-review rule matches, `ClinicalInformationExtractionAgent` uses the read-only `GeminiStructuredExtractionTool` to produce schema-validated, non-diagnostic facts. Every fact used for policy must include an exact evidence span from the patient text.
6. `StructuredSafetyAssessmentAgent` applies deterministic policy to the grounded facts, including normalized concepts, duration, activity, warning-sign negation, and risk context.
7. `AdaptiveQuestionPlanningAgent` selects one question by its stable requirement identifier. Only Missing requirements are eligible; the backend filters all planner output to one eligible question.
8. `CareRoutingAgent` proposes an allow-listed route and `SafetyValidationAgent` applies final deterministic validation.
9. Persist the objective, structured facts, evidence, decision basis, plan, agent/tool trace, validation state, timing, retry count, safety outcome, and audit events.
10. Pause a proposed emergency, urgent, or serious-context route for authorised Doctor review (Admin access is read-only); the patient is still instructed not to delay urgent or emergency care.
11. Return a safe fallback when information is insufficient, conflicting, invalid, or outside scope.
12. Merge extracted requirement updates into the existing assessment in `ResultJson`. Preserve each identifier, state, value and update timestamp, as well as the original complaint and previously grounded facts. Missing updates never erase collected information. Explicit later answers can replace Declined, Unknown or NotApplicable states.
13. Count questions only when the backend issues a new active question. `SafeTriage:MaxFollowUpQuestions` defaults to 10 and is clamped to the hard limit of 10; it is a ceiling, not a target. Reads and conversation retries do not increment it. Result JSON is an optimistic concurrency token to prevent concurrent answers/reviews from overwriting each other.
14. Stop earlier when the existing routine-scope and grounded-concept checks pass and requirements are Answered or NotApplicable. Extraction/planning/response failures, unavailable required answers without useful Missing fields, and the ceiling with insufficient information require clinical review. Existing emergency/urgent rules always take priority.
15. The patient can send a structured Declined action with no clinical value. Natural-language refusal, unknown and inapplicable answers are also recognized. Doctors receive the original complaint, collected/unavailable fields, facts, safety findings and suggestion. They approve the saved suggestion or save their own non-empty final response; the patient receives one reviewed response. Technical audit details remain available, collapsed by default.

## Safety controls

- The backend, not an LLM, enforces authorization, data validation, triage enums, emergency escalation, approval, and audit logging.
- The coordinator has a hard seven-step cap per turn and a persisted follow-up question ceiling. The Gemini extraction tool has a timeout and at most one bounded retry; a failed tool call becomes a recorded fallback rather than an unhandled exception.
- Every agent has a typed input/output contract and one declared tool permission. The local model has no database, routing, booking, prescription, or approval capability.
- Rules are identified by `safetriage-rules-v3` and orchestration by `safetriage-workflow-v2`. A valid LLM response never establishes clinical safety. Unknown complaints remain explicitly limited-information and receive controlled clarification; serious conditions and high-risk contexts require clinical review.
- Current serious-context rules include cancer/cancer treatment, pregnancy/postpartum, immunosuppression/transplant, and recent surgery. Cancer-treatment warning symptoms receive urgent routing, while independently matched emergency signs always take precedence.
- Audit APIs expose stage/event/timestamp only, avoiding symptom text in the clinical-review event summary.
- JWTs are stored in Flutter secure storage. Non-sensitive display preferences remain in shared preferences.
- Patient endpoints scope workflow reads to the signed-in patient. Clinical queue and audit access require Doctor or Admin roles.
- Follow-up answers keep stable question IDs but allow conversational free text. The server rejects unknown question IDs, duplicates, missing required responses, and overlong values; it never treats patient wording as workflow instructions.
- Gemini maps varied patient wording to structured facts and normalized symptom concepts. Facts used by deterministic policy must be grounded in exact patient-text evidence. The model cannot create a clinical protocol or assign the final urgency level.
- Gemini plans from persisted requirement IDs rather than a hardcoded symptom question pool. Existing deterministic safety rules, including grounded active nosebleed duration and warning-sign escalation, remain in force.

## Limitations and evaluation

No confidence percentage is shown because no clinical calibration or validation study has been performed. Invalid inputs produce a safe failure response. A configured emergency phrase produces emergency escalation and clinical review; the patient UI directs users to seek emergency care immediately.

Automated tests cover emergency escalation, cancer requiring clinical review, cancer treatment with fever requiring urgent assessment, emergency signs with cancer, unknown-symptom fail-closed behaviour, invalid vital values, cross-patient isolation, invalid approval decisions, prompt-injection text, audit persistence, and review-queue selection. Future changes to rules, prompts, tools, or models must retain and extend these regression tests.
