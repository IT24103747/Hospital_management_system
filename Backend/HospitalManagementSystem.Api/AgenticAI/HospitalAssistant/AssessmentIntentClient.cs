using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagementSystem.Api.DTOs;

namespace HospitalManagementSystem.Api.AgenticAI.HospitalAssistant;

public sealed record AssessmentIntent(string Intent, bool IsAnswer, string? NormalizedAnswer, string? Response);

public interface IAssessmentIntentClient
{
    Task<AssessmentIntent?> InterpretAsync(string message, TriageFollowUpQuestionDto question,
        TriageGuidanceDto guidance, IReadOnlyList<TriageAnswerDto> answers, string? previousReply, CancellationToken token);
}

public sealed class GeminiAssessmentIntentClient(HttpClient http, IConfiguration configuration,
    ILogger<GeminiAssessmentIntentClient> logger) : IAssessmentIntentClient
{
    private static readonly HashSet<string> Intents = ["ANSWER", "QUESTION_HELP", "CLARIFICATION", "GENERAL_QUERY", "UNCLEAR"];
    private const string DecisionSchema = """
    {"type":"object","additionalProperties":false,"required":["intent","isAnswer","normalizedAnswer","response"],"properties":{"intent":{"type":"string","enum":["ANSWER","QUESTION_HELP","CLARIFICATION","GENERAL_QUERY","UNCLEAR"]},"isAnswer":{"type":"boolean"},"normalizedAnswer":{"type":["string","null"]},"response":{"type":["string","null"]}}}
    """;
    public async Task<AssessmentIntent?> InterpretAsync(string message, TriageFollowUpQuestionDto question,
        TriageGuidanceDto guidance, IReadOnlyList<TriageAnswerDto> answers, string? previousReply, CancellationToken token)
    {
        var context = new { message, currentQuestion = question, guidance = new {
            guidance.Heading, guidance.Summary, guidance.Actions, guidance.SeekHelpIf, guidance.EvidenceSource
        }, previousAnswers = answers, previousReply };
        var instruction = "Classify the patient's latest message relative to the CURRENT question. ANSWER if it supplies the requested information or explicitly declines, does not know, or says the question does not apply. Preserve unavailable responses verbatim as shortText; never invent a clinical value for them. For ANSWER, normalizedAnswer must satisfy the question type: use an exact option for singleChoice, comma-separated exact options for multipleChoice, Yes or No for yesNo, a plain number for number/severityScale, or patient-supplied text for shortText. Normalize only information the patient actually supplied. For help, clarification or general queries, respond briefly using only the supplied question and approved guidance. Do not invent patient facts, diagnosis, treatment, appointments or hospital information. If context cannot support an answer, say so. Return only JSON with intent, isAnswer, normalizedAnswer and response. ANSWER has a non-null normalizedAnswer and null response; other intents have null normalizedAnswer and a non-null response.";
        instruction += " Interpret meaning, not literal wording: paraphrases of no change map to the stable/unchanged option, and natural-language quantities map to numbers when unambiguous. A useful PARTIAL response to a compound shortText question is ANSWER even if other requested fields remain missing. Preserve the supplied portion and never fill missing fields. An unavailable response expressed in different words should normalize to Prefer not to answer, I don't know, or Not applicable. Do not require a patient to repeat an exact phrase.";
        var geminiKey = configuration["Gemini:ApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (string.IsNullOrWhiteSpace(geminiKey))
        {
            logger.LogWarning("Gemini assessment intent unavailable: Gemini:ApiKey is not configured");
            return null;
        }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue<int?>("Gemini:TimeoutSeconds") ?? 30, 5, 90)));
            using var schema = JsonDocument.Parse(DecisionSchema);
            using var request = new HttpRequestMessage(HttpMethod.Post,
                $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(configuration["Gemini:AssessmentModel"] ?? "gemini-3.1-flash-lite")}:generateContent");
            request.Headers.Add("x-goog-api-key", geminiKey);
            request.Content = JsonContent.Create(new {
                    systemInstruction = new { parts = new[] { new { text = instruction } } },
                    contents = new[] { new { role = "user", parts = new[] { new { text = JsonSerializer.Serialize(context) } } } },
                    generationConfig = new { temperature = 0, maxOutputTokens = 1024,
                        responseMimeType = "application/json", responseJsonSchema = schema.RootElement }
                });
            using var response = await http.SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();
            using var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            var raw = envelope.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString();
            var decision = ParseDecision(raw);
            if (decision == null) logger.LogWarning("Gemini assessment intent returned an invalid decision");
            return decision;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Gemini assessment intent unavailable or returned invalid JSON");
            return null;
        }
    }

    private static AssessmentIntent? ParseDecision(string? raw)
    {
        if (raw is not { Length: > 0 and <= 4000 }) return null;
        using var parsed = JsonDocument.Parse(raw);
        var root = parsed.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 4) return null;
        var intent = root.GetProperty("intent").GetString();
        var isAnswer = root.GetProperty("isAnswer").GetBoolean();
        var normalized = root.GetProperty("normalizedAnswer").ValueKind == JsonValueKind.Null ? null : root.GetProperty("normalizedAnswer").GetString();
        var reply = root.GetProperty("response").ValueKind == JsonValueKind.Null ? null : root.GetProperty("response").GetString();
        if (intent == null || !Intents.Contains(intent) || isAnswer != (intent == "ANSWER") ||
            (isAnswer && (string.IsNullOrWhiteSpace(normalized) || reply != null)) ||
            (!isAnswer && (normalized != null || string.IsNullOrWhiteSpace(reply))) ||
            normalized?.Length > 500 || reply?.Length > 1000) return null;
        return new(intent, isAnswer, normalized, reply);
    }
}
