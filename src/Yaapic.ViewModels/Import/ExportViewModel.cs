using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Export;
using Yaapic.Core.Import;
using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;

namespace Yaapic.ViewModels.Import;

/// <summary>Drives the export dialog: choose a collection and a format, produce the file or zip, save it.</summary>
public sealed partial class ExportViewModel : ObservableObject
{
    private const string EnvironmentFormat = "postman-environment";

    private readonly ExporterRegistry _registry;
    private readonly ExportSourceReader _reader;
    private readonly ICollectionStore _collections;
    private readonly IEnvironmentStore _environments;
    private readonly IDialogService _dialogs;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private ExportChoice? _selectedCollection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequiresCollection), nameof(ShowEnvironments))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private ExportFormatChoice? _selectedFormat;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private bool _includeGlobals;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? _error;

    [ObservableProperty]
    private bool _saved;

    [ObservableProperty]
    private string? _savedName;

    /// <summary>Initializes a new instance of the <see cref="ExportViewModel"/> class.</summary>
    /// <param name="registry">The known exporters, listed as formats.</param>
    /// <param name="reader">Reads the data to export, secrets blanked.</param>
    /// <param name="collections">The collection store, listing the top-level folders.</param>
    /// <param name="environments">The environment store.</param>
    /// <param name="dialogs">The dialog service, used to save the file and for the report clipboard.</param>
    public ExportViewModel(ExporterRegistry registry, ExportSourceReader reader, ICollectionStore collections,
        IEnvironmentStore environments, IDialogService dialogs)
    {
        _registry = registry;
        _reader = reader;
        _collections = collections;
        _environments = environments;
        _dialogs = dialogs;
        Report = new ImportReportViewModel(dialogs);
        Formats = [.. registry.Exporters.Select(e => new ExportFormatChoice(e.Id, e.DisplayName))];
        _selectedFormat = Formats.FirstOrDefault(f => f.Id == "postman") ?? Formats.FirstOrDefault();
    }

    /// <summary>Gets the collections (top-level folders), sorted by name.</summary>
    public IReadOnlyList<ExportChoice> Collections { get; private set; } = [];

    /// <summary>Gets the environments that can be selected.</summary>
    public IReadOnlyList<ExportEnvironmentChoice> EnvironmentChoices { get; private set; } = [];

    /// <summary>Gets the export formats, one per exporter.</summary>
    public IReadOnlyList<ExportFormatChoice> Formats { get; }

    /// <summary>Gets the report of the last export: what was blanked, skipped or approximated.</summary>
    public ImportReportViewModel Report { get; }

    /// <summary>Gets a value indicating whether the selected format needs a collection.</summary>
    public bool RequiresCollection => CurrentExporter?.RequiresCollection ?? false;

    /// <summary>Gets a value indicating whether the environment list and the globals option are shown (environment format only).</summary>
    public bool ShowEnvironments => SelectedFormat?.Id == EnvironmentFormat;

    /// <summary>Gets a value indicating whether <see cref="Error"/> is set.</summary>
    public bool HasError => Error is not null;

    private IExporter? CurrentExporter => SelectedFormat is { } f ? _registry.Find(f.Id) : null;

    /// <summary>Loads the collections and the environments.</summary>
    /// <param name="preselectedCollectionId">The collection to select, or <see langword="null"/> for the first one.</param>
    /// <returns>A task that completes when the lists are loaded.</returns>
    public async Task LoadAsync(Guid? preselectedCollectionId)
    {
        Error = null;
        Saved = false;
        SavedName = null;
        Report.Load(null);
        try
        {
            var roots = await _collections.LoadTreeAsync(CancellationToken.None);
            Collections = [.. roots.Where(n => n.Kind == CollectionNodeKind.Folder)
                .Select(n => new ExportChoice(n.Id, n.Name))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)];
            var names = Collections.ToDictionary(c => c.Id, c => c.Name);
            EnvironmentChoices = [.. (await _environments.ListAsync(CancellationToken.None))
                .Select(e => new ExportEnvironmentChoice(e.Id, e.Name, e.CollectionId,
                    e.CollectionId is { } id && names.TryGetValue(id, out var n) ? n : null))];
            foreach (var choice in EnvironmentChoices)
            {
                choice.PropertyChanged += (_, _) => ExportCommand.NotifyCanExecuteChanged();
            }
        }
        catch (StorageException ex)
        {
            Error = Strings.ExportFailed(ex.Message);
            Collections = [];
            EnvironmentChoices = [];
        }

        OnPropertyChanged(nameof(Collections));
        OnPropertyChanged(nameof(EnvironmentChoices));
        SelectedCollection = Collections.FirstOrDefault(c => c.Id == preselectedCollectionId) ?? Collections.FirstOrDefault();
        ExportCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedFormatChanged(ExportFormatChoice? value) => IncludeGlobals &= value?.Id == EnvironmentFormat;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        if (CurrentExporter is not { } exporter)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        Saved = false;
        SavedName = null;
        var collectionId = exporter.RequiresCollection ? SelectedCollection?.Id : null;
        var ids = EnvironmentIdsFor(exporter);
        var globals = ShowEnvironments && IncludeGlobals;
        try
        {
            var report = new ImportReport();
            var (name, content) = await Task.Run(async () =>
            {
                var source = await _reader.ReadAsync(collectionId, ids, globals, report, CancellationToken.None);
                var result = exporter.Export(source);
                foreach (var e in result.Report.Entries)
                {
                    report.Add(e.Severity, e.Path, e.Message);
                }

                return ExportPackager.Package(result, source.Name, exporter.FileExtension);
            });
            Report.Load(report);
            Saved = await _dialogs.SaveFileAsync(name, content);
            SavedName = Saved ? name : null;
        }
        catch (ExportException ex)
        {
            Error = ex.Message;
        }
        catch (Exception ex)
        {
            Error = Strings.ExportFailed(ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanExport() =>
        !IsBusy && CurrentExporter is { } exporter
        && (exporter.RequiresCollection ? SelectedCollection is not null : IncludeGlobals || EnvironmentChoices.Any(e => e.IsSelected));

    private List<Guid> EnvironmentIdsFor(IExporter exporter)
    {
        if (!exporter.RequiresCollection)
        {
            return [.. EnvironmentChoices.Where(e => e.IsSelected).Select(e => e.Id)];
        }

        // Postman collections and OpenAPI documents carry no environment; the others take those scoped to the collection.
        if (exporter.Id == "postman" || exporter.Id.StartsWith("openapi", StringComparison.Ordinal) || SelectedCollection is not { } c)
        {
            return [];
        }

        return [.. EnvironmentChoices.Where(e => e.CollectionId == c.Id).Select(e => e.Id)];
    }
}
