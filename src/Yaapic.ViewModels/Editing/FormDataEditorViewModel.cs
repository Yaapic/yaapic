using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Domain.Http;
using Yaapic.UI.Abstractions;

namespace Yaapic.ViewModels.Editing;

/// <summary>Multipart form editor (text and file fields) that always ends with one empty placeholder row.</summary>
public sealed partial class FormDataEditorViewModel : ObservableObject
{
    private readonly IDialogService? _dialogs;
    private bool _loading;

    /// <summary>Initializes the editor with a single placeholder row.</summary>
    /// <param name="dialogs">The dialog service rows use to pick files; <see langword="null"/> disables picking.</param>
    public FormDataEditorViewModel(IDialogService? dialogs = null)
    {
        _dialogs = dialogs;
        AddRow();
    }

    /// <summary>Raised when a row changes or is removed; never raised by <see cref="Load"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets the rows; the last one is always the empty placeholder.</summary>
    public ObservableCollection<FormDataRowViewModel> Rows { get; } = [];

    /// <summary>Gets the number of rows that are enabled and not the empty placeholder.</summary>
    public int ActiveCount => Rows.Count(r => r.Enabled && !r.IsEmpty);

    /// <summary>Replaces all rows with <paramref name="fields"/> followed by a placeholder, without raising <see cref="Changed"/>.</summary>
    /// <param name="fields">The fields to show, in order.</param>
    public void Load(IEnumerable<FormField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        _loading = true;
        try
        {
            foreach (var row in Rows)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
            }

            Rows.Clear();
            foreach (var field in fields)
            {
                AddRow(field);
            }

            AddRow();
        }
        finally
        {
            _loading = false;
            OnPropertyChanged(nameof(ActiveCount));
        }
    }

    /// <summary>Returns the non-empty rows as fields, in order.</summary>
    /// <returns>The fields.</returns>
    public IReadOnlyList<FormField> ToFields() =>
        [.. Rows.Where(r => !r.IsEmpty).Select(r => new FormField(r.Key, r.Value, r.Enabled, r.Kind))];

    /// <summary>Removes a row; the placeholder row cannot be removed.</summary>
    /// <param name="row">The row to remove.</param>
    [RelayCommand]
    private void RemoveRow(FormDataRowViewModel row)
    {
        if (row == Rows[^1] || !Rows.Remove(row))
        {
            return;
        }

        row.PropertyChanged -= OnRowPropertyChanged;
        OnPropertyChanged(nameof(ActiveCount));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void AddRow(FormField? field = null)
    {
        var row = new FormDataRowViewModel(_dialogs);
        if (field is not null)
        {
            row.Key = field.Key;
            row.Value = field.Value;
            row.Enabled = field.Enabled;
            row.Kind = field.Kind;
        }

        row.PropertyChanged += OnRowPropertyChanged;
        Rows.Add(row);
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loading || e.PropertyName is nameof(FormDataRowViewModel.IsEmpty) or nameof(FormDataRowViewModel.IsFile))
        {
            return;
        }

        if (sender == Rows[^1] && !((FormDataRowViewModel)sender).IsEmpty)
        {
            AddRow();
        }

        OnPropertyChanged(nameof(ActiveCount));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
