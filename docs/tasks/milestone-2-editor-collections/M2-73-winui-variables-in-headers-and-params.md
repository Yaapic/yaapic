# M2-73: WinUI inline variables in header and param values

Status: todo
Platform: windows
Depends on: M2-72

## Goal

The `{{name}}` colouring and hover popup of the URL bar (M2-72) also work in the **value** cells of the Params and Headers
tables (and in their key cells, since `{{h}}: x` is legal), using the same `VariableTextBox` and the same
`VariableContext`. No new model code.

## Design decisions

- Read first: M2-71, M2-72 (its Notes: what the overlay needed), `Views/KeyValueEditorView.xaml(.cs)` and
  `Views/FormDataEditorView.xaml(.cs)`, `design/mockups/` for the table look.
- `KeyValueEditorView` gets a dependency property `VariableContext` (`VariableContext?`, default null). When set, the key
  and value cells use `VariableTextBox` (inside the existing cell template, same height, padding, borders as the current
  `TextBox` cells of M1-40; the cell surface comes from the row, the box background stays transparent). When null the
  view behaves exactly as today (the environments dialog, globals and folder settings tables use it without a context:
  no overlay, no cost). `RequestEditorView.xaml` passes `ViewModel.Variables` to the Params and Headers editors.
- Performance: a table can hold dozens of rows; each overlay only rebuilds when its own text or the context `Changed`
  event fires; unsubscribe from `Context.Changed` on `Unloaded` and subscribe on `Loaded` (virtualised rows are
  recycled). Verify with 100 rows that scrolling and typing stay smooth.
- Popups must not be clipped by the table's `ScrollViewer`: the `Popup` of M2-72 is placed in the window's `XamlRoot`, so
  this should already hold; verify at the last visible row and near the right edge.
- `FormDataEditorView` (form-data / urlencoded body fields): apply the same `VariableContext` to its text value cells
  (not file cells) if they are plain single-line `TextBox`es; otherwise leave it and list it in Notes.
- Do not touch the raw/JSON body (`CodeEditorView`, LoomEditor in M2-69): a later card handles it.

## Out of scope

Body editor, auth fields (token, username, password: secrets stay plain until the M3 vault), response viewer,
autocomplete, Avalonia (M2-75).

## Acceptance criteria

- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green.
- [ ] Resource keys: no new keys expected; any new XAML key is verified in `design/xaml/Yaapic.Colors.WinUI.xaml`
      (listed in Notes).
- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] **Exercise the PUBLISHED exe** (UI Automation and mouse): a header `Authorization` = `Bearer {{token}}` with `token`
      defined in the active environment shows `{{token}}` coloured resolved; `{{missing}}` in a param value shows
      unresolved; hovering opens the popup; creating `missing` from the popup recolours the cell live; editing the value
      of a param keeps the URL in sync (M2-13); adding, deleting and toggling rows keeps colours aligned; disabled rows
      still render; dark and light theme.
- [ ] The environments dialog, globals and folder settings tables still look and behave as before (no overlay, no
      regression): open each once.
- [ ] 100-row smoke test: no visible lag while typing or scrolling.
- [ ] Sending the request resolves the variables exactly as before (inspect the request received by the stub server).
- [ ] No Yaapic or stub-server process left running.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (the published exe, the stub server, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m2-editor-collections` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`feat(winui): colour variables inline in header and param cells`

## Notes
