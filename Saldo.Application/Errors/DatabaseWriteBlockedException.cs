namespace Saldo.Application.Errors;

public sealed class DatabaseWriteBlockedException()
    : InvalidOperationException("A successful startup backup is required before changing database data.");
