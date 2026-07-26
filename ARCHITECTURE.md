# Architecture

Saldo is an offline-first desktop application for tracking personal income and expenses. The primary goals are simplicity, maintainability, and clear separation of concerns.

## High-level Goals

- Offline-only: no network dependencies required to use the app
- Local data ownership: all data is stored locally in SQLite
- Testable core: business logic independent from the GUI
- GUI-agnostic rules: defaults and transaction behavior should live outside WPF where possible
- Maintainable UI: MVVM with clear boundaries

## Logical Layers

### 1) Presentation (UI)

Responsible for:

- Views and user interaction
- MVVM bindings
- Navigation and dialog flow
- Localization display concerns

Contains:

- Views
- ViewModels
- UI-specific services (dialogs, notifications)
- WPF-only controls such as autocomplete widgets

Rules:

- No direct database access
- No business rules in views
- UI labels may be localized, but domain values should not depend on translated text

### 2) Application (Use Cases)

Responsible for:

- Orchestrating user actions
- Validation at use-case boundaries
- Resolving defaults for new transactions
- Resolving dictionary references during transaction save

Contains:

- Use cases such as `AddTransaction`, `EditTransaction`, `DeleteTransaction`, `ListTransactions`, `GetSummary`, `GetNewTransactionDefaults`
- DTOs used by ViewModels
- Repository abstractions

Rules:

- Depends on Domain abstractions and repository interfaces
- No UI concepts
- May enforce workflow rules such as:
  - category must be chosen from an existing dictionary value
  - party and location may be created inline when saving a transaction

### 3) Domain (Core)

Responsible for:

- Domain model definitions
- Stable business concepts independent from translation

Contains:

- Entities such as `Transaction`, `Category`, `Party`, `Location`
- Enums such as `TransactionType`

Rules:

- No references to UI or infrastructure
- Pure C# logic
- Domain values use stable technical names even when the GUI shows localized labels

### 4) Infrastructure (Persistence)

Responsible for:

- SQLite persistence
- EF Core mappings
- Repository implementations
- Schema and migration files

Contains:

- `SaldoDbContext`
- Entity configurations
- Repository implementations
- Initial schema migration and model snapshot

Rules:

- Implements interfaces defined in the Application layer
- No UI code

## Conceptual Data Model

### Transaction

- Id
- Date
- Type (`Income` / `Expense`)
- Amount (positive number)
- CategoryId (required)
- PayerId (required)
- CounterpartyId (required)
- LocationId (optional)
- Description (optional)

### Category

- Id
- Name

### Party

- Id
- Name

### Location

- Id
- Name

## Important Behavioral Rules

- `Transaction.Type` is a domain enum and is translated only in the UI
- Default values for a new transaction are resolved in the Application layer, not hardcoded in WPF
- The default app language is chosen from the system culture
- Initial seed values may depend on the current culture
- `Category` is a controlled dictionary
- `Party` and `Location` are reusable dictionaries that may be extended inline while saving a transaction
- Matching for inline-created parties and locations should avoid duplicates caused by casing or diacritics where possible

## Key Design Decisions

- MVVM for UI maintainability and testability
- EF Core + SQLite as a local, reliable, zero-config persistence layer
- Repository pattern to separate application workflows from storage
- Resource-based localization for user-facing text
- One-way dependencies:
  - UI -> Application -> Domain
  - Infrastructure implements interfaces used by Application

## Non-goals (for now)

- Cloud sync or accounts
- Multi-device support
- Complex budgeting rules
- External banking integrations

## Testing Strategy

- Unit tests for Application and Domain behavior
- Integration tests for SQLite persistence
- Minimal GUI testing, with most business behavior verified outside WPF

## Future Extensions

- CSV import/export
- Recurring transactions
- Tags and advanced filtering
- Backup/restore to a single file
- Additional frontends reusing the same core
