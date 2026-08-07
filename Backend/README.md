# ASP.NET Core 8 REST API Backend

This directory contains the C# ASP.NET Core Web API solution, PostgreSQL database models (EF Core), and business logic services.

## Projects Structure:
- `SmartCare.API`: Controllers, JWT Auth, Middlewares, Swagger OpenAPI specs.
- `SmartCare.Infrastructure`: EF Core DbContext, PostgreSQL Data Access, Migrations.
- `SmartCare.Core`: Data Entities (User, Patient, Vitals, TriageWorkflowState), Interfaces, DTOs.
- `SmartCare.Tests`: xUnit Unit and Integration test suite.
