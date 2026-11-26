# M6-19: Core: run report, JUnit XML

Status: done
Platform: any
Depends on: M6-14

## Goal

Write a `RunResult` as JUnit XML so that CI tools can read it (ADR 0018, "Reports").

## Design decisions

- Read first: ADR 0018, M6-14, M6-18 (file name helper, duration rounding).
- `src/Yaapic.Core/Runner/RunReportJUnit.cs`: `public static byte[] Write(RunResult result)` with `System.Xml.XmlWriter`
  (`XmlWriterSettings { Indent = true, IndentChars = "  ", NewLineChars = "\n", Encoding = new UTF8Encoding(false) }`), and
  `SuggestedFileName` = `{slug}-run-{yyyyMMdd-HHmmss}.xml`.
- Shape:
  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <testsuites name="{run name}" tests="{n}" failures="{n}" errors="{n}" skipped="{n}" time="{seconds}">
    <testsuite name="Iteration 1 / {path}/{request name}" tests=".." failures=".." errors=".." skipped=".." time=".." timestamp="{UTC start, ISO 8601}">
      <properties><property name="method" value="GET"/><property name="url" value="{template}"/><property name="status" value="200"/></properties>
      <testcase name="{test name}" classname="{path}/{request name}" time="0.001"/>
      <testcase name="{failed test}" classname="..." time=".."><failure message="{message}"/></testcase>
      <testcase name="request" classname="..." time=".."><error message="{error}"/></testcase>        (outcome Error)
      <testcase name="request" classname="..." time="0"><skipped/></testcase>                       (outcome Skipped)
      <system-out>{console lines, one per line: "[level] message"}</system-out>                      (only when there are lines)
    </testsuite>
  </testsuites>
  ```
  A request with no test and outcome `Passed` gets one `<testcase name="request">` so that it counts. Iterations are 1-based in names.
  Times are seconds with three decimals, invariant culture. Counts on `<testsuites>` are sums of the suites.
- `XmlWriter` escapes text; control characters invalid in XML 1.0 (other than tab, CR, LF) are replaced with `?` before writing.

## Out of scope

JSON (M6-18), saving.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Runner/RunReportJUnitTests.cs`: exact expected XML for a result with passed, failed, error and skipped
  requests and console lines; counts; escaping of `<&">` and of a control character; the XML parses with `XmlReader`; no BOM.
- [x] The AOT analyzers report nothing for `System.Xml` use (0 IL2xxx/IL3xxx in the build of `Yaapic.Core`).
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): write run reports as JUnit XML`
