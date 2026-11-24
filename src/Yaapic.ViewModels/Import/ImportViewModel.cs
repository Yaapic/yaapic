using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Import;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;

namespace Yaapic.ViewModels.Import;

/// <summary>Drives the import dialog: choose a file, a folder or pasted text, preview the report and counts, import.</summary>
public sealed partial class ImportViewModel : ObservableObject
{
    private readonly ImportService _service;
    private readonly IImportInputReader _reader;
    private readonly IDialogService _dialogs;
    private readonly Func<Task> _reloadWorkspace;
    private CancellationTokenSource _cts = new();
    private int _run;
    private ImportInput? _input;
    private ImportPreview? _preview;

    [ObservableProperty]
    private ImportFormatChoice _selectedFormat;

    [ObservableProperty]
    private string? _sourceName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewPastedCommand))]
    private string _pastedText = "";

    [ObservableProperty]
    private bool _includeStaticResources;

    [ObservableProperty]
    private string? _detectedFormat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowStaticResourcesOption))]
    private string? _detectedId;

    [ObservableProperty]
    private int _folders;

    [ObservableProperty]
    private int _requests;

    [ObservableProperty]
    private int _environments;

    [ObservableProperty]
    private int _globalVariables;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private bool _hasPreview;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private bool _isDone;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    [ObservableProperty]
    private string? _resultSummary;

    [ObservableProperty]
    private ImportOutcome? _outcome;

    /// <summary>Initializes a new instance of the <see cref="ImportViewModel"/> class.</summary>
    /// <param name="service">Previews and applies imports.</param>
    /// <param name="registry">The known importers, listed as formats.</param>
    /// <param name="reader">Reads the chosen file or folder.</param>
    /// <param name="dialogs">The dialog service, used for the pickers and the report clipboard.</param>
    /// <param name="reloadWorkspace">Reloads the workspace after an import.</param>
    public ImportViewModel(ImportService service, ImporterRegistry registry, IImportInputReader reader, IDialogService dialogs, Func<Task> reloadWorkspace)
    {
        _service = service;
        _reader = reader;
        _dialogs = dialogs;
        _reloadWorkspace = reloadWorkspace;
        Report = new ImportReportViewModel(dialogs);
        Formats = [new ImportFormatChoice(null, Strings.ImportFormatAuto), .. registry.Importers.Select(i => new ImportFormatChoice(i.Id, i.DisplayName))];
        _selectedFormat = Formats[0];
    }

    /// <summary>Raised after an import was applied.</summary>
    public event EventHandler<ImportOutcome>? Imported;

    /// <summary>Gets the formats: automatic detection first, then one per importer.</summary>
    public IReadOnlyList<ImportFormatChoice> Formats { get; }

    /// <summary>Gets the report of the preview, then of the outcome once imported.</summary>
    public ImportReportViewModel Report { get; }

    /// <summary>Gets a value indicating whether <see cref="Error"/> is set.</summary>
    public bool HasError => Error is not null;

    /// <summary>Gets a value indicating whether the detected format reads static resources (HAR).</summary>
    public bool ShowStaticResourcesOption => DetectedId == "har";

    /// <summary>Gets the latest preview run, so a caller can await it after changing an option.</summary>
    public Task Completion { get; private set; } = Task.CompletedTask;

    /// <summary>Clears the input, the preview and the result; the dialog calls it when it opens.</summary>
    public void Reset()
    {
        Next();
        _input = null;
        SelectedFormat = Formats[0];
        IncludeStaticResources = false;
        PastedText = "";
        SourceName = null;
        Error = null;
        IsBusy = false;
        ClearResult();
        ClearPreview();
        Completion = Task.CompletedTask;
    }

    partial void OnSelectedFormatChanged(ImportFormatChoice value) => Completion = StartPreview();

    partial void OnIncludeStaticResourcesChanged(bool value) => Completion = StartPreview();

    [RelayCommand]
    private async Task ChooseFileAsync() =>
        await ChooseAsync(await _dialogs.PickFileAsync(Strings.ImportChooseFileTitle), (p, ct) => _reader.ReadFileAsync(p, ct));

    [RelayCommand]
    private async Task ChooseFolderAsync() =>
        await ChooseAsync(await _dialogs.PickFolderAsync(Strings.ImportChooseFolderTitle), (p, ct) => _reader.ReadFolderAsync(p, ct));

    [RelayCommand(CanExecute = nameof(HasPastedText))]
    private Task PreviewPastedAsync()
    {
        _input = ImportInput.FromText(Strings.ImportPastedText, PastedText);
        SourceName = Strings.ImportPastedText;
        return Completion = StartPreview();
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync()
    {
        if (_preview is not { } preview)
        {
            return;
        }

        var (ct, run) = Next();
        IsBusy = true;
        Error = null;
        try
        {
            var outcome = await _service.ApplyAsync(preview, ct);
            await _reloadWorkspace();
            Outcome = outcome;
            Report.Load(outcome.Report);
            ResultSummary = Strings.ImportResult(outcome.Requests, outcome.Folders, outcome.Environments);
            IsDone = true;
            Imported?.Invoke(this, outcome);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Error = ex.Message;
        }
        catch (OperationCanceledException)
        {
            // Reset or a newer run took over.
        }
        finally
        {
            if (run == _run)
            {
                IsBusy = false;
            }
        }
    }

    private bool HasPastedText() => !string.IsNullOrWhiteSpace(PastedText);

    private bool CanImport() =>
        HasPreview && !IsBusy && !IsDone && _preview is { Bundle: var b } && (b.RequestCount > 0 || b.Environments.Count > 0 || b.Globals.Count > 0);

    private async Task ChooseAsync(string? path, Func<string, CancellationToken, Task<ImportInput>> read)
    {
        if (path is null)
        {
            return;
        }

        var (ct, run) = Next();
        IsBusy = true;
        Error = null;
        ClearResult();
        try
        {
            _input = await read(path, ct);
        }
        catch (ImportException ex)
        {
            if (run == _run)
            {
                _input = null;
                Fail(ex.Message);
            }

            return;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (run != _run)
        {
            return;
        }

        SourceName = Path.GetFileName(path.TrimEnd('\\', '/'));
        await (Completion = StartPreview());
    }

    private Task StartPreview()
    {
        if (_input is not { } input)
        {
            return Task.CompletedTask;
        }

        var (ct, run) = Next();
        IsBusy = true;
        Error = null;
        ClearResult();
        return PreviewAsync(input, SelectedFormat.Id, new ImportOptions(IncludeStaticResources), ct, run);
    }

    private async Task PreviewAsync(ImportInput input, string? importerId, ImportOptions options, CancellationToken ct, int run)
    {
        try
        {
            var preview = await Task.Run(() => _service.Preview(input, importerId, options), ct);
            if (run != _run)
            {
                return;
            }

            _preview = preview;
            var bundle = preview.Bundle;
            DetectedFormat = preview.Importer.DisplayName;
            DetectedId = preview.Importer.Id;
            Folders = bundle.FolderCount;
            Requests = bundle.RequestCount;
            Environments = bundle.Environments.Count;
            GlobalVariables = bundle.Globals.Count;
            Report.Load(bundle.Report);
            HasPreview = true;
            IsBusy = false;
        }
        catch (ImportException ex)
        {
            if (run == _run)
            {
                Fail(ex.Message);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer run owns the state.
        }
    }

    private void Fail(string message)
    {
        ClearPreview();
        Error = message;
        IsBusy = false;
    }

    private void ClearPreview()
    {
        _preview = null;
        DetectedFormat = null;
        DetectedId = null;
        Folders = Requests = Environments = GlobalVariables = 0;
        Report.Load(null);
        HasPreview = false;
        ImportCommand.NotifyCanExecuteChanged();
    }

    private void ClearResult()
    {
        IsDone = false;
        Outcome = null;
        ResultSummary = null;
    }

    private (CancellationToken Token, int Run) Next()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        return (_cts.Token, ++_run);
    }
}
