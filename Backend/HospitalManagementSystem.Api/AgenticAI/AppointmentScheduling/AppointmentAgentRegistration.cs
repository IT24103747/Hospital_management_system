using Microsoft.Extensions.Options;

namespace HospitalManagementSystem.Api.AgenticAI.AppointmentScheduling;

public static class AppointmentAgentRegistration
{
    public static IServiceCollection AddAppointmentAgent(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AppointmentAgentOptions>().Bind(configuration.GetSection(AppointmentAgentOptions.SectionName))
            .Validate(o => Uri.TryCreate(o.OllamaUrl, UriKind.Absolute, out var uri) &&
                uri.Scheme is "http" or "https" && o.OllamaUrl.EndsWith('/'), "OllamaUrl must be an HTTP(S) URL ending in /.")
            .Validate(o => o.Model == "qwen2.5:3b", "The appointment agent uses qwen2.5:3b.")
            .Validate(o => o.TimeoutSeconds is >= 5 and <= 120 && o.MaxSteps is >= 1 and <= 12, "Invalid appointment agent limits.")
            .ValidateOnStart();
        services.AddHttpClient<IOllamaAppointmentClient, OllamaAppointmentClient>((provider, client) =>
        {
            client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<AppointmentAgentOptions>>().Value.OllamaUrl);
            client.Timeout = Timeout.InfiniteTimeSpan; // Per-call and overall cancellation are handled by the agent.
            client.MaxResponseContentBufferSize = 64 * 1024;
        });
        services.AddScoped<IAppointmentAgentTools, AppointmentAgentTools>();
        services.AddScoped<IAppointmentSchedulingAgent, AppointmentSchedulingAgent>();
        return services;
    }
}
