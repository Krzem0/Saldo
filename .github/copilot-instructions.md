# Copilot Instructions (Saldo)

This repository contains **Saldo** — an offline-first desktop app for tracking personal income and expenses.
Primary goals: **simplicity, maintainability, testability**, and **clean separation of concerns**.

## Architecture
Use a Clean Architecture / Onion style with strict dependency direction:

- `Saldo.Domain` (core business rules, entities, value objects)
- `Saldo.Application` (use cases, orchestration, interfaces/ports)
- `Saldo.Infrastructure.Sqlite` (SQLite persistence, migrations, repositories)
- `Saldo.Desktop.Wpf` (WPF UI shell, MVVM)
- `Saldo.Tests.Unit` (unit tests for Domain + Application)
- `Saldo.Tests.Integration` (integration tests for Infrastructure + SQLite)

Dependency rules:
- UI depends on Application and Domain.
- Infrastructure implements interfaces defined in Application (and may depend on Domain).
- Domain must not reference Application/Infrastructure/UI.
- Application must not reference Infrastructure/UI.

If you generate code that violates these rules, refactor it.

## Domain Model Guidelines
- `Transaction`:
  - Has `Date`, `Type` (Income/Expense), `Amount` (always positive), `CategoryId`, `PayerId`, `CounterpartyId`, optional `Description`, optional `Location`, and `Tags`.
  - Sign/meaning is represented by `Type`, not by negative amounts.
- Prefer immutable value objects when appropriate.
- Keep domain logic free from framework types (no WPF/WinUI/EF types in Domain).

## Application Layer Guidelines
- Implement use cases as explicit classes (e.g., `AddTransaction`, `EditTransaction`, `GetMonthlySummary`).
- Use interfaces for persistence:
  - `ITransactionRepository`, `ICategoryRepository`
  - optional `IUnitOfWork`
- Keep business validation at the use-case boundary; domain invariants inside Domain.
- Define command validation with FluentValidation in `Saldo.Application`.
- Reuse shared rule sets instead of copying rules between add/edit validators.
- Add/edit transaction use cases must execute their validators before resolving references or writing data, even when a UI performs input checks of its own. Apply the same pattern to future command-based workflows.
- Prefer `Result<T>` for expected validation failures and other business outcomes that the UI should handle explicitly.
- Return stable error codes for expected validation failures. Add `PropertyName` metadata when an error can be associated with a command property.
- Return simple result DTOs appropriate for UI consumption.
- Use exceptions only for unexpected technical failures or broken invariants.
- When a use case needs diagnostics, inject `ILogger<T>` and keep logging focused on technical events and successful operations; do not log expected validation failures as errors.

## Infrastructure (SQLite)
- Only `Saldo.Infrastructure.Sqlite` talks to SQLite.
- Repositories implement Application interfaces.
- Prefer explicit SQL (Dapper) or EF Core, but keep DB-specific code out of Application/Domain.
- Handle migrations / schema versioning (simple incremental migrations).

## WPF UI (MVVM)
- Views contain no business logic.
- ViewModels call Application use cases; no direct DB calls from UI.
- UI-only input checks, such as parsing amount text to `decimal`, may remain in the ViewModel but must not duplicate business rules.
- Do not prevent save merely to hide incomplete business input; run validation and provide actionable feedback.
- Map validation property metadata to controls and show localized messages next to affected fields.
- Show errors without a field in a form-level summary.
- Do not display raw technical error codes to users.
- Use async commands where IO is involved.
- Avoid static singletons. Prefer DI.
- The WPF shell uses `Microsoft.Extensions.Logging` with Serilog writing to `%AppData%\Saldo\Logs\saldo-.log`.
- Prefer `ILogger<T>` injection over static logging.

## Localization
- The WPF UI is localized using resource-based translations.
- Current supported cultures are `pl-PL` and `en-US`.
- Do not hardcode user-facing strings in Views or ViewModels when a localized resource should be used.
- When introducing new UI text, update the translation resources accordingly.

## Coding Conventions
- C# with nullable reference types enabled.
- Prefer clear naming: `Transaction`, `Category`, `MonthlySummary`.
- Avoid abbreviations except common ones (DTO, ID).
- Keep methods small and single-purpose.
- Favor composition over inheritance.

## Testing
- Unit tests:
  - focus on Domain invariants and Application use cases.
  - cover validators, stable error codes, and property metadata for field-level failures.
  - fast, deterministic, no filesystem/database.
- Integration tests:
  - real SQLite with temp file per test (or per fixture).
  - avoid shared state; disable parallelization if sharing resources.

## Output Expectations
When generating code:
- Provide complete compilable snippets (including namespaces, usings if needed).
- Prefer minimal but production-grade implementation.
- Include brief comments only where necessary to clarify intent.
- Do not invent features outside the described scope.
