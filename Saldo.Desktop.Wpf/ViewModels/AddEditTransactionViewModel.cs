using System.Globalization;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using FluentResults;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.Errors;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Localization;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Services;
using Saldo.Domain.Entities;
using Saldo.Domain.Enums;

namespace Saldo.Desktop.Wpf.ViewModels;

public sealed class AddEditTransactionViewModel : LocalizedViewModelBase, ITransactionTemplatePickerHost
{
    private const string AmountRequiredError = "Validation_AmountRequired";
    private const string AmountMustBeNumberError = "Validation_AmountMustBeNumber";
    private const string ReferenceSelectionRequiredError = "Validation_ReferenceSelectionRequired";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDialogService _dialogService;
    private readonly IReadOnlyList<TypeItem> _types;
    private bool _isRestoredDraft;
    private bool _showRestoredDraftNotice;
    private TransactionDraft? _initialDraft;
    private TransactionDraft _emptyDraft;

    private DateTime _date = DateTime.Today;
    private TypeItem _selectedType;
    private string _amountText = string.Empty;
    private Category? _selectedCategory;
    private string _categoryText = string.Empty;
    private Party? _selectedPayer;
    private string _payerText = string.Empty;
    private Party? _selectedCounterparty;
    private string _counterpartyText = string.Empty;
    private Location? _selectedLocation;
    private string _locationText = string.Empty;
    private string? _description;
    private readonly Dictionary<string, List<string>> _validationErrorCodes = [];
    private string _templateSaveMessage = string.Empty;
    private string? _lastTemplateName;
    private string? _sourceTemplateName;
    private bool _isSavingTemplate;
    private bool _isLoadingTemplate;
    public string TemplateSaveMessage { get => _templateSaveMessage; private set { if (SetField(ref _templateSaveMessage, value)) OnPropertyChanged(nameof(HasTemplateSaveMessage)); } }
    public bool HasTemplateSaveMessage => TemplateSaveMessage.Length > 0;

    public sealed class TypeItem : ViewModelBase
    {
        private readonly ILocalizationService _localization;
        private string _label;

        public TransactionType Value { get; }

        public string Label
        {
            get => _label;
            private set => SetField(ref _label, value);
        }

        public TypeItem(TransactionType value, ILocalizationService localization)
        {
            Value = value;
            _localization = localization;
            _label = GetLabel();
            _localization.PropertyChanged += OnLocalizationChanged;
        }

        private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(ILocalizationService.CurrentCulture) || e.PropertyName == "Item[]")
            {
                Label = GetLabel();
            }
        }

        private string GetLabel() => Value switch
        {
            TransactionType.Expense => _localization["Type_Expense"],
            TransactionType.Income => _localization["Type_Income"],
            _ => Value.ToString()
        };
    }

    public IReadOnlyList<TypeItem> Types => _types;

    public int? TransactionId { get; private set; }
    public bool IsNewTransaction => !TransactionId.HasValue;
    public bool IsRestoredDraft => _isRestoredDraft;
    public bool ShowRestoredDraftNotice => _showRestoredDraftNotice;
    public bool HasDraftContent => !DraftsMatch(_emptyDraft, CreateDraft());
    public string Title
    {
        get
        {
            var title = _isRestoredDraft ? T("Transaction_AddDraftTitle")
                : TransactionId.HasValue ? T("Transaction_EditTitle") : T("Transaction_AddTitle");
            return _sourceTemplateName is null ? title
                : title + string.Format(Localization.CurrentCulture, T("Transaction_TemplateTitleSuffix"), _sourceTemplateName);
        }
    }

    public DateTime Date { get => _date; set => SetField(ref _date, value); }

    public TypeItem SelectedType
    {
        get => _selectedType;
        set
        {
            if (SetField(ref _selectedType, value))
            {
                OnPropertyChanged(nameof(IsExpense));
                OnPropertyChanged(nameof(IsIncome));
            }
        }
    }

    public bool IsExpense
    {
        get => SelectedType.Value == TransactionType.Expense;
        set
        {
            if (value) SelectedType = Types.First(type => type.Value == TransactionType.Expense);
        }
    }

    public bool IsIncome
    {
        get => SelectedType.Value == TransactionType.Income;
        set
        {
            if (value) SelectedType = Types.First(type => type.Value == TransactionType.Income);
        }
    }

    public string AmountText
    {
        get => _amountText;
        set
        {
            if (SetField(ref _amountText, value)) ClearFieldError(nameof(AmountText));
        }
    }

    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetField(ref _selectedCategory, value)) ClearFieldError(nameof(SelectedCategory));
        }
    }
    public string CategoryText
    {
        get => _categoryText;
        set
        {
            if (SetField(ref _categoryText, value)) ClearFieldError(nameof(SelectedCategory));
        }
    }
    public Party? SelectedPayer
    {
        get => _selectedPayer;
        set
        {
            if (SetField(ref _selectedPayer, value)) ClearFieldError(nameof(PayerText));
        }
    }
    public string PayerText
    {
        get => _payerText;
        set
        {
            if (SetField(ref _payerText, value)) ClearFieldError(nameof(PayerText));
        }
    }
    public Party? SelectedCounterparty
    {
        get => _selectedCounterparty;
        set
        {
            if (SetField(ref _selectedCounterparty, value)) ClearFieldError(nameof(CounterpartyText));
        }
    }
    public string CounterpartyText
    {
        get => _counterpartyText;
        set
        {
            if (SetField(ref _counterpartyText, value)) ClearFieldError(nameof(CounterpartyText));
        }
    }
    public Location? SelectedLocation
    {
        get => _selectedLocation;
        set
        {
            if (SetField(ref _selectedLocation, value)) ClearFieldError(nameof(LocationText));
        }
    }

    public string? Description
    {
        get => _description;
        set
        {
            if (SetField(ref _description, value)) ClearFieldError(nameof(Description));
        }
    }
    public string LocationText
    {
        get => _locationText;
        set
        {
            if (SetField(ref _locationText, value)) ClearFieldError(nameof(LocationText));
        }
    }

    public ObservableCollection<Category> Categories { get; private set; }
    public ObservableCollection<Party> Parties { get; private set; }
    public ObservableCollection<Location> Locations { get; private set; }
    public ObservableCollection<SelectableTag> Tags { get; }

    public string DescriptionError => GetErrorText(nameof(Description));
    public bool HasDescriptionError => HasError(nameof(Description));

    public string AmountError => GetErrorText(nameof(AmountText));
    public string CategoryError => GetErrorText(nameof(SelectedCategory));
    public string PayerError => GetErrorText(nameof(PayerText));
    public string CounterpartyError => GetErrorText(nameof(CounterpartyText));
    public string LocationError => GetErrorText(nameof(LocationText));
    public string ValidationSummary => GetErrorText(string.Empty);

    public bool HasAmountError => HasError(nameof(AmountText));
    public bool HasCategoryError => HasError(nameof(SelectedCategory));
    public bool HasPayerError => HasError(nameof(PayerText));
    public bool HasCounterpartyError => HasError(nameof(CounterpartyText));
    public bool HasLocationError => HasError(nameof(LocationText));
    public bool HasValidationSummary => HasError(string.Empty);

    public event Action<bool>? RequestClose;

    public ICommand SaveCommand { get; }
    public ICommand ClearAmountCommand { get; }
    public ICommand ClearDescriptionCommand { get; }
    public TransactionTemplatePickerViewModel TemplatePicker { get; }
    public ICommand SaveAsTemplateCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand AddCategoryCommand { get; }
    public ICommand AddPayerCommand { get; }
    public ICommand AddCounterpartyCommand { get; }
    public ICommand AddLocationCommand { get; }
    public ICommand AddTagCommand { get; }

    public AddEditTransactionViewModel(
        IServiceScopeFactory scopeFactory,
        IDialogService dialogService,
        ILocalizationService localization,
        IReadOnlyList<Category> categories,
        IReadOnlyList<Party> parties,
        IReadOnlyList<Location> locations,
        IReadOnlyList<Tag> tags,
        NewTransactionDefaultsDto? defaults = null,
        TransactionDto? existing = null,
        TransactionDraft? draft = null,
        TransactionTemplateDto? template = null)
        : base(localization)
    {
        _scopeFactory = scopeFactory;
        _dialogService = dialogService;
        Categories = new ObservableCollection<Category>(categories);
        Parties = new ObservableCollection<Party>(parties);
        Locations = new ObservableCollection<Location>(locations);
        Tags = new ObservableCollection<SelectableTag>(tags.Select(tag => new SelectableTag(tag.Id, tag.Name, tag.ColorCode, tag.IconKey)));
        _types =
        [
            new TypeItem(TransactionType.Expense, localization),
            new TypeItem(TransactionType.Income, localization)
        ];
        _selectedType = _types[0];

        if (defaults is not null)
        {
            ApplyDefaults(defaults);
        }

        _emptyDraft = CreateDraft();
        TemplatePicker = new TransactionTemplatePickerViewModel(scopeFactory, localization, LoadTemplateAsync);

        if (template is not null)
        {
            ApplyTemplate(template);
        }

        if (existing is not null)
        {
            PopulateFrom(existing);
            _initialDraft = CreateDraft();
        }

        if (draft is not null)
        {
            _isRestoredDraft = true;
            _showRestoredDraftNotice = true;
            ApplyDraft(draft);
        }

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !_isSavingTemplate && !_isLoadingTemplate);
        ClearAmountCommand = new RelayCommand(() => AmountText = string.Empty,
            () => !_isSavingTemplate && !_isLoadingTemplate && AmountText.Length > 0);
        ClearDescriptionCommand = new RelayCommand(() => Description = string.Empty,
            () => !_isSavingTemplate && !_isLoadingTemplate && !string.IsNullOrEmpty(Description));
        SaveAsTemplateCommand = new AsyncRelayCommand(SaveAsTemplateAsync, () => IsNewTransaction && SaveCommand.CanExecute(null));
        ClearCommand = new RelayCommand(Clear, () => IsNewTransaction && HasDraftContent && !_isSavingTemplate && !_isLoadingTemplate);
        AddCategoryCommand = new AsyncRelayCommand(AddCategoryAsync);
        AddPayerCommand = new AsyncRelayCommand(AddPayerAsync);
        AddCounterpartyCommand = new AsyncRelayCommand(AddCounterpartyAsync);
        AddLocationCommand = new AsyncRelayCommand(AddLocationAsync);
        AddTagCommand = new AsyncRelayCommand(AddTagAsync);
        PropertyChanged += OnFormChanged;
        foreach (var tag in Tags) tag.PropertyChanged += OnFormChanged;
    }

    private void OnFormChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is SelectableTag && e.PropertyName == nameof(SelectableTag.IsSelected)
            || sender == this && e.PropertyName is nameof(Date) or nameof(SelectedType)
                or nameof(AmountText) or nameof(SelectedCategory) or nameof(CategoryText)
                or nameof(SelectedPayer) or nameof(PayerText) or nameof(SelectedCounterparty)
                or nameof(CounterpartyText) or nameof(SelectedLocation) or nameof(LocationText)
                or nameof(Description))
        {
            DismissRestoredDraftNotice();
            TemplateSaveMessage = string.Empty;
        }
        CommandManager.InvalidateRequerySuggested();
    }

    private void ApplyTemplate(TransactionTemplateDto template)
    {
        _sourceTemplateName = template.Name;
        _lastTemplateName = template.Name;
        OnPropertyChanged(nameof(Title));
        SelectedType = Types.First(type => type.Value == template.TransactionType);
        if (template.Amount.HasValue) AmountText = template.Amount.Value.ToString("0.##", CultureInfo.CurrentCulture);
        if (!string.IsNullOrWhiteSpace(template.Description)) Description = template.Description;
        if (template.CategoryId.HasValue)
        {
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == template.CategoryId);
            CategoryText = SelectedCategory?.Name ?? string.Empty;
        }
        if (template.PayerId.HasValue)
        {
            SelectedPayer = Parties.FirstOrDefault(p => p.Id == template.PayerId);
            PayerText = SelectedPayer?.Name ?? string.Empty;
        }
        if (template.CounterpartyId.HasValue)
        {
            SelectedCounterparty = Parties.FirstOrDefault(p => p.Id == template.CounterpartyId);
            CounterpartyText = SelectedCounterparty?.Name ?? string.Empty;
        }
        if (template.LocationId.HasValue)
        {
            SelectedLocation = Locations.FirstOrDefault(l => l.Id == template.LocationId);
            LocationText = SelectedLocation?.Name ?? string.Empty;
        }
        if (template.TagIds.Count > 0) SelectTags(template.TagIds);
    }

    public async Task LoadTemplateAsync(int id)
    {
        if (!IsNewTransaction || _isLoadingTemplate || _isSavingTemplate || !SaveCommand.CanExecute(null)) return;
        _isLoadingTemplate = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var result = await services.GetRequiredService<GetTransactionTemplate>().ExecuteAsync(id);
            if (result.IsFailed) { TemplatePicker.ReportError("Template_NotFound"); return; }
            // One scope shares a DbContext, so read dictionaries sequentially before changing the form.
            var categories = await services.GetRequiredService<ICategoryRepository>().GetAllAsync();
            var parties = await services.GetRequiredService<IPartyRepository>().GetAllAsync();
            var locations = await services.GetRequiredService<ILocationRepository>().GetAllAsync();
            var tags = await services.GetRequiredService<ITagRepository>().GetAllAsync();
            var previous = CreateDraft();
            Categories.Clear(); foreach (var category in categories) Categories.Add(category);
            Parties.Clear(); foreach (var party in parties) Parties.Add(party);
            Locations.Clear(); foreach (var location in locations) Locations.Add(location);
            foreach (var tag in Tags) tag.PropertyChanged -= OnFormChanged;
            Tags.Clear();
            foreach (var tag in tags)
            {
                var selectable = new SelectableTag(tag.Id, tag.Name, tag.ColorCode, tag.IconKey);
                selectable.PropertyChanged += OnFormChanged;
                Tags.Add(selectable);
            }
            // Rebind existing choices after refreshing dictionary instances, retaining typed text and tag selection.
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == previous.CategoryId);
            CategoryText = previous.CategoryText;
            SelectedPayer = Parties.FirstOrDefault(p => p.Id == previous.PayerId);
            PayerText = previous.PayerText;
            SelectedCounterparty = Parties.FirstOrDefault(p => p.Id == previous.CounterpartyId);
            CounterpartyText = previous.CounterpartyText;
            SelectedLocation = Locations.FirstOrDefault(l => l.Id == previous.LocationId);
            LocationText = previous.LocationText;
            SelectTags(previous.TagIds);
            ApplyTemplate(result.Value);
            ApplyValidationErrors([]);
            TemplateSaveMessage = string.Empty;
            _isRestoredDraft = false;
            DismissRestoredDraftNotice();
            OnPropertyChanged(nameof(IsRestoredDraft));
            OnPropertyChanged(nameof(Title));
            TemplatePicker.IsTemplatePickerOpen = false;
        }
        catch (Exception) { TemplatePicker.ReportError("TemplatePicker_LoadFailed"); }
        finally
        {
            _isLoadingTemplate = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void DismissRestoredDraftNotice()
    {
        if (!_showRestoredDraftNotice) return;
        _showRestoredDraftNotice = false;
        OnPropertyChanged(nameof(ShowRestoredDraftNotice));
    }

    private void Clear()
    {
        if (!IsNewTransaction) return;
        _sourceTemplateName = null;
        _lastTemplateName = null;

        SelectedType = Types.First(type => type.Value == _emptyDraft.Type);
        AmountText = string.Empty;
        SelectedCategory = null;
        CategoryText = string.Empty;
        SelectedPayer = Parties.FirstOrDefault(party => party.Id == _emptyDraft.PayerId);
        PayerText = _emptyDraft.PayerText;
        SelectedCounterparty = null;
        CounterpartyText = string.Empty;
        SelectedLocation = Locations.FirstOrDefault(location => location.Id == _emptyDraft.LocationId);
        LocationText = _emptyDraft.LocationText;
        Description = null;
        SelectTags([]);
        ApplyValidationErrors([]);
        _emptyDraft = CreateDraft();
        _isRestoredDraft = false;
        DismissRestoredDraftNotice();
        OnPropertyChanged(nameof(IsRestoredDraft));
        OnPropertyChanged(nameof(Title));
    }

    private async Task AddTagAsync()
    {
        var input = _dialogService.ShowReferenceColorDialog(
            string.Format(CultureInfo.CurrentCulture, T("AddEntityTitleTemplate"), T("Entity_Tag")), allowIcons: true);
        if (input is null) return;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var tag = await scope.ServiceProvider.GetRequiredService<AddTag>().ExecuteAsync(input.Name, input.ColorCode, iconKey: input.IconKey);
            var selectableTag = new SelectableTag(tag.Id, tag.Name, tag.ColorCode, tag.IconKey) { IsSelected = true };
            selectableTag.PropertyChanged += OnFormChanged;
            Tags.Add(selectableTag);
            DismissRestoredDraftNotice();
            CommandManager.InvalidateRequerySuggested();
        }
        catch (Exception ex)
        {
            ShowReferenceError(ex);
        }
    }

    private IReadOnlyList<int> SelectedTagIds => Tags.Where(tag => tag.IsSelected).Select(tag => tag.Id).Order().ToArray();

    private void SelectTags(IReadOnlyList<int> ids)
    {
        foreach (var tag in Tags) tag.IsSelected = ids.Contains(tag.Id);
    }

    private Task AddPayerAsync() => AddPartyAsync(true);

    private Task AddCounterpartyAsync() => AddPartyAsync(false);

    private async Task AddCategoryAsync()
    {
        var categoryInput = _dialogService.ShowReferenceColorDialog(
            string.Format(CultureInfo.CurrentCulture, T("AddEntityTitleTemplate"), T("Entity_Category")), allowIcons: true);
        if (categoryInput is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var category = await scope.ServiceProvider.GetRequiredService<AddCategory>().ExecuteAsync(categoryInput.Name, categoryInput.ColorCode, iconKey: categoryInput.IconKey);
            Categories.Add(category);
            SelectedCategory = category;
        }
        catch (Exception ex)
        {
            ShowReferenceError(ex);
        }
    }

    private async Task AddPartyAsync(bool payer)
    {
        var name = _dialogService.ShowNameDialog(
            string.Format(CultureInfo.CurrentCulture, T("AddEntityTitleTemplate"), T("Entity_Party")));
        if (name is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var party = await scope.ServiceProvider.GetRequiredService<AddParty>().ExecuteAsync(name);
            Parties.Add(party);

            if (payer)
            {
                SelectedPayer = party;
                PayerText = party.Name;
            }
            else
            {
                SelectedCounterparty = party;
                CounterpartyText = party.Name;
            }
        }
        catch (Exception ex)
        {
            ShowReferenceError(ex);
        }
    }

    private async Task AddLocationAsync()
    {
        var name = _dialogService.ShowNameDialog(
            string.Format(CultureInfo.CurrentCulture, T("AddEntityTitleTemplate"), T("Entity_Location")));
        if (name is null) return;

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var location = await scope.ServiceProvider.GetRequiredService<AddLocation>().ExecuteAsync(name);
            Locations.Add(location);
            SelectedLocation = location;
            LocationText = location.Name;
        }
        catch (Exception ex)
        {
            ShowReferenceError(ex);
        }
    }

    private void ShowReferenceError(Exception exception)
    {
        var message = exception is DuplicateReferenceException duplicate
            ? string.Format(CultureInfo.CurrentCulture, T("DuplicateReferenceErrorTemplate"), duplicate.Name)
            : exception is InvalidTagNameException ? T("Validation_TagNameInvalid") : exception.Message;
        MessageBox.Show(message, T("ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void ApplyDefaults(NewTransactionDefaultsDto defaults)
    {
        _date = defaults.Date.ToDateTime(TimeOnly.MinValue);
        _selectedType = Types.FirstOrDefault(d => d.Value == defaults.Type) ?? Types[0];
        _selectedPayer = defaults.PayerId.HasValue
            ? Parties.FirstOrDefault(p => p.Id == defaults.PayerId.Value)
            : null;
        _payerText = _selectedPayer?.Name ?? string.Empty;
        _selectedLocation = defaults.LocationId.HasValue
            ? Locations.FirstOrDefault(location => location.Id == defaults.LocationId.Value)
            : null;
        _locationText = _selectedLocation?.Name ?? string.Empty;
    }

    private void PopulateFrom(TransactionDto t)
    {
        TransactionId = t.Id;
        _date = t.Date.ToDateTime(TimeOnly.MinValue);
        _selectedType = Types.FirstOrDefault(d => d.Value == t.Type) ?? Types[0];
        _amountText = t.Amount.ToString("0.##", CultureInfo.CurrentCulture);
        _selectedCategory = Categories.FirstOrDefault(c => c.Id == t.CategoryId);
        _categoryText = _selectedCategory?.Name ?? string.Empty;
        _selectedPayer = Parties.FirstOrDefault(p => p.Id == t.PayerId);
        _payerText = t.PayerName ?? string.Empty;
        _selectedCounterparty = Parties.FirstOrDefault(p => p.Id == t.CounterpartyId);
        _counterpartyText = t.CounterpartyName ?? string.Empty;
        _selectedLocation = t.LocationId.HasValue
            ? Locations.FirstOrDefault(location => location.Id == t.LocationId.Value)
            : null;
        _description = t.Description;
        _locationText = t.Location ?? string.Empty;
        SelectTags(t.TagIds);
    }

    public TransactionDraft CreateDraft() => new()
    {
        SourceTemplateName = _sourceTemplateName,
        TransactionId = TransactionId,
        Date = Date,
        Type = SelectedType.Value,
        AmountText = AmountText,
        CategoryId = SelectedCategory?.Id,
        CategoryText = CategoryText,
        PayerId = SelectedPayer?.Id,
        PayerText = PayerText,
        CounterpartyId = SelectedCounterparty?.Id,
        CounterpartyText = CounterpartyText,
        LocationId = SelectedLocation?.Id,
        LocationText = LocationText,
        Description = Description,
        TagIds = SelectedTagIds
    };

    public bool HasUnsavedChanges => _initialDraft is not null && !DraftsMatch(_initialDraft, CreateDraft());

    public bool CanClose()
    {
        if (_isSavingTemplate || _isLoadingTemplate) return false;
        if (!HasUnsavedChanges) return true;

        var choice = _dialogService.ConfirmUnsavedChanges(
            T("Transaction_UnsavedChangesTitle"), T("Transaction_UnsavedChangesMessage"));
        if (choice == UnsavedChangesChoice.Save)
        {
            SaveCommand.Execute(null);
            // Only a successful save raises RequestClose; validation failures keep the form open.
            return false;
        }

        return choice == UnsavedChangesChoice.Discard;
    }

    private static bool DraftsMatch(TransactionDraft left, TransactionDraft right) =>
        left.TransactionId == right.TransactionId
        && left.Date == right.Date
        && left.Type == right.Type
        && left.AmountText == right.AmountText
        && left.CategoryId == right.CategoryId
        && left.CategoryText == right.CategoryText
        && left.PayerId == right.PayerId
        && left.PayerText == right.PayerText
        && left.CounterpartyId == right.CounterpartyId
        && left.CounterpartyText == right.CounterpartyText
        && left.LocationId == right.LocationId
        && left.LocationText == right.LocationText
        && left.Description == right.Description
        && left.TagIds.Order().SequenceEqual(right.TagIds.Order());

    private void ApplyDraft(TransactionDraft draft)
    {
        _sourceTemplateName = draft.SourceTemplateName;
        _lastTemplateName = draft.SourceTemplateName;
        TransactionId = draft.TransactionId;
        _date = draft.Date;
        _selectedType = Types.FirstOrDefault(type => type.Value == draft.Type) ?? Types[0];
        _amountText = draft.AmountText;
        _selectedCategory = draft.CategoryId.HasValue
            ? Categories.FirstOrDefault(category => category.Id == draft.CategoryId.Value)
            : null;
        _categoryText = draft.CategoryText;
        _selectedPayer = draft.PayerId.HasValue
            ? Parties.FirstOrDefault(party => party.Id == draft.PayerId.Value)
            : null;
        _payerText = draft.PayerText;
        _selectedCounterparty = draft.CounterpartyId.HasValue
            ? Parties.FirstOrDefault(party => party.Id == draft.CounterpartyId.Value)
            : null;
        _counterpartyText = draft.CounterpartyText;
        _selectedLocation = draft.LocationId.HasValue
            ? Locations.FirstOrDefault(location => location.Id == draft.LocationId.Value)
            : null;
        _locationText = draft.LocationText;
        _description = draft.Description;
        SelectTags(draft.TagIds);
    }

    private async Task SaveAsync()
    {
        string? amountInputError = null;
        decimal amount;

        if (string.IsNullOrWhiteSpace(AmountText))
        {
            amount = 0;
            amountInputError = AmountRequiredError;
        }
        else if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
        {
            amount = 0;
            amountInputError = AmountMustBeNumberError;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();

        if (TransactionId.HasValue)
        {
            var cmd = new EditTransactionCommand(
                TransactionId.Value,
                DateOnly.FromDateTime(Date),
                SelectedType.Value,
                amount,
                SelectedCategory?.Id ?? 0,
                SelectedPayer?.Id,
                ResolvePartyName(SelectedPayer, PayerText),
                SelectedCounterparty?.Id,
                ResolvePartyName(SelectedCounterparty, CounterpartyText),
                Description,
                ResolveLocationName(),
                SelectedTagIds);

            var result = await scope.ServiceProvider.GetRequiredService<EditTransaction>().ExecuteAsync(cmd);
            if (result.IsFailed)
            {
                ApplyValidationErrors(result.Errors);
                if (amountInputError is not null)
                    SetFieldError(nameof(AmountText), amountInputError);
                return;
            }
        }
        else
        {
            var cmd = new AddTransactionCommand(
                DateOnly.FromDateTime(Date),
                SelectedType.Value,
                amount,
                SelectedCategory?.Id ?? 0,
                SelectedPayer?.Id,
                ResolvePartyName(SelectedPayer, PayerText),
                SelectedCounterparty?.Id,
                ResolvePartyName(SelectedCounterparty, CounterpartyText),
                Description,
                ResolveLocationName(),
                SelectedTagIds);

            var result = await scope.ServiceProvider.GetRequiredService<AddTransaction>().ExecuteAsync(cmd);
            if (result.IsFailed)
            {
                ApplyValidationErrors(result.Errors);
                if (amountInputError is not null)
                    SetFieldError(nameof(AmountText), amountInputError);
                return;
            }
        }

        RequestClose?.Invoke(true);
    }

    public async Task SaveAsTemplateAsync()
    {
        if (_isSavingTemplate || !IsNewTransaction) return;
        TemplateSaveMessage = string.Empty;
        ApplyValidationErrors([]);
        decimal? amount = null;
        if (!string.IsNullOrWhiteSpace(AmountText))
        {
            if (!decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsed))
            {
                SetFieldError(nameof(AmountText), AmountMustBeNumberError);
                return;
            }
            amount = parsed;
        }
        // Resolve complete typed names without creating new dictionary entries.
        var category = SelectedCategory ?? Categories.FirstOrDefault(c => string.Equals(c.Name, CategoryText.Trim(), StringComparison.OrdinalIgnoreCase));
        var payer = SelectedPayer ?? Parties.FirstOrDefault(p => string.Equals(p.Name, PayerText.Trim(), StringComparison.OrdinalIgnoreCase));
        var counterparty = SelectedCounterparty ?? Parties.FirstOrDefault(p => string.Equals(p.Name, CounterpartyText.Trim(), StringComparison.OrdinalIgnoreCase));
        var location = SelectedLocation ?? Locations.FirstOrDefault(l => string.Equals(l.Name, LocationText.Trim(), StringComparison.OrdinalIgnoreCase));
        void Check(object? selected, string text, string field)
        {
            if (selected is null && !string.IsNullOrWhiteSpace(text)) SetFieldError(field, ReferenceSelectionRequiredError);
        }
        Check(category, CategoryText, nameof(SelectedCategory)); Check(payer, PayerText, nameof(PayerText));
        Check(counterparty, CounterpartyText, nameof(CounterpartyText)); Check(location, LocationText, nameof(LocationText));
        if (_validationErrorCodes.Count > 0) return;

        var name = _dialogService.ShowNameDialog(T("Transaction_SaveAsTemplate"), _lastTemplateName);
        if (name is null) return;
        _lastTemplateName = name;
        _isSavingTemplate = true;
        CommandManager.InvalidateRequerySuggested();
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<AddTransactionTemplate>().ExecuteAsync(
                new SaveTransactionTemplateCommand(name, SelectedType.Value, Description ?? string.Empty,
                    category?.Id, payer?.Id, counterparty?.Id, location?.Id, amount, SelectedTagIds));
            if (result.IsSuccess) TemplateSaveMessage = T("Transaction_TemplateSaved");
            else
            {
                foreach (var error in result.Errors)
                {
                    var field = error.Metadata.TryGetValue("PropertyName", out var property) ? property as string : null;
                    var target = field switch
                    {
                        "Description" => nameof(Description), "Amount" => nameof(AmountText),
                        "CategoryId" => nameof(SelectedCategory), "PayerId" => nameof(PayerText),
                        "CounterpartyId" => nameof(CounterpartyText), "LocationId" => nameof(LocationText), _ => string.Empty
                    };
                    var code = error.Message switch
                    {
                        "TransactionTemplate.NameRequired" => "Validation_NameRequired",
                        "TransactionTemplate.NameTooLong" => "Template_NameTooLong",
                        "TransactionTemplate.NameDuplicate" => "Template_NameDuplicate",
                        "TransactionTemplate.ContentRequired" => "Template_ContentRequired",
                        "TransactionTemplate.ContentDuplicate" => "Template_ContentDuplicate",
                        "TransactionTemplate.DescriptionTooLong" => "Validation_DescriptionTooLong",
                        "TransactionTemplate.AmountMustBePositive" => "Validation_AmountMustBePositive",
                        "TransactionTemplate.CategoryInvalid" => "Template_CategoryInvalid",
                        _ => "Template_InvalidReference"
                    };
                    SetFieldError(target, code);
                }
            }
        }
        catch (Exception)
        {
            SetFieldError(string.Empty, "Template_SaveFailed");
        }
        finally
        {
            _isSavingTemplate = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    protected override void OnCultureChanged()
    {
        OnPropertyChanged(nameof(Title));
        NotifyAllErrorProperties();
    }

    private void ApplyValidationErrors(IEnumerable<IError> errors)
    {
        _validationErrorCodes.Clear();

        foreach (var error in errors)
        {
            var propertyName = error.Metadata.TryGetValue("PropertyName", out var value)
                ? value?.ToString()
                : null;
            var viewModelProperty = MapCommandProperty(propertyName, error.Message);
            var errorCode = GetDisplayErrorCode(viewModelProperty, error.Message);

            if (!_validationErrorCodes.TryGetValue(viewModelProperty, out var codes))
            {
                codes = [];
                _validationErrorCodes[viewModelProperty] = codes;
            }

            codes.Add(errorCode);
        }

        NotifyAllErrorProperties();
    }

    private static string MapCommandProperty(string? propertyName, string errorCode) => propertyName switch
    {
        nameof(ITransactionCommand.Description) => nameof(Description),
        nameof(ITransactionCommand.Amount) => nameof(AmountText),
        nameof(ITransactionCommand.CategoryId) => nameof(SelectedCategory),
        nameof(ITransactionCommand.PayerName) => nameof(PayerText),
        nameof(ITransactionCommand.CounterpartyName) => nameof(CounterpartyText),
        _ => errorCode switch
        {
            ErrorCodes.Transaction.PayerInvalid => nameof(PayerText),
            ErrorCodes.Transaction.CounterpartyInvalid => nameof(CounterpartyText),
            ErrorCodes.Transaction.LocationInvalid => nameof(LocationText),
            _ => string.Empty
        }
    };

    private string GetDisplayErrorCode(string propertyName, string errorCode)
    {
        if (errorCode is ErrorCodes.Transaction.CategoryRequired
            or ErrorCodes.Transaction.PayerInvalid
            or ErrorCodes.Transaction.CounterpartyInvalid)
        {
            var hasTypedValue = propertyName switch
            {
                nameof(SelectedCategory) => !string.IsNullOrWhiteSpace(CategoryText),
                nameof(PayerText) => !string.IsNullOrWhiteSpace(PayerText),
                nameof(CounterpartyText) => !string.IsNullOrWhiteSpace(CounterpartyText),
                _ => false
            };

            if (hasTypedValue)
            {
                return ReferenceSelectionRequiredError;
            }
        }

        return errorCode;
    }

    private bool HasError(string propertyName) =>
        _validationErrorCodes.TryGetValue(propertyName, out var errors) && errors.Count > 0;

    private string GetErrorText(string propertyName) =>
        _validationErrorCodes.TryGetValue(propertyName, out var errors)
            ? string.Join(Environment.NewLine, errors.Select(LocalizeError))
            : string.Empty;

    private string LocalizeError(string errorCode) => errorCode switch
    {
        AmountRequiredError => T(AmountRequiredError),
        AmountMustBeNumberError => T(AmountMustBeNumberError),
        ErrorCodes.Transaction.IdMustBePositive => T("Validation_TransactionIdMustBePositive"),
        ErrorCodes.Transaction.AmountMustBePositive => T("Validation_AmountMustBePositive"),
        ErrorCodes.Transaction.CategoryRequired => T("Validation_CategoryRequired"),
        ErrorCodes.Transaction.PayerInvalid => T("Validation_PayerInvalid"),
        ErrorCodes.Transaction.CounterpartyInvalid => T("Validation_CounterpartyInvalid"),
        ErrorCodes.Transaction.DescriptionRequired => T("Validation_DescriptionRequired"),
        ErrorCodes.Transaction.DescriptionTooLong => T("Validation_DescriptionTooLong"),
        ErrorCodes.Transaction.DateRequired => T("Validation_DateRequired"),
        ErrorCodes.Transaction.TypeInvalid => T("Validation_TypeInvalid"),
        ErrorCodes.Transaction.LocationInvalid => T("Validation_LocationInvalid"),
        ReferenceSelectionRequiredError => T(ReferenceSelectionRequiredError),
        ErrorCodes.Transaction.NotFound => T("Validation_TransactionNotFound"),
        _ => T(errorCode)
    };

    private void SetFieldError(string propertyName, string errorCode)
    {
        _validationErrorCodes[propertyName] = [errorCode];
        NotifyErrorProperties(propertyName);
    }

    private void ClearFieldError(string propertyName)
    {
        if (_validationErrorCodes.Remove(propertyName))
        {
            NotifyErrorProperties(propertyName);
        }
    }

    private void NotifyAllErrorProperties()
    {
        NotifyErrorProperties(nameof(Description));
        NotifyErrorProperties(nameof(AmountText));
        NotifyErrorProperties(nameof(SelectedCategory));
        NotifyErrorProperties(nameof(PayerText));
        NotifyErrorProperties(nameof(CounterpartyText));
        NotifyErrorProperties(nameof(LocationText));
        NotifyErrorProperties(string.Empty);
    }

    private void NotifyErrorProperties(string propertyName)
    {
        var (textProperty, visibilityProperty) = propertyName switch
        {
            nameof(Description) => (nameof(DescriptionError), nameof(HasDescriptionError)),
            nameof(AmountText) => (nameof(AmountError), nameof(HasAmountError)),
            nameof(SelectedCategory) => (nameof(CategoryError), nameof(HasCategoryError)),
            nameof(PayerText) => (nameof(PayerError), nameof(HasPayerError)),
            nameof(CounterpartyText) => (nameof(CounterpartyError), nameof(HasCounterpartyError)),
            nameof(LocationText) => (nameof(LocationError), nameof(HasLocationError)),
            _ => (nameof(ValidationSummary), nameof(HasValidationSummary))
        };

        OnPropertyChanged(textProperty);
        OnPropertyChanged(visibilityProperty);
    }

    private string? ResolveLocationName()
    {
        if (SelectedLocation is not null)
        {
            return SelectedLocation.Name;
        }

        return string.IsNullOrWhiteSpace(LocationText) ? null : LocationText.Trim();
    }

    private static string? ResolvePartyName(Party? selectedParty, string? text)
    {
        if (selectedParty is not null)
        {
            return selectedParty.Name;
        }

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
