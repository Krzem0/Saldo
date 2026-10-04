# Saldo

**Saldo** is an offline desktop application for tracking personal income and expenses using a local SQLite database.

## Goals

- Simple and fast personal finance tracking
- Fully offline - no cloud, no accounts
- Clear data ownership (your data stays local)
- Educational project built with production-grade practices

## Current Scope

- Income and expense transactions
- Monthly transaction list and summary
- Categories managed as a controlled dictionary with optional colors
- Parties managed as a reusable dictionary
- Locations managed as a reusable dictionary
- Tags managed as a reusable dictionary, with multiple optional tags per transaction
- Local persistence with SQLite
- Manual database backups to a user-selected file
- UI localization based on resource files
- Default app language based on the system culture
- Light, dark, and system appearance themes for the WPF UI

## UX Rules Worth Knowing

- `Category`, `Party`, and `Location` are selected from existing values, with autocomplete support
- Reference data can be added explicitly from the transaction form with `+`; saving a transaction never creates a reference item implicitly
- Duplicate category, party, and location names are rejected with a user-friendly message
- New transaction defaults are resolved outside the GUI, in the Application layer
- A new transaction form keeps an in-memory draft when it is cancelled; editing an existing transaction instead asks before discarding changes
  - Restored drafts are marked inside the form. Clear resets the form to its defaults and removes tag selections; closing an unchanged fresh or cleared form leaves no draft
- User-facing labels are localized, while domain values remain stable in English
- Transaction form validation is displayed next to the relevant field; errors that cannot be assigned to a field are shown in a form-level summary
- The WPF transaction list formats amounts using the selected language and uses subtle amount-cell colors to distinguish expenses from income
- Monthly transactions are sorted by date descending by default; date and amount columns also support sorting from their headers
- Category colors can be selected or cleared when adding or editing a category, including adding one from the transaction form
- The category dialog uses the native Windows color picker; the transaction list shows category names with a subtle background based on the saved color
- Tags describe additional context across categories, for example `Dla Iwony` / `For Iwona`; they are separate from the payer and counterparty
- Add tags explicitly from the Tags page or the transaction form's `+` button; select them with chips and view them on the transaction list
- Fresh transaction forms start with no selected tags; quick add creates a dictionary tag and selects it for the current transaction
- Editing a transaction and restoring a new-transaction draft preserve selected tags by ID, including after a tag is renamed
- Tag names are trimmed, limited to 50 characters, and checked for duplicates on add/edit. Tags used by transactions cannot be deleted until they are removed from those transactions
  - Tags can have an optional `#RRGGBB` color. Add/edit and quick add use the same name/color dialog as categories, with a native color picker and a clear-color action
  - Unselected chips have an outline; selected chips use a solid custom color or the application accent, with contrasting text. Keyboard focus has a separate outline

## Tech Stack

- .NET 10
- WPF desktop UI (MVVM)
- EF Core + SQLite
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging + Serilog
- FluentValidation
- FluentResults

## Error Handling and Validation

- Transaction business validation is defined with FluentValidation and executed at the Application use-case boundary
- Category add/edit use cases share validation and normalization for optional colors: `#RRGGBB`, stored in uppercase, or `null` for no color; invalid codes are rejected with `ArgumentException`
- The UI handles input-format concerns that exist only in a text-based form, such as parsing an amount to `decimal`
- Validation failures carry stable error codes and, where applicable, the affected property name
- The UI localizes these codes and displays field errors inline, with a form-level summary as a fallback
- Use `Result<T>` for expected validation failures when a use case should return success/failure without throwing
- Reserve exceptions for unexpected technical failures

## Logging

- The WPF shell uses `Microsoft.Extensions.Logging` with Serilog
- Logs are written to `%AppData%\Saldo\Logs\saldo-.log` with daily rolling files
- The default minimum level is `Information`, with EF Core logging restricted to `Warning` and above
- Use `ILogger<T>` for technical diagnostics; keep expected validation failures in `Result<T>`

## Localization

- The WPF UI uses resource-based translations
- Supported cultures currently include `pl-PL` and `en-US`
- The app chooses its default language from the current system culture at startup
- Some seed data is culture-aware, for example the initial self party value (`Ja` / `Me`); it is chosen only for a new, empty database and is not translated later
- Number formatting in the WPF UI follows the currently selected culture
- New user-facing text should be added through localization resources instead of hardcoded strings

## Appearance

- The WPF UI supports `System`, `Light`, and `Dark` appearance themes
- `System` is the default and follows the Windows app theme while Saldo is running
- The selected language and theme are currently session-only and reset when the app is closed

## Backups

- In Settings, use **Create backup** to choose where to save a timestamp-named `.db` file
- **Open database folder** opens the application's data folder in Windows Explorer, where backups saved alongside the database can be managed
- The save dialog initially opens the application's data folder, so a backup can be saved alongside the active database or in another location
- Backups use SQLite's online backup mechanism and can be created while the app is running; the result is a standalone file containing all saved data
- Choosing an existing backup prompts before overwriting it. The active database and its SQLite sidecar files cannot be selected as backup destinations
- A previous backup is replaced only after the new snapshot is complete
- Restore, automatic backups, and a persisted preferred backup folder are not implemented yet

## Status

Work in progress - early development / learning project

## License

MIT
