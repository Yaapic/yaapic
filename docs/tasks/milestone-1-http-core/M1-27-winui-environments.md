# M1-27: WinUI environment management

Status: todo
Platform: windows
Depends on: M1-25

## Goal

Create, rename, edit and delete environments and their variables from the WinUI app.

## Design decisions

- A "Manage environments" button (gear icon from `Yaapic.Icons.WinUI.xaml` if one fits, else text) next to the
  environment `ComboBox` opens `Views/EnvironmentsDialog.xaml`, a `ContentDialog` (close button "Close") bound to
  `EnvironmentsViewModel`.
- Dialog layout: left list (200 px) of `Environments` (name, dirty marker `•` when `IsDirty`) bound to
  `EditedEnvironment`, with "Add" (`AddCommand`) and "Delete" (see below) buttons; right
  side: `TextBox` for `EditedEnvironment.Name`, the reusable `KeyValueEditorView` for `EditedEnvironment.Variables`
  (column headers "Variable" / "Value"), and a "Save" button (`SaveCommand`). Closing the dialog with unsaved items is
  allowed: edits stay in memory (they are used for sending, see M1-22) until saved or the app exits.
- WinUI shows only one `ContentDialog` at a time, so `IDialogService.ConfirmDeleteAsync` cannot be used inside this
  dialog. The "Delete" button opens a `Flyout` with "Delete '{name}'?" and a Delete button that calls
  `EnvironmentsViewModel.DeleteConfirmedAsync(item)` (added in M1-22).

## Out of scope

Globals, secrets, import/export, environment duplication.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke on the published exe: add environment "Local" with `baseUrl = https://httpbin.org`, save, select it,
      send `{{baseUrl}}/get`: 200; the file `%USERPROFILE%\Yaapic\Default\environments\local.env.yaml` exists; restart:
      "Local" is still active; delete it: the selector goes back to "No environment".
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add environment management dialog`
