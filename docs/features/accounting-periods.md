# Accounting periods

Status: persistence foundation implemented. Closing, reopening, integrity verification,
and period controls in the GUI are not implemented yet.

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
- An absent period is interpreted as open. All periods created by the current
  workflow are open; closed-period enforcement belongs to the next implementation stage.

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

- Application-level add/edit/delete protection, including source and destination
  periods when changing a transaction date.
- Atomic closing with stored totals, count, versioned deterministic SHA-256 hash;
  explicit reopening and integrity verification of closed periods.
- GUI state and close/reopen actions.
- Background automatic backups with one file per day and retention of five
  successful backup days, replacing today's file after a successful close.
- Other accounting cycles only when their transition rules have been designed.
