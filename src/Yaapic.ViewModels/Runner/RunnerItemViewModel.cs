using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Core.Runner;
using Yaapic.Domain.Http;

namespace Yaapic.ViewModels.Runner;

/// <summary>One request of the runner setup list.</summary>
public sealed partial class RunnerItemViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    /// <summary>Initializes a new instance of the <see cref="RunnerItemViewModel"/> class.</summary>
    /// <param name="item">The run item.</param>
    public RunnerItemViewModel(RunItem item)
    {
        Item = item;
    }

    /// <summary>Gets the run item.</summary>
    public RunItem Item { get; }

    /// <summary>Gets the request name.</summary>
    public string Name => Item.Request.Name;

    /// <summary>Gets the folder path of the request.</summary>
    public string Path => Item.Path;

    /// <summary>Gets the HTTP method text.</summary>
    public string Method => Item.Request.Method.ToText();
}
