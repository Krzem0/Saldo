# Accounting periods

Status: persistence, Application close/reopen operations, and closed-period write
protection implemented. Closing snapshots, integrity verification, and period
controls in the GUI are not implemented yet.

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

These operations currently change only `IsClosed`. They do not calculate totals
or hashes. Reopening retains any existing snapshot, and this stage's closing
operation also leaves snapshot fields untouched. The next stage will capture
a fresh snapshot atomically on a transition to closed. GUI close/reopen buttons
will be introduced separately; registration of these use cases does not add
new actions to the current monthly view.

## Stored data

`AccountingPeriods` contains `Id`, `StartDate`, `EndDateExclusive`, `IsClosed`,
`ClosedIncomeTotal`, `ClosedExpenseTotal`, `ClosedTransactionCount`,
`ClosedDataHash`, and `HashVersion`.

The closing snapshot columns are nullable and remain empty when a period is
created. No totals, counts, or hashes are maintained by transaction writes.
The period balance will be calculated as income minus expenses rather than
stored independently.

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

- Atomic closing with stored totals, count, versioned deterministic SHA-256 hash;
  integrity verification of closed periods.
- GUI state and close/reopen actions.
- Background automatic backups with one file per day and retention of five
  successful backup days, replacing today's file after a successful close.
- Other accounting cycles only when their transition rules have been designed.
