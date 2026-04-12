using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace Saldo.Desktop.Wpf.Controls;

public class AutocompleteComboBox : Control
{
    private const string EditableTextBoxPartName = "PART_EditableTextBox";
    private const string ClearButtonPartName = "PART_ClearButton";
    private const string DropDownButtonPartName = "PART_DropDownButton";
    private const string ItemsListPartName = "PART_ItemsList";

    private TextBox? _editableTextBox;
    private ButtonBase? _clearButton;
    private ToggleButton? _dropDownButton;
    private ListBox? _itemsList;
    private Popup? _popup;
    private ICollectionView? _filteredView;
    private bool _suppressTextHandling;
    private bool _hasAppliedDefaultSelection;

    static AutocompleteComboBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(AutocompleteComboBox),
            new FrameworkPropertyMetadata(typeof(AutocompleteComboBox)));
    }

    public AutocompleteComboBox()
    {
        Focusable = true;
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(AutocompleteComboBox),
            new PropertyMetadata(null, OnItemsSourceChanged));

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(object),
            typeof(AutocompleteComboBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

    public object? DefaultSelectedItem
    {
        get => GetValue(DefaultSelectedItemProperty);
        set => SetValue(DefaultSelectedItemProperty, value);
    }

    public static readonly DependencyProperty DefaultSelectedItemProperty =
        DependencyProperty.Register(
            nameof(DefaultSelectedItem),
            typeof(object),
            typeof(AutocompleteComboBox),
            new PropertyMetadata(null, OnDefaultSelectedItemChanged));

    public string? DisplayMemberPath
    {
        get => (string?)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public static readonly DependencyProperty DisplayMemberPathProperty =
        DependencyProperty.Register(
            nameof(DisplayMemberPath),
            typeof(string),
            typeof(AutocompleteComboBox),
            new PropertyMetadata(string.Empty, OnDisplayMemberPathChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            nameof(Text),
            typeof(string),
            typeof(AutocompleteComboBox),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));

    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    public static readonly DependencyProperty IsDropDownOpenProperty =
        DependencyProperty.Register(
            nameof(IsDropDownOpen),
            typeof(bool),
            typeof(AutocompleteComboBox),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public IEnumerable? FilteredItemsSource
    {
        get => (IEnumerable?)GetValue(FilteredItemsSourceProperty);
        private set => SetValue(FilteredItemsSourcePropertyKey, value);
    }

    private static readonly DependencyPropertyKey FilteredItemsSourcePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(FilteredItemsSource),
            typeof(IEnumerable),
            typeof(AutocompleteComboBox),
            new PropertyMetadata(null));

    public static readonly DependencyProperty FilteredItemsSourceProperty =
        FilteredItemsSourcePropertyKey.DependencyProperty;

    public override void OnApplyTemplate()
    {
        if (_editableTextBox is not null)
        {
            _editableTextBox.TextChanged -= EditableTextBox_TextChanged;
            _editableTextBox.GotKeyboardFocus -= EditableTextBox_GotKeyboardFocus;
        }

        if (_clearButton is not null)
        {
            _clearButton.Click -= ClearButton_Click;
        }

        if (_dropDownButton is not null)
        {
            _dropDownButton.Checked -= DropDownButton_Checked;
            _dropDownButton.Unchecked -= DropDownButton_Unchecked;
        }

        if (_itemsList is not null)
        {
            _itemsList.PreviewMouseLeftButtonUp -= ItemsList_PreviewMouseLeftButtonUp;
        }

        if (_popup is not null)
        {
            _popup.LostKeyboardFocus -= Popup_LostKeyboardFocus;
        }

        base.OnApplyTemplate();

        _editableTextBox = GetTemplateChild(EditableTextBoxPartName) as TextBox;
        _clearButton = GetTemplateChild(ClearButtonPartName) as ButtonBase;
        _dropDownButton = GetTemplateChild(DropDownButtonPartName) as ToggleButton;
        _itemsList = GetTemplateChild(ItemsListPartName) as ListBox;
        _popup = GetTemplateChild("PART_Popup") as Popup;

        if (_editableTextBox is not null)
        {
            _editableTextBox.TextChanged += EditableTextBox_TextChanged;
            _editableTextBox.GotKeyboardFocus += EditableTextBox_GotKeyboardFocus;
        }

        if (_clearButton is not null)
        {
            _clearButton.Click += ClearButton_Click;
        }

        if (_dropDownButton is not null)
        {
            _dropDownButton.Checked += DropDownButton_Checked;
            _dropDownButton.Unchecked += DropDownButton_Unchecked;
        }

        if (_itemsList is not null)
        {
            _itemsList.PreviewMouseLeftButtonUp += ItemsList_PreviewMouseLeftButtonUp;
        }

        if (_popup is not null)
        {
            _popup.LostKeyboardFocus += Popup_LostKeyboardFocus;
        }

        UpdateFilteredView();
        SyncTextWithSelection();
        ApplyDefaultSelectionIfNeeded();
        EnsureEmptyInitialSelection();
        UpdateClearButtonVisibility();
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);
        FocusEditableTextBox();
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(ClosePopupIfFocusOutside));
    }

    private void Popup_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(ClosePopupIfFocusOutside));
    }

    private void ClosePopupIfFocusOutside()
    {
        if (_editableTextBox?.IsKeyboardFocusWithin == true)
        {
            return;
        }

        if (_itemsList?.IsKeyboardFocusWithin == true)
        {
            return;
        }

        if (_popup?.IsKeyboardFocusWithin == true)
        {
            return;
        }

        IsDropDownOpen = false;
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AutocompleteComboBox)d).UpdateFilteredView();
    }

    private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AutocompleteComboBox)d).HandleSelectedItemChanged();
    }

    private static void OnDisplayMemberPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AutocompleteComboBox)d).RefreshFilter();
        ((AutocompleteComboBox)d).SyncTextWithSelection();
    }

    private static void OnDefaultSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (AutocompleteComboBox)d;
        control._hasAppliedDefaultSelection = false;
        control.ApplyDefaultSelectionIfNeeded();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((AutocompleteComboBox)d).HandleTextChanged();
    }

    private void HandleTextChanged()
    {
        if (_suppressTextHandling)
        {
            UpdateClearButtonVisibility();
            return;
        }

        RefreshFilter();
        ClearSelectionWhileTyping();
        UpdateClearButtonVisibility();

        if (!string.IsNullOrWhiteSpace(Text))
        {
            IsDropDownOpen = true;
        }
    }

    private void HandleSelectedItemChanged()
    {
        if (_suppressTextHandling)
        {
            return;
        }

        if (_editableTextBox is not null && _editableTextBox.IsKeyboardFocusWithin && !string.IsNullOrWhiteSpace(Text))
        {
            var selectedTextDuringTyping = GetItemText(SelectedItem);
            if (!string.Equals(selectedTextDuringTyping, Text, StringComparison.CurrentCultureIgnoreCase))
            {
                return;
            }
        }

        if (DefaultSelectedItem is null && SelectedItem is not null && string.IsNullOrWhiteSpace(Text))
        {
            _suppressTextHandling = true;
            try
            {
                SelectedItem = null;
                if (_itemsList is not null)
                {
                    _itemsList.SelectedItem = null;
                    _itemsList.SelectedIndex = -1;
                }
            }
            finally
            {
                _suppressTextHandling = false;
            }

            UpdateClearButtonVisibility();
            return;
        }

        if (SelectedItem is null)
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                return;
            }

            _suppressTextHandling = true;
            try
            {
                if (_editableTextBox is not null)
                {
                    _editableTextBox.Text = string.Empty;
                }
                else
                {
                    Text = string.Empty;
                }
            }
            finally
            {
                _suppressTextHandling = false;
            }

            UpdateClearButtonVisibility();
            return;
        }

        SyncTextWithSelection();
        UpdateClearButtonVisibility();
    }

    private void EnsureEmptyInitialSelection()
    {
        if (DefaultSelectedItem is not null || !string.IsNullOrWhiteSpace(Text) || SelectedItem is null)
        {
            return;
        }

        _suppressTextHandling = true;
        try
        {
            SelectedItem = null;
            if (_itemsList is not null)
            {
                _itemsList.SelectedItem = null;
                _itemsList.SelectedIndex = -1;
            }
        }
        finally
        {
            _suppressTextHandling = false;
        }
    }

    private void EditableTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextHandling)
        {
            return;
        }

        _suppressTextHandling = true;
        try
        {
            Text = _editableTextBox?.Text ?? string.Empty;
        }
        finally
        {
            _suppressTextHandling = false;
        }

        HandleTextChanged();
    }

    private void EditableTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        MoveCaretToEnd();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _suppressTextHandling = true;
        try
        {
            SelectedItem = null;
            Text = string.Empty;
            if (_editableTextBox is not null)
            {
                _editableTextBox.Text = string.Empty;
            }
        }
        finally
        {
            _suppressTextHandling = false;
        }

        IsDropDownOpen = false;
        UpdateClearButtonVisibility();
        RefreshFilter();
        FocusEditableTextBox();
    }

    private void DropDownButton_Checked(object sender, RoutedEventArgs e)
    {
        IsDropDownOpen = true;
    }

    private void DropDownButton_Unchecked(object sender, RoutedEventArgs e)
    {
        IsDropDownOpen = false;
    }

    private void ItemsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_itemsList is null)
        {
            return;
        }

        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (ItemsControl.ContainerFromElement(_itemsList, source) is not ListBoxItem container)
        {
            return;
        }

        ApplySelection(container.DataContext);
        e.Handled = true;
    }

    private void ClearSelectionWhileTyping()
    {
        if (SelectedItem is null || string.IsNullOrWhiteSpace(Text))
        {
            return;
        }

        if (string.Equals(GetItemText(SelectedItem), Text, StringComparison.CurrentCultureIgnoreCase))
        {
            return;
        }

        _suppressTextHandling = true;
        try
        {
            SelectedItem = null;
            if (_itemsList is not null)
            {
                _itemsList.SelectedItem = null;
                _itemsList.SelectedIndex = -1;
            }
        }
        finally
        {
            _suppressTextHandling = false;
        }
    }

    private void ApplySelection(object? selectedItem)
    {
        _suppressTextHandling = true;
        try
        {
            SelectedItem = selectedItem;
            Text = GetItemText(selectedItem);
            if (_editableTextBox is not null)
            {
                _editableTextBox.Text = Text;
            }
        }
        finally
        {
            _suppressTextHandling = false;
        }

        IsDropDownOpen = false;
        UpdateClearButtonVisibility();
        FocusEditableTextBox();
    }

    private void UpdateFilteredView()
    {
        if (ItemsSource is null)
        {
            _filteredView = null;
            FilteredItemsSource = null;
            return;
        }

        _filteredView = CreateView(ItemsSource);
        FilteredItemsSource = _filteredView;
        RefreshFilter();
        ApplyDefaultSelectionIfNeeded();
    }

    private void ApplyDefaultSelectionIfNeeded()
    {
        if (_hasAppliedDefaultSelection || DefaultSelectedItem is null)
        {
            return;
        }

        if (SelectedItem is not null || !string.IsNullOrWhiteSpace(Text) || ItemsSource is null)
        {
            return;
        }

        var candidate = ItemsSource.Cast<object?>().FirstOrDefault(item => Equals(item, DefaultSelectedItem));
        if (candidate is null)
        {
            return;
        }

        _hasAppliedDefaultSelection = true;
        _suppressTextHandling = true;
        try
        {
            SelectedItem = candidate;
        }
        finally
        {
            _suppressTextHandling = false;
        }

        SyncTextWithSelection();
        UpdateClearButtonVisibility();
    }

    private void RefreshFilter()
    {
        if (_filteredView is null)
        {
            return;
        }

        var text = Text ?? string.Empty;
        _filteredView.Filter = item => MatchesFilter(item, text);
        _filteredView.Refresh();
    }

    private static ICollectionView CreateView(IEnumerable source)
    {
        if (source is IList list)
        {
            return new ListCollectionView(list);
        }

        var copy = new List<object?>();
        foreach (var item in source)
        {
            copy.Add(item);
        }

        return new ListCollectionView(copy);
    }

    private bool MatchesFilter(object? item, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var itemText = GetItemText(item);
        if (string.IsNullOrWhiteSpace(itemText))
        {
            return false;
        }

        return CultureInfo.CurrentCulture.CompareInfo.IndexOf(
            itemText,
            text,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
    }

    private void SyncTextWithSelection()
    {
        if (SelectedItem is null)
        {
            return;
        }

        var itemText = GetItemText(SelectedItem);
        if (!string.Equals(Text, itemText, StringComparison.CurrentCulture))
        {
            _suppressTextHandling = true;
            try
            {
                Text = itemText;
                if (_editableTextBox is not null)
                {
                    _editableTextBox.Text = itemText;
                }
            }
            finally
            {
                _suppressTextHandling = false;
            }
        }
    }

    private string GetItemText(object? item)
    {
        if (item is null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(DisplayMemberPath))
        {
            var value = GetPropertyPathValue(item, DisplayMemberPath!);
            return value?.ToString() ?? string.Empty;
        }

        return item.ToString() ?? string.Empty;
    }

    private static object? GetPropertyPathValue(object source, string path)
    {
        object? current = source;
        foreach (var part in path.Split('.'))
        {
            if (current is null)
            {
                return null;
            }

            var property = current.GetType().GetProperty(part);
            if (property is null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
    }

    private void UpdateClearButtonVisibility()
    {
        if (_clearButton is null)
        {
            return;
        }

        _clearButton.Visibility = !string.IsNullOrWhiteSpace(Text) || SelectedItem is not null
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void FocusEditableTextBox()
    {
        if (_editableTextBox is not null)
        {
            _editableTextBox.Focus();
            MoveCaretToEnd();
        }
        else
        {
            Focus();
        }
    }

    private void MoveCaretToEnd()
    {
        if (_editableTextBox is null)
        {
            return;
        }

        _editableTextBox.Select(_editableTextBox.Text.Length, 0);
        _editableTextBox.CaretIndex = _editableTextBox.Text.Length;
    }
}
