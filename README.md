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
- Right-click a transaction row and choose “Add similar transaction…” to open a new form with the same type, description, references, and tags. The amount is left empty and must be entered before saving. The date follows the usual new-form date rules. Saving creates a separate transaction; the original and any existing Add-form draft are preserved. Cancelling keeps the similar transaction as a draft only if no draft already exists.
- Categories managed as a controlled dictionary with optional colors and MDI icons
- Parties managed as a reusable dictionary
- Locations managed as a reusable dictionary
- Tags managed as a reusable dictionary with optional colors and MDI icons, with multiple optional tags per transaction
- Local persistence with SQLite, including configurable default payer and location
- Manual database backups to a user-selected file
- UI localization based on resource files
- Default app language based on the system culture
- Light, dark, and system appearance themes for the WPF UI

## UX Rules Worth Knowing

- Transactions require a type, positive amount, category, non-blank description (up to 500 characters), and date. Payer, counterparty, location, and tags are optional

- `Category`, `Party`, and `Location` are selected from existing values, with autocomplete support. Typing after dismissing the suggestions reopens the filtered list
- Autocomplete selectors support Up/Down to highlight suggestions without committing them. While the list is open, Enter accepts the highlighted suggestion (or the only match) without submitting the form; ambiguous or empty results keep the list open. Escape dismisses the suggestions without cancelling the form. With the list closed, Enter retains the dialog's normal behavior.
- Reference data can be added explicitly from the transaction form with `+`; saving a transaction never creates a reference item implicitly
- Duplicate category, party, and location names are rejected with a user-friendly message
- Dictionary names (categories, parties, locations, and tags) are trimmed before saving, including quick add from the transaction form. Leading and trailing whitespace is removed; internal spaces are preserved, and whitespace-only names are rejected
- All dictionary management pages have a search field with a magnifier to the right of the title. The list filters immediately by a name substring, ignoring case and outer query whitespace. Clearing the search restores the full list. The filter remains active after reloads and CRUD operations; edit/delete always target a visible selected entry, and no matches show a localized message.
- Dictionary and template searches expose a × button when text is present; it clears the query and restores focus to the input. The transaction add/edit form has the same button for amount and description, clearing only that field. Empty fields hide the button. The icon picker already provides search clearing.
- Persisted transaction defaults are resolved outside the GUI, in the Application layer; the remembered date is temporary WPF session state
  - Default payer and location are stored by ID in SQLite `TransactionSettings`; the location starts empty. Choosing a value or clearing it with X in Settings saves immediately, without a Save button. The X appears between the selected name and dropdown arrow; the lists contain only dictionary entries, with no None option. Fresh forms use those defaults, while restored drafts and edits keep their own values. Deleting an unused default party or location clears its setting.
  - During a settings save, both selectors are disabled. If saving fails, the last saved selections are restored and an error is displayed.
- A new transaction form keeps an in-memory draft when it is cancelled; editing an existing transaction instead asks before discarding changes
  - New forms start with today's date when the app starts and reuse the date of the last successfully added transaction during that session. Cancellation, failed saves, and edits do not change the remembered date; restored drafts keep their own date.
  - Date editing does not insert leading zeroes after each keystroke: Delete and Backspace can remove day/month digits normally. Two-digit formatting is applied when leaving the field or selecting a date, preserving the culture's day/month order and hyphen separators.
  - Restored drafts are marked inside the form. Clear keeps the current form date, restores the other defaults, and removes tag selections; closing an unchanged fresh or cleared form leaves no draft.
- User-facing labels are localized, while domain values remain stable in English
- Transaction form validation is displayed next to the relevant field; errors that cannot be assigned to a field are shown in a form-level summary
- The WPF transaction list formats amounts using the selected language and uses subtle amount-cell colors to distinguish expenses from income. The type selector in add/edit forms also uses red for expenses and green for income, both in the dropdown and for the selected value
- Monthly transactions are sorted by date descending by default. Date, amount, category name, description, counterparty, payer, and location support header sorting; an arrow indicates the active column and direction. Tags do not support header sorting
- Below the monthly table, a small entry count appears on the left, with Edit and Delete on the right. It counts the transactions in the selected month and updates after adding or deleting a transaction and when switching months
- Form fields are ordered as date/amount/type, description, category, counterparty, payer, location, and tags. The monthly table columns are date, amount, category, description, counterparty, payer, location, and tags; type is represented by amount-cell color and a tooltip
- Add/edit date fields use a short date with hyphen separators, preserving the language's date order. Calendar selection and manual entry both use hyphens; the monthly table continues to display dates as `dd.MM.yyyy`
- Category colors can be selected or cleared when adding or editing a category, including adding one from the transaction form
  - Category and tag dictionaries display optional color and icon beside the name, without a separate Color column
- Category icons are optional and independent of color. Category and tag dialogs open the same owned icon picker with the full local MDI catalog, a search field (Search or Enter), and 60 icons per page with scrolling. Clearing the search with × immediately restores the full catalog; the page number can be entered directly (Enter or leaving the field). Search supports English names and common Polish aliases
- Category labels show a contrasting icon on a solid color tile and a subtle background behind the name. An icon without color uses the neutral theme; unknown icon identifiers fall back to the name/color
- The category selector uses the same color/icon label for dropdown results and the selected category. Clicking the field or editing switches to plain text for name-based search; choosing a result restores the label
- Category and tag dialogs use the native Windows color picker
- Tags describe additional context across categories, for example `Dla Iwony` / `For Iwona`; they are separate from the payer and counterparty
- Add tags explicitly from the Tags page or the transaction form's `+` button; select them with chips and view them on the transaction list
- Fresh transaction forms start with no selected tags; quick add creates a dictionary tag and selects it for the current transaction
- Editing a transaction and restoring a new-transaction draft preserve selected tags by ID, including after a tag is renamed
- Tag names are trimmed, limited to 50 characters, and checked for duplicates on add/edit. Tags used by transactions cannot be deleted until they are removed from those transactions
  - Tags can have an optional `#RRGGBB` color and an independent MDI icon. Add/edit and quick add use the same name/color dialog as categories, with a native color picker and a clear-color action
  - Form chips use a neutral outline when unselected and the application accent when selected, with contrasting text. An optional icon appears on a small tile filled with the tag color, or a neutral theme color when none is assigned. Tags without an icon show their optional color as a small dot in both states. Keyboard focus has a separate outline
  - Monthly transaction tag labels show optional icons and use solid custom colors with contrasting text; tags without a color use a neutral theme background

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

## Transaction Templates

- The Templates page supports adding, editing, and permanently deleting named reusable templates. The list shows name, description, category, transaction type, and optional amount. It reloads on navigation and preserves selection by ID.
- The owned add/edit dialog has a name, type, optional amount, description, category, optional counterparty/payer/location, and selectable tags. It uses the transaction form's expense/income toggle, amount formatting/input behavior, searchable autocomplete selectors, category color/icon badges, tag chips, and explicit quick-add buttons. Optional references can be cleared with X; a blank amount is stored as `null`. Typed full dictionary names resolve to existing entries; partial or unknown names cannot be saved implicitly. Validation appears beside fields, and closing a changed form asks whether to save, discard, or continue editing. Polish/English labels and both appearance themes are supported.
- The header's Add transaction split button opens the usual form on its main part; its stacked-document icon opens a searchable template list with bounded scrolling. Search matches names without case or diacritics. Choosing a template opens a new transaction with its fields and optional amount, keeping the current new-transaction date. References and templates are read fresh. An existing draft is preserved; cancelling a template form creates a draft only if none already exists.
- The new-transaction form also exposes the same searchable template picker through the same stacked-document icon beside Description. Loading a template updates its type and populated description, amount, references, and tags while keeping the form's date. Fields absent from the template retain existing values, including typed reference text and tags. A nonempty template tag set replaces the current tag selection. The form stays open and nothing is saved until the user saves the transaction. The picker is hidden when editing an existing transaction.
- The new-transaction form's Save as template button asks for a name and saves the current fields and selected tags without the date. An empty amount stays optional. Saving a template leaves the transaction form open and does not save a transaction. Duplicate names and invalid fields are reported without creating a template.
- Forms opened from a template or loading one later show its name in the window title. Save as template initially suggests that name even after fields are edited. Restored drafts retain the source template name alongside the draft title; loading another template updates it, and Clear removes it. This is temporary form state and creates no transaction/template database link.
- Templates require a name, a valid transaction type, and at least one additional field: description, category, payer, counterparty, location, positive amount, or a tag. Each additional field is individually optional. They have no date or recurrence fields and no link to saved transactions.
- `SaveTransactionTemplateCommand` is shared by add/edit. Names and descriptions are trimmed; names are limited to 100 characters and checked for duplicates case-insensitively. Descriptions are limited to 500 characters. All supplied reference IDs must exist; repeated tag IDs are deduplicated.
- Shared Application validation rejects identical content under a different name on add and edit, excluding the edited template itself. It compares type, trimmed description without case, amount, reference IDs, and the tag set without order or repetition. Null, empty, and whitespace descriptions are stored as NULL; empty categories are also stored as NULL. Empty templates return `TransactionTemplate.ContentRequired`; matching content returns `TransactionTemplate.ContentDuplicate`. Both WPF save paths display these errors.
- Validation returns `Result` errors with stable `TransactionTemplate.*` codes and field metadata. Missing templates return `TransactionTemplate.NotFound`.
- Dictionary entries referenced by templates cannot be deleted. Deleting a template removes its tag links, keeping dictionary entries and transactions.
- New databases receive both template tables with optional descriptions and categories from the initial migration. Existing databases need a manual schema update to make template descriptions and categories nullable. The content rules run in Application and do not rewrite or delete existing duplicate templates.

## Logging

- The WPF shell uses `Microsoft.Extensions.Logging` with Serilog
- Logs are written to `%AppData%\Saldo\Logs\saldo-.log` with daily rolling files
- The default minimum level is `Information`, with EF Core logging restricted to `Warning` and above
- Use `ILogger<T>` for technical diagnostics; keep expected validation failures in `Result<T>`

## Localization

- The WPF UI uses resource-based translations
- Supported cultures currently include `pl-PL` and `en-US`
- The app chooses its default language from the current system culture at startup
- Initial category names are culture-aware. The initial migration seeds the self party (`Ja`, ID 1) and links it to the default payer setting; renaming the party preserves that selection.
- Number formatting in the WPF UI follows the currently selected culture
- Transaction amount fields display two decimal places and grouping when unfocused, without a currency symbol; editing uses plain numeric input. Dot and comma keys, including the numeric keypad decimal key, insert the selected culture's decimal separator (comma in Polish, dot in English). A second separator or more than two fractional digits is rejected. Empty fields remain empty. Presentation formatting never changes the input stored in drafts or the decimal amount sent to Application. Currency selection is not implemented yet.
- New user-facing text should be added through localization resources instead of hardcoded strings

## Appearance

- The WPF UI supports `System`, `Light`, and `Dark` appearance themes
- `System` is the default and follows the Windows app theme while Saldo is running
- The selected language and theme are currently session-only and reset when the app is closed

### Application messages

- Application messages use an owned, theme-aware dialog with wrapped, scrollable text and a Close button. Enter and Escape close the dialog.
- Application messages use **Information** (blue information icon), **Warning** (amber triangle), or **Error** (red circle with a cross). Each includes a localized type label, so the meaning does not depend on color alone. Icons and colors adapt to the light and dark themes.
- **Information:** backup completed successfully. **Warning:** duplicate dictionary entries, invalid tag names, deletion blocked because an entry is in use, or a transaction no longer available for deletion. **Error:** failed reads, writes, backups, folder opening, startup, or unexpected UI errors. All current message-window calls explicitly choose their severity; the neutral variant remains available for compatibility.
- Field validation stays inline rather than opening additional dialogs. Saving a template keeps its inline success confirmation. Transaction-default settings display a blue information message after a successful save and a red error message after a failed load or save. Delete confirmations and unsaved-change prompts retain their dedicated decision dialogs.
- Errors in dictionaries, transactions, settings, startup, and unhandled UI errors use the application dialog. A system message box is retained only as a fallback if the themed error dialog cannot be displayed.

## Backups

- In Settings, use **Create backup** to choose where to save a timestamp-named `.db` file
- **Open database folder** opens the application's data folder in Windows Explorer, where backups saved alongside the database can be managed
- The save dialog initially opens the application's data folder, so a backup can be saved alongside the active database or in another location
- Backups use SQLite's online backup mechanism and can be created while the app is running; the result is a standalone file containing all saved data
- Choosing an existing backup prompts before overwriting it. The active database and its SQLite sidecar files cannot be selected as backup destinations
- A previous backup is replaced only after the new snapshot is complete
- Backup success (including the saved path) and failure messages use an owned, theme-aware application dialog with the existing Close button style, instead of the system MessageBox. Long messages wrap and can scroll; Enter or Escape closes the dialog.
- Automatic backups run in the background at startup and after successful month closing. They use an adjacent AutoBackups directory, one file per local day per database, and retain five successful backup days. Startup writes remain blocked until a valid daily copy is available; a persistent status supports retry. See [Automatic backups](docs/features/automatic-backups.md)
- Restore and a persisted preferred backup folder are not implemented yet

## Development Database Changes

- Earlier local-development schema changes were included in the initial migration. New changes use incremental migrations; `AddAccountingPeriods` creates the accounting-period foundation and backfills only months containing transactions
- Changing an already applied initial migration does not upgrade an existing database. An existing development database must be updated explicitly after a backup, with schema and data-integrity checks
- `TransactionSettings.DefaultLocationId` is nullable, indexed, and references `Locations` with `ON DELETE SET NULL`; new databases start with no default location

## Accounting Periods

See [Accounting periods](docs/features/accounting-periods.md) for calendar-month persistence and write protection. Browsing does not create periods; transaction writes create only the needed month, and gaps are allowed. The monthly summary bar shows green open or blue closed status with Close month/Reopen month controls. Integrity messages and Check again appear only when a problem is detected. State changes require confirmation. Closing atomically captures income/expense totals, transaction count, and a versioned SHA-256 hash. Loading a closed month checks consistency in the background without replacing the reference. Edit/Delete are disabled for closed or unknown periods; Add/copy/templates still allow preparing a transaction for an open date, with save-time validation. Successful closing also requests replacement of today's automatic backup after another integrity check.

## Status

Work in progress - early development / learning project

## License

MIT
