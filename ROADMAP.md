# Roadmap

This document tracks planned work. Implemented behavior and architectural decisions belong in `README.md`, `ARCHITECTURE.md`, and Git history.

## Next

- [ ] Add filters to the existing monthly transaction list (scalar-column header sorting with direction arrows and default date-descending order are already implemented)
- [ ] Dedicated monthly summary screen
- [ ] Add a separate all-transactions history tab alongside the existing monthly transaction tab, with date-range selection, search, filters, database-side sorting, and pagination

## App Reliability

- [ ] Persist UI preferences, including language and appearance theme (the default payer and location are already persisted separately in TransactionSettings)
- [ ] Persist a preferred backup folder
- [ ] Restore from a single-file database backup (manual backup creation is available in Settings)
- [ ] Packaging and installer

## Later

- [x] Select a named template from the Add transaction split button, with name search and scrolling. It opens a new form with template fields, optional amount, and the current new-transaction date, preserving any existing draft.
- [x] Save the new-transaction form as a named template without saving the transaction.
- [ ] Consider creating templates from existing saved transactions.
- [ ] Replace the full transaction tag cloud with a dropdown for choosing tags; display only selected tags below it using the current chip appearance (the current form still displays all available tags as selectable chips)
- [ ] Further WPF visual polish and accessibility review
- [ ] Recurring transactions
- [ ] CSV import and export
- [ ] Add tag filtering and summaries (tag management, transaction selection, display, and draft preservation are implemented)
- [ ] Charts and trends
- [ ] Add configurable currency selection and define its effect on amount display and summaries (currency selection is not implemented; amount inputs currently display no currency symbol)
- [ ] Add an optional payment method to transactions, for example card, cash, BLIK, or bank transfer. Consider filtering and summaries by payment method.

## Topics for Later Discussion

These are open questions retained from the early project notes, not commitments to implement. Discuss their usefulness and scope before deciding on changes.

- Soft delete and hard delete: decide whether transactions and dictionary entries need both options, and how deleted entries would be restored or permanently removed. Current deletion is permanent, and dictionary entries used by transactions cannot be deleted.
- Data change audit: decide whether to keep a history of transaction and dictionary edits/deletions, and what information it should contain. Git history documents project changes, not changes to user data.
- Expenses funded from savings: decide whether to distinguish them from current monthly spending and how they should affect monthly summaries. A tag can mark such expenses today, but does not change summary calculations.

## Second UI

- [ ] Create a second frontend reusing Application and Infrastructure through DI
- [ ] Reuse stable validation codes and property metadata
- [ ] Recreate documented presentation conventions, including drafts, amount/type rendering, and category colors/icons
