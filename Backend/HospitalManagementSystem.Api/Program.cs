using Microsoft.EntityFrameworkCore;
using HospitalManagementSystem.Api.Data;
using HospitalManagementSystem.Api.Repositories;
using HospitalManagementSystem.Api.Services;
using HospitalManagementSystem.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Security.Cryptography;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    jwtSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    builder.Configuration["Jwt:Secret"] = jwtSecret;
}
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ---------- Database Connection Check ----------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");


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
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "Enter the JWT received from the login endpoint."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "MediCore.Api",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "MediCore.Client",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });
builder.Services.AddAuthorization();

// PostgreSQL + EF Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Dependency Injection
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IAppointmentService, AppointmentService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IDoctorService, DoctorService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IDoctorScheduleService, DoctorScheduleService>();
builder.Services.AddScoped<ITriageWorkflowService, TriageWorkflowService>();
builder.Services.AddHttpClient<IClinicalInformationExtractionAgent, OllamaClinicalInformationExtractionAgent>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["SafeTriage:OllamaUrl"] ?? "http://127.0.0.1:11434/");
});

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

    if (app.Environment.IsEnvironment("Testing"))
        await db.Database.EnsureCreatedAsync();
    else
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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow
}));

app.Run();

static async Task SeedSampleDataAsync(ApplicationDbContext db)
{
    const string adminEmail = "admin@medicore.lk";
    if (!await db.Users.AnyAsync(user => user.Email == adminEmail))
    {
        var passwordHasher = new PasswordHasher<User>();
        var admin = new User { FullName = "System Administrator", Email = adminEmail, Role = "Admin" };
        admin.PasswordHash = passwordHasher.HashPassword(admin, "Admin1234");
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }

    if (!await db.Users.AnyAsync(user => user.Email == "amal.perera@email.com"))
    {
        var passwordHasher = new PasswordHasher<User>();
        var user1 = new User { FullName = "Amal Perera", Email = "amal.perera@email.com", Role = "Patient" };
        user1.PasswordHash = passwordHasher.HashPassword(user1, "Patient123!");
        db.Users.Add(user1);
        await db.SaveChangesAsync();
    }

    if (!await db.Users.AnyAsync(user => user.Email == "nimesha.silva@email.com"))
    {
        var passwordHasher = new PasswordHasher<User>();
        var user = new User { FullName = "Nimesha Silva", Email = "nimesha.silva@email.com", Role = "Patient" };
        user.PasswordHash = passwordHasher.HashPassword(user, "Patient123!");
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    if (!await db.Patients.AnyAsync())
    {
        db.Patients.AddRange(
            new HospitalManagementSystem.Api.Models.Patient
            {
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

        await db.SaveChangesAsync();
    }

    if (!await db.DoctorTimeSlots.AnyAsync())
    {
        db.DoctorTimeSlots.AddRange(
            new HospitalManagementSystem.Api.Models.DoctorTimeSlot
            {
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

        await db.SaveChangesAsync();
    }

    if (!await db.Appointments.AnyAsync())
    {
        var amalPatient = await db.Patients.SingleOrDefaultAsync(patient =>
            patient.Email == "amal.perera@email.com");
        var nimeshaPatient = await db.Patients.SingleOrDefaultAsync(patient =>
            patient.Email == "nimesha.silva@email.com");
        var generalMedicineSlot = await db.DoctorTimeSlots.SingleOrDefaultAsync(slot =>
            slot.DoctorName == "Dr. Priyantha Jayawardena" &&
            slot.StartAt == new DateTime(2026, 8, 8, 9, 0, 0, DateTimeKind.Utc));
        var cardiologySlot = await db.DoctorTimeSlots.SingleOrDefaultAsync(slot =>
            slot.DoctorName == "Dr. Chamari Gunaratne" &&
            slot.StartAt == new DateTime(2026, 8, 8, 14, 0, 0, DateTimeKind.Utc));

        if (amalPatient is null || nimeshaPatient is null ||
            generalMedicineSlot is null || cardiologySlot is null)
        {
            throw new InvalidOperationException(
                "Cannot seed sample appointments because their patients or doctor time slots are missing.");
        }

        db.Appointments.AddRange(
            new HospitalManagementSystem.Api.Models.Appointment
            {
                DoctorTimeSlotId = generalMedicineSlot.DoctorTimeSlotId,
                PatientId = amalPatient.PatientId,
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
                DoctorTimeSlotId = cardiologySlot.DoctorTimeSlotId,
                PatientId = nimeshaPatient.PatientId,
                AppointmentNumber = 1,
                EstimatedStartAt = new DateTime(2026, 8, 8, 14, 0, 0, DateTimeKind.Utc),
                PatientName = "Nimesha Silva",
                PatientPhone = "+94 76 345 6789",
                PatientEmail = "nimesha.silva@email.com",
                AppointmentType = "Follow-up",
                Reason = "Review ECG results",
                Status = "Confirmed",
                CreatedAt = new DateTime(2026, 8, 8, 5, 0, 0, DateTimeKind.Utc),
                UpdatedAt = new DateTime(2026, 8, 8, 5, 0, 0, DateTimeKind.Utc)
            }
        );
    }

    await db.SaveChangesAsync();
}

public partial class Program { }
