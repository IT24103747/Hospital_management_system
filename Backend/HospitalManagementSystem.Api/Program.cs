using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Repositories;
using HospitalManagementSystem.Api.Services;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ---------- Services ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Hospital Management System API",
        Version = "v1",
        Description = "SE3090 Assignment 1 – Patient Management API"
    });
});

// PostgreSQL + EF Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Dependency Injection
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();

// CORS – allow React and Flutter (dev)
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await SeedSampleDataAsync(db);
}

// ---------- Middleware ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("DevCors");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }));

app.Run();

static async Task SeedSampleDataAsync(ApplicationDbContext db)
{
    if (!await db.Patients.AnyAsync())
    {
        db.Patients.AddRange(
            new HospitalManagementSystem.Api.Models.Patient
            {
                PatientId = 1,
                FirstName = "Amal",
                LastName = "Perera",
                DateOfBirth = new DateTime(1985, 3, 14, 0, 0, 0, DateTimeKind.Utc),
                Gender = "Male",
                NIC = "850314123V",
                PhoneNumber = "+94 77 234 5678",
                Email = "amal.perera@email.com",
                Address = "45 Galle Rd, Colombo 03",
                BloodGroup = "B+",
                EmergencyContactName = "Kamala Perera",
                EmergencyContactPhone = "+94 71 234 5678",
                CreatedAt = new DateTime(2024, 1, 10, 8, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 10, 8, 0, 0, DateTimeKind.Utc)
            },
            new HospitalManagementSystem.Api.Models.Patient
            {
                PatientId = 2,
                FirstName = "Nimesha",
                LastName = "Silva",
                DateOfBirth = new DateTime(1992, 7, 22, 0, 0, 0, DateTimeKind.Utc),
                Gender = "Female",
                NIC = "920722234V",
                PhoneNumber = "+94 76 345 6789",
                Email = "nimesha.silva@email.com",
                Address = "12 Kandy Rd, Peradeniya",
                BloodGroup = "O+",
                EmergencyContactName = "Ruwan Silva",
                EmergencyContactPhone = "+94 70 345 6789",
                CreatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc)
            }
        );
    }

    if (!await db.DoctorTimeSlots.AnyAsync())
    {
        db.DoctorTimeSlots.AddRange(
            new HospitalManagementSystem.Api.Models.DoctorTimeSlot
            {
                DoctorTimeSlotId = 1,
                DoctorName = "Dr. Priyantha Jayawardena",
                Specialty = "General Medicine",
                StartAt = new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc),
                EndAt = new DateTime(2026, 8, 8, 11, 0, 0, DateTimeKind.Utc),
                Capacity = 4,
                IsActive = true,
                CreatedAt = new DateTime(2026, 8, 8, 4, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 8, 8, 4, 0, 0, DateTimeKind.Utc)
            },
            new HospitalManagementSystem.Api.Models.DoctorTimeSlot
            {
                DoctorTimeSlotId = 2,
                DoctorName = "Dr. Chamari Gunaratne",
                Specialty = "Cardiology",
                StartAt = new DateTime(2026, 8, 8, 14, 0, 0, DateTimeKind.Utc),
                EndAt = new DateTime(2026, 8, 8, 16, 0, 0, DateTimeKind.Utc),
                Capacity = 3,
                IsActive = true,
                CreatedAt = new DateTime(2026, 8, 8, 4, 15, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 8, 8, 4, 15, 0, DateTimeKind.Utc)
            }
        );
    }

    if (!await db.Appointments.AnyAsync())
    {
        db.Appointments.AddRange(
            new HospitalManagementSystem.Api.Models.Appointment
            {
                AppointmentId = 1,
                DoctorTimeSlotId = 1,
                PatientId = 1,
                AppointmentNumber = 1,
                EstimatedStartAt = new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc),
                PatientName = "Amal Perera",
                PatientPhone = "+94 77 234 5678",
                PatientEmail = "amal.perera@email.com",
                AppointmentType = "Consultation",
                Reason = "Fever and cough",
                Status = "Confirmed",
                CreatedAt = new DateTime(2026, 8, 8, 4, 30, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 8, 8, 4, 30, 0, DateTimeKind.Utc)
            },
            new HospitalManagementSystem.Api.Models.Appointment
            {
                AppointmentId = 2,
                DoctorTimeSlotId = 2,
                PatientId = 2,
                AppointmentNumber = 1,
                EstimatedStartAt = new DateTime(2026, 8, 8, 14, 0, 0, DateTimeKind.Utc),
                PatientName = "Nimesha Silva",
                PatientPhone = "+94 76 345 6789",
                PatientEmail = "nimesha.silva@email.com",
                AppointmentType = "Follow-up",
                Reason = "Review ECG results",
                Status = "Requested",
                CreatedAt = new DateTime(2026, 8, 8, 5, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 8, 8, 5, 0, 0, DateTimeKind.Utc)
            }
        );
    }

    await db.SaveChangesAsync();
}
