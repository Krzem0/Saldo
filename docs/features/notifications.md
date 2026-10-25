# Session notifications

`INotificationService` and the thread-safe `NotificationService` in Application maintain the last 50 events in publication order. Events contain a timestamp, stable code, severity, localized message and optional details. History is session-only and never written to SQLite. Messages retain the language used when published; window labels follow the current UI language.

WPF's singleton `NotificationsViewModel` receives events on the dispatcher. The footer shows one message for at least five seconds; the last stays visible until another event arrives. Pending events with the same code and severity are combined into the latest result with an occurrence count. Every individual event remains in history. Pending work is also bounded to 50 entries.

The information button opens a single modeless, owned history window, newest first, with local timestamps, severity labels and details (including backup paths). The view uses theme resources and supports Escape/Close. Its dispatcher timer and subscription are released with the main window.

Sources include automatic/manual backups, saved defaults, transaction add/edit/delete, dictionary add/edit/delete (including quick-add from forms), template saves/deletion, period close/reopen, and message dialogs. Template page/picker failures are recorded while keeping their local feedback. Field validation remains in the form; confirmations are not notifications. `IDialogService.Notify` is the presentation bridge to the Application service; it does not open a dialog. `ShowMessage` records the event and retains its modal presentation.

Automatic backup failure remains a separate persistent footer warning with Retry. Ordinary notifications cannot hide that warning or unlock writes. Only successful startup backup verification unlocks writes. Existing daily backups and newly created backups have different notification texts. Integrity warnings and important error dialogs retain their existing behavior. Unexpected errors in dictionary and transaction-list operations use a localized message while exceptions are logged.

Tests cover bounded session history, concurrent publication, queue timing and grouping, dispatcher delivery, backup failure/retry isolation, manual backup success without a dialog, and history rendering in both themes.
