namespace Saldo.Application.Errors;

public sealed class InvalidTagNameException : Exception
{
    public InvalidTagNameException() : base("Tag name must contain between 1 and 50 characters.") { }
}
