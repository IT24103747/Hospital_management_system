using Microsoft.Extensions.Options;
using HospitalManagementSystem.Api.AgenticAI.PatientCare.AppointmentProposal;

namespace HospitalManagementSystem.Api.AgenticAI.AppointmentScheduling;

public static class AppointmentAgentRegistration
{
    public static IServiceCollection AddAppointmentAgent(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AppointmentAgentOptions>().Bind(configuration.GetSection(AppointmentAgentOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Model), "A Gemini model must be configured.")
            .Validate(o => o.TimeoutSeconds is >= 5 and <= 120 && o.MaxSteps is >= 1 and <= 12, "Invalid appointment agent limits.")
            .ValidateOnStart();
        services.AddHttpClient<IAppointmentModelClient, GeminiAppointmentClient>((provider, client) =>
        {
            var agentOptions = provider.GetRequiredService<IOptions<AppointmentAgentOptions>>().Value;
            var apiKey = string.IsNullOrWhiteSpace(agentOptions.GeminiApiKey)
                ? configuration["Gemini:ApiKey"] : agentOptions.GeminiApiKey;
            client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/");
            if (!string.IsNullOrWhiteSpace(apiKey)) client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
            client.Timeout = Timeout.InfiniteTimeSpan; // Per-call and overall cancellation are handled by the agent.
            client.MaxResponseContentBufferSize = 64 * 1024;
        });
        services.AddScoped<IAppointmentAgentTools, AppointmentAgentTools>();
        services.AddScoped<IAppointmentSchedulingAgent, AppointmentSchedulingAgent>();
        return services;
    }
}
