# M2-21: Avalonia method picker and URL bar for all methods

Status: todo
Platform: unix
Depends on: M2-11, M2-13

## Goal

Same as M2-16, in `Yaapic.App.Avalonia`; read M2-16 first for behaviour and texts.

## Design decisions

- Extend `Views/RequestEditorView.axaml(.cs)` and `Presentation/EditorMap.cs`/`BrushMap.cs`: the method picker lists the
  seven methods (items built in code from `ToText`, index converter replaces the GET/POST-only `MethodIndex`),
  `BrushMap.Method` covers all seven `YaapicMethod*Brush` keys, hint "{METHOD} requests are sent without a body" shown
  when `AllowsBody` is false (replace the `IsGet` converter). No enum `ItemsSource` binding (see the M1-29 notes).
- Fix every other GET/POST assumption in `MainWindow.axaml` (tab header, sidebar rows) by using `MethodText` and
  `BrushMap.Method`.
- Params tab badge shows `QueryParams.ActiveCount`; URL `TextBox` stays two-way on `Url`.

## Out of scope

Body pane (M2-22), Auth/Settings (M2-23).

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke: same checks as M2-16 on the host OS.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): pick any HTTP method and sync the URL with the params table`
