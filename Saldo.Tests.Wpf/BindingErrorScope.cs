using System.Diagnostics;

namespace Saldo.Tests.Wpf;

internal sealed class BindingErrorScope : TraceListener
{
    private static readonly object Gate = new();
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private readonly SourceLevels _previousLevel;
    public List<string> Errors { get; } = [];

    public BindingErrorScope()
    {
        Monitor.Enter(Gate);
        _previousLevel = PresentationTraceSources.DataBindingSource.Switch.Level;
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        PresentationTraceSources.DataBindingSource.Listeners.Add(this);
    }

    public override void Write(string? message)
    {
        if (Environment.CurrentManagedThreadId == _threadId && message is not null) Errors.Add(message);
    }

    public override void WriteLine(string? message) => Write(message);

    protected override void Dispose(bool disposing)
    {
        PresentationTraceSources.DataBindingSource.Listeners.Remove(this);
        PresentationTraceSources.DataBindingSource.Switch.Level = _previousLevel;
        Monitor.Exit(Gate);
        base.Dispose(disposing);
    }
}
