# M6-28: Infrastructure: mock server file store

Status: todo
Platform: any
Depends on: M6-27

## Goal

Persist mock servers as `mocks/<slug>.mock.yaml` files in the workspace (ADR 0020, "File").

## Design decisions

- Read first: ADR 0020, `docs/file-format.md`, `src/Yaapic.Core/Storage/IEnvironmentStore.cs`, and in `src/Yaapic.Infrastructure/Storage/`:
  `FileEnvironmentStore.cs`, `EnvironmentFileDto.cs`, `EnvironmentYamlSerializer.cs`, `KeyValueMapper.cs`, `WorkspaceFiles.cs`
  (slugs, atomic writes, how a renamed item moves its file), `tests/Yaapic.TestDoubles/InMemoryEnvironmentStore.cs`.
- Port `src/Yaapic.Core/Storage/IMockServerStore.cs`: `Task<IReadOnlyList<MockServerDefinition>> ListAsync(CancellationToken ct)`
  (sorted by name, ordinal ignoring case, then id), `Task SaveAsync(MockServerDefinition server, CancellationToken ct)` (create or
  replace by id; a rename moves the file), `Task DeleteAsync(Guid id, CancellationToken ct)` (unknown id: no-op); all throw
  `StorageException` on I/O errors.
- `src/Yaapic.Infrastructure/Storage/MockServerFileDto.cs` (`[YamlObject]`, `version`, `id`, `name`, `port`, `cors`, `routes`) and
  `MockRouteFileDto` (`id`, `name`, `enabled`, `method`, `path`, `status`, `headers` (reuse the key/value DTO and `KeyValueMapper`),
  `body`, `delayMs`), `MockServerYamlSerializer` (bodies written as literal blocks the way request bodies are, see
  `ScriptSourceFormatter`/the request serializer), `FileMockServerStore` (folder `mocks/`, file `<slug>.mock.yaml`, slug collisions
  handled like environments). An unreadable file is skipped and reported the way `FileEnvironmentStore` reports listing errors.
- `tests/Yaapic.TestDoubles/InMemoryMockServerStore.cs` (same contract).
- `docs/file-format.md`: the layout gains `mocks/<slug>.mock.yaml`, and a "Mock server file" section with an example and the field rules
  of ADR 0020.

## Out of scope

Running mocks.

## Acceptance criteria

- [ ] Tests in `Yaapic.Infrastructure.Tests` on a temporary workspace: save, list, round trip of every field (multi-line body, headers,
  disabled route), rename moves the file, delete, two servers with the same name get distinct files, a corrupt file is skipped, the YAML
  text of a sample matches an expected literal.
- [ ] `InMemoryMockServerStore` tests (or shared contract tests if the project has them).
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green; no IL2xxx/IL3xxx in the build.

## Suggested commit message

`feat(infrastructure): store mock servers in the workspace`
