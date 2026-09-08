using HospitalManagementSystem.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HospitalManagementSystem.Api.Services;

public class AppointmentCompletionService(IServiceScopeFactory scopes, ILogger<AppointmentCompletionService> logger) : BackgroundService
{
    public static async Task CompleteDueAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var due = db.Appointments.Where(a => a.Status == "Confirmed" && a.DoctorTimeSlot != null && a.DoctorTimeSlot.EndAt <= now);
        if (db.Database.IsRelational())
        {
            // Conditional database update avoids overwriting a concurrent cancellation or reschedule.
            await due.ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Status, "Completed")
                .SetProperty(a => a.UpdatedAt, now), cancellationToken);
        }
        else
        {
            foreach (var appointment in await due.ToListAsync(cancellationToken))
            {
                appointment.Status = "Completed";
                appointment.UpdatedAt = now;
            }
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await CompleteDueAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Unable to complete expired appointments; will retry."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
