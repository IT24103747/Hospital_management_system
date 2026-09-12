using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HospitalManagementSystem.Api.AgenticAI.AppointmentScheduling;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using Microsoft.Extensions.Options;
using Xunit;

namespace HospitalManagementSystem.Api.Tests.AgenticAI;

public class GeminiAppointmentClientTests
{
    [Fact]
    public async Task Client_UsesGeminiStructuredOutputAndConfiguredModel()
    {
        using var handler = new StubHandler(async request =>
        {
            Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent", request.RequestUri!.ToString());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("application/json", body.RootElement.GetProperty("generationConfig").GetProperty("responseMimeType").GetString());
            Assert.Equal("object", body.RootElement.GetProperty("generationConfig").GetProperty("responseJsonSchema").GetProperty("type").GetString());
            return Reply("{\"action\":\"find_doctors\",\"query\":\"Cardiology\"}");
        });
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/") };
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
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/") };
        await Assert.ThrowsAsync<AppointmentModelException>(() => Client(http).DecideAsync([], default));
    }

    [Fact]
    public async Task ModelHttpFailure_IsTranslatedToAvailabilityError()
    {
        using var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/") };
        await Assert.ThrowsAsync<AppointmentModelException>(() => Client(http).DecideAsync([], default));
    }

    private static GeminiAppointmentClient Client(HttpClient http) => new(http, Options.Create(new AppointmentAgentOptions()));
    private static HttpResponseMessage Reply(string content) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new
        {
            candidates = new[] { new { content = new { parts = new[] { new { text = content } } } } }
        })
    };
    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
