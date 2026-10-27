# Accounting periods

Status: persistence, close/reopen operations and GUI controls, closed-period write
protection, atomic closing snapshots, and integrity verification implemented.
Automatic backups are implemented; see [Automatic backups](automatic-backups.md).

## Current behavior

- Only calendar months are supported. There is no cycle setting or cycle history.
- `CalendarMonth` in Domain calculates the inclusive `StartDate` and exclusive
  `EndDateExclusive`. January 2026 is `[2026-01-01, 2026-02-01)`.
- Transaction month queries use the same boundaries: `Date >= StartDate` and
  `Date < EndDateExclusive`.
- Calculating or browsing a month never creates a database record.
- A transaction insert or update ensures that its destination month has an open
  accounting period if none exists. Period creation and the transaction write
  share one database transaction; a failed write leaves no new period.
- Existing periods are never replaced or reset by transaction writes.
- Gaps are intentional. Saving transactions in January and April creates only
  those two periods. February and March can still be browsed normally.
- Moving or deleting the final transaction does not remove its original period.
- Transactions belong to periods by date; there is no `PeriodId` foreign key.
- An absent period is interpreted as open. Transaction writes create open periods;
  explicitly closing an absent month creates a closed period without filling gaps.

## Closing, reopening, and write protection

- `CloseAccountingPeriod.ExecuteAsync(year, month)` closes an existing or empty
  month. Repeated closing is idempotent.
- `ReopenAccountingPeriod.ExecuteAsync(year, month)` explicitly permits writes
  again. Reopening an absent or already open month is an idempotent no-op.
- Invalid month boundaries return the stable `AccountingPeriod.InvalidMonth`
  error rather than an argument exception.
- `AddTransaction`, `EditTransaction`, and `DeleteTransaction` require an
  `IAccountingPeriodRepository` and check the affected periods before writing.
- Adding, editing any fields (including tag assignments), or deleting a
  transaction in a closed period returns `AccountingPeriod.Closed`.
- Changing a date requires both the persisted source month and the destination
  month to be open. A rejected move does not create a destination period or
  alter the original transaction or its tags.
- SQLite repositories repeat the checks inside non-deferred write transactions,
  serializing transaction writes with period state changes. The source date is
  read from the database, even if the caller has already changed a tracked entity.
- A close occurring after the Application precheck is reported by persistence
  through `AccountingPeriodClosedException`, which the use case maps to the same
  validation result. It does not escape into the GUI as an unexpected exception.
- Closed-period messages are localized in Polish and English through existing
  form-level validation and deletion-warning handling.
- Period read queries are no-tracking, so they return current state even when
  the context contains an older tracked entity.

Closing an open (or absent) month captures totals, count, hash, and version in
the same write transaction as `IsClosed`. Repeated closing does not overwrite
the reference, even if it is incomplete or no longer matches the data.
Reopening retains the old snapshot. Only the next explicit transition from open
to closed captures a fresh snapshot.

## Monthly GUI

- The month-navigation row shows the selected month's open/closed state, or a checking,
  changing, or unknown-state message. Open periods use green, closed periods use
  blue, with theme-aware backgrounds and text. The text labels also identify the
  state independently of color. A second line appears only for integrity problems
  or failed reads; successful verification has no extra message.
- State and action share one colored, outlined group, separated internally by a
  divider, to the left of the centered month picker and above the summary bar.
- While a month loads, changes state, or has unknown state, the entire state/action
  group is hidden with its layout space retained. It appears only after the state
  is established. A new selection clears the previous presentation before changing
  the displayed month; unknown state never implies an open period. Problem text
  and retry remain available separately if reading the state fails.
- The action button switches between **Close month** and **Reopen month** based
  on the loaded state. **Check again** reloads data and status and appears only
  when a problem is reported; there is no always-visible refresh button.
- Closing and reopening require an owned, themed confirmation dialog naming the
  month and explaining the effect. Cancel is the default Enter/Escape action;
  cancellation writes nothing and does not create an absent period.
- After a successful operation, the monthly data and integrity result reload.
  There is no separate success popup; the visible state/action label changes.
- Edit and Delete are disabled for a closed or unknown period, during loading,
  and while changing state. Application/SQLite protections remain authoritative
  for a period closed externally or after a form was opened.
- Row-context **Edit transaction** and left-button double-click invoke the same
  Edit command as the footer and respect those availability rules. Right-click
  selects the target transaction before showing the menu. Double-clicking a row
  action button, a column header, or empty table space does not open editing.
- Add, copy, and template actions remain available for a loaded closed month:
  users can prepare a new transaction and choose an open destination date. A
  save with a closed date returns the existing localized validation error.
  Editing a transaction from a closed source cannot move it to an open month.
- During loading or a state change, new transaction/template/copy actions are
  disabled. During a state change, navigation, refresh, and repeat state changes
  are also blocked. Closing/reopening preserves existing new-form drafts.
- Disabled previous/next month arrows use the current theme's resources, so
  closing/reopening does not introduce light-only colors in the dark theme.
- Startup backup availability also gates these commands, including copying from
  the row context menu. Normal application exit waits for a pending state change
  and its resulting automatic backup.
- A failed state operation leaves the visible state unknown; the user can refresh
  to read the actual database state before another change. A failed verification
  also prevents editing/deleting and changing state until a successful reload.
- Navigation starts a new load even if a previous load command is still busy.
  Only the newest selection may update rows, state, and warning messages.
- Switching language updates period and action labels without another database
  read. All labels, confirmations, and errors support Polish and English.
- Closing an edit form refreshes the view even when cancelled, so an external
  period closure is reflected after a rejected save.

## Integrity verification

- `VerifyAccountingPeriod.ExecuteAsync(year, month)` returns a typed result:
  `Open`, `Valid`, `Mismatch`, `MissingSnapshot`, or `UnsupportedHashVersion`.
- Open/absent months require only an indexed state lookup and no transaction scan.
- A closed month is read with its transactions and tag assignments in one
  deferred SQLite read transaction. Concurrent writes cannot mix different
  database snapshots during comparison. All entity reads are no-tracking.
- Full verification reads and hashing run in a worker task. Each operation must
  have exclusive use of its scoped repository/DbContext until it completes.
- Differences identify income total, expense total, transaction count, and data
  hash independently. The balance is derived from totals.
- Missing/partial snapshots and unknown hash versions are never reported as
  valid. Older closed periods without a snapshot are left intact for review,
  rather than silently given a new baseline.
- Unexpected read/calculation failures are logged and returned as
  `AccountingPeriod.VerificationFailed`; cancellation propagates to the caller.
- The monthly GUI verifies on load/reload, including month navigation. It shows
  localized warnings for mismatches, missing snapshots, or unsupported versions,
  and an error for failed verification. It never repairs or overwrites a snapshot.
- Navigation ignores stale load results and warnings from a previously selected
  month. A valid/open result needs no popup.

### Hash format, version 1

SHA-256 covers a format marker, hash version, month start/end day numbers,
transaction count, and transactions sorted by ID. Each transaction includes ID,
date, type, amount, category/payer/counterparty/location IDs, exact description,
tag count, and sorted tag IDs. Dictionary names, colors, and icons are excluded,
so renaming a reference does not invalidate closed transactions.

Integers use BinaryWriter's little-endian representation; strings use its
length-prefixed UTF-8 encoding; nullable IDs have a presence marker. Dates are
`DateOnly.DayNumber`, and decimals use invariant `G29`, so language and decimal
scale do not alter the hash. The digest is lowercase hexadecimal. The payload is
streamed into SHA-256 rather than assembled into a second in-memory copy.
Changing this format requires a new hash version. Tests pin a version-1 fixture
and cover all stored transaction fields, ordering, culture, and decimal scale.

## Stored data

`AccountingPeriods` contains `Id`, `StartDate`, `EndDateExclusive`, `IsClosed`,
`ClosedIncomeTotal`, `ClosedExpenseTotal`, `ClosedTransactionCount`,
`ClosedDataHash`, and `HashVersion`.

The closing snapshot columns are nullable and remain empty when an open period
is created by a transaction write. Closing populates all of them, including zero
totals/count and a deterministic hash for an empty month. Transaction writes do
not maintain these fields. The period balance is income minus expenses rather
than an independent stored value.

A SQLite check constraint requires exact calendar-month boundaries. A unique
index on `StartDate` prevents duplicate months; together these rules prevent
overlaps. December 9999 cannot be represented because its exclusive end is
outside the `DateOnly` range and is rejected.

## Existing databases

The incremental `AddAccountingPeriods` migration creates the table and inserts
one open period for each distinct month already containing transactions. It
does not fill gaps, modify transactions, or close historical months. Fresh
databases without transactions start with no periods.

The earlier initial migration is unchanged. Startup applies migrations through
the existing `MigrateAsync` workflow. As with other schema upgrades, keep a
manual backup before first starting the upgraded app against an existing database.

## Next stages

The [integration verification report](../verification/accounting-periods-and-backups.md)
records the final automated checks and practical limits of the verification.

- Other accounting cycles only when their transition rules have been designed.
