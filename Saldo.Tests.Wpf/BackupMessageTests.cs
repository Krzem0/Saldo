using System.ComponentModel;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Application.Interfaces;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;

namespace Saldo.Tests.Wpf;

public sealed class BackupMessageTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Backup_UsesDialogServiceForSuccessAndFailure_AndCancellationShowsNothing(bool cancelled, bool fail)
    {
        var backup = new Backup(fail);
        using var provider = new ServiceCollection().AddSingleton<IDatabaseBackupService>(backup).BuildServiceProvider();
        var dialogs = new TransactionTemplatesTests.TestDialogs { BackupPath = cancelled ? null : @"C:\backups\saldo.db" };
        var localization = new LocalizationService();
        var vm = new SettingsViewModel(localization, new Theme(), provider.GetRequiredService<IServiceScopeFactory>(),
            dialogs, NullLogger<SettingsViewModel>.Instance, new Folder());
        vm.CreateBackupCommand.Execute(null);
        Assert.Equal(!cancelled, backup.Called);
        if (cancelled) Assert.Empty(dialogs.Messages);
        else
        {
            var message = Assert.Single(dialogs.Messages);
            Assert.Equal(localization["BackupTitle"], message.Title);
            Assert.Equal(fail ? localization["BackupFailedMessage"]
                : string.Format(localization.CurrentCulture, localization["BackupSuccessMessage"], dialogs.BackupPath), message.Message);
        }
    }
    private sealed class Backup(bool fail) : IDatabaseBackupService
    {
        public bool Called { get; private set; }
        public Task CreateAsync(string destinationPath, CancellationToken ct = default)
        {
            Called = true;
            return fail ? Task.FromException(new IOException("Backup failed")) : Task.CompletedTask;
        }
    }
    private sealed class Theme : IThemeService
    {
        public AppearanceTheme SelectedTheme { get; set; }
        public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
    }
    private sealed class Folder : IDatabaseFolderService { public void OpenFolder() => throw new NotSupportedException(); }
}
