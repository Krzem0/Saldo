using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Services;
using Saldo.Desktop.Wpf.ViewModels;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;
using Saldo.Tests.Unit.Fakes;

namespace Saldo.Tests.Wpf;

public sealed class TransactionTagSelectionTests
{
    [Fact]
    public void FreshForm_DoesNotSelectAnyTags()
    {
        var viewModel = Create();
        Assert.All(viewModel.Tags, tag => Assert.False(tag.IsSelected));
        Assert.Empty(viewModel.CreateDraft().TagIds);
        Assert.False(viewModel.HasDraftContent);
        Assert.False(viewModel.IsRestoredDraft);
        Assert.False(viewModel.ClearCommand.CanExecute(null));
    }

    [Fact]
    public void ClearCommand_IsEnabledOnlyWhileFormDiffersFromDefaults()
    {
        var viewModel = Create();
        Assert.False(viewModel.ClearCommand.CanExecute(null));

        viewModel.AmountText = "12";
        Assert.True(viewModel.ClearCommand.CanExecute(null));
        viewModel.AmountText = string.Empty;
        Assert.False(viewModel.ClearCommand.CanExecute(null));

        viewModel.Tags[0].IsSelected = true;
        Assert.True(viewModel.ClearCommand.CanExecute(null));
        viewModel.Tags[0].IsSelected = false;
        Assert.False(viewModel.ClearCommand.CanExecute(null));

        var originalDate = viewModel.Date;
        viewModel.Date = originalDate.AddDays(-1);
        Assert.True(viewModel.ClearCommand.CanExecute(null));
        viewModel.ClearCommand.Execute(null);
        Assert.Equal(originalDate, viewModel.Date);
        Assert.False(viewModel.ClearCommand.CanExecute(null));
    }

    [Fact]
    public void Clear_RestoredDraft_ReturnsToDefaultsAndDropsDraftUntilNewInput()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var defaults = new NewTransactionDefaultsDto(new DateOnly(2019, 5, 1), TransactionType.Expense, 1);
        var viewModel = new AddEditTransactionViewModel(provider.GetRequiredService<IServiceScopeFactory>(),
            new UnusedDialogs(), new LocalizationService(), [new Category { Id = 1, Name = "Food" }],
            [new Party { Id = 1, Name = "Me" }, new Party { Id = 2, Name = "Shop" }],
            [new Location { Id = 1, Name = "Home" }], [new Tag { Id = 1, Name = "Holiday" }], defaults,
            draft: new TransactionDraft
            {
                Date = new DateTime(2019, 5, 20), Type = TransactionType.Income, AmountText = "10",
                CategoryId = 1, CategoryText = "Food", PayerId = 2, PayerText = "Shop",
                CounterpartyId = 2, CounterpartyText = "Shop", LocationId = 1, LocationText = "Home",
                Description = "Draft", TagIds = [1]
            });
        Assert.True(viewModel.IsRestoredDraft);
        Assert.True(viewModel.HasDraftContent);
        Assert.True(viewModel.ClearCommand.CanExecute(null));
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        viewModel.ClearCommand.Execute(null);

        Assert.False(viewModel.IsRestoredDraft);
        Assert.False(viewModel.HasDraftContent);
        Assert.False(viewModel.ClearCommand.CanExecute(null));
        Assert.Equal(new DateTime(2019, 5, 1), viewModel.Date);
        Assert.Equal(TransactionType.Expense, viewModel.SelectedType.Value);
        Assert.Equal("Me", viewModel.PayerText);
        Assert.Equal(1, viewModel.SelectedPayer?.Id);
        Assert.Empty(viewModel.AmountText);
        Assert.Null(viewModel.SelectedCategory);
        Assert.Empty(viewModel.CategoryText);
        Assert.Null(viewModel.SelectedCounterparty);
        Assert.Empty(viewModel.CounterpartyText);
        Assert.Null(viewModel.SelectedLocation);
        Assert.Empty(viewModel.LocationText);
        Assert.Null(viewModel.Description);
        Assert.All(viewModel.Tags, tag => Assert.False(tag.IsSelected));
        Assert.Single(viewModel.Tags);
        Assert.Contains(nameof(viewModel.IsRestoredDraft), notifications);
        Assert.Contains(nameof(viewModel.Title), notifications);
        viewModel.AmountText = "20";
        Assert.True(viewModel.HasDraftContent);
    }

    [Fact]
    public void Clear_IsUnavailableWhenEditingAnExistingTransaction()
    {
        var dto = new TransactionDto(1, new DateOnly(2026, 10, 4), TransactionType.Expense, 10m,
            1, "Food", null, 1, "Me", 1, "Shop", null, null, null, ["Old name"], [1]);
        var viewModel = Create(existing: dto);
        var before = viewModel.CreateDraft();

        Assert.False(viewModel.IsNewTransaction);
        Assert.False(viewModel.ClearCommand.CanExecute(null));
        viewModel.ClearCommand.Execute(null);

        Assert.Equal(before.AmountText, viewModel.AmountText);
        Assert.Equal(before.TagIds, viewModel.CreateDraft().TagIds);
        Assert.False(viewModel.HasUnsavedChanges);
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, " #aabbcc ")]
    [InlineData(true, " #aabbcc ")]
    public async Task QuickAdd_SelectsNewTagAndPreservesExistingSelection(bool alreadySelected, string? colorCode)
    {
        var repository = new TagRepositoryFake();
        var services = new ServiceCollection();
        services.AddSingleton(new AddTag(repository));
        using var provider = services.BuildServiceProvider();
        var viewModel = new AddEditTransactionViewModel(provider.GetRequiredService<IServiceScopeFactory>(),
            new UnusedDialogs("Dla Iwony", colorCode: colorCode), new LocalizationService(), [], [], [], await repository.GetAllAsync());
        viewModel.Tags[0].IsSelected = alreadySelected;
        var added = new TaskCompletionSource<bool>();
        viewModel.Tags.CollectionChanged += (_, _) => added.TrySetResult(true);

        viewModel.AddTagCommand.Execute(null);

        await added.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, viewModel.Tags.Count);
        Assert.Equal(alreadySelected, viewModel.Tags[0].IsSelected);
        Assert.True(viewModel.Tags[1].IsSelected);
        Assert.Equal("Dla Iwony", viewModel.Tags[1].Name);
        Assert.Equal(colorCode is null ? null : "#AABBCC", viewModel.Tags[1].ColorCode);
        Assert.Equal(colorCode is null ? null : "#AABBCC", (await repository.GetAllAsync())[1].ColorCode);
        Assert.Equal(alreadySelected ? new[] { 1, 2 } : new[] { 2 }, viewModel.CreateDraft().TagIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveCommand_AddOrEdit_PassesSelectedTagsToApplication(bool editing)
    {
        var repository = new FakeTransactionRepository();
        var parties = new[] { new Party { Id = 1, Name = "Me" }, new Party { Id = 2, Name = "Shop" } };
        var partyRepository = new FakePartyRepository(parties);
        var locations = new FakeLocationRepository([]);
        var services = new ServiceCollection();
        services.AddSingleton(new AddTransaction(repository, partyRepository, locations));
        services.AddSingleton(new EditTransaction(repository, partyRepository, locations));
        using var provider = services.BuildServiceProvider();
        TransactionDto? existing = null;
        if (editing)
        {
            var added = await provider.GetRequiredService<AddTransaction>().ExecuteAsync(new AddTransactionCommand(
                new DateOnly(2026, 10, 4), TransactionType.Expense, 10m, 1, 1, "Me", 2, "Shop", null, null, [1]));
            existing = added.Value;
        }
        var viewModel = new AddEditTransactionViewModel(provider.GetRequiredService<IServiceScopeFactory>(),
            new UnusedDialogs(unsavedChanges: UnsavedChangesChoice.Save), new LocalizationService(), [new Category { Id = 1, Name = "Food" }], parties, [],
            [new Tag { Id = 1, Name = "Dla Iwony" }, new Tag { Id = 2, Name = "Wakacje" }], existing: existing);
        viewModel.AmountText = "10";
        viewModel.SelectedCategory = viewModel.Categories[0];
        viewModel.SelectedPayer = viewModel.Parties[0];
        viewModel.SelectedCounterparty = viewModel.Parties[1];
        viewModel.Tags[0].IsSelected = true;
        viewModel.Tags[1].IsSelected = true;
        var closed = new TaskCompletionSource<bool>();
        viewModel.RequestClose += result => closed.SetResult(result);

        if (editing) Assert.False(viewModel.CanClose());
        else viewModel.SaveCommand.Execute(null);

        Assert.True(await closed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        var saved = await repository.GetByIdAsync(1);
        Assert.NotNull(saved);
        Assert.Equal(new[] { 1, 2 }, saved.Tags.Select(tag => tag.TagId).Order());
    }

    private static AddEditTransactionViewModel Create(TransactionDto? existing = null, TransactionDraft? draft = null,
        UnusedDialogs? dialogs = null)
    {
        var provider = new ServiceCollection().BuildServiceProvider();
        return new AddEditTransactionViewModel(provider.GetRequiredService<IServiceScopeFactory>(),
            dialogs ?? new UnusedDialogs(), new LocalizationService(), [], [], [],
            [new Tag { Id = 1, Name = "Renamed tag" }, new Tag { Id = 2, Name = "Wakacje" }],
            existing: existing, draft: draft);
    }

    [Fact]
    public void NewTransactionDraft_RestoresMultipleTagsById()
    {
        var original = Create();
        foreach (var tag in original.Tags) tag.IsSelected = true;
        var restored = Create(draft: original.CreateDraft());
        Assert.All(restored.Tags, tag => Assert.True(tag.IsSelected));
        Assert.Equal(new[] { 1, 2 }, restored.CreateDraft().TagIds);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoredDraftNotice_DisappearsOnFirstEditWithoutDiscardingDraft(bool changeTag)
    {
        var viewModel = Create(draft: new TransactionDraft
        {
            Date = DateTime.Today, Type = TransactionType.Expense, AmountText = "12", TagIds = [1]
        });
        Assert.True(viewModel.ShowRestoredDraftNotice);
        viewModel.AmountText = "12";
        Assert.True(viewModel.ShowRestoredDraftNotice);
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, e) => notifications.Add(e.PropertyName);

        if (changeTag) viewModel.Tags[1].IsSelected = true;
        else viewModel.Description = "Updated draft";

        Assert.False(viewModel.ShowRestoredDraftNotice);
        Assert.Contains(nameof(viewModel.ShowRestoredDraftNotice), notifications);
        Assert.True(viewModel.IsRestoredDraft);
        Assert.Equal("12", viewModel.AmountText);
        Assert.True(viewModel.Tags[0].IsSelected);
        if (changeTag) viewModel.Tags[1].IsSelected = false;
        else viewModel.Description = null;
        Assert.False(viewModel.ShowRestoredDraftNotice);
    }

    [Fact]
    public void Editing_RenamedTag_RemainsSelectedAndChangesAreDetected()
    {
        var dto = new TransactionDto(1, new DateOnly(2026, 10, 4), TransactionType.Expense, 10m,
            1, "Food", null, 1, "Me", 1, "Shop", null, null, null, ["Old name"], [1]);
        var viewModel = Create(existing: dto);
        Assert.True(viewModel.Tags[0].IsSelected);
        Assert.False(viewModel.Tags[1].IsSelected);
        Assert.False(viewModel.HasUnsavedChanges);
        viewModel.Tags[1].IsSelected = true;
        Assert.True(viewModel.HasUnsavedChanges);
        viewModel.Tags[1].IsSelected = false;
        Assert.False(viewModel.HasUnsavedChanges);
        viewModel.Tags[0].IsSelected = false;
        Assert.True(viewModel.HasUnsavedChanges);
        Assert.Empty(viewModel.CreateDraft().TagIds);
    }

    [Fact]
    public void RestoredDraft_DeletedUnusedTag_DoesNotSelectAnotherTag()
    {
        var restored = Create(draft: new TransactionDraft { TagIds = [3], Type = TransactionType.Expense });
        Assert.Empty(restored.CreateDraft().TagIds);
    }

    [Theory]
    [InlineData(UnsavedChangesChoice.Cancel, false)]
    [InlineData(UnsavedChangesChoice.Discard, true)]
    public void ClosingEditedTransaction_RespectsDialogChoice(UnsavedChangesChoice choice, bool canClose)
    {
        var dto = new TransactionDto(1, new DateOnly(2026, 10, 4), TransactionType.Expense, 10m,
            1, "Food", null, 1, "Me", 1, "Shop", null, null, null, [], []);
        var dialogs = new UnusedDialogs(unsavedChanges: choice);
        var viewModel = Create(existing: dto, dialogs: dialogs);
        Assert.True(viewModel.CanClose());
        Assert.Equal(0, dialogs.UnsavedChangesPromptCount);
        viewModel.Description = "Edited";

        Assert.Equal(canClose, viewModel.CanClose());
        Assert.Equal(1, dialogs.UnsavedChangesPromptCount);
        Assert.Equal("Edited", viewModel.Description);
    }

    [Fact]
    public void ClosingNewTransaction_KeepsDraftWithoutPrompting()
    {
        var dialogs = new UnusedDialogs();
        var viewModel = Create(dialogs: dialogs);
        viewModel.AmountText = "20";

        Assert.True(viewModel.CanClose());
        Assert.Equal(0, dialogs.UnsavedChangesPromptCount);
        Assert.Equal("20", viewModel.CreateDraft().AmountText);
    }

    [Fact]
    public async Task ClosingEditedTransaction_SaveWithInvalidAmount_KeepsFormOpen()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new EditTransaction(new FakeTransactionRepository(),
            new FakePartyRepository([]), new FakeLocationRepository([])));
        using var provider = services.BuildServiceProvider();
        var dto = new TransactionDto(1, new DateOnly(2026, 10, 4), TransactionType.Expense, 10m,
            1, "Food", null, 1, "Me", 1, "Shop", null, null, null, [], []);
        var viewModel = new AddEditTransactionViewModel(provider.GetRequiredService<IServiceScopeFactory>(),
            new UnusedDialogs(unsavedChanges: UnsavedChangesChoice.Save), new LocalizationService(),
            [new Category { Id = 1, Name = "Food" }],
            [new Party { Id = 1, Name = "Me" }, new Party { Id = 2, Name = "Shop" }], [], [], existing: dto);
        viewModel.AmountText = string.Empty;
        var validated = new TaskCompletionSource<bool>();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(viewModel.HasAmountError) && viewModel.HasAmountError)
                validated.TrySetResult(true);
        };
        var closed = false;
        viewModel.RequestClose += _ => closed = true;

        Assert.False(viewModel.CanClose());
        await validated.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(closed);
        Assert.True(viewModel.HasAmountError);
        Assert.True(viewModel.HasUnsavedChanges);
    }

    private sealed class TagRepositoryFake : ITagRepository
    {
        private readonly List<Tag> _tags = [new() { Id = 1, Name = "Wakacje" }];
        public Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tag>>(_tags.ToArray());
        public Task AddAsync(Tag tag, CancellationToken ct = default)
        {
            tag.Id = _tags.Count + 1;
            _tags.Add(tag);
            return Task.CompletedTask;
        }
        public Task<Tag?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task UpdateAsync(Tag tag, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(int id, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class UnusedDialogs(string? name = null,
        UnsavedChangesChoice unsavedChanges = UnsavedChangesChoice.Cancel, string? colorCode = null) : IDialogService
    {
        public int UnsavedChangesPromptCount { get; private set; }
        public UnsavedChangesChoice ConfirmUnsavedChanges(string title, string message)
        {
            UnsavedChangesPromptCount++;
            return unsavedChanges;
        }
        public bool? ShowAddEditTransaction(AddEditTransactionViewModel viewModel) => throw new NotSupportedException();
        public string? ShowNameDialog(string title, string? initialValue = null) => name;
        public ReferenceColorDialogResult? ShowReferenceColorDialog(string title, string? initialName = null, string? initialColorCode = null)
            => name is null ? null : new(name, colorCode);
        public bool ConfirmDelete(string title, string message) => throw new NotSupportedException();
        public string? ShowBackupSaveDialog(string title, string suggestedFileName, string filter) => throw new NotSupportedException();
    }
}
