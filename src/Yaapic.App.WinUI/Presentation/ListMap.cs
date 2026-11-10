using System.Collections.ObjectModel;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Responses;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Adapts view-model collections for <c>ItemsSource</c>; used through <c>x:Bind</c> function binding.</summary>
public static class ListMap
{
    /// <summary>Copies entries into an observable collection.</summary>
    /// <param name="entries">The entries to show.</param>
    /// <returns>
    /// An <see cref="ObservableCollection{T}"/>: under Native AOT, CsWinRT marshals it as a bindable vector, whereas a
    /// generic <c>IReadOnlyList&lt;T&gt;</c> or <c>List&lt;T&gt;</c> of a non-WinRT type kills the process with a
    /// stowed XAML exception once the list is not empty.
    /// </returns>
    public static ObservableCollection<KeyValueEntry> Entries(IReadOnlyList<KeyValueEntry> entries) => new(entries);
    /// <summary>Copies test results into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The test rows to show.</param>
    /// <returns>An observable collection of the rows.</returns>
    public static ObservableCollection<TestResultItemViewModel> TestItems(IReadOnlyList<TestResultItemViewModel> items) => new(items);

    /// <summary>Copies script errors into an observable collection (see <see cref="Entries"/> for why).</summary>
    /// <param name="items">The error blocks to show.</param>
    /// <returns>An observable collection of the errors.</returns>
    public static ObservableCollection<ScriptErrorItemViewModel> ScriptErrors(IReadOnlyList<ScriptErrorItemViewModel> items) => new(items);
}
