# Architecture

Saldo is an **offline-first** desktop application for tracking personal income and expenses. The primary goals are simplicity, maintainability, and clear separation of concerns.

## High-level Goals
- **Offline-only**: no network dependencies required to use the app
- **Local data ownership**: all data stored locally in SQLite
- **Testable core**: business logic independent from UI
- **Maintainable UI**: MVVM with clear boundaries

## Logical Layers
### 1) Presentation (UI)
Responsible for:
- Views (screens) and user interaction
- MVVM bindings
- Navigation and dialog flow

Contains:
- Views
- ViewModels
- UI-specific services (dialogs, file pickers, notifications)

Rules:
- No direct database access
- No business rules in views

### 2) Application (Use Cases)
Responsible for:
- Orchestrating user actions (use cases)
- Transactions and application workflows
- Validation at the use-case boundary

Contains:
- Commands/Handlers or Services (e.g., `AddTransaction`, `EditTransaction`, `GetMonthlySummary`)
- DTOs / result models used by ViewModels

Rules:
- Depends on Domain abstractions and repository interfaces
- No UI concepts

### 3) Domain (Core)
Responsible for:
- Business rules and invariants
- Domain model definitions

Contains:
- Entities/Value Objects (e.g., `Transaction`, `Category`, `Money`)
- Domain services (if needed)
- Domain validation rules

Rules:
- No references to UI or infrastructure
- Pure C# logic, easy to unit test

### 4) Infrastructure (Persistence + External)
Responsible for:
- SQLite persistence
- Repository implementations
- Migrations / schema management
- File system operations (export/import) if added later

Contains:
- SQLite context/connection factory
- Repository implementations
- Migration runner

Rules:
- Implements interfaces defined in Application/Domain
- No UI code

## Data Model (Conceptual)
- **Transaction**
  - Id
  - Date
  - Amount (positive number)
  - Type (Income / Expense)
  - CategoryId (optional)
  - Note (optional)
- **Category**
  - Id
  - Name
  - Type (Income / Expense / Both)

## Key Design Decisions
- **MVVM** for UI maintainability and testability
- **SQLite** as a local, reliable, zero-config database
- **Repository pattern** to abstract persistence from application logic
- **One-way dependencies**:
  - UI → Application → Domain
  - Infrastructure implements interfaces used by Application/Domain

## Non-goals (for now)
- Cloud sync / accounts
- Multi-device support
- Complex budgeting rules (envelopes, forecasting)
- Integrations (bank imports, APIs)

## Testing Strategy (planned)
- Unit tests for Domain and Application layers
- Integration tests for Infrastructure (SQLite)
- Minimal UI testing initially (focus on core correctness)

## Future Extensions (optional)
- CSV import/export
- Recurring transactions
- Tags and advanced filtering
- Backup/restore to a single file
