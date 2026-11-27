using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Mocks;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Mocks;

/// <summary>One row of the mock server list.</summary>
public sealed partial class MockServerItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BaseUrl))]
    private int _port;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isRunning;

    /// <summary>Initializes a new instance of the <see cref="MockServerItemViewModel"/> class.</summary>
    /// <param name="server">The server shown.</param>
    /// <param name="isRunning">Whether it runs.</param>
    public MockServerItemViewModel(MockServerDefinition server, bool isRunning)
    {
        ArgumentNullException.ThrowIfNull(server);
        Id = server.Id;
        _name = server.Name;
        _port = server.Port;
        _isRunning = isRunning;
    }

    /// <summary>Gets the server identifier.</summary>
    public Guid Id { get; }

    /// <summary>Gets the localized running state.</summary>
    public string StatusText => IsRunning ? Strings.MockRunning : Strings.MockStopped;

    /// <summary>Gets the base URL of the server.</summary>
    public string BaseUrl => $"http://127.0.0.1:{Port}";

    internal void Update(MockServerDefinition server)
    {
        Name = server.Name;
        Port = server.Port;
    }
}
