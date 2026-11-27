using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Mocks;
using Yaapic.ViewModels.Editing;

namespace Yaapic.ViewModels.Mocks;

/// <summary>The editor of one mock route.</summary>
public sealed partial class MockRouteViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private string _method;

    [ObservableProperty]
    private string _path;

    [ObservableProperty]
    private int _status;

    [ObservableProperty]
    private string _body;

    [ObservableProperty]
    private int _delayMs;

    [ObservableProperty]
    private string _issueText = "";

    /// <summary>Initializes a new instance of the <see cref="MockRouteViewModel"/> class.</summary>
    /// <param name="route">The route to edit.</param>
    public MockRouteViewModel(MockRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);
        Id = route.Id;
        _name = route.Name;
        _enabled = route.Enabled;
        _method = route.Method;
        _path = route.Path;
        _status = route.Status;
        _body = route.Body;
        _delayMs = route.DelayMs;
        Headers.Load(route.Headers);
        Headers.Changed += (_, _) => Touch();
    }

    /// <summary>Raised when an edited value changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the route identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the methods to choose from.</summary>
    public IReadOnlyList<string> Methods => MockMethods.All;

    /// <summary>Gets the response header editor.</summary>
    public KeyValueEditorViewModel Headers { get; } = new();

    /// <summary>Builds the route from the edited values.</summary>
    /// <returns>The route.</returns>
    public MockRoute ToRoute() => new(Id, Name, Enabled, Method, Path, Status, Headers.ToEntries(), Body, DelayMs);

    /// <inheritdoc/>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName != nameof(IssueText))
        {
            Touch();
        }
    }

    private void Touch()
    {
        IssueText = "";
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
