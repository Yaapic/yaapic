using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Import;
using Yaapic.Core.Runner;
using Yaapic.Domain.Collections;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;

namespace Yaapic.ViewModels.Runner;

/// <summary>The left side of the Runner window: the requests to run and their order, the options and the data file.</summary>
public sealed partial class RunnerSetupViewModel : ObservableObject
{
    private const int PreviewRows = 5;

    private readonly IImportInputReader _reader;
    private readonly IDialogService _dialogs;
    private IReadOnlyList<IterationDataRow> _rows = [];

    [ObservableProperty]
    private Guid _folderId;

    [ObservableProperty]
    private string _folderName = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationError))]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    private int _iterations = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ValidationError))]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    private int _delayMs;

    [ObservableProperty]
    private bool _stopOnFailure;

    [ObservableProperty]
    private bool _persistVariables = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    private bool _isLoadingData;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasData))]
    private string? _dataFileName;

    [ObservableProperty]
    private string? _dataError;

    [ObservableProperty]
    private int _dataRowCount;

    [ObservableProperty]
    private IReadOnlyList<string> _dataColumns = [];

    [ObservableProperty]
    private IReadOnlyList<IReadOnlyList<string>> _dataPreview = [];

    /// <summary>Initializes a new instance of the <see cref="RunnerSetupViewModel"/> class.</summary>
    /// <param name="reader">Reads the chosen data file.</param>
    /// <param name="dialogs">The dialog service, used for the file picker.</param>
    public RunnerSetupViewModel(IImportInputReader reader, IDialogService dialogs)
    {
        _reader = reader;
        _dialogs = dialogs;
    }

    /// <summary>Gets the requests, in run order.</summary>
    public ObservableCollection<RunnerItemViewModel> Items { get; } = [];

    /// <summary>Gets the number of selected requests.</summary>
    public int SelectedCount => Items.Count(static i => i.IsSelected);

    /// <summary>Gets a value indicating whether a data file is loaded.</summary>
    public bool HasData => DataFileName is not null;

    /// <summary>Gets the first validation error, or <see langword="null"/> when the options are valid.</summary>
    public string? ValidationError =>
        Iterations is < 1 or > 10000 ? Strings.RunnerIterationsRange
        : DelayMs is < 0 or > 60000 ? Strings.RunnerDelayRange
        : SelectedCount == 0 ? Strings.RunnerNothingSelected
        : null;

    /// <summary>Gets a value indicating whether the run can start.</summary>
    public bool CanRun => ValidationError is null && !IsLoadingData;

    /// <summary>Loads the requests of a folder and resets the options and the data file.</summary>
    /// <param name="folder">The run folder.</param>
    public void Load(CollectionNode folder)
    {
        IReadOnlyList<RunItem> found = RunPlanBuilder.ListRequests(folder);
        foreach (RunnerItemViewModel old in Items)
        {
            old.PropertyChanged -= OnItemChanged;
        }

        Items.Clear();
        foreach (RunItem item in found)
        {
            RunnerItemViewModel vm = new(item);
            vm.PropertyChanged += OnItemChanged;
            Items.Add(vm);
        }

        FolderId = folder.Id;
        FolderName = folder.Name;
        Iterations = 1;
        DelayMs = 0;
        StopOnFailure = false;
        PersistVariables = true;
        ClearData();
        RefreshSelection();
        RefreshMoves();
    }

    /// <summary>Builds the plan of the run.</summary>
    /// <returns>The selected requests in order, the options and the data rows.</returns>
    /// <exception cref="InvalidOperationException">The run cannot start.</exception>
    public RunPlan BuildPlan()
    {
        if (!CanRun)
        {
            throw new InvalidOperationException("The run setup is not valid.");
        }

        return new RunPlan(
            FolderName,
            [.. Items.Where(static i => i.IsSelected).Select(static i => i.Item)],
            new RunOptions(Iterations, DelayMs, StopOnFailure, PersistVariables),
            _rows);
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp(RunnerItemViewModel item) => Move(item, -1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown(RunnerItemViewModel item) => Move(item, 1);

    [RelayCommand]
    private void SelectAll() => SetAll(true);

    [RelayCommand]
    private void SelectNone() => SetAll(false);

    [RelayCommand]
    private async Task ChooseDataFileAsync()
    {
        if (await _dialogs.PickFileAsync(Strings.RunnerChooseDataTitle) is not { } path)
        {
            return;
        }

        IsLoadingData = true;
        try
        {
            ImportInput input = await _reader.ReadFileAsync(path, CancellationToken.None);
            IterationData data = IterationDataReader.Read(path, input.Single.Text);
            _rows = data.Rows;
            DataFileName = data.FileName;
            DataRowCount = data.Rows.Count;
            DataColumns = data.Columns;
            DataPreview = [.. data.Rows.Take(PreviewRows).Select(r => Cells(r, data.Columns))];
            DataError = null;
            Iterations = Math.Clamp(data.Rows.Count, 1, 10000);
        }
        catch (Exception e) when (e is ImportException or IterationDataException)
        {
            ClearData();
            DataError = e.Message;
        }
        finally
        {
            IsLoadingData = false;
        }
    }

    [RelayCommand]
    private void ClearData()
    {
        _rows = [];
        DataFileName = null;
        DataRowCount = 0;
        DataColumns = [];
        DataPreview = [];
        DataError = null;
    }

    private static IReadOnlyList<string> Cells(IterationDataRow row, IReadOnlyList<string> columns) =>
        [.. columns.Select(c => row.TryGet(c, out string value) ? value : "")];

    private bool CanMoveUp(RunnerItemViewModel? item) => item is not null && Items.IndexOf(item) > 0;

    private bool CanMoveDown(RunnerItemViewModel? item) => item is not null && Items.IndexOf(item) is var i && i >= 0 && i < Items.Count - 1;

    private void Move(RunnerItemViewModel item, int delta)
    {
        int index = Items.IndexOf(item);
        Items.Move(index, index + delta);
        RefreshMoves();
    }

    private void RefreshMoves()
    {
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    private void SetAll(bool selected)
    {
        foreach (RunnerItemViewModel item in Items)
        {
            item.IsSelected = selected;
        }
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(RunnerItemViewModel.IsSelected))
        {
            RefreshSelection();
        }
    }

    private void RefreshSelection()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(ValidationError));
        OnPropertyChanged(nameof(CanRun));
    }
}
