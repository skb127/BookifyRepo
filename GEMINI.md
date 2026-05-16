# GEMINI.md — Bookify Project Guide

> This file provides context for Gemini (and other AI coding assistants) to understand the project structure, architecture, conventions, and common workflows. Read this before making changes.

---

## Project Overview

**Bookify** is a production-grade apartment booking REST API built with **.NET 9** and **C#**. It follows **Clean Architecture** with a strong focus on domain-driven design (DDD), CQRS via MediatR, and solid infrastructure patterns.

The solution exposes versioned REST endpoints (via URL segments), uses **Keycloak** for identity & OAuth2/JWT authentication, **PostgreSQL** as the primary database, **Redis** for caching, and **Seq** for structured log aggregation.

---

## Solution Structure

```
BookifyRepo/
├── src/
│   ├── Bookify.Api              # ASP.NET Core Web API (entry point)
│   ├── Bookify.Application      # Use cases, commands, queries, abstractions
│   ├── Bookify.Domain           # Domain entities, value objects, domain events
│   └── Bookify.Infrastructure   # EF Core, Keycloak, Redis, SMTP, Quartz, Polly
├── test/
│   ├── Bookify.Domain.UnitTests
│   ├── Bookify.Application.UnitTests
│   ├── Bookify.Application.IntegrationTests  # Testcontainers-based
│   ├── Bookify.Api.FunctionalTests
│   └── Bookify.ArchitectureTests             # NetArchTest layer enforcement
├── .github/workflows/
│   ├── ci.yml                   # Runs on PRs from dev → main
│   └── gate-from-dev.yml
├── docker-compose.yml           # Local full-stack dev environment
└── Directory.Packages.props     # Central NuGet version management (CPM)
```

### Layer Dependency Rule
```
Api → Application → Domain
Infrastructure → Application + Domain
```
Infrastructure implements the abstractions defined in Application. The Domain has **zero** external dependencies.

---

## Domain Model

| Aggregate | Key Concepts |
|-----------|-------------|
| `Apartment` | Amenities, Address (value object), Money (value object), multi-currency pricing |
| `Booking`   | State machine (Reserved → Confirmed → Completed / Cancelled / Rejected), DateRange value object, PricingDetails |
| `Review`    | Rating (1–5), belongs to a completed Booking |
| `User`      | Roles (Guest/Admin), permission-based authorization via Keycloak claims |

Domain events are raised inside aggregates and persisted via the **Outbox pattern** using Quartz jobs.

---

## Application Layer (CQRS)

- Commands and Queries are handled via **MediatR**.
- All commands/queries go through a pipeline of **Behaviors** (in `Application/Abstractions/Behaviors/`):
  - Validation (FluentValidation)
  - Logging
  - Caching (query responses via Redis)
- Abstractions are defined as interfaces (`ICommand<T>`, `IQuery<T>`, `ICacheService`, etc.) and implemented in Infrastructure.

---

## Infrastructure Layer

### Key Services

| Service | Implementation |
|---------|---------------|
| Auth/JWT | `JwtBearerOptionsSetup`, validated against Keycloak OIDC metadata |
| Identity | `KeycloakIdentityProvider` — creates/manages users via Keycloak Admin API |
| Persistence | `ApplicationDbContext` (EF Core 9 + Npgsql, snake_case naming) + Dapper for read-side |
| Caching | `CacheService` wrapping `IDistributedCache` (Redis via StackExchange) |
| Email | `SmtpEmailService` (MailKit) + `ScribanTemplateService` for templating |
| Background Jobs | Quartz.NET — Outbox processor, CompleteBookings batch, NotifyCompletedBookings |
| Resilience | Polly via `Microsoft.Extensions.Resilience` |
| Bot Protection | Cloudflare Turnstile (`TurnstileService`) |
| Rate Limiting | ASP.NET Core native `RateLimiter` |

### Dependency Injection

All registrations live in `Bookify.Infrastructure/DependencyInjection.cs` via a single extension method `AddInfrastructure(IConfiguration)`. Each concern has a dedicated private `Add*` method:

```csharp
AddEmail / AddPersistence / AddAuthentication / AddIdentity / AddAuthorization
AddCaching / AddHealthChecks / AddApiVersioning / AddBackgroundJobs
AddTurnstile / AddOptions / AddRateLimiting
```

---

## Rate Limiting

Three named policies (all implemented as `IRateLimiterPolicy<string>`):

| Policy | Algorithm | Limit | Scope |
|--------|-----------|-------|-------|
| `write-operations` | Fixed Window | 15 req/min | POST/PUT/DELETE endpoints |
| `search` | Sliding Window | 30 req/min | Apartment search endpoints |
| `health-checks` | Fixed Window | 5 req/min | `/health` endpoint |

A **global sliding-window limiter** (80 req/min, 2 segments) applies to all routes, keyed by user identity ID (authenticated) or IP address (anonymous).

`OnRejected` returns HTTP **429** with a `ProblemDetails` JSON body.

The middleware order in `Program.cs` is:
```
HTTPS → RequestContextLogging → Serilog → ExceptionHandler → StatusCodePages
→ Authentication → RateLimiter → Authorization → Controllers
```

---

## Resilience (Polly)

| Pipeline | Strategy | Config |
|----------|----------|--------|
| `email-notification-retry` | Retry (exponential) | 3 attempts, 1s/2s/4s delays |
| `turnstile-pipeline` | Circuit Breaker + Retry | CB: 50% failure rate, 5 min throughput; Retry: 2 attempts, 1s/2s |

---

## API Versioning

URL-segment versioning: `/api/v1/...`, `/api/v2/...`

Versions are declared in `ApiVersions.cs`. Swagger groups endpoints by version automatically. New versions should add a constant there and attribute controllers accordingly.

---

## Health Checks

Endpoint: `GET /health` — returns JSON (HealthChecksUI format).

Checks:
- PostgreSQL connectivity
- Redis connectivity
- Keycloak base URL reachability

Protected by the `health-checks` rate limiter policy.

---

## Configuration (appsettings)

Key sections in `appsettings.Development.json`:

| Section | Purpose |
|---------|---------|
| `ConnectionStrings.Database` | PostgreSQL connection string |
| `ConnectionStrings.Cache` | Redis connection string |
| `Authentication` | JWT audience, issuer, OIDC metadata URL |
| `Keycloak` | Admin/Auth client credentials, realm, URLs |
| `Outbox` | Interval and batch size for the outbox processor job |
| `CompleteBookings` | Cron expression for the batch completion job |
| `NotifyCompletedBookings` | Cron expression + batch size for notification job |
| `Turnstile` | BaseUrl, SiteKey, SecretKey (Cloudflare Turnstile) |
| `BookifyApp` | FrontendUrl (CORS) |
| `Email` | SMTP credentials |
| `Expiration` | Token expiry in seconds for email change and password recovery |

---

## Local Development

### Prerequisites

- .NET 9 SDK
- Docker & Docker Compose

### Start infrastructure

```bash
docker-compose up -d
```

This starts: PostgreSQL, Redis, Keycloak (with realm import), Seq, and an HTTPS cert generator.

| Service | URL |
|---------|-----|
| API | https://localhost:7274 |
| Swagger | https://localhost:7274/swagger |
| Keycloak Admin | http://localhost:18080 (admin/admin) |
| Seq Logs | http://localhost:8081 |
| Redis | localhost:6379 |
| PostgreSQL | localhost:5432 |

### Run the API

```bash
dotnet run --project src/Bookify.Api
```

Migrations are applied automatically on startup in Development mode (`app.ApplyMigrations()`).

### Sensitive Configuration — User Secrets

`Bookify.Api` has User Secrets enabled (`UserSecretsId` is set in `.csproj`). In development, **never** put sensitive values directly in `appsettings.Development.json`. Use the .NET User Secrets store instead:

```bash
# Set a secret (key mirrors the appsettings path, using : as separator)
dotnet user-secrets set "Keycloak:AdminClientSecret" "<value>" --project src/Bookify.Api
dotnet user-secrets set "Keycloak:AuthClientSecret" "<value>"  --project src/Bookify.Api
dotnet user-secrets set "Turnstile:SecretKey" "<value>"        --project src/Bookify.Api
dotnet user-secrets set "Email:Password" "<value>"             --project src/Bookify.Api

# List all stored secrets
dotnet user-secrets list --project src/Bookify.Api
```

Secrets are stored at `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json` and are **never committed to source control**.

Values that should live in User Secrets (not in `appsettings`):

| Key | Description |
|-----|-------------|
| `Keycloak:AdminClientSecret` | Keycloak admin client credential |
| `Keycloak:AuthClientSecret` | Keycloak auth client credential |
| `Turnstile:SecretKey` | Cloudflare Turnstile secret key |
| `Turnstile:SiteKey` | Cloudflare Turnstile site key |
| `Email:Password` | SMTP account password |
| `ConnectionStrings:Database` | If using a remote/non-default DB |
| `ConnectionStrings:Cache` | If using a remote/non-default Redis |

---

## Running Tests

```bash
# All tests
dotnet test Bookify.sln

# Specific project
dotnet test test/Bookify.Application.IntegrationTests

# With detailed output
dotnet test Bookify.sln --logger "console;verbosity=detailed"
```

**Integration tests** use **Testcontainers** to spin up PostgreSQL, Redis, and Keycloak automatically — no manual infrastructure setup needed.

**Architecture tests** (`Bookify.ArchitectureTests`) use `NetArchTest.Rules` to enforce layer dependency rules.

---

## Build & CI

```bash
dotnet restore Bookify.sln
dotnet build Bookify.sln --configuration Release
```

CI runs on GitHub Actions (`ci.yml`):
- Triggers on non-draft PRs from `dev` → `main`
- Steps: checkout → setup .NET 9 → NuGet cache → restore → build Release

> ⚠️ `TreatWarningsAsErrors=true` is set globally in `Directory.Build.props`. All warnings must be resolved before building.

---

## Code Conventions

- **Nullable reference types** enabled globally (`<Nullable>enable</Nullable>`)
- **Implicit usings** enabled
- **SonarAnalyzer.CSharp** runs on every build via `Directory.Build.props`
- `.editorconfig` enforces code style (tabs, naming rules, etc.)
- **Central Package Management** via `Directory.Packages.props` — do not specify versions in individual `.csproj` files
- All domain errors are expressed via the **Result pattern** (`Result<T>`) — never throw exceptions for domain failures
- Repository pattern for persistence abstraction
- `internal sealed` modifier preferred for infrastructure classes
- Outbox pattern for reliable domain event delivery (never dispatch events directly from handlers)

---

## Adding New Features — Checklist

- [ ] Define domain entity/value object/error in `Bookify.Domain`
- [ ] Add repository interface in Domain, implement in Infrastructure
- [ ] Add EF Core configuration in `Configurations/`
- [ ] Create a migration: `dotnet ef migrations add <Name> -p src/Bookify.Infrastructure -s src/Bookify.Api`
- [ ] Add command/query + handler in `Bookify.Application`
- [ ] Add FluentValidation validator for commands
- [ ] Add controller endpoint in `Bookify.Api/Controllers`
- [ ] Apply `[EnableRateLimiting("...")]` attribute on controller actions if applicable (`[RequireRateLimiting]` is for Minimal API endpoints)
- [ ] Register any new services in the appropriate `Add*` method in `DependencyInjection.cs`
- [ ] Add integration tests using the existing `IntegrationTestWebAppFactory`
