using System.Globalization;
using System.Windows.Data;
using Saldo.Domain.Enums;

namespace Saldo.Desktop.Wpf.Localization;

public sealed class TransactionTypeToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not TransactionType type)
        {
            return string.Empty;
        }

        var localization = System.Windows.Application.Current?.Resources["Localization"] as ILocalizationService;
        if (localization is null)
        {
            return type.ToString();
        }

        return type switch
        {
            TransactionType.Expense => localization["Type_Expense"],
            TransactionType.Income => localization["Type_Income"],
            _ => type.ToString()
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
