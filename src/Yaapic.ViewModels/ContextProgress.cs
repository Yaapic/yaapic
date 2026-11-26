namespace Yaapic.ViewModels;

// The services report from a worker thread and the UI frameworks reject property changes from it (WinUI: 0x8001010E),
// so reports are posted back to the context the operation started on; without one (tests) they run inline.
internal sealed class ContextProgress<T>(Action<T> report) : IProgress<T>
{
    private readonly SynchronizationContext? _context = SynchronizationContext.Current;

    public void Report(T value)
    {
        if (_context is null)
        {
            report(value);
        }
        else
        {
            _context.Post(_ => report(value), null);
        }
    }
}
