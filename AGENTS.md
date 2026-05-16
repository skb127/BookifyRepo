# AGENTS.md — AI Agent Instructions for Bookify

> This file defines behavioral rules, constraints, and workflows for AI coding agents (Gemini, Claude, Copilot, etc.) working on this repository. Read this **before** making any changes.

---

## 1. Understand the Architecture First

This is a **Clean Architecture** solution. Strictly enforce the dependency rule:

```
Domain  ←  Application  ←  Infrastructure
                              ↑
                            Api (entry point)
```

- **Domain** has zero dependencies on other layers or third-party packages.
- **Application** depends only on Domain and defines abstractions (interfaces) for infrastructure concerns.
- **Infrastructure** implements Application abstractions. It may depend on NuGet packages.
- **Api** wires everything together via `Program.cs` and `DependencyInjection.cs`.

> ❌ Never add Infrastructure or Api references into Domain or Application projects.

---

## 2. Before Writing Any Code

1. Read `GEMINI.md` for the full project context.
2. Check the existing folder structure for the relevant layer — never duplicate patterns.
3. Verify whether an abstraction (interface) already exists in `Application/Abstractions/` before creating a new one.
4. Check `Directory.Packages.props` for available NuGet packages before adding new ones.

---

## 3. Domain Rules

- Aggregate roots live in `Bookify.Domain/<AggregateName>/`.
- Use **value objects** for any concept with structural equality (e.g., `Money`, `Address`, `DateRange`).
- All domain errors must be static `Error` or `DomainError` instances on a companion `*Errors.cs` class — never `throw` for domain failures.
- Domain events: raise via `RaiseDomainEvent(...)` inside the aggregate. They are persisted via the Outbox pattern — do not dispatch inline.
- No external package references in `Bookify.Domain.csproj`.

---

## 4. Application Layer Rules

- Every use case is a **command** or **query** handled by a dedicated **Handler** class.
- Use `ICommand<TResult>` / `IQuery<TResult>` marker interfaces from `Application/Abstractions/Messaging/`.
- Register validators with **FluentValidation** — one validator per command/query.
- Caching: use `ICacheable` / `ICacheService` abstractions; never call Redis directly from a handler.
- Handlers must not depend on infrastructure types — only on abstractions.
- Pagination: use `IPagedQuery<TResult>` and `PagedResponse<T>` for list queries.

---

## 5. Infrastructure Rules

- All registrations go into dedicated private `Add*` methods in `Bookify.Infrastructure/DependencyInjection.cs`.
- New services must be registered inside the appropriate method; do not inline ad-hoc registrations.
- EF Core entity configurations live in `Configurations/` and must implement `IEntityTypeConfiguration<T>`.
- Use **snake_case** column naming (`UseSnakeCaseNamingConvention()`).
- Read-side (list/search) queries: use **Dapper** with raw SQL; write-side: use EF Core.
- Background jobs: implement via Quartz.NET; add a `*Setup.cs` class alongside the job.
- Resilience pipelines: use `Microsoft.Extensions.Resilience` (`AddResiliencePipeline` / `AddResilienceHandler`). Never add Polly strategies inline in service classes.

---

## 6. API Layer Rules

- Controllers inherit from base controller and are attribute-routed.
- Use `[ApiVersion("N")]` and declare versions in `Controllers/ApiVersions.cs`.
- Apply `[EnableRateLimiting("policy-name")]` to controller actions that need a named policy (write operations, search). Note: `[RequireRateLimiting]` is for Minimal API endpoints only.
- Return `IActionResult` using problem details for errors — leverage the existing `ProblemDetail` extensions.
- Never inject `DbContext` or infrastructure types directly into controllers.

---

## 7. Rate Limiting — Established Policies

| Policy name | When to use |
|-------------|-------------|
| `write-operations` | POST/PUT/DELETE endpoints that mutate state |
| `search` | GET endpoints that execute expensive searches |
| `health-checks` | Applied to `/health` — do not reuse elsewhere |

To add a **new policy**:
1. Create `<Name>RateLimiterPolicy.cs` in `Bookify.Infrastructure/RateLimiting/`.
2. Implement `IRateLimiterPolicy<string>`.
3. Register the singleton and add the policy inside `AddRateLimiting()` in `DependencyInjection.cs`.
4. Apply the policy name string via `[EnableRateLimiting("name")]` on the controller action (MVC controllers). Use `[RequireRateLimiting("name")]` only for Minimal API endpoints.

> The global limiter (80 req/min sliding window) is always active — named policies are additional constraints.

---

## 8. Resilience — Established Pipelines

| Pipeline key | Used by |
|--------------|---------|
| `email-notification-retry` | `NotifyCompletedBookingsJob` |
| `turnstile-pipeline` | `TurnstileService` (HTTP client) |

To add a **new resilience pipeline** for an HTTP client:
```csharp
services.AddHttpClient<IMyService, MyService>(...)
    .AddResilienceHandler("my-pipeline", builder => { ... });
```

For non-HTTP resilience (background jobs):
```csharp
services.AddResiliencePipeline("my-key", builder => { ... });
// Inject ResiliencePipelineProvider<string> and call .GetPipeline("my-key")
```

---

## 9. Database Migrations

```bash
# Add a new migration
dotnet ef migrations add <MigrationName> \
  -p src/Bookify.Infrastructure \
  -s src/Bookify.Api

# Apply migrations manually
dotnet ef database update \
  -p src/Bookify.Infrastructure \
  -s src/Bookify.Api
```

Migrations are auto-applied in Development via `app.ApplyMigrations()` in `Program.cs`.

---

## 10. Testing Rules

### Integration Tests (`Bookify.Application.IntegrationTests`)
- Use the existing `IntegrationTestWebAppFactory` (or `RateLimitTestWebAppFactory` for rate-limit tests).
- Infrastructure is provided by **Testcontainers** (PostgreSQL, Redis, Keycloak) — never mock them.
- Seed data via the factory's setup; don't rely on production seed data.
- Test classes should follow the `<Feature>Tests.cs` naming convention.

### Unit Tests
- Use **NSubstitute** for mocking.
- Use **FluentAssertions** for assertions.
- Use **Bogus** for generating fake data.

### Architecture Tests (`Bookify.ArchitectureTests`)
- Add a test whenever a new architectural rule is established.
- Do not bypass NetArchTest rules.

---

## 11. Build & Quality Gates

```bash
dotnet build Bookify.sln --configuration Release
dotnet test Bookify.sln
```

- `TreatWarningsAsErrors=true` — fix all compiler warnings before committing.
- `SonarAnalyzer.CSharp` runs on every build — address all Sonar issues.
- Code style is enforced by `.editorconfig` — do not override it inline.
- Central Package Management is in `Directory.Packages.props` — do not add `Version=` attributes to individual `.csproj` files.

---

## 12. Git & Branch Conventions

| Branch | Purpose |
|--------|---------|
| `main` | Protected; only receives PRs from `dev` |
| `dev` | Integration branch |
| `feature/*` | Feature branches off `dev` |

CI (`ci.yml`) runs automatically on non-draft PRs from `dev` → `main`.

---

## 13. Things to NEVER Do

- ❌ Add business logic to the API or Infrastructure layer.
- ❌ Reference `DbContext` or EF Core types outside of Infrastructure.
- ❌ `throw` exceptions for domain validation failures — use the Result pattern.
- ❌ Dispatch domain events directly from handlers — use the Outbox pattern.
- ❌ Add `Version=` to `.csproj` packages — versions are managed centrally.
- ❌ Skip writing integration tests for new endpoints.
- ❌ Create Polly strategies inline inside service constructors — use `DependencyInjection.cs`.
- ❌ Hardcode secrets or connection strings — use `appsettings.json` sections + `IOptions<T>` for structure, and **User Secrets** (`dotnet user-secrets`) for sensitive values in development (Keycloak credentials, Turnstile keys, SMTP passwords, etc.).
- ❌ Use `[AllowAnonymous]` on write endpoints without explicit approval.
- ❌ Break the layer dependency rules — architecture tests will catch it and CI will fail.
