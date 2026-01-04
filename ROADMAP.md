# Roadmap

This roadmap is scoped for an **offline-first** personal finance tracker (Saldo) with a clean architecture core and a WPF UI.

## Phase 0 — Project Setup (Day 0)
- [ ] Create solution and projects:
  - [ ] Saldo.Domain
  - [ ] Saldo.Application
  - [ ] Saldo.Infrastructure.Sqlite
  - [ ] Saldo.Desktop.Wpf
  - [ ] Saldo.Tests
- [ ] Add basic repo files:
  - [ ] README.md
  - [ ] ARCHITECTURE.md
  - [ ] ROADMAP.md
  - [ ] LICENSE (MIT)
  - [ ] .gitignore
- [ ] Define coding conventions (nullable, analyzers, formatting)

## Phase 1 — Domain + Use Cases (MVP Core)
**Goal:** a testable core with minimal rules.
- [ ] Define domain model:
  - [ ] `Transaction` (Income/Expense, date, amount, category?, note)
  - [ ] `Category`
  - [ ] `Money` / amount rules (no negative amounts; sign handled by type)
- [ ] Define application contracts:
  - [ ] `ITransactionRepository`
  - [ ] `ICategoryRepository`
  - [ ] `IUnitOfWork` (optional)
- [ ] Implement use cases (application layer):
  - [ ] AddTransaction
  - [ ] EditTransaction
  - [ ] DeleteTransaction
  - [ ] ListTransactions (with paging/filter by month)
  - [ ] GetSummary (month totals: income, expense, balance)
- [ ] Unit tests for domain rules + use cases

## Phase 2 — SQLite Persistence
**Goal:** reliable local storage.
- [ ] Choose persistence approach (one):
  - [ ] EF Core + SQLite
  - [ ] Dapper + migrations
- [ ] Implement SQLite schema + migrations:
  - [ ] Transactions table
  - [ ] Categories table
- [ ] Implement repositories in `Saldo.Infrastructure.Sqlite`
- [ ] Integration tests against a temp SQLite file

## Phase 3 — WPF UI (MVP)
**Goal:** usable app for day-to-day tracking.
- [ ] WPF shell setup + MVVM:
  - [ ] Navigation (simple: tabs/pages)
  - [ ] DI setup (Microsoft.Extensions.DependencyInjection)
- [ ] Screens:
  - [ ] Transactions list (current month default)
  - [ ] Add/Edit transaction dialog
  - [ ] Categories management (basic)
  - [ ] Monthly summary view
- [ ] UX basics:
  - [ ] Validation messages
  - [ ] Keyboard-friendly input
  - [ ] Sorting/filtering on list

## Phase 4 — Quality + Ops (Local App)
**Goal:** “production-grade” hygiene.
- [ ] Structured logging (Serilog or built-in)
- [ ] Global error handling + user-friendly error dialog
- [ ] Config:
  - [ ] DB path location
  - [ ] Backup folder
- [ ] Packaging:
  - [ ] MSIX or simple installer (optional)

## Phase 5 — Nice-to-haves
- [ ] Recurring transactions
- [ ] CSV import/export
- [ ] Tags + advanced filters
- [ ] Backup/restore to a single file
- [ ] Charts (monthly trend)

## Phase 6 — Second UI (WinUI 3) (Optional)
**Goal:** reuse the same core with a different desktop frontend.
- [ ] Create `Saldo.Desktop.WinUI`
- [ ] Reuse Application + Infrastructure via DI
- [ ] Rebuild screens with WinUI components
