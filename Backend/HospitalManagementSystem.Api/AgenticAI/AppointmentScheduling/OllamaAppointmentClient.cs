using System.Text.Json;
using Microsoft.Extensions.Options;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;

namespace HospitalManagementSystem.Api.AgenticAI.AppointmentScheduling;

public interface IAppointmentModelClient
{
    Task<AppointmentAgentDecision> DecideAsync(IReadOnlyList<AppointmentAgentMessage> messages, CancellationToken cancellationToken);
}

public sealed class GeminiAppointmentClient(HttpClient http, IOptions<AppointmentAgentOptions> options) : IAppointmentModelClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // This is a small, constrained decision protocol. The model never receives database access.
    public const string DecisionSchema = """
    {"type":"object","additionalProperties":false,"required":["action"],"properties":{
      "action":{"type":"string","enum":["find_doctors","find_slots","book_appointment","final"]},
      "query":{"type":"string","maxLength":100},
      "doctorRef":{"type":"string","maxLength":30},
      "date":{"type":"string","maxLength":10},
      "slotRef":{"type":"string","maxLength":30},
      "slotRefs":{"type":"array","maxItems":5,"items":{"type":"string"}},
      "doctorRefs":{"type":"array","maxItems":5,"items":{"type":"string"}},
      "outcome":{"type":"string","enum":["recommend","doctors","clarify","no_matches","out_of_scope"]}
    }}
    """;

    public async Task<AppointmentAgentDecision> DecideAsync(IReadOnlyList<AppointmentAgentMessage> messages, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.Value.TimeoutSeconds));
        try
        {
            using var schema = JsonDocument.Parse(DecisionSchema);
            var systemInstruction = string.Join("\n", messages.Where(message => message.Role == "system").Select(message => message.Content));
            var contents = messages.Where(message => message.Role != "system").Select(message => new
            {
                role = message.Role == "assistant" ? "model" : "user",
                parts = new[] { new { text = message.Content } }
            });
            using var response = await http.PostAsJsonAsync($"models/{Uri.EscapeDataString(options.Value.Model)}:generateContent", new
            {
                systemInstruction = new { parts = new[] { new { text = systemInstruction } } },
                contents,
                generationConfig = new
                {
                    temperature = 0,
                    maxOutputTokens = 512,
                    responseMimeType = "application/json",
                    responseJsonSchema = schema.RootElement
                }
            }, timeout.Token);
            response.EnsureSuccessStatusCode();
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var content = payload.RootElement.TryGetProperty("candidates", out var candidates) && candidates.ValueKind == JsonValueKind.Array &&
                          candidates.GetArrayLength() > 0 && candidates[0].TryGetProperty("content", out var contentElement) &&
                          contentElement.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array && parts.GetArrayLength() > 0 &&
                          parts[0].TryGetProperty("text", out var text) ? text.GetString() : null;
            if (content is not { Length: > 0 and <= 8000 })
                throw new JsonException("Incomplete model response.");
            return JsonSerializer.Deserialize<AppointmentAgentDecision>(content, Json)
                ?? throw new JsonException("Empty model decision.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or NotSupportedException)
        {
            throw new AppointmentModelException("The appointment assistant is unavailable. Please try again or use regular appointment booking.", exception);
        }
    }
}
