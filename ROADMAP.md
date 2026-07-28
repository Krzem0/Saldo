# Roadmap

This roadmap is scoped for an offline-first personal finance tracker (Saldo) with a clean architecture core and a WPF UI.

## Phase 0 - Project Setup

- [x] Create solution and projects:
  - [x] Saldo.Domain
  - [x] Saldo.Application
  - [x] Saldo.Infrastructure.Sqlite
  - [x] Saldo.Desktop.Wpf
  - [x] Saldo.Tests
- [x] Add basic repo files:
  - [x] README.md
  - [x] ARCHITECTURE.md
  - [x] ROADMAP.md
  - [x] LICENSE (MIT)
  - [x] .gitignore
- [x] Define coding conventions (nullable, analyzers, formatting)

## Phase 1 - Domain + Use Cases

Goal: a testable core with minimal but real business rules.

- [x] Define domain model:
  - [x] `Transaction`
  - [x] `Category`
  - [x] `Party`
  - [x] `Location`
  - [x] `TransactionType`
- [x] Define application contracts:
  - [x] `ITransactionRepository`
  - [x] `ICategoryRepository`
  - [x] `IPartyRepository`
  - [x] `ILocationRepository`
  - [ ] `IUnitOfWork` (optional)
- [x] Implement use cases:
  - [x] AddTransaction
  - [x] EditTransaction
  - [x] DeleteTransaction
  - [x] ListTransactions
  - [x] GetSummary
  - [x] GetNewTransactionDefaults
- [x] Keep business defaults and validation outside the GUI
  - [x] FluentValidation command validators in Application
  - [x] Shared transaction rules reused by add/edit validation
  - [x] Stable validation error codes with property metadata
- [x] Unit tests for core behavior

## Phase 2 - SQLite Persistence

Goal: reliable local storage.

- [x] Choose persistence approach:
  - [x] EF Core + SQLite
  - [ ] Dapper + migrations
- [x] Implement SQLite schema and mappings:
  - [x] Transactions
  - [x] Categories
  - [x] Parties
  - [x] Locations
- [x] Keep the initial migration aligned with the current early-stage model
- [x] Implement repositories in `Saldo.Infrastructure.Sqlite`
- [x] Integration tests against a temporary SQLite database

## Phase 3 - WPF UI

Goal: usable app for day-to-day tracking.

- [x] WPF shell setup + MVVM
- [x] DI setup
- [x] Resource-based localization
- [x] Default app culture based on the system culture
- [x] Screens:
  - [x] Transactions list
  - [x] Add/Edit transaction dialog
  - [x] Categories management
  - [x] Parties management
  - [x] Locations management
- [x] UX basics:
  - [x] Validation messages localized and displayed next to affected fields
  - [x] Form-level summary for errors that cannot be assigned to a field
  - [x] All applicable errors collected during one save attempt
  - [x] Save remains available so validation can explain incomplete input
  - [x] Keyboard-friendly input
  - [x] Autocomplete for dictionary-backed fields
- [x] Dictionary behavior:
  - [x] Category selected from existing values only
  - [x] Party can be added inline from transaction entry
  - [x] Location can be added inline from transaction entry
- [ ] Sorting/filtering on the transaction list
- [ ] Dedicated monthly summary screen

## Phase 4 - Quality + Local App Ops

Goal: production-grade hygiene for a local desktop app.

- [x] Structured logging
- [x] Global error handling with user-friendly dialogs
- [x] Configurable DB path location
- [ ] Backup folder configuration
- [ ] Packaging / installer

## Phase 5 - Nice-to-haves

- [ ] Recurring transactions
- [ ] CSV import/export
- [ ] Tags + advanced filters
- [ ] Backup/restore to a single file
- [ ] Charts / trends

## Phase 6 - Second UI (Optional)

Goal: reuse the same core with another frontend.

- [ ] Create a second desktop frontend
- [ ] Reuse Application + Infrastructure via DI
- [x] Keep transaction validation, defaults, and dictionary rules frontend-agnostic
- [ ] Reuse stable validation codes and property metadata in the second frontend
