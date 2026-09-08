using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagementSystem.Api.AgenticAI.AppointmentScheduling;
using Microsoft.Extensions.Options;
using Xunit;

namespace HospitalManagementSystem.Api.Tests.AgenticAI;

public class OllamaAppointmentClientTests
{
    [Fact]
    public async Task Client_UsesLocalChatSchemaAndConfiguredModel()
    {
        using var handler = new StubHandler(async request =>
        {
            Assert.Equal("http://127.0.0.1:11434/api/chat", request.RequestUri!.ToString());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("qwen2.5:3b", body.RootElement.GetProperty("model").GetString());
            Assert.False(body.RootElement.GetProperty("stream").GetBoolean());
            Assert.Equal("object", body.RootElement.GetProperty("format").GetProperty("type").GetString());
            return Reply("{\"action\":\"find_doctors\",\"query\":\"Cardiology\"}");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        var result = await Client(http).DecideAsync([new("user", "Find a cardiologist")], default);
        Assert.Equal("find_doctors", result.Action);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"action\":\"book_appointment\",\"patientId\":999}")]
    [InlineData("{\"action\":\"final\",\"message\":\"Booked appointment 999\"}")]
    [InlineData("null")]
    public async Task InvalidOrInventedModelFields_FailWithoutExecutingTools(string content)
    {
        using var handler = new StubHandler(_ => Task.FromResult(Reply(content)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        await Assert.ThrowsAsync<AppointmentModelException>(() => Client(http).DecideAsync([], default));
    }

    [Fact]
    public async Task ModelHttpFailure_IsTranslatedToAvailabilityError()
    {
        using var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1:11434/") };
        await Assert.ThrowsAsync<AppointmentModelException>(() => Client(http).DecideAsync([], default));
    }

    private static OllamaAppointmentClient Client(HttpClient http) => new(http, Options.Create(new AppointmentAgentOptions()));
    private static HttpResponseMessage Reply(string content) => new(HttpStatusCode.OK)
    { Content = JsonContent.Create(new { done = true, message = new { role = "assistant", content } }) };
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
