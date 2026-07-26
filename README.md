# Saldo

**Saldo** is an offline desktop application for tracking personal income and expenses using a local SQLite database.

## Goals

- Simple and fast personal finance tracking
- Fully offline - no cloud, no accounts
- Clear data ownership (your data stays local)
- Educational project built with production-grade practices

## Current Scope

- Income and expense transactions
- Monthly transaction list and summary
- Categories managed as a controlled dictionary
- Parties managed as a reusable dictionary
- Locations managed as a reusable dictionary
- Local persistence with SQLite
- UI localization based on resource files
- Default app language based on the system culture

## UX Rules Worth Knowing

- `Category` is selected from existing values only, with autocomplete support
- `Party` and `Location` use autocomplete, but can also be created inline while saving a transaction
- New transaction defaults are resolved outside the GUI, in the Application layer
- User-facing labels are localized, while domain values remain stable in English
- Transaction form validation is displayed next to the relevant field; errors that cannot be assigned to a field are shown in a form-level summary

## Tech Stack

- .NET 10
- WPF desktop UI (MVVM)
- EF Core + SQLite
- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Logging + Serilog
- FluentValidation
- FluentResults

## Error Handling and Validation

- Business validation is defined with FluentValidation and always executed at the Application use-case boundary
- The UI handles input-format concerns that exist only in a text-based form, such as parsing an amount to `decimal`
- Validation failures carry stable error codes and, where applicable, the affected property name
- The UI localizes these codes and displays field errors inline, with a form-level summary as a fallback
- Use `Result<T>` for expected validation failures when a use case should return success/failure without throwing
- Reserve exceptions for unexpected technical failures

## Logging

- The WPF shell uses `Microsoft.Extensions.Logging` with Serilog
- Logs are written to `%AppData%\Saldo\Logs\saldo-.log` with daily rolling files
- Use `ILogger<T>` for technical diagnostics; keep expected validation failures in `Result<T>`

## Localization

- The WPF UI uses resource-based translations
- Supported cultures currently include `pl-PL` and `en-US`
- The app chooses its default language from the current system culture at startup
- Some seed data is culture-aware, for example the initial self party value (`Ja` / `Me`)
- New user-facing text should be added through localization resources instead of hardcoded strings

## Status

Work in progress - early development / learning project

## License

MIT
