using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Saldo.Application.DTOs;
using Saldo.Application.Interfaces;
using Saldo.Application.UseCases;
using Saldo.Desktop.Wpf.Infrastructure;
using Saldo.Desktop.Wpf.Localization;

namespace Saldo.Desktop.Wpf.ViewModels;

public interface ITransactionTemplatePickerHost
{
    TransactionTemplatePickerViewModel TemplatePicker { get; }
}

public sealed class TransactionTemplatePickerViewModel : LocalizedViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Func<bool>? _canUseTemplate;
    public TransactionTemplatePickerViewModel(IServiceScopeFactory scopeFactory, ILocalizationService localization, Func<int, Task> useTemplate, Func<bool>? canUseTemplate = null)
        : base(localization)
    {
        _scopeFactory = scopeFactory;
        _canUseTemplate = canUseTemplate;
        OpenTemplatePickerCommand = new AsyncRelayCommand(OpenTemplatePickerAsync, () => _canUseTemplate?.Invoke() != false);
        ClearSearchCommand = new RelayCommand(() => TemplateSearch = string.Empty, () => TemplateSearch.Length > 0);
        UseTemplateCommand = new AsyncRelayCommand(() => useTemplate(SelectedTemplate!.Id),
            () => SelectedTemplate is not null && !IsLoadingTemplates && _canUseTemplate?.Invoke() != false);
    }
    public void ReportError(string key)
    {
        TemplatePickerError = T(key);
        using var scope = _scopeFactory.CreateScope();
        scope.ServiceProvider.GetService<INotificationService>()?.Publish(key, TemplatePickerError, NotificationSeverity.Error);
        OnPropertyChanged(nameof(TemplatePickerStatus));
    }
    protected override void OnCultureChanged() => FilterTemplates();
    private IReadOnlyList<TransactionTemplateDto> _templates = [];
    private bool _isTemplatePickerOpen;
    private bool _isLoadingTemplates;
    private string _templateSearch = string.Empty;
    private string _templatePickerError = string.Empty;
    private TransactionTemplateDto? _selectedTemplate;

    public bool IsTemplatePickerOpen { get => _isTemplatePickerOpen; set => SetField(ref _isTemplatePickerOpen, value); }
    public bool IsLoadingTemplates { get => _isLoadingTemplates; private set => SetField(ref _isLoadingTemplates, value); }
    public string TemplatePickerError { get => _templatePickerError; private set => SetField(ref _templatePickerError, value); }
    public ObservableCollection<TransactionTemplateDto> FilteredTemplates { get; } = [];
    public TransactionTemplateDto? SelectedTemplate
    {
        get => _selectedTemplate;
        set { SetField(ref _selectedTemplate, value); CommandManager.InvalidateRequerySuggested(); }
    }
    public string TemplateSearch
    {
        get => _templateSearch;
        set { if (SetField(ref _templateSearch, value)) FilterTemplates(); }
    }
    public string TemplatePickerStatus => IsLoadingTemplates ? T("TemplatePicker_Loading")
        : TemplatePickerError.Length > 0 ? TemplatePickerError
        : FilteredTemplates.Count > 0 ? string.Empty
        : _templates.Count == 0 ? T("TemplatePicker_Empty") : T("TemplatePicker_NoMatches");
    public ICommand OpenTemplatePickerCommand { get; }
    public ICommand UseTemplateCommand { get; }
    public ICommand ClearSearchCommand { get; }

    private void FilterTemplates()
    {
        SelectedTemplate = null;
        FilteredTemplates.Clear();
        var query = SearchText(TemplateSearch.Trim());
        foreach (var template in _templates.Where(t => SearchText(t.Name).Contains(query, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase))
            FilteredTemplates.Add(template);
        OnPropertyChanged(nameof(TemplatePickerStatus));
    }

    private static string SearchText(string value) => new(value.Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
        .Select(c => c switch { 'ł' => 'l', 'Ł' => 'L', _ => c }).ToArray());

    public async Task OpenTemplatePickerAsync()
    {
        if (_canUseTemplate?.Invoke() == false) return;
        if (IsTemplatePickerOpen) { IsTemplatePickerOpen = false; return; }
        IsTemplatePickerOpen = true;
        TemplateSearch = string.Empty;
        IsLoadingTemplates = true;
        TemplatePickerError = string.Empty;
        _templates = [];
        FilterTemplates();
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            _templates = await scope.ServiceProvider.GetRequiredService<ListTransactionTemplates>().ExecuteAsync();
        }
        catch (Exception)
        {
            ReportError("TemplatePicker_LoadFailed");
        }
        finally
        {
            IsLoadingTemplates = false;
            FilterTemplates();
        }
    }

}
