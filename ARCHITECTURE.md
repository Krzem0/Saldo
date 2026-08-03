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
- Parsing UI-specific input representations, such as converting amount text to `decimal`
- Presenting validation feedback returned by Application
- Keeping temporary GUI state, such as a new-transaction draft
- Formatting amounts for the selected culture and applying UI-only visual cues based on transaction type
- Applying GUI-specific appearance themes through WPF resource dictionaries

Contains:

- Views
- ViewModels
- UI-specific services (dialogs, notifications)
- WPF-only controls such as autocomplete widgets
- WPF-only services such as `ThemeService` and light/dark theme resource dictionaries

Rules:

- No direct database access
- No business rules in views
- UI input parsing must not become a second copy of business validation
- Field-level errors should be displayed next to their controls; errors without a field should be displayed in a form-level summary
- UI labels may be localized, but domain values should not depend on translated text
- GUI-specific state and visual conventions must not leak into Application or Domain
- Theme colors should be referenced through dynamic theme resources rather than hardcoded in views

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
- FluentValidation validators for use-case commands
- Stable error codes and validation-result mapping

Rules:

- Depends on Domain abstractions and repository interfaces
- No UI concepts
- Add/edit transaction use cases validate their commands before resolving references or writing data
- Shared transaction rules are defined once and reused by add/edit command validators
- Expected validation failures are returned as `Result<T>` errors rather than exceptions
- Validation errors include the command property name when the error can be assigned to a field
- May enforce workflow rules such as:
  - category must be chosen from an existing dictionary value
  - payer and counterparty must be chosen from existing party values
  - location is optional, but when provided it must be chosen from an existing location value
  - reference items are added through explicit `AddCategory`, `AddParty`, or `AddLocation` workflows, never implicitly while saving a transaction

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
- `Party` and `Location` are reusable dictionaries that can be extended through explicit add workflows from their tabs or the transaction form's `+` buttons
- Add workflows reject duplicate reference names before a database constraint error reaches the user
- `TransactionDraft` is WPF-only temporary state for a new transaction and does not survive an application restart; editing an existing transaction asks before discarding unsaved changes instead
- Amount formatting and the transaction-type colors in the list are Presentation concerns; another GUI must implement its own equivalent rendering from `Amount` and `Transaction.Type`
- Appearance selection is a Presentation concern. The current WPF `ThemeService` supports system, light, and dark themes without persisting the choice yet

## Validation and Error Contract

- FluentValidation in `Saldo.Application` is the source of truth for business rules at the use-case boundary
- The WPF layer may validate representation-specific input before command creation, for example whether amount text can be parsed as `decimal`
- Parsing does not replace business validation; the resulting command is still validated by Application
- Validation failures use stable technical codes such as `Transaction.CategoryRequired`
- When possible, an error carries `PropertyName` metadata identifying the command property that failed
- Presentation maps command properties to controls, localizes technical codes, and displays messages without depending on FluentValidation types
- Errors that cannot be mapped to a control remain visible in a form-level summary
- A single save attempt should collect and present all applicable validation errors

## Key Design Decisions

- MVVM for UI maintainability and testability
- EF Core + SQLite as a local, reliable, zero-config persistence layer
- Repository pattern to separate application workflows from storage
- Resource-based localization for user-facing text
- FluentValidation for reusable, UI-independent command validation
- FluentResults for expected business failures and their metadata
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
- Validator and use-case tests should verify stable error codes and relevant property metadata
- Integration tests for SQLite persistence
- Minimal GUI testing, with most business behavior verified outside WPF

## Future Extensions

- CSV import/export
- Recurring transactions
- Tags and advanced filtering
- Backup/restore to a single file
- Additional frontends reusing the same core
