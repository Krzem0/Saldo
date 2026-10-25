namespace Saldo.Application.Interfaces;

public interface IDatabaseWriteAccess
{
    bool CanWrite { get; }
    void EnsureCanWrite();
}
