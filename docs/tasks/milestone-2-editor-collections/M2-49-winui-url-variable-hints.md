# M2-49: WinUI URL variable hints

Status: done
Platform: windows
Depends on: M2-46, M2-16

## Goal

A strip under the URL bar shows the variables used by the URL and whether they resolve.

## Design decisions

- In `Views/RequestEditorView.xaml`, directly under the URL row, an `ItemsRepeater` (horizontal wrap) of chips bound to
  `ViewModel.UrlVariables`, visible only when `HasUrlVariables`. A chip shows `{{name}}`; resolved chips use the success
  chip style of M1-37, unresolved ones the warning style; resolved non-dynamic chips append ` = value` (value
  trimmed to 40 characters with an ellipsis) and the scope word (Environment, Collection, Globals) in the tooltip;
  dynamic ones show "dynamic".
- `x:Bind` only with `x:DataType` = `VariableHint`; mapping functions in `Presentation/TextMap.cs`/`BrushMap.cs`.
- The strip takes no space when hidden.

## Out of scope

Click actions on chips, hints for headers and body, autocomplete.

## Acceptance criteria

- [x] AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual smoke on the published exe: with environment `Local` (`baseUrl`) active, the URL `{{baseUrl}}/get?x={{nope}}`
      shows a resolved chip and an unresolved chip; switching to "No environment" turns the first into unresolved without
      retyping.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): show URL variable hints under the URL bar`

## Notes

- The chips use a horizontal `StackLayout` (no wrap): WinUI has no built-in wrapping layout; long URLs with many
  variables overflow instead of wrapping.
- `ItemsSource` goes through `ListMap.Hints` (an `ObservableCollection`): binding the VM's `IReadOnlyList` directly
  rendered nothing under Native AOT (CsWinRT marshalling of non-WinRT generics).
- Smoke run on the published exe (UI Automation + mouse): `{{baseUrl}}/get?x={{nope}}` shows
  `{{baseUrl}} = http://127.0.0.1:5199` and `{{nope}}`; hover and click on a chip are harmless (no click action);
  switching to "No environment" turns `{{baseUrl}}` into an unresolved chip without retyping. The tooltip text is
  checked through the chip's accessible name (the tooltip popup itself was not captured).
