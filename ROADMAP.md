# Roadmap

This document tracks planned work. Implemented behavior and architectural decisions belong in `README.md`, `ARCHITECTURE.md`, and Git history.

## Next

- [ ] Add filters to the existing monthly transaction list (scalar-column header sorting with direction arrows and default date-descending order are already implemented)
- [ ] Dedicated monthly summary screen
- [ ] Add a separate all-transactions history tab alongside the existing monthly transaction tab, with date-range selection, search, filters, database-side sorting, and pagination

## App Reliability

- [ ] Persist UI preferences, including language and appearance theme (the manually configured default payer is already persisted separately in TransactionSettings)
- [ ] Persist a preferred backup folder
- [ ] Restore from a single-file database backup (manual backup creation is available in Settings)
- [ ] Packaging and installer

## Later

- [ ] Further WPF visual polish and accessibility review
- [ ] Recurring transactions
- [ ] CSV import and export
- [ ] Add tag filtering and summaries (tag management, transaction selection, display, and draft preservation are implemented)
- [ ] Charts and trends
- [ ] Add configurable currency selection and define its effect on amount display and summaries (currency selection is not implemented; amount inputs currently display no currency symbol)

## Second UI

- [ ] Create a second frontend reusing Application and Infrastructure through DI
- [ ] Reuse stable validation codes and property metadata
- [ ] Recreate documented presentation conventions, including drafts, amount/type rendering, and category colors/icons
