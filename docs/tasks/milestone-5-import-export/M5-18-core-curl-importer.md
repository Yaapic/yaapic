# M5-18: Core: cURL importer

Status: done
Platform: any
Depends on: M5-06, M5-17

## Goal

`CurlImporter` turns pasted `curl` commands into unfiled requests.

## Design decisions

- Read first: ADR 0017, M5-06, M5-17 (words), `src/Yaapic.Domain/Http/RequestBody.cs`, `HttpVersionPreference.cs`, `RequestSettings.cs`, `docs/file-format.md`.
- `src/Yaapic.Core/Import/Curl/CurlImporter.cs`, `public sealed class CurlImporter : IImporter`, `Id` `curl`, `DisplayName` `cURL command`. **Sniff**: `Certain` when the first non-whitespace word of the text (after an optional `$ ` prompt) is `curl` or `curl.exe`; `Possible` when `curl ` appears
  at the start of any line; else `None`. `Import`: `CurlTokenizer.Tokenize(input.Single.Text)` (a `FormatException` becomes `ImportException(message)`); no command: `ImportException(Strings.ImportCurlNoCommand)`; each command becomes one request in `UnfiledRequests`.
- **Options** (words after the first; `-abc` short flags are **not** combined except `-sSL` style groups of the known no-value flags `s S L k i v f g N 0 1 2 3 4 6 I G` which are expanded letter by letter; an unknown group is one unknown option). `--opt=value` is accepted for long options.

  | Option | Effect |
  |---|---|
  | `-X`, `--request` METHOD | method (through `TryParseMethod`; unsupported: `Skipped` entry, the command is not imported) |
  | `-H`, `--header` 'Name: value' | header (split at the first `:`, both trimmed); `Name;` (no colon, ends with `;`) is a header with an empty value; `Name:` removes nothing (ignored with an `Info`). Repeated names are kept in order |
  | `-d`, `--data`, `--data-raw`, `--data-ascii` | body data; several are joined with `&`; `-d` value starting with `@` (not `--data-raw`) is a file: `Binary` body with that path |
  | `--data-binary` | like `-d` but a leading `@file` is a `Binary` body with the path; no joining changes |
  | `--data-urlencode` | one url-encoded field: `name=value` -> (name, value); `=value` -> ("", value); `name@file` and `@file` are not supported (`Warning`, field dropped); a bare `value` -> ("", value) |
  | `-F`, `--form` | multipart field: `name=value`; `name=@path` or `name=<path` -> file field (`;type=...` suffix dropped); `--form-string name=value` is always text |
  | `-u`, `--user` user[:pass] | Basic auth; with `--digest` Digest auth; `--ntlm`/`--negotiate` -> `Warning` unsupported |
  | `--oauth2-bearer` TOKEN | Bearer auth |
  | `-A`, `--user-agent` | header `User-Agent` (replaces an explicit one) |
  | `-e`, `--referer` | header `Referer` |
  | `-b`, `--cookie` text | when it contains `=`: header `Cookie` with the text; otherwise (a file name) `Warning` ignored |
  | `-I`, `--head` | method HEAD |
  | `-G`, `--get` | the data options are appended to the URL query instead of the body and the method is GET |
  | `-m`, `--max-time` N | `TimeoutSeconds = ceil(N)` (decimal accepted; `<= 0` ignored) |
  | `--max-redirs` 0 | `FollowRedirects = false`; `-L`/`--location` sets true; default true |
  | `--http1.0`, `--http1.1` / `--http2`, `--http2-prior-knowledge` / `--http3` | `HttpVersionPreference` Http1 / Http2 / Http3 |
  | `--url` URL | the URL |
  | `-k`, `--insecure` | `Warning` `Strings.ImportCurlInsecure` (TLS verification cannot be disabled in Yaapic) |
  | `--cert`, `--key`, `--cacert`, `-E`, `-x`, `--proxy`, `--proxy-user`, `-o`, `--output`, `-O`, `-T`, `--upload-file`, `-w`, `--write-out`, `--resolve`, `--connect-timeout`, `--retry`, `-K`, `--config`, `--interface` | take a value; ignored with a `Warning` `Strings.ImportCurlOptionIgnored(option)` |
  | `-s`, `--silent`, `-S`, `--show-error`, `-v`, `--verbose`, `-i`, `--include`, `-f`, `--fail`, `--compressed`, `-#`, `-g`, `--globoff`, `-N`, `--no-buffer`, `--tr-encoding`, `--path-as-is` | no effect, silently ignored |
  | any other word starting with `-` | `Warning` `Strings.ImportCurlOptionIgnored(option)`; it is assumed to take no value |
  | a word not starting with `-` | the URL (the first one); a second one: `Warning` `Strings.ImportCurlExtraArgument(word)`, ignored |

- **URL**: no URL: `Skipped` `Strings.ImportCurlNoUrl`. A URL without a scheme gets nothing added (kept as typed). `ImportHelpers.SplitUrl` gives `Url` and params; with `-G` the data pairs (`a=b&c=d` split on `&`, then at the first `=`, URL-decoded) are appended as params.
  `TranslateDynamicVariables` is **not** applied (a pasted command is already concrete).
- **Method**: `-X` wins; else `-I` HEAD; else `-G` GET; else POST when there is body data, form data or an upload; else GET.
- **Body** (when not `-G`): form fields (`-F`) -> `FormData`; `--data-urlencode` fields -> `UrlEncoded`; `-d`/`--data*` text -> decided by the effective `Content-Type` header (case-insensitive name lookup among the imported headers): contains `json`/`xml`/`html`/`javascript` -> `Raw` with `ContentTypeFromMime`
  and the text as is; contains `x-www-form-urlencoded` -> `UrlEncoded` fields parsed from the text (`&` split, `=` split, URL-decoded; a text that is not `a=b` shaped becomes `Raw Text`); any other `Content-Type` -> `Raw` with `ContentTypeFromMime`; **no** `Content-Type` header -> when the text is `a=b(&c=d)*` shaped
  (every `&` piece contains `=` and no whitespace or `{`) `UrlEncoded` fields, otherwise `Raw Text` **and** an explicit header `Content-Type: application/x-www-form-urlencoded` is added (that is what curl would send). A `@file` value or `--data-binary @file` -> `Binary` with `Warning` `Strings.ImportFileReference`.
  A `Content-Type: multipart/form-data` header together with `-F` is dropped (the editor generates the boundary).
- **Name**: `DescribeRequest(method, url)`. `Settings`: `FollowRedirects`, `TimeoutSeconds`, `HttpVersion` as above, else defaults. No dynamic translation, no scripts.
- Report: one `Info` per command when something was ignored silently is **not** needed; the `Warning`s above carry `Path` = the request name.
- Strings (EN and FR): `import.curl.noCommand` = "No curl command was found."; `import.curl.noUrl` = "The command has no URL."; `import.curl.insecure` = "-k (skip TLS verification) is not supported in Yaapic; the request keeps normal certificate checks.";
  `import.curl.optionIgnored` = "The option {0} is not supported and was ignored."; `import.curl.extraArgument` = "The extra argument \"{0}\" was ignored."; `import.fileReference` (M5-13).

## Out of scope

Pasting into the URL box (blocked M5-48), cmd and PowerShell forms, `@file` reading for `-d`.

## Acceptance criteria

- [x] Tests with exact `HttpRequestDefinition` expectations: minimal `curl URL`; `-X POST` + JSON `-H` + `-d`; the browser bash form with `$'...'` and `--compressed`; no Content-Type with `a=b&c=d` (UrlEncoded) and with free text (Raw Text + added header); `--data-urlencode` pairs; `-F` with a file and `;type=`;
  `-u user:pass`, `--digest -u`, `--oauth2-bearer`; `-A`, `-e`, `-b`; `-I`; `-G -d a=b` (query); `-m 2.5` (3); `--max-redirs 0`; `--http2`; `-sSL` expansion; `--opt=value`; `-k` Warning; `--proxy x` Warning; an unknown option Warning; a second URL Warning; TRACE skipped; no URL skipped; two commands in one text give two requests;
  query params split; a `curl` text that is the output of `CurlCodeGenerator` for 3 `CodeSamples` imports back to equivalent method, URL, headers and body; sniff cases.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the cURL importer`
