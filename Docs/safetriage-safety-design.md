# SafeTriage safety design

SafeTriage is a prototype clinical decision-support workflow. It is not clinically validated, does not diagnose disease, prescribe medication, or replace a qualified clinician.

## Workflow

1. Authenticate the patient and obtain their profile only from the API.
2. `TriageWorkflowCoordinator` creates a persisted, bounded seven-step plan.
3. `IntakeValidationAgent` validates symptoms and optional patient-reported vitals through `ValidateVitalsTool`.
4. `SafetyRedFlagAgent` applies versioned deterministic emergency, urgent, and serious/high-risk-context rules through `EvaluateRedFlagsTool` before any generative component.
5. Only when no configured escalation or mandatory-review rule matches, `ClinicalInformationExtractionAgent` uses the read-only `OllamaStructuredExtractionTool` to produce schema-validated, non-diagnostic facts. Every fact used for policy must include an exact evidence span from the patient text.
6. `StructuredSafetyAssessmentAgent` applies deterministic policy to the grounded facts, including normalized concepts, duration, activity, warning-sign negation, and risk context.
7. `AdaptiveQuestionPlanningAgent` ranks missing decision-relevant information and permits at most one clarification round containing three questions.
8. `CareRoutingAgent` proposes an allow-listed route and `SafetyValidationAgent` applies final deterministic validation.
9. Persist the objective, structured facts, evidence, decision basis, plan, agent/tool trace, validation state, timing, retry count, safety outcome, and audit events.
10. Pause a proposed emergency, urgent, or serious-context route for authorised Doctor or Admin review; the patient is still instructed not to delay urgent or emergency care.
11. Return a safe fallback when information is insufficient, conflicting, invalid, or outside scope.
12. For an initially unknown complaint, adaptively select at most three relevant questions from the controlled symptom-specific pool. Safety and risk questions are prioritized, while details already present in the patient's report are deprioritized. The patient answers in natural language; the server validates required answers, IDs, duplicates, and length bounds before the responses re-enter the safety workflow as explicitly untrusted patient data.

## Safety controls

- The backend, not an LLM, enforces authorization, data validation, triage enums, emergency escalation, approval, and audit logging.
- The coordinator has a hard seven-step cap and only one clarification cycle. The Ollama extraction tool has a timeout and at most one bounded retry; a failed tool call becomes a recorded fallback rather than an unhandled exception.
- Every agent has a typed input/output contract and one declared tool permission. The local model has no database, routing, booking, prescription, or approval capability.
- Rules are identified by `safetriage-rules-v3` and orchestration by `safetriage-workflow-v2`. A valid LLM response never establishes clinical safety. Unknown complaints remain explicitly limited-information and receive controlled clarification; serious conditions and high-risk contexts require clinical review.
- Current serious-context rules include cancer/cancer treatment, pregnancy/postpartum, immunosuppression/transplant, and recent surgery. Cancer-treatment warning symptoms receive urgent routing, while independently matched emergency signs always take precedence.
- Audit APIs expose stage/event/timestamp only, avoiding symptom text in the clinical-review event summary.
- JWTs are stored in Flutter secure storage. Non-sensitive display preferences remain in shared preferences.
- Patient endpoints scope workflow reads to the signed-in patient. Clinical queue and audit access require Doctor or Admin roles.
- Follow-up answers keep stable question IDs but allow conversational free text. The server rejects unknown question IDs, duplicates, missing required responses, and overlong values; it never treats patient wording as workflow instructions.
- Ollama maps varied patient wording to structured facts and normalized symptom concepts. Facts used by deterministic policy must be grounded in exact patient-text evidence. The model cannot create a clinical protocol or assign the final urgency level.
- The validated nosebleed pathway asks about duration, amount, ongoing bleeding, injury, recurrence, warning signs, and risk context in conversational text. Safety parsers retain deterministic escalation for prolonged/major or warning-sign bleeding; recurrent or higher-risk cases require clinical review, and only a completed low-risk answer set can receive routine guidance.

## Limitations and evaluation

No confidence percentage is shown because no clinical calibration or validation study has been performed. Invalid inputs produce a safe failure response. A configured emergency phrase produces emergency escalation and clinical review; the patient UI directs users to seek emergency care immediately.

Automated tests cover emergency escalation, cancer requiring clinical review, cancer treatment with fever requiring urgent assessment, emergency signs with cancer, unknown-symptom fail-closed behaviour, invalid vital values, cross-patient isolation, invalid approval decisions, prompt-injection text, audit persistence, and review-queue selection. Future changes to rules, prompts, tools, or models must retain and extend these regression tests.
