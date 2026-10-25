# Automatic database backups

Status: implemented. Restore from a backup remains a separate planned feature.

## Files and retention

- A database at `folder/saldo.db` uses `folder/AutoBackups/`.
- The daily filename is `saldo.db.auto-yyyy-MM-dd.db`. The complete source
  filename is included, so multiple databases in the same folder have distinct
  sets of automatic backups.
- Dates use the machine's local calendar day, independently of UI language.
- Retention keeps the current backup and the four most recent other backup days.
  Days without application use need no files; a long break does not expire every
  previous backup. The newly checked/created day is kept even after a clock rollback.
- Only filenames with the exact database prefix, valid date, and `.db` suffix
  participate. Manual backup names, other databases, and unrelated files are untouched.
- Cleanup runs only after a complete new copy or a successful check of today's
  existing copy. Cleanup failure is reported separately: the successful backup
  remains usable and startup writes can be enabled. Retry attempts cleanup again.

## Startup

1. Before the main window exists, startup applies migrations and seeds initial
   data using a bootstrap context without the write gate.
2. The window opens with write controls disabled; page navigation, reads,
   searching, and manual backup creation remain available.
3. The automatic service checks today's file in a worker task. An existing file
   must pass SQLite `quick_check` and contain the Transactions and AccountingPeriods
   tables. A missing, empty, corrupted, or unrelated SQLite file is replaced.
4. A valid existing copy is retained unchanged. A second launch on the same day
   does not replace it with later transaction data.
5. Success enables writes. Failure leaves the app read-only, with a persistent
   localized status and a Retry button. Failure is not marked as a completed backup.

This is a startup check, not a midnight timer or an operating-system scheduled
job. If the app stays open overnight, the next month-closing backup uses the new
local day; otherwise the next launch performs the next daily check.

The startup copy is made after schema migration/seed, not before upgrading an
existing database. Keep the existing manual-backup procedure before an upgrade.

## After month closing

- A successful close/reload requests another automatic copy in the background.
- Before replacement, the service checks that the requested month is still
  closed and its saved snapshot matches current data. Failed verification,
  missing snapshots, or reopened months cannot silently produce a replacement.
- The copy replaces today's file; multiple closures still leave one daily file.
- Reopening and ordinary integrity checks do not request a new backup.
- Copy failure does not roll back the saved close or revoke write access. The
  status explains the separate backup failure; retry remembers the requested month
  and verifies it again. A later successful copy clears the pending failure.
- No additional permanent pre-closing copy is retained. Replacing today's file
  intentionally discards the morning version; earlier daily files remain subject
  to the five-day retention rule.

## Implementation

- Application defines `IAutomaticDatabaseBackupService`, `AutomaticBackupResult`,
  and `IDatabaseWriteAccess` without filesystem or WPF dependencies.
- SQLite Infrastructure implements the automatic service with a captured
  connection string, independent connections, and an injectable `TimeProvider`.
  A per-database semaphore serializes automatic requests within the process,
  including across service instances.
- The shared low-level backup service also serializes manual and automatic
  snapshot creation for the same source database within the process.
- SQLite's online backup captures committed data, including WAL contents. A
  temporary destination is switched to DELETE journal mode and checked before
  replacing the final path. Failed replacements retain the previous copy and
  remove the temporary file.
- `AutomaticBackupViewModel` owns the startup write gate, status, request queue,
  and retry. A failure is logged; automatic operations do not show success popups.
- Production scoped DbContexts receive the write gate. Both SaveChanges paths
  and repository bulk/raw writes enforce it. Tests and design-time/bootstrap
  contexts can be explicitly constructed without the desktop startup gate.
- `DatabaseWriteGuard` binds write controls/panels to the gate without disabling
  read-only lists or navigation. The database-side gate also rejects writes invoked
  programmatically before startup backup success.
- Closing starts the backup independently of its UI command lifetime, so navigation
  and other operations can resume while the copy completes.

## Verification and limitations

Integration tests use isolated temporary databases and a controlled local clock.
They cover same-day restart behavior, concurrent requests, five-day retention
with gaps, per-database filenames, post-close snapshot content, corrupted files,
locked destinations, failed retention/retry, and database write protection.
WPF tests cover disabled write controls, retry, successful close/reopen hooks,
and preservation of closed state after a copy failure.

Backups adjacent to the database protect against accidental changes; they share
the source disk's failure risk. Manual copies to another location remain available.
The hash detects changes but does not identify or restore individual changed rows.
