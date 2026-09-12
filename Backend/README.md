# ASP.NET Core 8 REST API Backend

This directory contains the C# ASP.NET Core Web API solution, PostgreSQL database models (EF Core), and business logic services.

## Run locally

From the repository root:

```powershell
dotnet run --project Backend/HospitalManagementSystem.Api --launch-profile http
```

Open http://localhost:5000/swagger/index.html after the API reports that it is listening.
The checked-in launch profile sets the Development environment and port 5000.
Swagger is enabled only in Development; starting the executable directly or using
`--no-launch-profile` without setting the environment can result in a Swagger 404.
After changing launch settings, stop the existing API process and restart it with
the command above. PostgreSQL must be available for startup migrations and seeding.

## Projects Structure:
- `SmartCare.API`: Controllers, JWT Auth, Middlewares, Swagger OpenAPI specs.
- `SmartCare.Infrastructure`: EF Core DbContext, PostgreSQL Data Access, Migrations.
- `SmartCare.Core`: Data Entities (User, Patient, Vitals, TriageWorkflowState), Interfaces, DTOs.
- `SmartCare.Tests`: xUnit Unit and Integration test suite.
