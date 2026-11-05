# M2-41: Globals YAML serializer and file store

Status: done
Platform: any
Depends on: M2-40

## Goal

`IGlobalsStore` on `<workspace>/globals.yaml`.

## Design decisions

- `internal partial class GlobalsFileDto` (`[YamlObject(NamingConvention.LowerCamelCase)]`: `Version`, `Variables` as
  `List<KeyValueDto>`) and `public static class GlobalsYamlSerializer { string Serialize(IReadOnlyList<KeyValueEntry>);
  IReadOnlyList<KeyValueEntry> Deserialize(string yaml); }`, same conventions as `EnvironmentYamlSerializer` (M1-14):
  `version: 1` required (`StorageException` otherwise), missing `variables` = empty, `\n` endings, one trailing newline.
- `public sealed class FileGlobalsStore(string workspaceRoot) : IGlobalsStore`: file `Path.Combine(workspaceRoot,
  "globals.yaml")`. `LoadAsync`: missing file -> empty; an unparsable file -> `StorageException` (the ViewModel shows it
  and never overwrites the file on its own). `SaveAsync`: `WorkspaceFiles.WriteAtomicAsync`. I/O exceptions ->
  `StorageException`.
- Golden file `tests/Yaapic.Infrastructure.Tests/Storage/golden/globals-v1.yaml`.

## Out of scope

The ViewModel (M2-42), secrets.

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests` (temp directory): serializer round trip, golden match, version 2 and
      malformed YAML throw; store: missing file loads empty, save then load, overwrite, corrupt file throws on load,
      no `.tmp` left behind, `\n` endings and no BOM.
- [x] 0 warnings (AOT analyzers on).

## Suggested commit message

`feat(infrastructure): add globals YAML serializer and file store`
