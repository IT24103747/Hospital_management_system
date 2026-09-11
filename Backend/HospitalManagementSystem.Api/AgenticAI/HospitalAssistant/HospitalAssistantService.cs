using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.ClinicalSafety;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.SafetyApproval;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.DTOs;
using HospitalManagementSystem.Api.Models;
using HospitalManagementSystem.Api.Services;
using Microsoft.EntityFrameworkCore;
using static HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.AssistantPreferences;

namespace HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;

/// Coordinates existing agents and services. Only DecideAsync can execute an approved hospital mutation.
public sealed class HospitalAssistantService(
    ApplicationDbContext db,
    AssistantAgentRegistry registry,
    ITriageWorkflowService workflows,
    IHospitalAppointmentProposalAgent proposals,
    ISafetyValidationApprovalAgent approval,
    IAppointmentAgentTools tools,
    IAppointmentService appointments)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // Fixed stripes avoid a lock dictionary growing with patient data. PostgreSQL locks also
    // serialize requests across API instances; stripes cover the in-memory test provider.
    private static readonly SemaphoreSlim[] Gates = Enumerable.Range(0, 64).Select(_ => new SemaphoreSlim(1)).ToArray();

    public IReadOnlyList<AssistantCapability> Capabilities => registry.Capabilities;

    public async Task<object> HistoryAsync(int patientId, CancellationToken token) =>
        await db.AssistantConversations.AsNoTracking().Where(c => c.PatientId == patientId)
            .OrderByDescending(c => c.UpdatedAt).Take(50)
            .Select(c => new { conversationId = c.AssistantConversationId, c.Title, c.UpdatedAt }).ToListAsync(token);

    public Task<AssistantConversationResponse> GetAsync(int patientId, Guid id, CancellationToken token) =>
        LockedAsync(patientId, async () => {
            var entity = await OwnedAsync(patientId, id, token);
            var state = Read(entity);
            if (state.PendingAction?.ExpiresAt <= DateTime.UtcNow)
            {
                await InvalidateAsync(state, "Expired", token);
                Reply(state, "The pending request expired. Ask me to search again for current options.", "CANCELLED");
            }
            if (state.WorkflowId.HasValue && state.Awaiting == "clinical-review")
            {
                var workflow = await workflows.GetForPatientAsync(state.WorkflowId.Value, patientId);
                if (workflow != null && workflow.ApprovalStatus != TriageApprovalStatuses.Pending)
                {
                    ApplyWorkflow(state, workflow);
                    Reply(state, workflow.PatientMessage, state.SafetyBlocked ? "WAITING_FOR_HUMAN_APPROVAL" : "COMPLETED");
                }
            }
            await SaveAsync(entity, state, token);
            return Response(entity, state);
        }, token);

    public Task<AssistantConversationResponse> MessageAsync(PatientDto patient, AssistantMessageRequest request, CancellationToken token)
    {
        if (request.RequestId == Guid.Empty || string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 4000)
            throw new ArgumentException("Provide a message and a unique request ID.");
        return LockedAsync(patient.PatientId, async () => {
            var entity = request.ConversationId.HasValue
                ? await OwnedAsync(patient.PatientId, request.ConversationId.Value, token)
                : await db.AssistantConversations.SingleOrDefaultAsync(c => c.PatientId == patient.PatientId && c.InitialRequestId == request.RequestId, token);
            if (entity == null)
            {
                entity = new AssistantConversation {
                    PatientId = patient.PatientId, InitialRequestId = request.RequestId,
                    Title = request.Message.Trim()[..Math.Min(100, request.Message.Trim().Length)]
                };
                db.AssistantConversations.Add(entity);
            }
            var state = Read(entity);
            var fingerprint = Fingerprint("message:" + request.Message);
            if (Seen(state, request.RequestId, fingerprint)) return Response(entity, state);
            if (state.Messages.Count >= 200) throw new ArgumentException("This conversation is full. Start a new conversation.");
            state.State = "UNDERSTANDING";
            state.Messages.Add(new(Guid.NewGuid().ToString(), "user", request.Message.Trim(), DateTime.UtcNow, []));
            state.Appointments = []; state.Slots = []; state.Doctors = [];
            await RouteAsync(patient, state, request.Message.Trim(), token);
            state.Requests[request.RequestId] = fingerprint;
            await SaveAsync(entity, state, token);
            return Response(entity, state);
        }, token);
    }

    public Task<AssistantConversationResponse> DecideAsync(PatientDto patient, Guid id, AssistantActionRequest request, CancellationToken token)
    {
        if (request.RequestId == Guid.Empty || request.ActionId == Guid.Empty || request.Decision is not ("confirm" or "cancel" or "chooseAnother"))
            throw new ArgumentException("Use an explicit confirm, cancel, or chooseAnother action.");
        return LockedAsync(patient.PatientId, async () => {
            var entity = await OwnedAsync(patient.PatientId, id, token);
            var state = Read(entity);
            var fingerprint = Fingerprint(JsonSerializer.Serialize(withAction(), Json));
            if (Seen(state, request.RequestId, fingerprint)) return Response(entity, state);
            var action = state.PendingAction;
            if (action == null || action.ActionId != request.ActionId)
                throw new InvalidOperationException("This request is no longer awaiting confirmation. Refresh the conversation.");
            if (request.Decision != "confirm")
            {
                await InvalidateAsync(state, "Cancelled", token);
                state.Awaiting = null;
                Reply(state, request.Decision == "chooseAnother"
                    ? "The previous request was dismissed. What doctor, date, or time would you prefer?"
                    : "Request dismissed. No appointment was changed.", "CANCELLED");
            }
            else if (action.ExpiresAt <= DateTime.UtcNow)
            {
                await InvalidateAsync(state, "Expired", token);
                Reply(state, "This request expired. Search again before confirming.", "CANCELLED");
            }
            else
            {
                // Recheck the persisted clinical workflow on the final action boundary.
                if (state.WorkflowId.HasValue)
                {
                    var workflow = await workflows.GetForPatientAsync(state.WorkflowId.Value, patient.PatientId);
                    if (workflow != null) ApplyWorkflow(state, workflow);
                }
                await CheckPatientSafetyAsync(patient.PatientId, state);
                if (state.SafetyBlocked)
                {
                    await InvalidateAsync(state, "BlockedByClinicalSafety", token);
                    Reply(state, "This action cannot proceed while the safety assessment requires attention. Follow the clinical guidance above.", "WAITING_FOR_HUMAN_APPROVAL");
                }
                else if (action.Type == "book")
                {
                    if (!request.DoctorTimeSlotId.HasValue || !action.Slots.Any(s => s.DoctorTimeSlotId == request.DoctorTimeSlotId) || !action.ProposalId.HasValue)
                        throw new ArgumentException("Select one of the appointment options shown in this request.");
                    state.State = "EXECUTING";
                    var result = await approval.ConfirmAsync(new(action.ProposalId.Value, request.DoctorTimeSlotId.Value), patient, token);
                    state.PendingAction = null;
                    state.WantsAppointment = false;
                    state.Awaiting = null;
                    if (result.AppointmentId.HasValue)
                    {
                        var booked = await appointments.GetAppointmentByIdAsync(result.AppointmentId.Value);
                        if (booked?.PatientId != patient.PatientId) throw new InvalidOperationException("Could not verify the booking result.");
                        state.Appointments = [booked];
                        Reply(state, $"Appointment confirmed with {booked.DoctorName}. Your appointment number is {booked.AppointmentNumber}. " +
                            $"Session: {Local(booked.StartAt):ddd, d MMM yyyy h:mm tt}. The session time is not an individual consultation time.",
                            "COMPLETED", ["Your confirmation was validated.", "Availability was checked again.", "The hospital assigned and saved your appointment number."]);
                    }
                    else Reply(state, result.Message, result.RequiresClinicalApproval ? "WAITING_FOR_HUMAN_APPROVAL" : "FAILED");
                }
                else if (action.Type == "cancel")
                {
                    if (!request.AppointmentId.HasValue || !action.Appointments.Any(a => a.AppointmentId == request.AppointmentId))
                        throw new ArgumentException("Select an appointment from this cancellation request.");
                    var current = await appointments.GetAppointmentByIdAsync(request.AppointmentId.Value);
                    if (current?.PatientId != patient.PatientId || current.Status is "Cancelled" or "Completed" || current.EndAt <= DateTime.UtcNow)
                        throw new InvalidOperationException("That appointment is no longer available to cancel. Refresh your appointments.");
                    state.State = "EXECUTING";
                    var cancelled = await appointments.CancelAppointmentAsync(current.AppointmentId, state.CancellationReason ?? "Patient confirmed cancellation through Hospital AI Assistant.");
                    if (cancelled == null) throw new InvalidOperationException("The appointment could not be found.");
                    state.PendingAction = null;
                    state.WantsAppointment = false;
                    state.Appointments = [cancelled];
                    Reply(state, $"Appointment No. {cancelled.AppointmentNumber} with {cancelled.DoctorName} was cancelled.", "COMPLETED",
                        ["Your cancellation was explicitly confirmed.", "Appointment ownership and status were checked.", "The cancellation was saved."]);
                }
                else throw new ArgumentException("This action is not supported.");
            }
            state.Requests[request.RequestId] = fingerprint;
            await SaveAsync(entity, state, token);
            return Response(entity, state);

            object withAction() => new { request.ActionId, request.Decision, request.DoctorTimeSlotId, request.AppointmentId };
        }, token);
    }

    private async Task RouteAsync(PatientDto patient, AssistantState state, string text, CancellationToken token)
    {
        state.State = "ROUTING";
        var rawSafety = ClinicalSafetyTools.EvaluateRedFlags(text);
        var symptoms = rawSafety.HasEscalation || ClinicalSafetyTools.IsRoutine(text, null) ||
            Has(text, @"\b(symptom|symptoms|pain|bleeding|fever|cough|sick|unwell|dizzy|headache|nausea|vomiting|breathing|rash|swollen|feel ill|hurt|suffering|nosebleed|shortness|feeling)\b");
        var appointmentIntent = Has(text, @"\b(appointment|appointments|book|booking|doctor|specialist|cardiologist|ophthalmologist|dermatologist|neurologist|consultation|cardiology|ophthalmology)\b");
        var askingCancel = Has(text, @"\b(cancel|cancellation)\b") && appointmentIntent;
        var lookingForAlternative = appointmentIntent && Has(text, @"\b(another|alternative|cannot attend|can't attend)\b");
        var askingList = Has(text, @"\b(my|do i have|show|list|check|view)\b") && Has(text, @"\b(appointment|appointments)\b") && !askingCancel &&
            !lookingForAlternative && !Has(text, @"\b(book|find|earliest|need|want)\b");

        // A raw safety flag always takes priority, including during a clarification or approval.
        if (rawSafety.HasEscalation)
        {
            await InvalidateAsync(state, "Superseded", token);
            await StartClinicalAsync(patient, state, text, token);
            return;
        }
        if (state.Awaiting == "clinical-answer" && state.WorkflowId.HasValue)
        {
            if (text.Length > 500) { Reply(state, "Please keep this answer under 500 characters.", "GATHERING_INFORMATION"); return; }
            var question = state.Questions.FirstOrDefault(q => state.Answers.All(a => a.QuestionId != q.Id));
            if (question != null) state.Answers.Add(new() { QuestionId = question.Id, Value = text });
            if (state.Questions.Any(q => state.Answers.All(a => a.QuestionId != q.Id)))
            { Reply(state, "Thank you. Please answer the next question below.", "GATHERING_INFORMATION"); return; }
            var workflow = await workflows.ContinueForPatientAsync(state.WorkflowId.Value, patient.PatientId, new() { Answers = state.Answers });
            if (workflow == null) throw new InvalidOperationException("The assessment is no longer waiting for answers. Refresh the conversation.");
            ApplyWorkflow(state, workflow);
            ClinicalReply(state, workflow);
            if (!state.SafetyBlocked && state.WantsAppointment) await SearchAsync(patient, state, token);
            return;
        }
        // Conversational assent is not an action token, and cannot mutate a proposal.
        if (state.PendingAction != null && Has(text, @"^(yes|okay|ok|confirm|book it|that looks good|go ahead|do it)[.! ]*$"))
        { Reply(state, "Please select an option and use the confirmation button below. Nothing has been changed yet.", "WAITING_FOR_HUMAN_APPROVAL"); return; }
        if (state.PendingAction != null && Has(text, @"^(cancel|stop|never mind|nevermind|dismiss|no)[.! ]*$"))
        {
            await InvalidateAsync(state, "Cancelled", token);
            state.WantsAppointment = false; state.Awaiting = null;
            Reply(state, "Request dismissed. No appointment was changed.", "CANCELLED");
            return;
        }

        if (Has(text, @"\b(reschedule|rescheduling)\b"))
        {
            await InvalidateAsync(state, "Superseded", token);
            Reply(state, "Rescheduling is not available in the mobile assistant. I can find another appointment, or help you cancel an existing one with your explicit confirmation. These are separate actions.", "COMPLETED");
            return;
        }
        if (symptoms || state.Awaiting == "symptoms")
        {
            await InvalidateAsync(state, "Superseded", token);
            if (Has(text, @"^(i need help with my symptoms|patient help|help with symptoms)[.! ]*$"))
            { state.Awaiting = "symptoms"; Reply(state, "Please describe how you feel, when it started, and any current symptoms.", "GATHERING_INFORMATION"); return; }
            if (appointmentIntent)
            {
                state.WantsAppointment = true;
                var doctors = await tools.FindDoctorsAsync("");
                state.SearchQuery = Query(text, doctors) ?? state.SearchQuery;
                var dateError = ApplyDates(text, state, Today());
                if (dateError != null) state.Awaiting = "preferences";
            }
            await StartClinicalAsync(patient, state, text, token);
            if (!state.SafetyBlocked && state.WantsAppointment) await SearchAsync(patient, state, token);
            return;
        }
        if (state.SafetyBlocked && (appointmentIntent || state.WantsAppointment || askingCancel))
        {
            // Re-evaluate persisted safety state. Conversations created before a
            // clinical-review decision may carry an old cached SafetyBlocked value.
            await CheckPatientSafetyAsync(patient.PatientId, state);
            if (state.SafetyBlocked)
            {
                await InvalidateAsync(state, "BlockedByClinicalSafety", token);
                Reply(state, "Appointment booking is paused because urgent safety guidance needs attention. Follow the guidance shown above and do not delay urgent care.", "WAITING_FOR_HUMAN_APPROVAL");
                return;
            }
        }
        if (state.Awaiting == "cancellation-reason")
        {
            if (text.Length is < 3 or > 500) { Reply(state, "Please give a cancellation reason between 3 and 500 characters.", "GATHERING_INFORMATION"); return; }
            state.CancellationReason = text;
            state.Awaiting = null;
            await CancellationAsync(patient, state, token);
            return;
        }
        var extension = registry.AdditionalAgents.FirstOrDefault(agent => agent.Capability.Enabled && agent.CanHandle(text));
        if (extension != null)
        {
            await InvalidateAsync(state, "Superseded", token);
            Reply(state, await extension.ReadAsync(text, patient, token), "COMPLETED");
            return;
        }
        if (Has(text, @"\b(report|reports|medical record|medical records|doctor schedule|change schedule)\b"))
        {
            await InvalidateAsync(state, "Superseded", token);
            Reply(state, "Medical report AI and doctor schedule management are coming soon. You can continue using the existing hospital screens for those services.", "COMPLETED");
            return;
        }
        if (askingCancel)
        {
            await InvalidateAsync(state, "Superseded", token);
            state.Awaiting = "cancellation-reason";
            state.CancellationReason = null;
            Reply(state, "What is your reason for cancelling? I will then show your appointments for explicit confirmation.", "GATHERING_INFORMATION");
            return;
        }
        if (askingList)
        {
            await InvalidateAsync(state, "Superseded", token);
            state.WantsAppointment = false; state.Awaiting = null;
            var filters = new AssistantState();
            var error = ApplyDates(text, filters, Today());
            if (error != null) { Reply(state, error, "GATHERING_INFORMATION"); return; }
            var all = await MyAppointmentsAsync(patient);
            state.Appointments = FilterAppointments(all, filters).Take(30).ToArray();
            Reply(state, state.Appointments.Count == 0 ? "No appointments matched that request."
                : $"Here are {state.Appointments.Count} of your appointments.", "COMPLETED", ["Your appointment records were retrieved from the hospital."]);
            return;
        }
        // A patient declining a further search must never be interpreted as a
        // doctor/specialty name (for example, "no need" used to become a search).
        if (state.Awaiting == "preferences" && Has(text, @"^(no need|not needed|nothing else|no thanks|that's all|that is all)[.! ]*$"))
        {
            state.WantsAppointment = false;
            state.Awaiting = null;
            Reply(state, "No problem. I have not created an appointment. You can ask me to find a doctor whenever you are ready.", "COMPLETED");
            return;
        }
        if (appointmentIntent || state.Awaiting == "preferences" || state.WantsAppointment)
        {
            await InvalidateAsync(state, "Superseded", token);
            var doctors = await tools.FindDoctorsAsync("");
            var query = Query(text, doctors);
            if (query == null && lookingForAlternative)
            {
                var current = (await MyAppointmentsAsync(patient)).Where(a => a.Status == "Confirmed" && a.EndAt > DateTime.UtcNow).ToArray();
                if (current.Length == 1) query = current[0].DoctorName;
                Reply(state, "I can look for a separate appointment. Your current appointment will remain active unless you request and confirm its cancellation.", "GATHERING_INFORMATION");
            }
            if (query != null) state.SearchQuery = query;
            // Do not treat arbitrary conversational text as a specialty. Keep the
            // existing valid preference until the patient supplies a recognised
            // doctor/specialty or an explicit date/time preference.
            state.WantsAppointment = true;
            var error = ApplyDates(text, state, Today());
            if (error != null) { state.Awaiting = "preferences"; Reply(state, error, "GATHERING_INFORMATION"); return; }
            await SearchAsync(patient, state, token);
            return;
        }
        Reply(state, "I can help with symptoms, find a doctor, show your appointments, or prepare a booking or cancellation for your confirmation. What would you like to do?", "GATHERING_INFORMATION");
    }

    private async Task StartClinicalAsync(PatientDto patient, AssistantState state, string text, CancellationToken token)
    {
        state.State = "GATHERING_INFORMATION";
        state.Answers = [];
        // Keep one active review per patient. Repeated messages/conversations must
        // not flood clinicians with duplicate cases for the same unresolved issue.
        var existingReview = (await workflows.GetHistoryForPatientAsync(patient.PatientId))
            .FirstOrDefault(item => item.RequiresHumanReview &&
                item.ApprovalStatus is TriageApprovalStatuses.Pending or TriageApprovalStatuses.RevisionRequested);
        if (existingReview is not null)
        {
            state.WorkflowId = existingReview.WorkflowId;
            ApplyWorkflow(state, existingReview);
            ClinicalReply(state, existingReview);
            return;
        }
        var workflow = await workflows.StartForPatientAsync(patient.PatientId, new() { Symptoms = text });
        state.WorkflowId = workflow.WorkflowId;
        ApplyWorkflow(state, workflow);
        ClinicalReply(state, workflow);
    }

    private static void ApplyWorkflow(AssistantState state, TriageWorkflowDto workflow)
    {
        var unsafeResult = workflow.TriageLevel is "Emergency" or "Urgent" || workflow.Status == TriageWorkflowStatuses.FailedSafely;
        var needsReview = workflow.RequiresHumanReview &&
            workflow.ApprovalStatus is TriageApprovalStatuses.Pending or TriageApprovalStatuses.RevisionRequested;
        // Emergency/urgent outcomes are never downgraded by a later conversational message.
        var alreadyUrgent = state.Clinical?.TriageLevel is "Emergency" or "Urgent" || state.Clinical?.FailedSafely == true;
        // A clinical review requires staff approval after the patient's explicit slot
        // selection; it must not prevent the patient from receiving a safe proposal.
        // Only urgent/emergency, failed-safe, and missing-required-information states
        // block normal appointment actions.
        state.SafetyBlocked = unsafeResult || alreadyUrgent || workflow.Status == TriageWorkflowStatuses.PendingPatientInput;
        if (!alreadyUrgent)
            state.Clinical = new(workflow.TriageLevel, "Existing safety workflow", needsReview,
                workflow.Status == TriageWorkflowStatuses.FailedSafely, [], workflow.RedFlags, workflow.UrgentFlags,
                workflow.ClinicalReviewFlags, null, workflow.MissingInformation, []);
        state.Questions = workflow.Status == TriageWorkflowStatuses.PendingPatientInput
            ? (workflow.Guidance?.FollowUpItems ?? []).Select(q => new AssistantQuestion(q.Id, q.Prompt, q.Required)).ToList() : [];
        state.Awaiting = state.Questions.Count > 0 ? "clinical-answer" : needsReview ? "clinical-review" : null;
    }

    private static void ClinicalReply(AssistantState state, TriageWorkflowDto workflow)
    {
        var guidance = workflow.Guidance;
        var parts = new List<string> { workflow.PatientMessage };
        if (!string.IsNullOrWhiteSpace(guidance?.Summary)) parts.Add(guidance.Summary);
        parts.AddRange(guidance?.Actions ?? []);
        parts.AddRange(guidance?.SeekHelpIf ?? []);
        if (state.Awaiting == "clinical-review") parts.Add("Clinical review is pending. You can refresh this conversation to check its status.");
        Reply(state, string.Join("\n\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct()),
            state.Questions.Count > 0 ? "GATHERING_INFORMATION" : state.SafetyBlocked || state.Awaiting == "clinical-review" ? "WAITING_FOR_HUMAN_APPROVAL" : "COMPLETED",
            ["The existing safety workflow checked your report.", "Guidance and any required clinical review were saved."]);
    }

    private async Task SearchAsync(PatientDto patient, AssistantState state, CancellationToken token)
    {
        await CheckPatientSafetyAsync(patient.PatientId, state);
        if (state.SafetyBlocked)
        {
            Reply(state, "Appointment booking is paused because urgent safety guidance needs attention. Follow the guidance shown above and do not delay urgent care.", "WAITING_FOR_HUMAN_APPROVAL");
            return;
        }
        if (string.IsNullOrWhiteSpace(state.SearchQuery))
        {
            state.Awaiting = "preferences";
            Reply(state, "Which doctor or specialty would you prefer? You can also include a date and morning, afternoon, or evening.", "GATHERING_INFORMATION");
            return;
        }
        state.State = "PROPOSING_ACTION";
        var proposal = await proposals.CreateAsync(new(patient.PatientId, state.Clinical, state.SearchQuery,
            state.PreferredDate, state.Period, state.ThroughDate), token);
        state.Doctors = proposal.Doctors;
        state.Slots = proposal.Slots;
        state.Awaiting = "preferences";
        if (proposal.ProposalId.HasValue && proposal.Slots.Count > 0)
        {
            state.PendingAction = new() {
                Type = "book", Title = "Confirm an appointment", ProposalId = proposal.ProposalId,
                Description = "Select an option, then confirm. The hospital will assign the final appointment number.", Slots = proposal.Slots
            };
            var preference = state.PreferredDate.HasValue ? $" for {state.PreferredDate:yyyy-MM-dd}" : "";
            if (state.ThroughDate.HasValue) preference += $" through {state.ThroughDate:yyyy-MM-dd}";
            if (state.Period != null) preference += $" ({state.Period})";
            Reply(state, $"I found {proposal.Slots.Count} available option(s){preference}. Please review the details and confirm one below.",
                "WAITING_FOR_HUMAN_APPROVAL", ["Approved doctors were found.", "Available hospital sessions were checked.", "Options were saved for your confirmation; no booking was made."]);
        }
        else Reply(state, proposal.Message + " Tell me another doctor, specialty, or date to try.", "GATHERING_INFORMATION",
            ["Approved doctors and available hospital sessions were checked."]);
    }

    private async Task CancellationAsync(PatientDto patient, AssistantState state, CancellationToken token)
    {
        var available = (await MyAppointmentsAsync(patient)).Where(a => a.Status is not ("Cancelled" or "Completed") && a.EndAt > DateTime.UtcNow).ToArray();
        if (available.Length == 0) { Reply(state, "You have no upcoming appointments available to cancel.", "COMPLETED"); return; }
        state.PendingAction = new() { Type = "cancel", Title = "Confirm cancellation",
            Description = "Select the appointment you want to cancel. Reason: " + state.CancellationReason,
            Appointments = available };
        Reply(state, "Select the appointment below and use Confirm cancellation. Nothing has been cancelled yet.", "WAITING_FOR_HUMAN_APPROVAL",
            ["Your upcoming appointments were checked."]);
    }

    private async Task CheckPatientSafetyAsync(int patientId, AssistantState state)
    {
        // Opening a new conversation must not bypass an unresolved safety assessment.
        var history = await workflows.GetHistoryForPatientAsync(patientId);
        // Only genuinely open review states lock a future conversation. A rejected
        // review is finalized; it must show its care-team guidance, but it must not
        // permanently prevent the patient from starting an unrelated conversation.
        var unresolved = history.FirstOrDefault(w =>
            w.ApprovalStatus is TriageApprovalStatuses.Pending or TriageApprovalStatuses.RevisionRequested ||
            w.Status is TriageWorkflowStatuses.FailedSafely or TriageWorkflowStatuses.PendingPatientInput);
        if (unresolved == null) return;
        if (state.WorkflowId != unresolved.WorkflowId) state.Answers = [];
        state.WorkflowId = unresolved.WorkflowId;
        ApplyWorkflow(state, unresolved);
    }

    private async Task<IReadOnlyList<AppointmentDto>> MyAppointmentsAsync(PatientDto patient)
    {
        var result = await appointments.GetAllAppointmentsAsync(null, null, null, null, "date", "desc", 1, 100, patient.PatientId);
        return result.Data.Where(a => a.PatientId == patient.PatientId).ToArray();
    }

    private static IEnumerable<AppointmentDto> FilterAppointments(IEnumerable<AppointmentDto> all, AssistantState state) => all.Where(a =>
        (!state.PreferredDate.HasValue || DateOnly.FromDateTime(Local(a.StartAt)) >= state.PreferredDate &&
         DateOnly.FromDateTime(Local(a.StartAt)) <= (state.ThroughDate ?? state.PreferredDate)) &&
        (state.Period == null || state.Period switch { "morning" => Local(a.StartAt).Hour < 12,
            "afternoon" => Local(a.StartAt).Hour is >= 12 and < 17, "evening" => Local(a.StartAt).Hour >= 17, _ => false }));

    private async Task InvalidateAsync(AssistantState state, string status, CancellationToken token)
    {
        if (state.PendingAction?.ProposalId is int proposalId)
        {
            var proposal = await db.AppointmentProposals.SingleOrDefaultAsync(p => p.AppointmentProposalId == proposalId, token);
            if (proposal?.Status == "PendingPatientConfirmation") proposal.Status = status;
        }
        state.PendingAction = null;
    }

    private static bool Seen(AssistantState state, Guid id, string fingerprint)
    {
        if (!state.Requests.TryGetValue(id, out var stored)) return false;
        if (stored != fingerprint) throw new ArgumentException("This request ID was already used for different content.");
        return true;
    }
    private static string Fingerprint(string input) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    private static AssistantState Read(AssistantConversation entity) => JsonSerializer.Deserialize<AssistantState>(entity.StateJson, Json) ?? new();
    private static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), AppointmentAgentTools.HospitalTimeZone);
    private static DateOnly Today() => DateOnly.FromDateTime(Local(DateTime.UtcNow));
    private static void Reply(AssistantState state, string text, string status, IReadOnlyList<string>? progress = null)
    {
        state.State = status;
        state.Messages.Add(new(Guid.NewGuid().ToString(), "assistant", text, DateTime.UtcNow, progress ?? []));
    }
    private async Task<AssistantConversation> OwnedAsync(int patientId, Guid id, CancellationToken token) =>
        await db.AssistantConversations.SingleOrDefaultAsync(c => c.PatientId == patientId && c.AssistantConversationId == id, token)
            ?? throw new KeyNotFoundException("Conversation not found.");
    private async Task SaveAsync(AssistantConversation entity, AssistantState state, CancellationToken token)
    { entity.StateJson = JsonSerializer.Serialize(state, Json); entity.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(token); }
    private AssistantConversationResponse Response(AssistantConversation entity, AssistantState state) => new(
        entity.AssistantConversationId, entity.Title, state.State, entity.UpdatedAt, state.Messages, state.PendingAction,
        state.Questions.Where(q => state.Answers.All(a => a.QuestionId != q.Id)).Take(1).ToArray(),
        state.Appointments, state.Slots, state.Doctors, Capabilities);

    private async Task<T> LockedAsync<T>(int patientId, Func<Task<T>> action, CancellationToken token)
    {
        var gate = Gates[(patientId & int.MaxValue) % Gates.Length];
        await gate.WaitAsync(token);
        try
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(token) : null;
            if (db.Database.IsNpgsql())
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(1396916552, {patientId})", token);
            var result = await action();
            if (transaction != null) await transaction.CommitAsync(token);
            return result;
        }
        finally { gate.Release(); }
    }
}
