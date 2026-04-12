# Saldo

**Saldo** is an offline desktop application for tracking personal income and expenses using a local SQLite database.

## Goals
- Simple and fast personal finance tracking
- Fully offline — no cloud, no accounts
- Clear data ownership (your data stays local)
- Educational project built with production-grade practices

## Scope
- Income and expense records
- Basic categorization
- Balance and simple summaries
- Local persistence (SQLite)

## Tech Stack (planned)
- .NET 10
- WPF desktop UI (MVVM)
- SQLite local persistence

## Error Handling and Validation
- Business validation is handled in the Application layer.
- Use `Result<T>` for expected validation failures when a use case should return success/failure without throwing.
- Reserve exceptions for unexpected technical failures.

## Logging
- The WPF shell uses `Microsoft.Extensions.Logging` with Serilog.
- Logs are written to `%AppData%\Saldo\Logs\saldo-.log` with daily rolling files.
- Use `ILogger<T>` for technical diagnostics; keep expected validation failures in `Result<T>`.

## Status
🚧 Work in progress — early development / learning project

## License
MIT
