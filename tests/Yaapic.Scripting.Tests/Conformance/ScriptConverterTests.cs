using Yaapic.Core.Scripting;
using Yaapic.Domain.Scripting;

namespace Yaapic.Scripting.Tests.Conformance;

/// <summary>Checks <see cref="ScriptConverter"/>: Postman and Bruno members, skipped text, reported members.</summary>
public sealed class ScriptConverterTests
{
    private static ScriptConversionResult Js(string source, ScriptDialect dialect) =>
        ScriptConverter.Convert(source, dialect, ScriptLanguage.JavaScript);

    public static TheoryData<string> ConformanceCases => [.. JavaScriptConformanceTests.Cases
        .SelectMany(c => new[] { c.Pre, c.Post })
        .Where(s => s is not null && !s.Contains("postman.", StringComparison.Ordinal))
        .Select(s => s!)];

    [Theory]
    [MemberData(nameof(ConformanceCases))]
    public void Every_Postman_idiom_of_the_conformance_suite_converts_back_to_the_yaapic_script(string yaapicScript)
    {
        var result = Js(yaapicScript.Replace("yaapic.", "pm.", StringComparison.Ordinal), ScriptDialect.Postman);

        Assert.Equal(yaapicScript, result.Text);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Postman_legacy_calls_map_to_yaapic()
    {
        var result = Js("postman.setEnvironmentVariable('a', 1); postman.getGlobalVariable('b'); postman.clearEnvironmentVariable('c');", ScriptDialect.Postman);

        Assert.Equal("yaapic.environment.set('a', 1); yaapic.globals.get('b'); yaapic.environment.unset('c');", result.Text);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Strings_comments_and_template_text_are_left_alone_but_template_expressions_convert()
    {
        const string Source = "// pm.environment.get('x')\nconst a = 'pm.environment.get';\n/* pm.test */ const b = `pm.x ${pm.variables.get('k')}`;";

        var result = Js(Source, ScriptDialect.Postman);

        Assert.Equal("// pm.environment.get('x')\nconst a = 'pm.environment.get';\n/* pm.test */ const b = `pm.x ${yaapic.variables.get('k')}`;", result.Text);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Members_of_other_objects_named_like_a_root_are_left_alone()
    {
        Assert.Equal("obj.pm.x(); mypm.y();", Js("obj.pm.x(); mypm.y();", ScriptDialect.Postman).Text);
    }

    [Theory]
    [InlineData("pm.execution.setNextRequest(null);", "pm.execution.setNextRequest", 1)]
    [InlineData("\n\npm.vault.get('k');", "pm.vault.get", 3)]
    [InlineData("pm.response.to.have.jsonSchema(s);", "pm.response.to.have.jsonSchema", 1)]
    [InlineData("pm.iterationData.get('x');", "pm.iterationData.get", 1)]
    [InlineData("postman.setNextRequest('x');", "postman.setNextRequest", 1)]
    [InlineData("const c = require('crypto-js');", "require", 1)]
    public void Unsupported_members_are_reported(string source, string member, int line)
    {
        var finding = Assert.Single(Js(source, ScriptDialect.Postman).Findings);

        Assert.Equal(member, finding.Member);
        Assert.Equal(line, finding.Line);
    }

    [Fact]
    public void Unsupported_Postman_members_are_renamed_so_they_fail_with_the_Yaapic_message()
    {
        Assert.Equal("yaapic.vault.get('k');", Js("pm.vault.get('k');", ScriptDialect.Postman).Text);
    }

    [Fact]
    public void Bruno_members_map_to_yaapic()
    {
        const string Source = """
            const t = bru.getEnvVar('token');
            bru.setVar('id', res.body.id);
            req.setHeader('Authorization', 'Bearer ' + t);
            req.setUrl(bru.interpolate('{{base}}/x'));
            test('ok', function () { expect(res.status).to.equal(200); expect(res.getBody()).to.have.property('id'); });
            const h = res.getHeader('x'); const ms = res.getResponseTime(); const m = req.getMethod();
            """;

        var result = Js(Source, ScriptDialect.Bruno);

        Assert.Equal("""
            const t = yaapic.environment.get('token');
            yaapic.variables.set('id', yaapic.response.json().id);
            yaapic.request.headers.upsert('Authorization', 'Bearer ' + t);
            yaapic.request.url.update(yaapic.variables.replaceIn('{{base}}/x'));
            yaapic.test('ok', function () { yaapic.expect(yaapic.response.code).to.equal(200); yaapic.expect(yaapic.response.json()).to.have.property('id'); });
            const h = yaapic.response.headers.get('x'); const ms = yaapic.response.responseTime; const m = yaapic.request.method;
            """, result.Text);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Bruno_setBody_becomes_a_raw_body_assignment_with_a_caveat()
    {
        var result = Js("req.setBody(JSON.stringify({ a: 1 }));", ScriptDialect.Bruno);

        Assert.Equal("yaapic.request.body.raw =(JSON.stringify({ a: 1 }));", result.Text);
        Assert.Single(result.Findings);
    }

    [Fact]
    public void Unknown_Bruno_members_are_reported_and_kept()
    {
        var result = Js("await bru.runRequest('x'); bru.setNextRequest('y');", ScriptDialect.Bruno);

        Assert.Equal("await bru.runRequest('x'); bru.setNextRequest('y');", result.Text);
        Assert.Equal(["bru.runRequest", "bru.setNextRequest"], result.Findings.Select(f => f.Member));
    }

    [Fact]
    public void Bare_test_is_only_converted_when_called_in_Bruno()
    {
        Assert.Equal("const test = 1; yaapic.test('a', () => {});", Js("const test = 1; test('a', () => {});", ScriptDialect.Bruno).Text);
        Assert.Equal("test('a', () => {});", Js("test('a', () => {});", ScriptDialect.Postman).Text);
    }

    [Fact]
    public void Lua_editors_are_not_converted()
    {
        var result = ScriptConverter.Convert("pm.test('a')", ScriptDialect.Postman, ScriptLanguage.Lua);

        Assert.Equal("pm.test('a')", result.Text);
        Assert.Single(result.Findings);
    }

    [Theory]
    [InlineData("pm.environment.get('a')", ScriptDialect.Postman)]
    [InlineData("postman.setNextRequest(null)", ScriptDialect.Postman)]
    [InlineData("bru.setVar('a', 1)", ScriptDialect.Bruno)]
    [InlineData("expect(res.status).to.equal(200)", ScriptDialect.Bruno)]
    [InlineData("console.log(1)", ScriptDialect.Postman)]
    public void Detect_guesses_the_dialect(string source, ScriptDialect expected)
    {
        Assert.Equal(expected, ScriptConverter.Detect(source));
    }
}
