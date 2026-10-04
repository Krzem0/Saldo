using System.Diagnostics;

namespace Saldo.Desktop.Wpf.Services;

public sealed class WindowsDatabaseFolderService(string databaseDirectory) : IDatabaseFolderService
{
    public void OpenFolder()
    {
        // Opening Explorer is a Windows presentation concern; use the same folder as DI's database configuration.
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = databaseDirectory,
            UseShellExecute = true
        });
    }
}
