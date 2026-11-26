# M6-20: ViewModel: runner setup

Status: done
Platform: any
Depends on: M6-08, M6-14

## Goal

`RunnerSetupViewModel` holds the left side of the Runner window: the requests to run and their order, the options and the
data file (ADR 0018, "UI").

## Design decisions

- Read first: ADR 0018, M6-08, M6-14 (`RunPlan`, `RunPlanBuilder`, `RunOptions`), `src/Yaapic.ViewModels/Import/ImportViewModel.cs`
  (CommunityToolkit `[ObservableProperty]`/`[RelayCommand]` style, `IImportInputReader` use, error handling),
  `src/Yaapic.ViewModels/Collections/CollectionNodeViewModel.cs`, `src/Yaapic.Domain/Http/HttpMethodKindExtensions.cs`.
- `src/Yaapic.ViewModels/Runner/RunnerItemViewModel.cs` (`ObservableObject`): `RunItem Item`, `string Name`, `string Path`,
  `string Method` (`ToText()`), `bool IsSelected` (default true).
- `src/Yaapic.ViewModels/Runner/RunnerSetupViewModel.cs`, constructor `(IImportInputReader reader, IDialogService dialogs)`:
  - `void Load(CollectionNode folder)`: `FolderId`, `FolderName`, `Items` (`ObservableCollection<RunnerItemViewModel>` from
    `RunPlanBuilder.ListRequests`), resets options to defaults and clears the data file.
  - `MoveUpCommand(RunnerItemViewModel)` / `MoveDownCommand(...)` (disabled at the ends), `SelectAllCommand`, `SelectNoneCommand`,
    `SelectedCount` (updates when an item's `IsSelected` changes).
  - Options: `Iterations` (int, default 1), `DelayMs` (int, default 0), `StopOnFailure` (false), `PersistVariables` (true).
    `ValidationError` (string?): `Strings.RunnerIterationsRange` when not 1..10000, `Strings.RunnerDelayRange` when not 0..60000,
    `Strings.RunnerNothingSelected` when no item is selected; `CanRun` = no validation error and not `IsLoadingData`.
  - Data: `ChooseDataFileCommand` (`dialogs.PickFileAsync(Strings.RunnerChooseDataTitle)`; canceled: nothing changes; then
    `reader.ReadFileAsync` and `IterationDataReader.Read`), `ClearDataCommand`. Results: `DataFileName`, `DataRowCount`,
    `DataColumns` (`IReadOnlyList<string>`), `DataPreview` (`IReadOnlyList<IReadOnlyList<string>>`, the first 5 rows aligned on
    `DataColumns`, `""` for missing cells), `HasData`, `DataError` (the `ImportException` or `IterationDataException` message; the
    previous data is cleared). Loading data sets `Iterations = DataRowCount` (at least 1, at most 10000).
  - `RunPlan BuildPlan()`: selected items in the current order, the options, the rows (or none); throws
    `InvalidOperationException` when `CanRun` is false.
- Strings (EN and FR): `runner.title` = "Runner"; `runner.requests` = "Requests"; `runner.selectAll` = "Select all";
  `runner.selectNone` = "Select none"; `runner.selectedCount` = "{0} of {1} selected"; `runner.iterations` = "Iterations";
  `runner.delay` = "Delay (ms)"; `runner.stopOnFailure` = "Stop on failure"; `runner.persistVariables` = "Keep variable values";
  `runner.data` = "Data file"; `runner.chooseData` = "Choose a CSV or JSON file..."; `runner.chooseDataTitle` = "Choose a data file";
  `runner.clearData` = "Remove"; `runner.dataSummary` = "{0}: {1} rows, {2} columns"; `runner.iterationsRange` = "Iterations must be
  between 1 and 10000."; `runner.delayRange` = "The delay must be between 0 and 60000 ms."; `runner.nothingSelected` = "Select at
  least one request."; `runner.moveUp` = "Move up"; `runner.moveDown` = "Move down".

## Out of scope

Running and results (M6-21), the windows (M6-44, M6-51).

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests/Runner/` with `FakeDialogService` and a fake `IImportInputReader`: `Load` order and paths;
  move up/down and ends; select all/none and `SelectedCount`; each validation message; data file loads (columns, preview of 5 rows,
  iterations set), canceled picker, reader error and parse error set `DataError`; `ClearData`; `BuildPlan` keeps order and selection,
  throws when it cannot run.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the runner setup view model`
