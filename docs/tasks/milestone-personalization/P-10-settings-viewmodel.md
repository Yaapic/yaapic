# P-10: Settings ViewModel

Status: done
Platform: any
Depends on: P-02, P-03, P-07

## Goal

`SettingsViewModel`: the language list, the accent presets (plus custom colour entry and validation), saving through
`IPreferencesStore`, and events the UI uses to apply the accent now and to offer a restart for the language.

## Acceptance criteria

- [x] Tests: choosing a language or an accent saves and raises the right event; an invalid custom colour is rejected with a message.
- [x] The command palette gets "Settings", "Change language" and "Change accent colour".

## Suggested commit message

`feat: add the settings view model`
