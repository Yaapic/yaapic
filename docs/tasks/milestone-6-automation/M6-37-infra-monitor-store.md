# M6-37: Infrastructure: monitor file store

Status: todo
Platform: any
Depends on: M6-28, M6-36

## Goal

Persist monitors as `monitors/<slug>.monitor.yaml` in the workspace (ADR 0021, "Definition").

## Design decisions

- Read first: ADR 0021, M6-28 (`FileMockServerStore`: copy its structure), M6-36.
- `src/Yaapic.Infrastructure/Storage/MonitorFileDto.cs` (`[YamlObject]`: `version` (1), `id`, `name`, `folderId`, `environmentId`
  (omitted when null), `intervalMinutes`, `enabled`), `MonitorYamlSerializer`, `FileMonitorStore` (folder `monitors/`, file
  `<slug>.monitor.yaml`). A file with an interval not in `AllowedIntervals` loads with the nearest allowed value above it (1440 at most)
  and is reported like other listing warnings.
- `docs/file-format.md`: layout line `monitors/<slug>.monitor.yaml` and a "Monitor file" section with an example and the rule that
  enabled monitors run for everyone who opens the workspace.

## Out of scope

Run history (M6-38).

## Acceptance criteria

- [ ] Tests in `Yaapic.Infrastructure.Tests` on a temporary workspace: round trip, no environment, rename moves the file, delete, slug
  collision, corrupt file skipped, invalid interval normalised, expected YAML text of a sample.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green; no IL2xxx/IL3xxx.

## Suggested commit message

`feat(infrastructure): store monitors in the workspace`
