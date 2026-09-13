using System.Globalization;
using System.Text.RegularExpressions;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;

namespace HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;

/// Bounded intent/constraint parsing. Unknown wording asks for clarification; it never invents facts.
public static class AssistantPreferences
{
    public static bool Has(string text, string pattern) => Regex.IsMatch(text, pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static string? Query(string text, IReadOnlyList<AgentDoctor> doctors)
    {
        var named = doctors.Where(doctor =>
            text.Contains(doctor.Name, StringComparison.OrdinalIgnoreCase) ||
            text.Contains(doctor.Name.Replace("Dr. ", "", StringComparison.OrdinalIgnoreCase), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (named.Length == 1) return named[0].Name;
        var match = Regex.Match(text, @"\bdr\.?\s+([\p{L}]+(?:\s+[\p{L}]+)?)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            // A surname alone is a valid search, but candidates still come from the approved doctor service.
            return match.Groups[1].Value.Split(' ')[0];
        }
        foreach (var specialty in doctors.Select(d => d.Specialty).Distinct().OrderByDescending(x => x.Length))
            if (text.Contains(specialty, StringComparison.OrdinalIgnoreCase)) return specialty;
        (string Pattern, string Query)[] aliases = [
            (@"\b(cardiologist|cardiology|heart doctor)\b", "Cardiology"),
            (@"\b(eye doctor|eye specialist|ophthalmologist|ophthalmology)\b", "Ophthalmology"),
            (@"\b(dermatologist|skin doctor|dermatology)\b", "Dermatology"),
            (@"\b(pediatrician|paediatrician|child specialist)\b", "Pediatrics"),
            (@"\b(general doctor|general medicine|general practitioner)\b", "General Medicine"),
            (@"\b(neurologist|neurology)\b", "Neurology")
        ];
        foreach (var (pattern, query) in aliases)
            if (Has(text, pattern))
            {
                if (query == "Ophthalmology" && !doctors.Any(d => d.Specialty.Contains(query, StringComparison.OrdinalIgnoreCase)))
                    return doctors.Select(d => d.Specialty).FirstOrDefault(s => s.Contains("eye", StringComparison.OrdinalIgnoreCase)) ?? query;
                return query;
            }
        return null;
    }

    public static string? ApplyDates(string text, AssistantState state, DateOnly today)
    {
        if (Has(text, @"\b(any date|earliest|any day|first available)\b"))
        { state.PreferredDate = null; state.ThroughDate = null; }
        var iso = Regex.Match(text, @"\b\d{4}-\d{2}-\d{2}\b");
        DateOnly? date = null;
        if (iso.Success)
        {
            if (!DateOnly.TryParseExact(iso.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return "Please provide a valid date as YYYY-MM-DD.";
            date = parsed;
        }
        else if (Has(text, @"\bday after tomorrow\b")) date = today.AddDays(2);
        else if (Has(text, @"\btomorrow\b")) date = today.AddDays(1);
        else if (Has(text, @"\btoday\b")) date = today;
        else
        {
            foreach (var day in Enum.GetValues<DayOfWeek>())
            {
                if (!Has(text, $@"\b{day}\b")) continue;
                var distance = ((int)day - (int)today.DayOfWeek + 7) % 7;
                date = today.AddDays(distance == 0 ? 7 : distance);
                break;
            }
        }
        if (date.HasValue)
        {
            state.PreferredDate = date;
            state.ThroughDate = null;
        }
        else if (Has(text, @"\bnext week\b"))
        {
            var distance = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            state.PreferredDate = today.AddDays(distance == 0 ? 7 : distance);
            state.ThroughDate = state.PreferredDate.Value.AddDays(6);
        }
        else if (Has(text, @"\b(january|february|march|april|may|june|july|august|september|october|november|december)\s+\d|\b\d{1,2}[/\-]\d{1,2}\b"))
            return "Please give that date as YYYY-MM-DD so I can check the correct day.";
        if (state.PreferredDate < today) return "Please choose today or a future date.";
        if (Has(text, @"\b(any time|anytime)\b")) state.Period = null;
        else foreach (var period in new[] { "morning", "afternoon", "evening" })
            if (Has(text, $@"\b{period}\b")) state.Period = period;
        if (Has(text, @"\b\d{1,2}(?::\d{2})?\s*(am|pm)\b|\b\d{1,2}:\d{2}\b"))
            return "I can filter by morning, afternoon, or evening. Which would you prefer? The options show each session's exact time.";
        return null;
    }
}
