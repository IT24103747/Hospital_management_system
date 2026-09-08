using System.Text.Json;
using Microsoft.Extensions.Options;

namespace HospitalManagementSystem.Api.AgenticAI.AppointmentScheduling;

public interface IOllamaAppointmentClient
{
    Task<AppointmentAgentDecision> DecideAsync(IReadOnlyList<AppointmentAgentMessage> messages, CancellationToken cancellationToken);
}

public sealed class OllamaAppointmentClient(HttpClient http, IOptions<AppointmentAgentOptions> options) : IOllamaAppointmentClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    // A small JSON tool protocol works with the existing local qwen2.5:3b model.
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
            using var response = await http.PostAsJsonAsync("api/chat", new
            {
                model = options.Value.Model, messages, stream = false, format = schema.RootElement,
                options = new { temperature = 0, num_predict = 512, num_ctx = 8192 }
            }, timeout.Token);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<OllamaReply>(cancellationToken: timeout.Token);
            if (payload?.Done != true || payload.Message?.Content is not { Length: > 0 and <= 8000 } content)
                throw new JsonException("Incomplete model response.");
            return JsonSerializer.Deserialize<AppointmentAgentDecision>(content, Json)
                ?? throw new JsonException("Empty model decision.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or JsonException or NotSupportedException)
        {
            throw new AppointmentModelException("The local appointment assistant is unavailable. Please try again or use regular appointment booking.", exception);
        }
    }

    private sealed class OllamaReply
    {
        public bool Done { get; set; }
        public AppointmentAgentMessage? Message { get; set; }
    }
}
