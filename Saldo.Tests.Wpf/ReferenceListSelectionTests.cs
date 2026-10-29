using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Entities;

namespace Saldo.Tests.Wpf;

public sealed class ReferenceListSelectionTests
{
    [Fact]
    public void Add_SuggestsNamesFromEntireDictionaryDespiteActiveSearch() => OnSta(() =>
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var dialogs = new UnusedDialogs();
        var vm = new TestReferenceList(provider.GetRequiredService<IServiceScopeFactory>(), dialogs)
        {
            Rows = [new Tag { Id = 1, Name = "Zakupy" }, new Tag { Id = 2, Name = "Transport" }]
        };
        vm.LoadCommand.Execute(null);
        vm.SearchText = "Zakupy";
        Assert.Single(vm.Items);
        vm.AddCommand.Execute(null);
        Assert.Equal(new[] { "Zakupy", "Transport" }, dialogs.ExistingNames);
    });

    [Fact]
    public void Search_FiltersByNamePreservesVisibleSelectionAndRemainsActiveAfterReload() => OnSta(() =>
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var vm = new TestReferenceList(provider.GetRequiredService<IServiceScopeFactory>());
        vm.Rows = [new Tag { Id = 1, Name = "Zakupy do pracy" }, new Tag { Id = 2, Name = "Zakupy domowe" }, new Tag { Id = 3, Name = "Transport" }];
        vm.LoadCommand.Execute(null);
        vm.SelectedItem = vm.Items[1];
        vm.SearchText = "  ZAKUPY  ";
        Assert.Equal(2, vm.Items.Count);
        Assert.Equal(2, vm.SelectedItem!.Id);
        vm.SearchText = "pracy";
        Assert.Equal(1, Assert.Single(vm.Items).Id);
        Assert.Null(vm.SelectedItem);
        vm.SearchText = "nie istnieje";
        Assert.Empty(vm.Items);
        Assert.Null(vm.SelectedItem);
        Assert.False(vm.EditCommand.CanExecute(null));
        Assert.False(vm.DeleteCommand.CanExecute(null));
        Assert.Equal(new LocalizationService()["ReferenceList_NoMatches"], vm.EmptyMessage);
        vm.SearchText = "Zakupy";
        vm.Rows = [new Tag { Id = 1, Name = "Transport" }, new Tag { Id = 4, Name = "Zakupy nowe" }];
        vm.LoadCommand.Execute(null);
        Assert.Equal("Zakupy", vm.SearchText);
        Assert.Equal(4, Assert.Single(vm.Items).Id);
        vm.SearchText = "";
        Assert.Equal(2, vm.Items.Count);
    });

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Reload_PreservesExistingSelectionWithoutSelectingAnotherItem(bool removeSelected, bool emptyList) => OnSta(() =>
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var viewModel = new TestReferenceList(provider.GetRequiredService<IServiceScopeFactory>());
        viewModel.Rows = [new Tag { Id = 1, Name = "First" }, new Tag { Id = 2, Name = "Second" }];
        var list = new ListBox { DataContext = viewModel };
        list.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(viewModel.Items)));
        list.SetBinding(System.Windows.Controls.Primitives.Selector.SelectedItemProperty,
            new Binding(nameof(viewModel.SelectedItem)) { Mode = BindingMode.TwoWay });
        viewModel.LoadCommand.Execute(null);
        Pump();
        Assert.Null(viewModel.SelectedItem);
        Assert.Null(list.SelectedItem);
        Assert.False(viewModel.EditCommand.CanExecute(null));
        list.SelectedItem = viewModel.Items[1];
        var oldSelection = viewModel.SelectedItem;
        Assert.NotNull(oldSelection);

        // Reloading after an operation loads fresh instances; names and order may also change.
        viewModel.Rows = emptyList ? [] : removeSelected
            ? [new Tag { Id = 1, Name = "First" }]
            : [new Tag { Id = 2, Name = "Renamed" }, new Tag { Id = 1, Name = "First" }];
        viewModel.LoadCommand.Execute(null);
        Pump();

        Assert.False(viewModel.IsLoading);
        if (emptyList || removeSelected)
        {
            Assert.Null(viewModel.SelectedItem);
            Assert.Null(list.SelectedItem);
            Assert.False(viewModel.EditCommand.CanExecute(null));
            Assert.False(viewModel.DeleteCommand.CanExecute(null));
        }
        else
        {
            Assert.NotSame(oldSelection, viewModel.SelectedItem);
            Assert.Same(viewModel.Items[0], viewModel.SelectedItem);
            Assert.Same(viewModel.SelectedItem, list.SelectedItem);
            Assert.Equal("Renamed", viewModel.SelectedItem!.Name);
            Assert.True(viewModel.EditCommand.CanExecute(null));
            Assert.True(viewModel.DeleteCommand.CanExecute(null));
        }
    });

    private sealed class TestReferenceList(IServiceScopeFactory scopes, IDialogService? dialogs = null)
        : ReferenceListViewModel<Tag>(scopes, dialogs ?? new UnusedDialogs(), new LocalizationService())
    {
        public IReadOnlyList<Tag> Rows { get; set; } = [];
        protected override string EntityDisplayNameKey => "Entity_Tag";
        protected override int GetId(Tag item) => item.Id;
        protected override string GetName(Tag item) => item.Name;
        protected override Task<IReadOnlyList<Tag>> GetAllAsync(IServiceScope scope, CancellationToken ct)
            => Task.FromResult(Rows);
        protected override Task AddCoreAsync(IServiceScope scope, string name, string? colorCode, CancellationToken ct)
            => throw new NotSupportedException();
        protected override Task UpdateCoreAsync(IServiceScope scope, Tag item, string name, string? colorCode, CancellationToken ct)
            => throw new NotSupportedException();
        protected override Task DeleteCoreAsync(IServiceScope scope, Tag item, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class UnusedDialogs : IDialogService
    {
        public IReadOnlyList<string>? ExistingNames { get; private set; }
        public bool? ShowAddEditTransaction(AddEditTransactionViewModel viewModel) => throw new NotSupportedException();
        public string? ShowNameDialog(string title, string? initialValue = null, IReadOnlyList<string>? existingNames = null)
        {
            ExistingNames = existingNames;
            return null;
        }
        public ReferenceColorDialogResult? ShowReferenceColorDialog(string title, string? initialName = null, string? initialColorCode = null, bool allowIcons = false, string? initialIconKey = null, IReadOnlyList<string>? existingNames = null)
            => throw new NotSupportedException();
        public bool ConfirmDelete(string title, string message) => throw new NotSupportedException();
        public UnsavedChangesChoice ConfirmUnsavedChanges(string title, string message) => throw new NotSupportedException();
        public string? ShowBackupSaveDialog(string title, string suggestedFileName, string filter) => throw new NotSupportedException();
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    private static void OnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
            finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "WPF selection test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
