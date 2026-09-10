using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using HospitalManagementSystem.Api.AgenticAI.AppointmentScheduling;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;
using HospitalManagementSystem.Api.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace HospitalManagementSystem.Api.Tests.AgenticAI;

public class AppointmentAgentApiTests
{
    [Fact]
    public async Task Endpoint_RequiresAuthentication_AndRejectsCrossPatientRequests()
    {
        var spy = new CapturingAgent();
        await using var root = new AppointmentApiFactory();
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAppointmentSchedulingAgent>();
            services.AddSingleton<IAppointmentSchedulingAgent>(spy);
        }));
        using var client = factory.CreateClient();
        var request = new AppointmentAgentRequest { Message = "Find a doctor tomorrow" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/appointment-agent", request)).StatusCode);
        await Login(client, "amal.perera@email.com", "Patient123!");
        request.PatientId = int.MaxValue;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/appointment-agent", request)).StatusCode);
        Assert.Null(spy.Patient);
        request.PatientId = null;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/appointment-agent", request)).StatusCode);
        Assert.Equal("amal.perera@email.com", spy.Patient!.Email);
        Assert.True(spy.Patient.PatientId > 0);
    }

    [Fact]
    public async Task AdminBooking_RequiresAnExistingSelectedPatient_AndInputIsValidated()
    {
        await using var factory = new AppointmentApiFactory();
        using var client = factory.CreateClient();
        await Login(client, "admin@medicore.lk", "Admin1234");
        var request = new AppointmentAgentRequest { Message = "Book an appointment", AllowBooking = true };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/appointment-agent", request)).StatusCode);
        request.PatientId = int.MaxValue;
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/appointment-agent", request)).StatusCode);
        request.Message = new string('a', 2001);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/appointment-agent", request)).StatusCode);
    }

    [Fact]
    public async Task UnavailableModel_Returns503WithoutInternalErrorDetails()
    {
        await using var root = new AppointmentApiFactory();
        await using var factory = root.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAppointmentSchedulingAgent>();
            services.AddSingleton<IAppointmentSchedulingAgent>(new UnavailableAgent());
        }));
        using var client = factory.CreateClient();
        await Login(client, "amal.perera@email.com", "Patient123!");
        var response = await client.PostAsJsonAsync("/api/appointment-agent", new AppointmentAgentRequest { Message = "Find an appointment" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("private error", await response.Content.ReadAsStringAsync());
    }

    private static async Task Login(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginReply>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
    }
    private sealed record LoginReply(string Token);
    private sealed class CapturingAgent : IAppointmentSchedulingAgent
    {
        public PatientDto? Patient { get; private set; }
        public Task<AppointmentAgentResponse> RunAsync(AppointmentAgentRequest request, PatientDto? patient, CancellationToken cancellationToken)
        {
            Patient = patient;
            return Task.FromResult(new AppointmentAgentResponse("NeedsDetails", "Choose a doctor.", [], []));
        }
    }
    private sealed class UnavailableAgent : IAppointmentSchedulingAgent
    {
        public Task<AppointmentAgentResponse> RunAsync(AppointmentAgentRequest request, PatientDto? patient, CancellationToken cancellationToken) =>
            throw new AppointmentModelException("private error");
    }
}
