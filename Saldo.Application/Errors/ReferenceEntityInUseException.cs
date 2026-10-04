namespace Saldo.Application.Errors;

public sealed class ReferenceEntityInUseException : Exception
{
    public ReferenceEntityInUseException()
        : base("The reference entity is used by existing transactions.") { }

    public ReferenceEntityInUseException(Exception innerException)
        : base("The reference entity is used by existing transactions.", innerException)
    {
    }
}
