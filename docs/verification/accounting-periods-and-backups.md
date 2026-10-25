# Accounting periods and backups: integration verification

Verification date: 2026-10-25. Scope: the six-stage accounting-period and automatic-backup implementation.

## Result

The complete solution passed: **450 tests** (97 unit, 123 SQLite integration,
230 WPF). Seven regression/workflow checks were added during this review.
`git diff --check` found no whitespace errors. No new schema migration was required.

All database operations used disposable temporary SQLite files. The user's
application database and existing backup files were not opened or modified.

## End-to-end check

`PeriodWorkflowVerificationTests` creates a fresh database through the actual
incremental migration chain and exercises the Application use cases:

1. Reject a transaction while the startup write gate is closed.
2. Create the daily startup copy, then allow writes.
3. Add income and expense transactions and create their accounting period.
4. Close the month, verify the captured snapshot, and replace the daily copy.
5. Reject add/edit/delete attempts in that closed month.
6. Reopen, retain the old snapshot, and edit the expense.
7. Confirm that reopening/editing did not replace the daily backup.
8. Close again, replace the same daily file, then verify the copied database's
   new totals, count, balance, and hash using the verification use case.

The broader suite covers gaps, empty months, month boundaries, migration of
historical transactions, exact decimal totals, deterministic hashing, changed
amounts/descriptions/tags/references, missing/unsupported snapshots, concurrent
reads, retention, startup retry, failure preservation, and themed/localized WPF views.

## Review fixes

- **Source/copy race:** checking the source before copying did not guarantee that
  the file still contained a valid closed month. The temporary copy is now verified
  before replacement. Regression cases reopen a month or change a description
  after the source precheck; the previous daily file must remain byte-for-byte intact.
- **Exit during background work:** ordinary window closing now waits for an
  unfinished period change and its subsequently enqueued automatic backup. Idle
  waiters include queued requests and complete after failures as well as success.
- **Context-menu startup gate:** transaction command availability now checks the
  startup backup state, protecting row-context copying as well as toolbar controls.
- **Same-context edit:** an earlier inserted transaction could remain tracked while
  a no-tracking query supplied another instance for editing. The repository now
  detaches the same-ID tracked instance before attaching the update. The full
  lifecycle test covers this path without changing unrelated tracked entities.

## Performance observation

One successful local Debug-run sample used **5,000 transactions and 10,000 tag
assignments** in a temporary SQLite database:

| Operation | Elapsed |
|---|---:|
| Close and capture snapshot | 83.5 ms |
| Read and verify closed month | 80.6 ms |

This is a smoke measurement, not a performance threshold or a hardware-independent
benchmark. Verification time includes its database reads and hash computation;
it excludes the monthly table's separate queries, rendering, and navigation overhead.
Open months skip the transaction scan. Closed-period verification runs on a worker;
WPF tests confirm that the dispatcher can continue while verification is paused.

## Limits and remaining work

- Full application interaction against the user's real database was deliberately
  excluded. The shutdown wait contract and GUI command behavior are automated
  tests; forced process termination and OS shutdown were not exercised.
- Backup restoration remains a separate roadmap item. This review checks that
  copied databases can be opened and verified, not a restore/import user workflow.
- Build output still reports the existing NU1903 advisory for transitive
  `SQLitePCLRaw.lib.e_sqlite3` 2.1.11. Dependency upgrading was not part of this
  verification and that warning remains unresolved.
- The build uses the installed preview .NET SDK while targeting .NET 10, and
  retains the existing documentation/analyzer configuration warnings.
