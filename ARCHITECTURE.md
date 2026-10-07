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
- Keeping temporary GUI state, such as a new-transaction draft and the date of the last successfully added transaction in the current session
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

- Use cases such as `AddTransaction`, `EditTransaction`, `DeleteTransaction`, `ListTransactions`, `GetSummary`, `GetNewTransactionDefaults`, `GetTransactionSettings`, `SetDefaultPayer`, `SetDefaultLocation`, `SetTransactionDefaults`
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
  - payer and counterparty are optional, but when provided must refer to existing party values
  - location is optional, but when provided it must be chosen from an existing location value
  - reference items are added through explicit `AddCategory`, `AddParty`, or `AddLocation` workflows, never implicitly while saving a transaction

### 3) Domain (Core)

Responsible for:

- Domain model definitions
- Stable business concepts independent from translation

Contains:

- Entities such as `Transaction`, `Category`, `Party`, `Location`, `Tag`, `TransactionTag`, and `TransactionSettings`
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
- Editing an already applied initial migration does not alter an existing database. Updating a local development database is a separate, explicit operation: create a consistent backup, apply the required schema change, and verify data preservation, integrity, and foreign keys

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
- PayerId (optional)
- CounterpartyId (optional)
- LocationId (optional)
- Description (required, non-blank, up to 500 characters)

### Category

- Id
- Name
- IconKey (optional portable identifier, e.g. `mdi:Home`, up to 100 characters)
- ColorCode (optional uppercase `#RRGGBB`, stored as nullable text with a maximum length of 7)

### Party

- Id
- Name

### Location

- Id
- Name

### Tag and TransactionTag

- `Tag`: Id, Name (required, up to 50 characters), optional ColorCode (`#RRGGBB`), and optional IconKey (`mdi:<name>`, up to 100 characters)
- `TransactionTag`: TransactionId and TagId, forming a many-to-many association
- Transaction commands carry selected tag IDs; `TransactionDto` exposes tag IDs and names so Presentation can display labels and preserve identity after renames

### TransactionSettings

- Id (singleton key, constrained to 1)
- DefaultPayerId (optional foreign key to Party; deletion sets it to NULL)
- DefaultLocationId (optional foreign key to Location; deletion sets it to NULL; initially empty)
- Stores manually configured payer and location defaults for new transactions; the remembered transaction date is session-only WPF state, and currency selection is not implemented

## Important Behavioral Rules

- `Transaction.Type` is a domain enum and is translated only in the UI
- Persisted payer/location defaults and the initial date/type are resolved in the Application layer. WPF replaces the new-form date with its remembered session date; restored drafts and edits retain their own date
- The default app language is chosen from the system culture
- Initial category names depend on the current culture and are created by WPF startup for a database without category/tag/transaction data. The initial migration creates the self party `Ja` with ID 1 and the transaction-settings row referencing that ID; names are not used to resolve defaults
- `Category` is a controlled dictionary
- WPF dictionary lists preserve selection by entity ID after reload, rebinding to the refreshed instance so the visible selection matches edit/delete actions. If there is no selection or the selected entity no longer exists, the first available item is selected. An empty list has no selection; edit/delete are disabled during loading and when nothing is selected
- Category colors are persisted data independent of WPF; `TransactionDto.CategoryColorCode` carries the current category color to Presentation when transactions are loaded
- WPF uses `CategoryBadge` for category names, optional icons, and colors in the monthly list, dictionary, autocomplete results, and the selected category field. Clicking or editing the selected field switches to plain name-based text input; choosing a result restores the badge. Category and tag dictionaries have no separate Color column. Other dictionary lists show names only
- `AutocompleteComboBox.ItemTemplate` customizes result and selected-item presentation while `DisplayMemberPath` continues to resolve text for filtering and selection. Its inner ListBox uses either an item template or a display-member path. Popup open state is bound two-way so dismissing suggestions does not prevent reopening them on further typing
- The native color picker receives the category dialog's HWND through an `IWin32Window` adapter, explicitly associating it with its WPF owner for modal behavior and activation
- `Party` and `Location` are reusable dictionaries that can be extended through explicit add workflows from their tabs or the transaction form's `+` buttons
- Tags are an optional reusable dictionary independent of categories and parties. Add/edit tag use cases normalize names and reject case-insensitive duplicates; invalid names and duplicates are presented with localized messages
  - Optional colors are normalized and validated on add/edit with the same Application validator as category colors. The nullable ColorCode column is included in the initial migration and EF model snapshot
  - The shared name/color dialog supports tag dictionary add/edit and transaction quick add, including choosing or clearing an independent icon with the shared MDI picker, and removing a previously chosen color
- WPF supports a Tags page, multiple chip selections and quick add in transaction forms, and tag labels on the monthly transaction list
  - `TransactionDto.TagDetails` carries tag IDs, names, current colors, and icon identifiers as `TransactionTagDto` records. Monthly list labels use a noninteractive template with a solid custom fill or a neutral theme background and contrasting text; the view does not query repositories to resolve colors
  - `TagChipStyles.xaml` gives CheckBox controls a rounded chip template while retaining selection bindings and keyboard/automation behavior. Unselected chips use a neutral outline; selected chips uniformly use the dynamically resolved theme accent. An optional icon replaces the color dot and appears on a tile using the tag color or the neutral theme color. Tags without an icon keep the optional color dot in both states; unknown icon identifiers fall back to the dot/name. Text on the fill uses black or white according to relative luminance and contrast. Keyboard focus has a separate outline; hover does not change the selection appearance
- A fresh form selects no tags by default. Quick add selects the new tag and preserves existing selections; editing and restored drafts keep their saved selections
- Selected tag IDs are included in drafts and unsaved-change detection. Saving a transaction sends the actual selection, including an empty selection when tags are deliberately removed
- Deletion of a tag used by transactions is blocked by the repository before SQLite's cascade deletion can remove associations
- Transaction updates replace tag associations and scalar fields within one database transaction; a failed write rolls back both. Only scalar transaction data is attached before inserting tag links to avoid EF tracking conflicts
- Reference data for a form is loaded sequentially because repositories in a scope share an EF Core DbContext
- Add workflows reject duplicate reference names before a database constraint error reaches the user
- Application add workflows trim category, party, location, and tag names before validation/duplicate comparison and persistence. Whitespace-only names are rejected; whitespace inside a name is preserved. Quick add uses the same workflows
- `TransactionDraft` is WPF-only temporary state for a new transaction and does not survive an application restart; editing an existing transaction asks before discarding unsaved changes instead
  - Unsaved edits use a themed WPF dialog through `IDialogService`, offering Save, Discard changes, and Continue editing. Escape or closing the prompt keeps the editor open; failed validation also prevents closing after Save
  - A restored draft is indicated in the form as well as the window title. The notice above the buttons disappears after the first field or tag selection change; the draft itself is retained. Clear is available only for new transactions: it preserves the current form date, resets the other fields to their original defaults, and clears validation errors and tag selections. It captures a new empty-draft baseline, so cancelling immediately after Clear leaves no draft. Subsequent changes can create a new draft
- Amount formatting and transaction-type colors are Presentation concerns; another GUI must implement its own equivalent rendering from `Amount` and `Transaction.Type`. The WPF type selector uses expense/income theme colors for both options and the selected value
- `AmountInputBehavior.RawText` binds to the ViewModel's numeric input text separately from `TextBox.Text`. Unfocused presentation uses culture-specific grouping and two decimal places, without a currency symbol. Focus restores plain numeric editing; letters and currency text are rejected during typing/paste. Empty fields remain empty, and focus-only formatting does not modify drafts or dirty-state detection. The ViewModel parses the raw input to `decimal` before sending an Application command
- Transaction form order is date/amount/type, description, category, counterparty, payer, location, and tags. Monthly table columns are date, amount, category, description, counterparty, payer, location, and tags. Type is represented by amount-cell color and tooltip. Header sorting applies to the scalar columns, including category name, with visible ascending/descending arrows; tags are not sortable. This sorting uses the WPF collection view, not a database query
- The monthly table footer binds its left-side count to `Transactions.Count`; Edit and Delete remain on the right. The count tracks the loaded month and collection changes. Read-only bindings in `Run.Text` explicitly use `Mode=OneWay`, including the localized label, because that property's default binding mode is two-way
- `HyphenDatePicker` is a WPF presentation control used by the add/edit form. It normalizes dot and slash date separators to hyphens while retaining short-date culture ordering and the standard calendar/parser. Formatting does not change the bound date value; table dates continue to use `dd.MM.yyyy`
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
- Integration tests use disposable SQLite files named `saldo-test-{GUID}.db` in the temporary directory. Schema-sensitive color, icon, tag, optional-reference, and settings tests apply the initial migration to a fresh database; other repository tests can use `EnsureCreated()`. Tests do not access the application's `%AppData%\Saldo\saldo.db`
- Backup integration tests verify standalone snapshots from an open WAL database, persisted references and tags, migration history and database integrity, replacement of previous backups, and protection against invalid destinations, failure, and cancellation
- Minimal GUI testing, with most business behavior verified outside WPF
- `Saldo.Tests.Wpf` verifies tag selection, draft restoration, rename handling, unsaved-change detection, and tag IDs passed by the form's save command without opening application windows. STA rendering tests exercise the actual chip template, both themes, selection bindings, theme changes, and text contrast
- Settings integration tests verify the initial payer reference and empty default location, preservation after rename, persistence across contexts, clearing, deletion behavior, invalid-reference rejection, atomic validation of both defaults, the singleton constraint, and agreement between the initial migration and EF model. WPF tests cover automatic selection/clear saves, loading without writes, rollback of the visible selection after a failed save, and the shared X-button template for payer/location
- Date-session tests exercise successful add, failed validation, cancellation, editing, and a fresh session. Clear tests verify date preservation, default payer/location restoration, and removal of a restored draft. Date-control tests cover manual entry, selection, and clearing in both supported cultures
- Transaction-list XAML tests verify that the view loads without the read-only localization binding exception and that the footer count tracks collection changes
- Autocomplete tests exercise icon/color templates, name filtering, selected-label/editing transitions, and reopening after dismissal. Amount-field tests verify focus formatting, raw binding preservation, empty/reset behavior, and rejection of letters/currency text
- Tag integration tests cover dictionary validation, optional color persistence through the initial migration, invalid-color rejection on add/edit, multi-tag transactions, rename/update/removal, blocked deletion of used tags, and rollback after a failed transaction update

## Future Extensions

- CSV import/export
- Recurring transactions
- Tag filtering and summaries
- Restore and automatic backups (manual single-file backup creation is implemented)
- Additional frontends reusing the same core

### Category and tag icon presentation

Application add/edit validates and trims optional `mdi:<name>` identifiers without referencing a WPF enum. SQLite stores category and tag `IconKey` as nullable text in the initial development migration. Transaction DTOs include the current category and tag icons, so changing a dictionary entry updates existing transactions on reload.

Categories and tags use the shared `ReferenceIconKeyNormalizer` in Application. Only WPF references MahApps.Metro.IconPacks.Material and its Core dependency. The full catalog is cached as metadata; the owned picker creates controls for at most 60 results per page. Search and paging work offline. `CategoryBadge` renders categories in dictionaries, the monthly list, autocomplete results, and the selected category field, and tags in their dictionary. `TagChipStyles.xaml` and `TagLabelTemplates.xaml` render selectable and monthly-list tag chips. All use theme-aware fallback and contrasting icon foreground. Unknown identifiers hide the icon while preserving the name/color.

## Transaction settings

`TransactionSettings` contains one row (`Id = 1`, enforced by a check constraint) and optional `DefaultPayerId` and `DefaultLocationId` foreign keys to `Parties` and `Locations`. The default location is initially NULL. Deleting an unused default party or location sets its reference to NULL. The initial migration seeds `Ja` with ID 1 and sets the default to that ID. WPF startup continues to seed category names; it does not recreate the initial party.

Application exposes `GetTransactionSettings`, `SetDefaultPayer`, `SetDefaultLocation`, `SetTransactionDefaults` (validates both IDs before one atomic repository save), and `GetNewTransactionDefaults`. `ITransactionSettingsRepository` is implemented in SQLite Infrastructure. Both foreign keys are nullable and indexed in the initial migration and model snapshot.

WPF Settings loads both dictionaries and settings on navigation. The selectors share `DefaultReferenceComboBoxStyle`, show only dictionary entries, and expose X between the selected label and dropdown arrow. Selection or clearing saves automatically through `SetTransactionDefaults`; there is no Save button. Saves are serialized, selectors are disabled while a save runs, and a failed save restores the last saved selections and displays a localized error. Loading and changing the UI language do not write settings.

New forms resolve both defaults by ID, without name matching or alphabetical fallback; restored drafts and transaction edits preserve their own values. Theme and language remain separate in-memory UI preferences.

### Session date and Clear

`TransactionListViewModel` initializes its remembered new-transaction date to today and updates it only after a successful add. Subsequent new forms use that date alongside the persisted payer/location defaults. Changing months or navigating between pages does not reset it; a new application session starts from today. Cancelled forms, failed saves, and edits do not update it, while restored drafts retain their own date.

Clear preserves the current new-form date, restores the other original defaults, and establishes a new empty-draft baseline. Closing immediately after clearing therefore retains no draft, and Clear itself does not update the list's remembered date. Clear is unavailable when editing an existing transaction.
