using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagementSystem.Api.AgenticAI.SafeTriage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;

/// <summary>
/// Returned by Gemini after the function-calling loop: a set of validated, Gemini-chosen slots
/// along with a patient-facing message and suggested actions.
/// Every slot in this record was sourced from live tool calls — not invented by the LLM.
/// </summary>
public sealed record GeminiAppointmentSuggestion(
    IReadOnlyList<AgentSlot> Slots,
    IReadOnlyList<AgentDoctor> Doctors,
    string Message,
    IReadOnlyList<string> SuggestedActions);

public interface IGeminiAppointmentIntelligenceClient
{
    /// <summary>
    /// Runs a Gemini function-calling loop to find and rank appointment slots.
    /// Returns null if Gemini is unavailable or fails (caller falls back to deterministic logic).
    /// </summary>
    Task<GeminiAppointmentSuggestion?> SuggestAsync(
        AppointmentProposalRequest request,
        IAppointmentSearchTools tools,
        CancellationToken cancellationToken = default);
}

public sealed class GeminiAppointmentIntelligenceClient(
    HttpClient http,
    IConfiguration configuration,
    ILogger<GeminiAppointmentIntelligenceClient> logger) : IGeminiAppointmentIntelligenceClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// Maximum function-call rounds before falling back to deterministic logic.
    private const int MaxToolRounds = 6;

    private const string SystemPrompt = """
    You are the Appointment Proposal Agent for SmartCare Hospital Management System.
    Your job is to find suitable, available appointment slots for a patient using the provided tools.

    ALLOWED TOOLS (you may ONLY call these):
    - find_doctors(query): Search approved doctors by specialty or name.
    - find_slots(doctor_id, date?): Find available slots for a specific doctor, optionally filtered by date.

    STRICT RULES:
    - NEVER book an appointment. Booking is strictly prohibited in this step.
    - NEVER invent or guess doctor_ids or slot references. Use only IDs returned by the tools.
    - NEVER give clinical advice, diagnoses, or emergency guidance. A clinical safety agent handles that.
    - Patient confirmation is always required before any booking.
    - If a date is requested but has no slots, try the next available dates rather than giving up immediately.
    - Prefer slots closest to the patient's preferred date if one was specified.

    After completing your tool calls, respond with JSON only (no markdown, no explanation):
    {
      "chosenSlotReferences": ["S<id>", ...],
      "message": "<patient-facing summary, max 200 chars>",
      "suggestedActions": ["<action 1>", "<action 2>"]
    }
    - chosenSlotReferences: slot references from find_slots results, maximum 5, ordered by preference.
    - message: written for the patient, not technical. If no slots found, explain clearly.
    - suggestedActions: 1-3 clear next steps the patient should take.
    """;

    public async Task<GeminiAppointmentSuggestion?> SuggestAsync(
        AppointmentProposalRequest request,
        IAppointmentSearchTools tools,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var apiKey = configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(apiKey)) return null;

            var timeoutSeconds = Math.Clamp(
                configuration.GetValue<int?>("Gemini:TimeoutSeconds") ?? 45, 15, 90);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var rawModel = configuration["Gemini:AppointmentModel"]?.Trim()
                           ?? GeminiRequest.Model(configuration);
            var model = SanitiseModel(rawModel);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var userMessage = JsonSerializer.Serialize(new
            {
                specialty     = request.Specialty,
                preferredDate = request.PreferredDate?.ToString("yyyy-MM-dd"),
                throughDate   = request.ThroughDate?.ToString("yyyy-MM-dd"),
                period        = request.Period,
                todayDate     = today.ToString("yyyy-MM-dd"),
                note          = "Search for available slots matching the request. Prefer closest date if preferredDate is given."
            }, JsonOpts);

            var contents = new List<object>
            {
                new { role = "user", parts = new[] { new { text = userMessage } } }
            };

            // Accumulated tool results — used to validate Gemini's final slot selection.
            var allDoctors = new Dictionary<int, AgentDoctor>();
            var allSlots   = new Dictionary<string, AgentSlot>(); // keyed by Reference e.g. "S42"

            for (var round = 0; round < MaxToolRounds; round++)
            {
                timeoutCts.Token.ThrowIfCancellationRequested();

                var requestBody = new
                {
                    systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
                    tools             = new[] { new { functionDeclarations = BuildFunctionDeclarations() } },
                    toolConfig        = new { functionCallingConfig = new { mode = "AUTO" } },
                    contents          = contents.ToArray(),
                    generationConfig  = new { temperature = 0.0, maxOutputTokens = 1500 }
                };

                var httpResponse = await http.PostAsJsonAsync(
                    GeminiRequest.GenerateContentPath(model), requestBody, JsonOpts, timeoutCts.Token);
                await GeminiRequest.EnsureSuccessAsync(httpResponse, model, timeoutCts.Token);

                var rawBody = await httpResponse.Content.ReadAsStringAsync(timeoutCts.Token);

                List<(string Name, Dictionary<string, object?> Args)> functionCalls;
                string? finalText;

                using (var payload = JsonDocument.Parse(rawBody))
                {
                    var parts = payload.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts");

                    (functionCalls, finalText) = ExtractPartsFromGeminiTurn(parts);
                }

                // Final text turn: Gemini finished reasoning.
                if (finalText is not null)
                    return ParseAndValidateSuggestion(finalText, allSlots, allDoctors);

                // Function-call turn: nothing to dispatch.
                if (functionCalls.Count == 0) break;

                // Add the model's function-call turn to conversation history.
                contents.Add(new
                {
                    role  = "model",
                    parts = functionCalls
                        .Select(c => new { functionCall = new { name = c.Name, args = (object)c.Args } })
                        .ToArray<object>()
                });

                // Execute each function call with the real tools.
                var functionResponses = new List<object>();
                foreach (var (name, args) in functionCalls)
                {
                    object result = name switch
                    {
                        "find_doctors" => await ExecuteFindDoctorsAsync(args, tools, allDoctors, timeoutCts.Token),
                        "find_slots"   => await ExecuteFindSlotsAsync(args, tools, allDoctors, allSlots, timeoutCts.Token),
                        _              => new { error = $"Unknown tool: {name}. Only find_doctors and find_slots are allowed." }
                    };

                    functionResponses.Add(new { functionResponse = new { name, response = result } });
                }

                contents.Add(new { role = "user", parts = functionResponses.ToArray<object>() });
            }

            logger.LogWarning("Gemini appointment agent did not return a final response within {MaxRounds} rounds.", MaxToolRounds);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException
                                       or JsonException or InvalidOperationException or OperationCanceledException)
        {
            logger.LogWarning(ex, "Gemini appointment intelligence failed; deterministic fallback will be used.");
            return null;
        }
    }

    // ── Tool declarations ────────────────────────────────────────────────────────

    private static object[] BuildFunctionDeclarations() =>
    [
        new
        {
            name        = "find_doctors",
            description = "Search the hospital for approved doctors by specialty or partial name. " +
                          "Returns doctors with their IDs that can be passed to find_slots.",
            parameters  = new
            {
                type       = "object",
                properties = new
                {
                    query = new
                    {
                        type        = "string",
                        description = "Specialty keyword or partial doctor name " +
                                      "(e.g. 'cardiology', 'orthopaedics', 'neurology', 'Dr. Silva')"
                    }
                },
                required   = new[] { "query" }
            }
        },
        new
        {
            name        = "find_slots",
            description = "Find available future appointment slots for a specific doctor. " +
                          "Only returns slots with available capacity right now. Optionally filter by date.",
            parameters  = new
            {
                type       = "object",
                properties = new
                {
                    doctor_id = new
                    {
                        type        = "integer",
                        description = "The DoctorId returned by find_doctors. Must be an ID returned by that tool."
                    },
                    date = new
                    {
                        type        = "string",
                        description = "Optional date filter in YYYY-MM-DD format (Sri Lanka timezone). " +
                                      "Omit to return all future available slots for this doctor."
                    }
                },
                required   = new[] { "doctor_id" }
            }
        }
    ];

    // ── Tool execution ───────────────────────────────────────────────────────────

    private static async Task<object> ExecuteFindDoctorsAsync(
        Dictionary<string, object?> args, IAppointmentSearchTools tools,
        Dictionary<int, AgentDoctor> allDoctors, CancellationToken token)
    {
        var query = args.TryGetValue("query", out var q) ? q?.ToString() ?? "" : "";
        var doctors = await tools.FindDoctorsAsync(query);
        foreach (var d in doctors) allDoctors[d.DoctorId] = d;

        return new
        {
            doctors = doctors.Select(d => new
            {
                reference = d.Reference, doctor_id = d.DoctorId,
                name = d.Name, specialty = d.Specialty
            }).ToArray()
        };
    }

    private static async Task<object> ExecuteFindSlotsAsync(
        Dictionary<string, object?> args, IAppointmentSearchTools tools,
        Dictionary<int, AgentDoctor> allDoctors, Dictionary<string, AgentSlot> allSlots,
        CancellationToken token)
    {
        if (!args.TryGetValue("doctor_id", out var rawId) || rawId is null
            || !int.TryParse(rawId.ToString(), out var doctorId))
            return new { error = "doctor_id must be a valid integer." };

        if (!allDoctors.TryGetValue(doctorId, out var doctor))
            return new { error = $"Doctor ID {doctorId} was not returned by find_doctors. Call find_doctors first." };

        DateOnly? date = null;
        if (args.TryGetValue("date", out var rawDate) && rawDate is not null
            && DateOnly.TryParse(rawDate.ToString(), out var parsed))
            date = parsed;

        var slots = await tools.FindSlotsAsync(doctor, date);
        foreach (var s in slots) allSlots[s.Reference] = s;

        return new
        {
            slots = slots.Select(s => new
            {
                reference          = s.Reference,
                slot_id            = s.DoctorTimeSlotId,
                doctor_name        = s.DoctorName,
                specialty          = s.Specialty,
                start_at           = s.StartAt.ToString("yyyy-MM-dd HH:mm zzz"),
                end_at             = s.EndAt.ToString("yyyy-MM-dd HH:mm zzz"),
                appointment_number = s.AppointmentNumber,
                available_count    = s.AvailableCount,
                consultation_fee   = s.ConsultationFee,
                location           = s.Location
            }).ToArray()
        };
    }

    // ── Response parsing ─────────────────────────────────────────────────────────

    /// <summary>
    /// Reads all parts from a Gemini response turn while the owning JsonDocument is still alive,
    /// capturing all data needed before the document is disposed.
    /// </summary>
    private static (List<(string Name, Dictionary<string, object?> Args)> FunctionCalls, string? FinalText)
        ExtractPartsFromGeminiTurn(JsonElement parts)
    {
        var calls    = new List<(string, Dictionary<string, object?>)>();
        string? text = null;

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textEl))
            {
                text = textEl.GetString();
                break;
            }

            if (!part.TryGetProperty("functionCall", out var fc)) continue;

            var name = fc.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var args = new Dictionary<string, object?>();

            if (fc.TryGetProperty("args", out var argsEl) && argsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in argsEl.EnumerateObject())
                {
                    args[prop.Name] = prop.Value.ValueKind switch
                    {
                        JsonValueKind.String                                        => prop.Value.GetString(),
                        JsonValueKind.Number when prop.Value.TryGetInt32(out var i) => (object?)i,
                        JsonValueKind.Number                                        => (object?)prop.Value.GetDouble(),
                        JsonValueKind.True                                          => true,
                        JsonValueKind.False                                         => false,
                        _                                                           => null
                    };
                }
            }

            calls.Add((name, args));
        }

        return (calls, text);
    }

    /// <summary>
    /// Parses Gemini's final JSON response and validates every slot reference
    /// against what the real tools actually returned. Gemini cannot invent a slot.
    /// </summary>
    private static GeminiAppointmentSuggestion? ParseAndValidateSuggestion(
        string json, Dictionary<string, AgentSlot> allSlots, Dictionary<int, AgentDoctor> allDoctors)
    {
        json = json.Trim();
        if (json.StartsWith("```", StringComparison.Ordinal))
        {
            var start = json.IndexOf('\n') + 1;
            var end   = json.LastIndexOf("```", StringComparison.Ordinal);
            if (end > start) json = json[start..end].Trim();
        }

        using var doc  = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("chosenSlotReferences", out var refsEl)
            || refsEl.ValueKind != JsonValueKind.Array)
            return null;

        // Hard safety gate: only include slots that were actually returned by the tools.
        var chosenSlots = new List<AgentSlot>();
        foreach (var r in refsEl.EnumerateArray().Take(5))
        {
            var reference = r.GetString();
            if (string.IsNullOrWhiteSpace(reference)) continue;
            if (!allSlots.TryGetValue(reference, out var slot)) continue; // not from tools → reject
            chosenSlots.Add(slot);
        }

        var message = root.TryGetProperty("message", out var msgEl)
            ? msgEl.GetString()?.Trim() ?? ""
            : "";
        if (string.IsNullOrWhiteSpace(message)) return null;

        var actions = new List<string>();
        if (root.TryGetProperty("suggestedActions", out var actEl) && actEl.ValueKind == JsonValueKind.Array)
            foreach (var a in actEl.EnumerateArray().Take(3))
                if (a.GetString()?.Trim() is { Length: > 0 } action) actions.Add(action);
        if (actions.Count == 0)
            actions = ["Select one appointment option.", "Confirm your selection before booking."];

        var referencedDoctors = chosenSlots
            .Select(s => s.DoctorId).Distinct()
            .Where(id => allDoctors.ContainsKey(id))
            .Select(id => allDoctors[id])
            .ToList();

        return new GeminiAppointmentSuggestion(chosenSlots, referencedDoctors, message, actions);
    }

    private static string SanitiseModel(string raw)
    {
        if (raw.StartsWith("models/", StringComparison.OrdinalIgnoreCase)) raw = raw["models/".Length..];
        if (raw.Contains('/') || raw.Contains(':') || raw.Any(char.IsWhiteSpace))
            throw new InvalidOperationException(
                $"Gemini model ID '{raw}' is invalid. Use a bare model ID such as 'gemini-2.0-flash'.");
        return raw;
    }
}
