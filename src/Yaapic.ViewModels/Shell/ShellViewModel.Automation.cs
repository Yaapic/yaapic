using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Domain.Collections;
using Yaapic.Localization;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.LoadTesting;
using Yaapic.ViewModels.Mocks;
using Yaapic.ViewModels.Monitors;
using Yaapic.ViewModels.Runner;

namespace Yaapic.ViewModels.Shell;

public sealed partial class ShellViewModel
{
    private readonly List<IAsyncDisposable> _ownedAsyncResources = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRequestsSection))]
    [NotifyPropertyChangedFor(nameof(IsRunnerSection))]
    [NotifyPropertyChangedFor(nameof(IsLoadTestSection))]
    [NotifyPropertyChangedFor(nameof(IsMockServersSection))]
    [NotifyPropertyChangedFor(nameof(IsMonitorsSection))]
    [NotifyPropertyChangedFor(nameof(IsCollectionsSelected))]
    [NotifyPropertyChangedFor(nameof(IsHistorySelected))]
    private ShellSection _section = ShellSection.Requests;

    /// <summary>Gets a value indicating whether the main area shows the requests.</summary>
    public bool IsRequestsSection => Section == ShellSection.Requests;

    /// <summary>Gets a value indicating whether the main area shows the Runner.</summary>
    public bool IsRunnerSection => Section == ShellSection.Runner;

    /// <summary>Gets a value indicating whether the main area shows the load test tool.</summary>
    public bool IsLoadTestSection => Section == ShellSection.LoadTest;

    /// <summary>Gets a value indicating whether the main area shows the mock servers.</summary>
    public bool IsMockServersSection => Section == ShellSection.MockServers;

    /// <summary>Gets a value indicating whether the main area shows the monitors.</summary>
    public bool IsMonitorsSection => Section == ShellSection.Monitors;

    /// <summary>Gets a value indicating whether the Collections rail entry is the selected one.</summary>
    public bool IsCollectionsSelected => IsRequestsSection && Mode == SidebarMode.Collections;

    /// <summary>Gets a value indicating whether the History rail entry is the selected one.</summary>
    public bool IsHistorySelected => IsRequestsSection && Mode == SidebarMode.History;

    /// <summary>Gets the Runner view model; null until <see cref="AttachAutomation"/> is called.</summary>
    public RunnerViewModel? Runner { get; private set; }

    /// <summary>Gets the load test view model; null until <see cref="AttachAutomation"/> is called.</summary>
    public LoadTestViewModel? LoadTest { get; private set; }

    /// <summary>Gets the mock servers view model; null until <see cref="AttachAutomation"/> is called.</summary>
    public MockServersViewModel? Mocks { get; private set; }

    /// <summary>Gets the monitors view model; null until <see cref="AttachAutomation"/> is called.</summary>
    public MonitorsViewModel? Monitors { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="AttachAutomation"/> was called.</summary>
    public bool CanAutomate => Runner is not null;

    /// <summary>Gets the status bar text of the Runner: its name when idle, the name and the progress while running.</summary>
    public string RunnerStatusText => Runner is { IsRunning: true } r ? $"{Strings.RunnerStatusBar} {r.ProgressText}" : Strings.RunnerStatusBar;

    /// <summary>Gets the status bar text of the mock servers; empty hides the item.</summary>
    public string MocksStatusText => Mocks?.RunningText ?? "";

    /// <summary>Gets the status bar text of the monitors; empty hides the item.</summary>
    public string MonitorsStatusText => Monitors?.FailingText ?? "";

    /// <summary>Hands the shell the tool view models, which live as long as the shell; the composition root calls it once.</summary>
    /// <param name="runner">The Runner.</param>
    /// <param name="load">The load test tool.</param>
    /// <param name="mocks">The mock servers.</param>
    /// <param name="monitors">The monitors.</param>
    /// <exception cref="InvalidOperationException">Already attached.</exception>
    public void AttachAutomation(RunnerViewModel runner, LoadTestViewModel load, MockServersViewModel mocks, MonitorsViewModel monitors)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(mocks);
        ArgumentNullException.ThrowIfNull(monitors);
        if (CanAutomate)
        {
            throw new InvalidOperationException("The automation tools are already attached.");
        }

        Runner = runner;
        LoadTest = load;
        Mocks = mocks;
        Monitors = monitors;
        runner.PropertyChanged += (_, e) => Forward(e, nameof(RunnerStatusText), nameof(RunnerViewModel.IsRunning), nameof(RunnerViewModel.ProgressText));
        mocks.PropertyChanged += (_, e) => Forward(e, nameof(MocksStatusText), nameof(MockServersViewModel.RunningText));
        monitors.PropertyChanged += (_, e) => Forward(e, nameof(MonitorsStatusText), nameof(MonitorsViewModel.FailingText));
        Collections.Refreshed += (_, _) => UpdateRunFolders();
        UpdateRunFolders();
        OnPropertyChanged(nameof(CanAutomate));
        OnPropertyChanged(nameof(RunnerStatusText));
        OnPropertyChanged(nameof(MocksStatusText));
        OnPropertyChanged(nameof(MonitorsStatusText));
        RunFolderCommand.NotifyCanExecuteChanged();
        LoadTestFolderCommand.NotifyCanExecuteChanged();
        MockFromFolderCommand.NotifyCanExecuteChanged();
        MonitorFolderCommand.NotifyCanExecuteChanged();
        ShowRunnerCommand.NotifyCanExecuteChanged();
        ShowLoadTestCommand.NotifyCanExecuteChanged();
        ShowMockServersCommand.NotifyCanExecuteChanged();
        ShowMonitorsCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(Runner));
        OnPropertyChanged(nameof(LoadTest));
        OnPropertyChanged(nameof(Mocks));
        OnPropertyChanged(nameof(Monitors));
    }

    /// <summary>Shows the Runner in the main area; does nothing when the tools are not attached.</summary>
    public void RequestRunner()
    {
        if (CanAutomate)
        {
            Section = ShellSection.Runner;
        }
    }

    /// <summary>Shows the load test tool in the main area; does nothing when the tools are not attached.</summary>
    public void RequestLoadTest()
    {
        if (CanAutomate)
        {
            Section = ShellSection.LoadTest;
        }
    }

    /// <summary>Shows the mock servers in the main area; does nothing when the tools are not attached.</summary>
    public void RequestMockServers()
    {
        if (CanAutomate)
        {
            Section = ShellSection.MockServers;
        }
    }

    /// <summary>Shows the monitors in the main area; does nothing when the tools are not attached.</summary>
    public void RequestMonitors()
    {
        if (CanAutomate)
        {
            Section = ShellSection.Monitors;
        }
    }

    /// <summary>Opens the Runner on a folder of the tree.</summary>
    /// <param name="node">The tree row.</param>
    [RelayCommand(CanExecute = nameof(CanAutomateFolder))]
    private void RunFolder(CollectionNodeViewModel? node)
    {
        Runner!.Open(node!.Node);
        RequestRunner();
    }

    /// <summary>Opens the load test tool on a folder of the tree.</summary>
    /// <param name="node">The tree row.</param>
    [RelayCommand(CanExecute = nameof(CanAutomateFolder))]
    private void LoadTestFolder(CollectionNodeViewModel? node)
    {
        LoadTest!.Open(node!.Node);
        RequestLoadTest();
    }

    /// <summary>Generates a mock server from a folder of the tree.</summary>
    /// <param name="node">The tree row.</param>
    /// <returns>A task that completes when the server is created.</returns>
    [RelayCommand(CanExecute = nameof(CanAutomateFolder))]
    private async Task MockFromFolderAsync(CollectionNodeViewModel? node)
    {
        await Mocks!.GenerateFromFolderAsync(node!.Node);
        RequestMockServers();
    }

    /// <summary>Creates a monitor for a folder of the tree.</summary>
    /// <param name="node">The tree row.</param>
    /// <returns>A task that completes when the monitor is created.</returns>
    [RelayCommand(CanExecute = nameof(CanAutomateFolder))]
    private async Task MonitorFolderAsync(CollectionNodeViewModel? node)
    {
        await Monitors!.NewForFolderAsync(node!.Id);
        RequestMonitors();
    }

    [RelayCommand(CanExecute = nameof(CanAutomate))]
    private void ShowRunner() => RequestRunner();

    [RelayCommand(CanExecute = nameof(CanAutomate))]
    private void ShowLoadTest() => RequestLoadTest();

    [RelayCommand(CanExecute = nameof(CanAutomate))]
    private void ShowMockServers() => RequestMockServers();

    [RelayCommand(CanExecute = nameof(CanAutomate))]
    private void ShowMonitors() => RequestMonitors();

    private bool CanAutomateFolder(CollectionNodeViewModel? node) => CanAutomate && node is { IsFolder: true };

    /// <summary>Makes the shell dispose a resource asynchronously when the application closes, after the synchronous ones.</summary>
    /// <param name="resource">The resource.</param>
    public void OwnAsync(IAsyncDisposable resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        _ownedAsyncResources.Add(resource);
    }

    /// <summary>Gets the top-level folder of the request open in the active tab.</summary>
    /// <returns>The collection node, or null when there is no active tab or its request is unfiled.</returns>
    internal CollectionNode? ActiveCollectionNode() =>
        Tabs.ActiveTab is { } active ? Collections.FindCollectionNode(active.Id) : null;

    private void UpdateRunFolders()
    {
        Runner!.Setup.UpdateFolders(Collections.Tree);
        LoadTest!.Requests.UpdateFolders(Collections.Tree);
    }

    private void Forward(PropertyChangedEventArgs e, string target, params string[] sources)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || sources.Contains(e.PropertyName))
        {
            OnPropertyChanged(target);
        }
    }
}
