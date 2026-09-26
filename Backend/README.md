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

## Connect to Neon

The API already uses PostgreSQL through Npgsql. To create its Neon database:

1. Sign in to the [Neon console](https://console.neon.tech/) and create a project.
   Choose a region close to the API server. Neon creates a default database named
   `neondb`; you can use it or create another database in the project.
2. Open **Connect** on the project dashboard. Select the branch and database that
   this API should use, and turn **Connection pooling** off to get a direct host.
   The API applies EF Core migrations at startup, and Neon recommends a direct
   connection for migrations.
3. Copy the host, database name, role, and password into the secret command below.

Set the API's `ConnectionStrings:DefaultConnection` to an Npgsql connection string.
Use the host, database, role, and password shown by Neon; the example values below
are placeholders. Run this from the repository root, replacing the entire quoted
value with your own details:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=ep-YOUR-ENDPOINT.REGION.aws.neon.tech;Port=5432;Database=neondb;Username=YOUR_ROLE;Password=YOUR_PASSWORD;SSL Mode=Require" \
  --project Backend/HospitalManagementSystem.Api
```

The Neon dashboard commonly shows a `postgresql://...` URL. The API expects the
Npgsql key/value format above, so copy the individual values from that URL. If a
password contains `;`, use the Npgsql connection-string quoting rules or set the
value through a deployment secret manager. Do not commit credentials to Git.

For a deployed API, set the environment variable
`ConnectionStrings__DefaultConnection` to the same Npgsql string in the hosting
platform's secret settings. Also configure `Jwt__Secret` and
`Cors__AllowedOrigins__0` for that deployment. User secrets are for local
Development only.

Start the API with the command above and open `http://localhost:5000/health`.
Startup applies pending migrations to the Neon database; a successful response
confirms that startup completed. Development startup also inserts sample accounts,
so use a separate Neon development branch or database for local runs. To check the
schema, inspect the `__EFMigrationsHistory` table in the Neon SQL Editor.

## Projects Structure:
- `MediCore.API`: Controllers, JWT Auth, Middlewares, Swagger OpenAPI specs.
- `MediCore.Infrastructure`: EF Core DbContext, PostgreSQL Data Access, Migrations.
- `MediCore.Core`: Data Entities (User, Patient, Vitals, TriageWorkflowState), Interfaces, DTOs.
- `MediCore.Tests`: xUnit Unit and Integration test suite.
