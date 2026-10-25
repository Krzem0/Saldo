using System.Globalization;
using System.IO;
using System.Windows.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.Interfaces;

namespace Saldo.Tests.Wpf;

public sealed class ThemedErrorMessageTests
{
    [Theory]
    [InlineData(false, MessageDialogKind.Information)]
    [InlineData(true, MessageDialogKind.Error)]
    public async Task Backup_ChoosesInformationOrError(bool fail, MessageDialogKind expected)
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        h.Dialogs.BackupPath = "test-backup.db";
        var backup = new Backup(fail);
        using var provider = new ServiceCollection().AddSingleton<IDatabaseBackupService>(backup).BuildServiceProvider();
        var vm = new SettingsViewModel(h.Localization, null!, provider.GetRequiredService<IServiceScopeFactory>(),
            h.Dialogs, NullLogger<SettingsViewModel>.Instance, new FailingFolder());
        vm.CreateBackupCommand.Execute(null);
        await WaitFor(vm.CreateBackupCommand);
        Assert.Equal("test-backup.db", backup.Destination);
        if (!fail)
        {
            Assert.Empty(h.Dialogs.Messages);
            var notification = Assert.Single(h.Dialogs.Notifications);
            Assert.Equal("Backup.Manual.Created", notification.Code);
            Assert.Equal("test-backup.db", notification.Details);
            return;
        }
        var message = Assert.Single(h.Dialogs.Messages);
        Assert.Equal(expected, message.Kind);
        Assert.Equal(fail ? h.Localization["BackupFailedMessage"]
            : string.Format(h.Localization.CurrentCulture, h.Localization["BackupSuccessMessage"], h.Dialogs.BackupPath), message.Message);
    }

    [Fact]
    public async Task CancellingBackup_DoesNotShowAMessage()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var vm = new SettingsViewModel(h.Localization, null!, h.Scopes, h.Dialogs,
            NullLogger<SettingsViewModel>.Instance, new FailingFolder());
        vm.CreateBackupCommand.Execute(null);
        await WaitFor(vm.CreateBackupCommand);
        Assert.Empty(h.Dialogs.Messages);
    }

    [Fact]
    public async Task DeletePartyUsedByTemplate_ShowsWarning()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var template = h.Form();
        template.Name = "Szablon";
        template.SelectedPayer = h.Party;
        await template.SaveAsync();
        h.Dialogs.DeleteConfirmed = true;
        var vm = new PartiesViewModel(h.Scopes, h.Dialogs, h.Localization);
        vm.LoadCommand.Execute(null);
        await WaitFor(vm.LoadCommand);
        vm.SelectedItem = vm.Items.Single(p => p.Id == h.Party.Id);
        vm.DeleteCommand.Execute(null);
        await WaitFor(vm.DeleteCommand);
        Assert.Equal(MessageDialogKind.Warning, Assert.Single(h.Dialogs.Messages).Kind);
        Assert.Contains(vm.Items, p => p.Id == h.Party.Id);
    }

    [Fact]
    public async Task UnexpectedDictionaryFailure_ShowsError()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        using var provider = new ServiceCollection().BuildServiceProvider();
        // A missing required repository must be treated as an operational failure, not a duplicate warning.
        var vm = new PartiesViewModel(provider.GetRequiredService<IServiceScopeFactory>(), h.Dialogs, h.Localization);
        vm.LoadCommand.Execute(null);
        await WaitFor(vm.LoadCommand);
        Assert.Equal(MessageDialogKind.Error, Assert.Single(h.Dialogs.Messages).Kind);
    }

    [Theory]
    [InlineData("pl-PL", false)]
    [InlineData("en-US", false)]
    [InlineData("pl-PL", true)]
    [InlineData("en-US", true)]
    public async Task DuplicateParty_UsesDialogServiceAndPreservesExistingEntry(string culture, bool fromTransaction)
    {
        using var h = new TransactionTemplatesTests.Harness(culture);
        h.Dialogs.NameResult = h.Party.Name;
        ICommand command;
        AddEditTransactionViewModel? form = null;
        if (fromTransaction)
        {
            form = new AddEditTransactionViewModel(h.Scopes, h.Dialogs, h.Localization,
                [h.Category], [h.Party], [h.Location], [h.Tag]);
            command = form.AddCounterpartyCommand;
        }
        else
        {
            command = new PartiesViewModel(h.Scopes, h.Dialogs, h.Localization).AddCommand;
        }
        command.Execute(null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!command.CanExecute(null)) await Task.Delay(10, timeout.Token);
        var message = Assert.Single(h.Dialogs.Messages);
        Assert.Equal(string.Format(CultureInfo.GetCultureInfo(culture),
            h.Localization["DuplicateReferenceErrorTemplate"], h.Party.Name), message.Message);
        Assert.Equal(MessageDialogKind.Warning, message.Kind);
        Assert.Equal(fromTransaction ? h.Localization["Message_Warning"]
            : string.Format(CultureInfo.GetCultureInfo(culture), h.Localization["AddErrorTemplate"],
                h.Localization["Entity_Party"]), message.Title);
        if (form is not null)
        {
            Assert.Single(form.Parties);
            Assert.Null(form.SelectedCounterparty);
        }
    }

    [Fact]
    public void OpenDatabaseFolderFailure_UsesDialogService()
    {
        using var h = new TransactionTemplatesTests.Harness("pl-PL");
        var vm = new SettingsViewModel(h.Localization, null!, h.Scopes, h.Dialogs,
            NullLogger<SettingsViewModel>.Instance, new FailingFolder());
        vm.OpenDatabaseFolderCommand.Execute(null);
        var message = Assert.Single(h.Dialogs.Messages);
        Assert.Equal(h.Localization["BackupTitle"], message.Title);
        Assert.Equal(h.Localization["OpenDatabaseFolderFailed"], message.Message);
        Assert.Equal(MessageDialogKind.Error, message.Kind);
    }

    private sealed class FailingFolder : IDatabaseFolderService
    {
        public void OpenFolder() => throw new InvalidOperationException("Cannot open folder.");
    }

    private static async Task WaitFor(ICommand command)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!command.CanExecute(null)) await Task.Delay(10, timeout.Token);
    }

    private sealed class Backup(bool fail) : IDatabaseBackupService
    {
        public string? Destination { get; private set; }
        public Task CreateAsync(string destinationPath, CancellationToken ct = default)
        {
            Destination = destinationPath;
            return fail ? Task.FromException(new IOException("Backup failed.")) : Task.CompletedTask;
        }
    }
}
