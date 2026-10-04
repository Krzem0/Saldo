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
- `ReferenceColorDialog`, shared by categories and tags and using the native Windows color picker through WinForms interop, and `CategoryColorBrushConverter` for transaction-list rendering

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
- `AddCategory` and `EditCategory`, sharing category name and color normalization
- `AddTag` and `EditTag`, sharing tag-name validation and duplicate checks
- DTOs used by ViewModels
- Repository abstractions
- `IDatabaseBackupService`, exposing backup creation independently of the GUI and storage implementation
- FluentValidation validators for use-case commands
- Stable error codes and validation-result mapping

Rules:

- Depends on Domain abstractions and repository interfaces
- No UI concepts
- Add/edit transaction use cases validate their commands before resolving references or writing data
- Shared transaction rules are defined once and reused by add/edit command validators
- Expected transaction validation failures are returned as `Result<T>` errors rather than exceptions
- Category add/edit use cases validate optional colors before writing data, using the same `#RRGGBB` rule and uppercase normalization; empty values become `null`
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
- `SqliteDatabaseBackupService`, using dedicated SQLite connections and the online backup API to create a consistent snapshot including committed WAL data

Rules:

- Implements interfaces defined in the Application layer
- No UI code
- During the current single-instance local development phase, schema changes update the initial migration, its designer, and the model snapshot. Incremental upgrade migrations are deferred until deployed databases need to be preserved across schema versions

### Database Backups

- Settings chooses a destination using an owned Windows save dialog and invokes `IDatabaseBackupService` through a scoped service
- Opening the database folder in Explorer is a WPF service (`IDatabaseFolderService`), configured with the same data directory as the database; it introduces no Windows shell dependency into Application or Infrastructure
- Backup runs off the UI thread with a dedicated read-only source connection; it does not share an EF Core connection with other workflows
- The snapshot is written to a temporary file in the destination folder and switched to DELETE journal mode so the backup is self-contained
- Only a completed snapshot replaces the selected destination. Failed or cancelled operations preserve previous backups and clean up the temporary file
- Destination validation prevents replacing the active database or its WAL, SHM, and journal files
- Backup success and failure are logged and shown through localized messages. Restore is deferred

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
- ColorCode (optional uppercase `#RRGGBB`, stored as nullable text with a maximum length of 7)

### Party

- Id
- Name

### Location

- Id
- Name

### Tag and TransactionTag

- `Tag`: Id, Name (required, up to 50 characters), and optional ColorCode (`#RRGGBB`)
- `TransactionTag`: TransactionId and TagId, forming a many-to-many association
- Transaction commands carry selected tag IDs; `TransactionDto` exposes tag IDs and names so Presentation can display labels and preserve identity after renames

## Important Behavioral Rules

- `Transaction.Type` is a domain enum and is translated only in the UI
- Default values for a new transaction are resolved in the Application layer, not hardcoded in WPF
- The default app language is chosen from the system culture
- Initial seed values may depend on the current culture
- `Category` is a controlled dictionary
- Category colors are persisted data independent of WPF; `TransactionDto.CategoryColorCode` carries the current category color to Presentation when transactions are loaded
- WPF renders the category color as a translucent background behind its name in the transaction list; the reference list and transaction-form category selector currently show names only
- The native color picker receives the category dialog's HWND through an `IWin32Window` adapter, explicitly associating it with its WPF owner for modal behavior and activation
- `Party` and `Location` are reusable dictionaries that can be extended through explicit add workflows from their tabs or the transaction form's `+` buttons
- Tags are an optional reusable dictionary independent of categories and parties. Add/edit tag use cases normalize names and reject case-insensitive duplicates; invalid names and duplicates are presented with localized messages
  - Optional colors are normalized and validated on add/edit with the same Application validator as category colors. The nullable ColorCode column is included in the initial migration and EF model snapshot
  - The shared name/color dialog supports tag dictionary add/edit and transaction quick add, including removing a previously chosen color
- WPF supports a Tags page, multiple chip selections and quick add in transaction forms, and tag labels on the monthly transaction list
  - `TagChipStyles.xaml` gives CheckBox controls a rounded chip template while retaining selection bindings and keyboard/automation behavior. Unselected chips use an outline; selected chips use the tag color or a dynamically resolved theme accent. Text on the fill uses black or white according to relative luminance and contrast. Keyboard focus has a separate outline; hover does not change the selection appearance
- A fresh form selects no tags by default. Quick add selects the new tag and preserves existing selections; editing and restored drafts keep their saved selections
- Selected tag IDs are included in drafts and unsaved-change detection. Saving a transaction sends the actual selection, including an empty selection when tags are deliberately removed
- Deletion of a tag used by transactions is blocked by the repository before SQLite's cascade deletion can remove associations
- Transaction updates replace tag associations and scalar fields within one database transaction; a failed write rolls back both. Only scalar transaction data is attached before inserting tag links to avoid EF tracking conflicts
- Reference data for a form is loaded sequentially because repositories in a scope share an EF Core DbContext
- Add workflows reject duplicate reference names before a database constraint error reaches the user
- `TransactionDraft` is WPF-only temporary state for a new transaction and does not survive an application restart; editing an existing transaction asks before discarding unsaved changes instead
  - Unsaved edits use a themed WPF dialog through `IDialogService`, offering Save, Discard changes, and Continue editing. Escape or closing the prompt keeps the editor open; failed validation also prevents closing after Save
  - A restored draft is indicated in the form as well as the window title. The notice above the buttons disappears after the first field or tag selection change; the draft itself is retained. Clear is available only for new transactions and resets fields, validation errors, and tag selections to the fresh-form state. The list retains a draft on cancellation only when the form differs from its initial defaults
- Amount formatting and the transaction-type colors in the list are Presentation concerns; another GUI must implement its own equivalent rendering from `Amount` and `Transaction.Type`
- Appearance selection is a Presentation concern. The current WPF `ThemeService` supports system, light, and dark themes without persisting the choice yet

## Validation and Error Contract

- FluentValidation in `Saldo.Application` is the source of truth for transaction business rules at the use-case boundary
- Category name and color rules currently use a shared Application normalizer and reject invalid input with `ArgumentException`; they do not yet use the transaction validation-result contract
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
- Color tests cover normalization, rejection before writes, optional-color persistence, and adding, changing, or clearing colors as reflected in existing transaction DTOs
- Integration tests use disposable SQLite files named `saldo-test-{GUID}.db` in the temporary directory. Color integration tests apply the initial migration to a fresh database; other repository tests currently use `EnsureCreated()`. Tests do not access the application's `%AppData%\Saldo\saldo.db`
- Backup integration tests verify standalone snapshots from an open WAL database, persisted references and tags, migration history and database integrity, replacement of previous backups, and protection against invalid destinations, failure, and cancellation
- Minimal GUI testing, with most business behavior verified outside WPF
- `Saldo.Tests.Wpf` verifies tag selection, draft restoration, rename handling, unsaved-change detection, and tag IDs passed by the form's save command without opening application windows. STA rendering tests exercise the actual chip template, both themes, selection bindings, theme changes, and text contrast
- Tag integration tests cover dictionary validation, optional color persistence through the initial migration, invalid-color rejection on add/edit, multi-tag transactions, rename/update/removal, blocked deletion of used tags, and rollback after a failed transaction update

## Future Extensions

- CSV import/export
- Recurring transactions
- Tags and advanced filtering
- Backup/restore to a single file
- Additional frontends reusing the same core
