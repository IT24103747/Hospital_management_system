using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.DTOs;
using static HospitalManagementSystem.Api.AgenticAI.HospitalAssistant.AssistantPreferences;

namespace HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;

public sealed partial class HospitalAssistantService
{
    private async Task<bool> TryReadAsync(PatientDto patient, AssistantState state, string text, CancellationToken token)
    {
        // Mutation verbs remain in the existing proposal/approval route.
        if (Has(text, @"\b(book|booking|reserve|schedule|reschedule|cancel|create|confirm|proceed)\b")) return false;
        if (Has(text, @"^(yes|okay|ok|go ahead|do it|no need|not needed|nothing else|no thanks|that's all|that is all)[.! ]*$")) return false;
        if (Has(text, @"^(hello|hi|help|what can you do)[?!. ]*$"))
        {
            Reply(state, "I can show your appointments, find approved doctors, check availability, and prepare appointment requests for your confirmation. You can keep using these information services while an assessment is pending.", "COMPLETED");
            return true;
        }
        var extension = registry.AdditionalAgents.FirstOrDefault(agent => agent.Capability.Enabled && agent.CanHandle(text));
        if (extension != null)
        {
            Reply(state, await extension.ReadAsync(text, patient, token), "COMPLETED");
            return true;
        }
        if (Has(text, @"\bappointments?\b") && Has(text, @"\b(my|next|existing|have|history|all|cancelled|canceled|show|list|check|view)\b"))
        {
            var all = await MyAppointmentsAsync(patient);
            var next = Has(text, @"\bnext\b");
            var cancelled = Has(text, @"\b(cancelled|canceled)\b");
            var history = Has(text, @"\bhistory\b");
            var includeAll = Has(text, @"\ball\b");
            var records = all.AsEnumerable();
            if (next || (!cancelled && !history && !includeAll))
                records = records.Where(a => a.Status == "Confirmed" && a.StartAt > DateTime.UtcNow);
            else if (cancelled) records = records.Where(a => a.Status == "Cancelled");
            else if (history) records = records.Where(a => a.Status is "Cancelled" or "Completed" || a.EndAt <= DateTime.UtcNow);
            var filters = new AssistantState();
            // History is not constrained by the future-only booking date parser.
            if (!history && !cancelled && !includeAll)
            {
                var error = ApplyDates(text, filters, Today());
                if (error != null) { Reply(state, error, "GATHERING_INFORMATION"); return true; }
                records = FilterAppointments(records, filters);
            }
            state.Appointments = records.OrderBy(a => a.StartAt).Take(next ? 1 : 30).ToArray();
            var first = state.Appointments.FirstOrDefault();
            var reply = first == null ? "No appointments matched that request."
                : next ? $"Your next appointment is with {first.DoctorName} on {Local(first.StartAt):d MMMM yyyy} at {Local(first.StartAt):h:mm tt}. This is the session start time."
                : $"Here are {state.Appointments.Count} matching appointments{(cancelled ? " (cancelled)" : history ? " from your history" : "")}.";
            Reply(state, reply, "COMPLETED", ["Your appointment records were retrieved from the hospital."]);
            return true;
        }

        var doctorRequest = Has(text, @"\b(doctors?|specialists?|cardiologists?|cardiology|general medicine|ophthalmologists?|dermatologists?|neurologists?)\b");
        var followup = state.SearchQuery != null && Has(text, @"\b(which one|which of them|available|availability|tomorrow|morning|afternoon|evening)\b");
        var preferenceReply = state.ReadSearchMode != null && state.PendingAction == null;
        if (!doctorRequest && !followup && !preferenceReply) return false;
        // A date-only reply to an explicit booking stays in the booking flow.
        if (!doctorRequest && state.WantsAppointment && state.ReadSearchMode == null) return false;
        var doctors = await tools.FindDoctorsAsync("");
        if (!doctorRequest && !followup && Query(text, doctors) == null) return false;
        var query = Query(text, doctors) ?? state.SearchQuery;
        var availability = Has(text, @"\b(available|availability|tomorrow|today|monday|tuesday|wednesday|thursday|friday|saturday|sunday|morning|afternoon|evening)\b") ||
            Has(text, @"\b\d{4}-\d{2}-\d{2}\b") || state.ReadSearchMode == "availability";
        state.ReadSearchMode = availability ? "availability" : "doctors";
        if (query == null)
        {
            Reply(state, "Which doctor or specialty would you like me to look up?", "GATHERING_INFORMATION", ["The approved doctor directory was checked."]);
            return true;
        }
        state.SearchQuery = query;
        state.Doctors = await tools.FindDoctorsAsync(query);
        if (!availability)
        {
            Reply(state, state.Doctors.Count == 0 ? "No approved doctors matched that request."
                : $"These approved doctors match {query}. You can ask about their availability or request a booking.",
                "COMPLETED", ["The approved doctor directory was checked."]);
            return true;
        }
        var preferences = new AssistantState();
        var dateError = ApplyDates(text, preferences, Today());
        if (dateError != null) { Reply(state, dateError, "GATHERING_INFORMATION"); return true; }
        var slots = new List<AgentSlot>();
        foreach (var doctor in state.Doctors.Take(5))
        {
            token.ThrowIfCancellationRequested();
            if (preferences.ThroughDate.HasValue)
                for (var date = preferences.PreferredDate!.Value; date <= preferences.ThroughDate.Value; date = date.AddDays(1))
                    slots.AddRange(await tools.FindSlotsAsync(doctor, date));
            else slots.AddRange(await tools.FindSlotsAsync(doctor, preferences.PreferredDate));
        }
        state.Slots = slots.Where(s => preferences.Period == null || preferences.Period switch {
            "morning" => s.StartAt.Hour < 12, "afternoon" => s.StartAt.Hour is >= 12 and < 17,
            "evening" => s.StartAt.Hour >= 17, _ => false
        }).DistinctBy(s => s.DoctorTimeSlotId).OrderBy(s => s.StartAt).Take(5).ToArray();
        // Retain verified preferences for an explicit follow-up booking request.
        state.PreferredDate = preferences.PreferredDate;
        state.ThroughDate = preferences.ThroughDate;
        state.Period = preferences.Period;
        Reply(state, state.Slots.Count == 0 ? "No available sessions matched that request."
            : "Here are available sessions. Nothing is reserved. Ask me to book an option if you want a confirmation proposal.",
            "COMPLETED", state.Doctors.Count == 0 ? ["The approved doctor directory was checked."]
                : ["The approved doctor directory was checked.", "Doctor availability was checked using hospital sessions."]);
        return true;
    }
}
