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
- [x] Load a template into an already open new-transaction form through a searchable picker beside Description, preserving the date.
- [ ] Consider creating templates from existing saved transactions.
- [x] Duplicate a transaction through the row context menu into a new-transaction form with its fields prefilled, except for an empty amount that must be entered, and the usual new-form date, allowing changes before saving a separate transaction without creating a template or modifying the original.
- [ ] Replace the full transaction tag cloud with a dropdown for choosing tags; display only selected tags below it using the current chip appearance (the current form still displays all available tags as selectable chips)
- [ ] Further WPF visual polish and accessibility review
- [ ] Recurring transactions
- [x] Calendar-month accounting-period persistence foundation: shared inclusive-start/exclusive-end boundaries, on-demand creation during transaction writes, intentional gaps, and incremental migration of occupied historical months. Closing snapshot columns are reserved but not populated yet. See [Accounting periods](docs/features/accounting-periods.md).
- [x] Accounting-period Application close/reopen operations and transaction-write protection: block add/edit/delete and tag reassignment in closed months; check both source and destination for date changes; repeat checks atomically in SQLite and return localized validation errors. Closing snapshots and GUI controls remain separate stages.
- [ ] [Medium priority] Configurable accounting periods: keep calendar months as the default, and allow weekly, biweekly (every two weeks), and monthly periods with a configurable start weekday or anchor date/day of month. Let users match their own pay cycle rather than infer it from their country. Persist the setting and use consistent period boundaries in transaction lists, summaries, and period navigation. Define how this interacts with the planned month closing/reopening feature before implementation.
- [ ] Finish month closing and reopening: capture totals, transaction count, and a versioned data hash atomically at closing; verify closed periods; add visible period status and explicit close/reopen actions in the GUI with appropriate command availability. Application operations and atomic transaction-write protection are already implemented.
- [ ] [Low priority] Budget planning: explore setting a planned spending limit for an accounting period and comparing actual expenses against it, including the remaining budget. Priority is below configurable accounting periods and above transaction line items. The workflow and how it fits the current Saldo implementation are still undecided; discuss the scope and UI before implementation, including how it relates to configurable periods and the existing income-minus-expenses balance.
- [ ] [Very low priority] Optional transaction line items: allow a single transaction to be split into multiple amounts with their own categories and tags, while retaining one transaction total. Revisit only if a concrete need emerges: years of personal expense tracking since the original Walter idea have shown little need for this level of detail.
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
- [ ] Add an optional console interface (CLI) alongside the Windows WPF interface, reusing Application use cases, validation, defaults, and SQLite Infrastructure through DI. Both interfaces should operate on the same selected database, letting users choose windows or console commands without migrating data. Start by defining commands for adding income/expenses, listing transactions, and viewing period summaries; support use from scripts and define clear output and exit codes. The CLI can be used independently without opening the GUI.
- [ ] Reuse stable validation codes and property metadata
- [ ] Recreate documented presentation conventions, including drafts, amount/type rendering, and category colors/icons
