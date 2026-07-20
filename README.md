
# Short-term Apartment API

This repository contains the backend API for the short-term apartment booking platform. It provides REST endpoints for apartments, bookings, payments, identity verification, reporting, and admin workflows.

## Overview

The backend is built with ASP.NET Core and follows a layered architecture:

- API layer: controllers, middleware, and Swagger configuration
- BLL layer: business logic, services, AutoMapper profiles, and dependency injection
- DAL layer: EF Core DbContext, repositories, models, and database access
- Common layer: shared DTOs, enums, settings, and utilities

## Tech Stack

- .NET 9
- ASP.NET Core Web API
- Entity Framework Core
- MySQL
- Redis
- JWT authentication
- Swagger / OpenAPI

## Project Structure

- Short-termApartmentAPI/ - API host project
- BLL/ - business logic services and mappings
- DAL/ - EF Core models, repositories, and data access
- Common/ - shared abstractions and utilities
- tests/ - unit and integration tests
- sql/ - SQL scripts and migration helpers

## Prerequisites

Make sure the following are installed:

- .NET SDK 9
- MySQL
- Redis (if required by your local environment)

## Getting Started

### Restore and build

```bash
dotnet restore
dotnet build Short-termApartmentAPI.sln
```

### Run the API

```bash
dotnet run --project Short-termApartmentAPI/Short-termApartmentAPI.csproj
```

The API will start locally and can be explored through Swagger.

## Database

This project uses EF Core with MySQL. Common development commands:

```bash
dotnet ef migrations add <MigrationName> --project DAL --startup-project Short-termApartmentAPI
dotnet ef database update --project DAL --startup-project Short-termApartmentAPI
```

## Testing

Run all tests:

```bash
dotnet test Short-termApartmentAPI.sln
```

Run targeted test projects:

```bash
dotnet test tests/BLL.Tests/BLL.Tests.csproj
dotnet test tests/DAL.Tests/DAL.Tests.csproj
dotnet test tests/Short-termApartmentAPI.IntegrationTests/Short-termApartmentAPI.IntegrationTests.csproj
```

## Key Features

- Apartment and booking management
- Payment processing integration
- Identity verification workflows
- Admin and landlord operations
- Background workers for automation and cleanup
- Reporting and analytics support

## Notes

- The project uses a soft-delete convention for applicable entities.
- Timestamps are handled in Vietnam time.
- API responses follow a consistent wrapper format handled by middleware.

## License

This project is licensed under the terms described in the LICENSE file.
