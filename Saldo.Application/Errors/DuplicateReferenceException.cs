namespace Saldo.Application.Errors;

public sealed class DuplicateReferenceException : Exception
{
    public DuplicateReferenceException(string entityName, string name)
        : base($"A {entityName} named '{name}' already exists.")
    {
        EntityName = entityName;
        Name = name;
    }

    public string EntityName { get; }
    public string Name { get; }
}
