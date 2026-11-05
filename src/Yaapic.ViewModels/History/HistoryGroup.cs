using System.Collections.ObjectModel;

namespace Yaapic.ViewModels.History;

/// <summary>The history entries of one local day.</summary>
/// <param name="title">"Today", "Yesterday", or the date as <c>yyyy-MM-dd</c>.</param>
/// <param name="items">The entries of the day, newest first.</param>
public sealed class HistoryGroup(string title, ObservableCollection<HistoryItemViewModel> items)
{
    /// <summary>Gets the group title.</summary>
    public string Title { get; } = title;

    /// <summary>Gets the entries of the day, newest first.</summary>
    public ObservableCollection<HistoryItemViewModel> Items { get; } = items;
}
