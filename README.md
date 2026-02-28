# PaymentService

A payment processing service built with Clean Architecture, DDD, and the Result pattern.

## Tech Stack

- **.NET 10** — target framework
- **ASP.NET Core** — web API layer
- **Entity Framework Core** — data access
- **PostgreSQL** — database (via EF Core)
- **xUnit + FluentAssertions** — testing
- **Serilog** — structured logging _(planned)_
- **FluentValidation** — input validation _(planned)_

## Architecture

```
src/
├── PaymentService.Api/             # ASP.NET Core Web API (entry point)
├── PaymentService.Application/     # Application layer (use-cases, interfaces)
├── PaymentService.Domain/          # Domain layer (entities, value objects, Result pattern)
├── PaymentService.Infrastructure/  # Infrastructure layer (EF Core, repositories)
└── tests/
    ├── PaymentService.Domain.Tests/       # Domain unit tests
    └── PaymentService.Application.Tests/ # Application unit tests
```

## How to Build & Test

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
# Restore packages
dotnet restore PaymentService.slnx

# Build the solution
dotnet build PaymentService.slnx

# Run all tests
dotnet test PaymentService.slnx

# Run tests with coverage
dotnet test PaymentService.slnx --collect:"XPlat Code Coverage"
```

## Contributing

1. Keep domain logic free of infrastructure concerns.
2. Use the `Result<T>` type for operations that can fail; avoid throwing exceptions for expected failures.
3. Write tests for domain and application logic before implementation (TDD).
