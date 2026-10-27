using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Saldo.Desktop.Wpf.Controls;

// Amounts are edited by digit. Keep mouse selection independent of WPF's
// word-selection and text drag-and-drop state, including grouped numbers.
internal static class AmountMouseSelection
{
    private static readonly DependencyProperty AnchorProperty = DependencyProperty.RegisterAttached(
        "Anchor", typeof(int), typeof(AmountMouseSelection), new PropertyMetadata(-1));

    internal static bool IsActive(TextBox box) => (int)box.GetValue(AnchorProperty) >= 0;

    internal static void Begin(TextBox box, Point point)
    {
        var anchor = GetCaretIndex(box, point);
        box.SetValue(AnchorProperty, anchor);
        Extend(box, point);
    }

    internal static void Extend(TextBox box, Point point)
    {
        if (!IsActive(box)) return;
        var anchor = Math.Min((int)box.GetValue(AnchorProperty), box.Text.Length);
        var end = GetCaretIndex(box, point);
        if (box.SelectionStart == Math.Min(anchor, end) && box.SelectionLength == Math.Abs(end - anchor)) return;

        box.Select(anchor, 0);
        // These commands also preserve the active selection end when dragging left,
        // so subsequent Shift+arrow input behaves like normal TextBox selection.
        var command = end < anchor ? EditingCommands.SelectLeftByCharacter : EditingCommands.SelectRightByCharacter;
        for (var i = 0; i < Math.Abs(end - anchor); i++) command.Execute(null, box);
    }

    internal static void End(TextBox box)
    {
        box.ClearValue(AnchorProperty);
        if (box.IsMouseCaptured) box.ReleaseMouseCapture();
    }

    private static int GetCaretIndex(TextBox box, Point point)
    {
        var index = box.GetCharacterIndexFromPoint(point, true);
        if (index < 0) return 0;
        var leading = box.GetRectFromCharacterIndex(index);
        var trailing = box.GetRectFromCharacterIndex(Math.Min(index + 1, box.Text.Length));
        return !leading.IsEmpty && !trailing.IsEmpty && Math.Abs(point.X - trailing.X) < Math.Abs(point.X - leading.X)
            ? Math.Min(index + 1, box.Text.Length) : index;
    }
}
