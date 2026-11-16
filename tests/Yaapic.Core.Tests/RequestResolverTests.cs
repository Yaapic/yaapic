using Yaapic.Core.Http;
using Yaapic.Core.Variables;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests;

public class RequestResolverTests
{
    static IVariableSource Env(params (string K, string V)[] vars) =>
        new EnvironmentVariableSource(EnvironmentDefinition.Create("e") with { Variables = [.. vars.Select(v => new KeyValueEntry(v.K, v.V))] });

    static HttpRequestDefinition Req(string url, HttpMethodKind method = HttpMethodKind.Get, KeyValueEntry[]? query = null,
        KeyValueEntry[]? headers = null, RequestBody? body = null) =>
        HttpRequestDefinition.Create("r") with { Url = url, Method = method, QueryParams = query ?? [], Headers = headers ?? [], Body = body ?? RequestBody.None };

    static ResolvedRequest R(HttpRequestDefinition r, params (string K, string V)[] vars) => RequestResolver.Resolve(r, Env(vars));

    [Fact]
    public void Prefixes_scheme_only_when_missing()
    {
        Assert.Equal("http://a.com", R(Req(" a.com ")).Url);
        Assert.Equal("https://a.com", R(Req("https://a.com")).Url);
        Assert.Equal("", R(Req("  ")).Url);
    }

    [Theory]
    [InlineData("http://a", "http://a?k=v")]
    [InlineData("http://a?x=1", "http://a?x=1&k=v")]
    [InlineData("http://a?", "http://a?k=v")]
    [InlineData("http://a?x=1&", "http://a?x=1&k=v")]
    [InlineData("http://a#frag", "http://a?k=v#frag")]
    [InlineData("http://a?x=1#frag", "http://a?x=1&k=v#frag")]
    public void Appends_params(string url, string expected) =>
        Assert.Equal(expected, R(Req(url, query: [new("k", "v")])).Url);

    [Fact]
    public void Escapes_keys_and_values() =>
        Assert.Equal("http://a?a%20b=c%26d%3De&%C3%A9=x",
            R(Req("http://a", query: [new("a b", "c&d=e"), new("é", "x")])).Url);

    [Fact]
    public void Drops_disabled_and_empty_key_entries()
    {
        var r = R(Req("http://a", query: [new("a", "1", false), new("", "2"), new("{{k}}", "3"), new("b", "4")],
            headers: [new("H", "1", false), new("{{k}}", "x"), new("G", "2")]), ("k", ""));
        Assert.Equal("http://a?b=4", r.Url);
        Assert.Equal([new KeyValueEntry("G", "2")], r.Headers);
    }

    [Fact]
    public void Resolves_variables_everywhere()
    {
        var r = R(Req("{{h}}/p", HttpMethodKind.Post, [new("{{k}}", "{{v}}")], [new("X-{{k}}", "{{v}}")], RequestBody.Raw(RawContentType.Text, "b={{v}}")),
            ("h", "a.com"), ("k", "key"), ("v", "val"));
        Assert.Equal("http://a.com/p?key=val", r.Url);
        Assert.Equal([new KeyValueEntry("X-key", "val")], r.Headers);
        Assert.Equal("b=val", r.BodyText);
    }

    [Fact]
    public void Get_drops_body()
    {
        var r = R(Req("http://a", body: RequestBody.Raw(RawContentType.Json, "{}")));
        Assert.Null(r.BodyText);
        Assert.Null(r.BodyContentType);
    }

    [Theory]
    [InlineData(RawContentType.Text, "text/plain; charset=utf-8")]
    [InlineData(RawContentType.Json, "application/json")]
    [InlineData(RawContentType.Xml, "application/xml")]
    public void Post_default_content_type(RawContentType type, string expected)
    {
        var r = R(Req("http://a", HttpMethodKind.Post, body: RequestBody.Raw(type, "x")));
        Assert.Equal(expected, r.BodyContentType);
    }

    [Fact]
    public void Post_without_raw_body_has_no_body() =>
        Assert.Null(R(Req("http://a", HttpMethodKind.Post)).BodyText);

    [Fact]
    public void User_content_type_overrides_and_is_removed_for_post_body()
    {
        var r = R(Req("http://a", HttpMethodKind.Post, headers: [new("content-type", "x/first"), new("Content-Type", "x/second"), new("A", "1")],
            body: RequestBody.Raw(RawContentType.Json, "{}")));
        Assert.Equal("x/first", r.BodyContentType);
        Assert.Equal([new KeyValueEntry("A", "1")], r.Headers);
    }

    [Fact]
    public void User_content_type_stays_without_body()
    {
        var r = R(Req("http://a", headers: [new("Content-Type", "x/y")]));
        Assert.Equal([new KeyValueEntry("Content-Type", "x/y")], r.Headers);
        Assert.Null(r.BodyContentType);
    }

    [Theory]
    [InlineData(HttpMethodKind.Put)]
    [InlineData(HttpMethodKind.Patch)]
    [InlineData(HttpMethodKind.Delete)]
    public void Put_patch_delete_send_raw_body(HttpMethodKind method)
    {
        var r = R(Req("http://a", method, body: RequestBody.Raw(RawContentType.Json, "{}")));
        Assert.Equal("{}", r.BodyText);
        Assert.Equal("application/json", r.BodyContentType);
    }

    [Theory]
    [InlineData(HttpMethodKind.Head)]
    [InlineData(HttpMethodKind.Options)]
    public void Head_and_options_send_no_body(HttpMethodKind method)
    {
        var r = R(Req("http://a", method, body: RequestBody.Raw(RawContentType.Json, "{}")));
        Assert.Null(r.BodyText);
        Assert.Null(r.BodyContentType);
        Assert.Null(R(Req("http://a", method, body: RequestBody.Binary("f"))).BodyFilePath);
        Assert.Null(R(Req("http://a", method, body: RequestBody.FormData([new("k", "v")]))).Parts);
    }

    [Theory]
    [InlineData(RawContentType.Html, "text/html; charset=utf-8")]
    [InlineData(RawContentType.JavaScript, "application/javascript")]
    public void Raw_html_and_javascript_content_types(RawContentType type, string expected) =>
        Assert.Equal(expected, R(Req("http://a", HttpMethodKind.Post, body: RequestBody.Raw(type, "x"))).BodyContentType);

    [Fact]
    public void UrlEncoded_escapes_and_skips_disabled_and_empty_keys()
    {
        var r = R(Req("http://a", HttpMethodKind.Post, body: RequestBody.UrlEncoded(
            [new("a b", "c&d"), new("é", "ü"), new("off", "1", false), new("{{e}}", "2"), new("k", "{{v}}")])), ("e", ""), ("v", "x y"));
        Assert.Equal("a%20b=c%26d&%C3%A9=%C3%BC&k=x%20y", r.BodyText);
        Assert.Equal("application/x-www-form-urlencoded", r.BodyContentType);
    }

    [Fact]
    public void FormData_builds_text_and_file_parts()
    {
        var r = R(Req("http://a", HttpMethodKind.Post, headers: [new("Content-Type", "x/y"), new("A", "1")], body: RequestBody.FormData(
            [new("{{k}}", "{{v}}"), new("f", "{{p}}", true, FormFieldKind.File), new("off", "1", false), new("", "2")])),
            ("k", "key"), ("v", "val"), ("p", "C:\\a.txt"));
        Assert.Equal([new ResolvedPart("key", "val", null), new ResolvedPart("f", null, "C:\\a.txt")], r.Parts);
        Assert.Null(r.BodyText);
        Assert.Null(r.BodyContentType);
        Assert.Equal([new KeyValueEntry("A", "1")], r.Headers);
    }

    [Fact]
    public void Binary_uses_substituted_path_and_content_type()
    {
        var r = R(Req("http://a", HttpMethodKind.Put, body: RequestBody.Binary("{{p}}")), ("p", "C:\\f.bin"));
        Assert.Equal("C:\\f.bin", r.BodyFilePath);
        Assert.Equal("application/octet-stream", r.BodyContentType);
        Assert.Null(R(Req("http://a", HttpMethodKind.Put, body: RequestBody.Binary(""))).BodyFilePath);
    }

    [Fact]
    public void User_content_type_overrides_urlencoded_and_binary()
    {
        var h = new[] { new KeyValueEntry("Content-Type", "x/y") };
        var u = R(Req("http://a", HttpMethodKind.Post, headers: h, body: RequestBody.UrlEncoded([new("a", "b")])));
        var b = R(Req("http://a", HttpMethodKind.Post, headers: h, body: RequestBody.Binary("f")));
        Assert.Equal("x/y", u.BodyContentType);
        Assert.Equal("x/y", b.BodyContentType);
        Assert.Empty(u.Headers);
        Assert.Empty(b.Headers);
    }

    [Fact]
    public void Unresolved_collected_from_form_and_binary_bodies()
    {
        Assert.Equal(["a", "b", "c"], R(Req("http://a", HttpMethodKind.Post,
            body: RequestBody.FormData([new("{{a}}", "{{b}}", true, FormFieldKind.Text), new("f", "{{c}}", true, FormFieldKind.File)]))).Unresolved);
        Assert.Equal(["p"], R(Req("http://a", HttpMethodKind.Post, body: RequestBody.Binary("{{p}}"))).Unresolved);
    }

    [Fact]
    public void Settings_are_copied()
    {
        var r = R(Req("http://a") with { Settings = new RequestSettings(false, 30) });
        Assert.False(r.FollowRedirects);
        Assert.Equal(30, r.TimeoutSeconds);
        Assert.True(R(Req("http://a")).FollowRedirects);
    }

    [Fact]
    public void Unresolved_aggregated_in_order()
    {
        var r = R(Req("{{a}}{{b}}", HttpMethodKind.Post, [new("{{c}}", "{{a}}")], [new("h", "{{d}}")], RequestBody.Raw(RawContentType.Text, "{{e}}{{b}}")));
        Assert.Equal(["a", "b", "c", "d", "e"], r.Unresolved);
    }

    static RequestAuth Auth(AuthKind kind, string user = "", string pass = "", string token = "", string key = "", string value = "",
        ApiKeyLocation loc = ApiKeyLocation.Header) => new(kind, user, pass, token, key, value, loc);

    static ResolvedRequest A(RequestAuth auth, KeyValueEntry[]? headers = null, string url = "http://a", params (string K, string V)[] vars) =>
        R(Req(url, headers: headers) with { Auth = auth }, vars);

    [Fact]
    public void Basic_auth_adds_header_after_user_headers()
    {
        var r = A(Auth(AuthKind.Basic, "Aladdin", "open sesame"), [new("X", "1")]);
        Assert.Equal([new("X", "1"), new("Authorization", "Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==")], r.Headers);
    }

    [Fact]
    public void Basic_auth_skipped_when_both_empty() =>
        Assert.Empty(A(Auth(AuthKind.Basic)).Headers);

    [Fact]
    public void Bearer_auth_trims_and_skips_empty()
    {
        Assert.Equal([new("Authorization", "Bearer t")], A(Auth(AuthKind.Bearer, token: " t ")).Headers);
        Assert.Empty(A(Auth(AuthKind.Bearer, token: "  ")).Headers);
    }

    [Fact]
    public void Auth_fields_use_variables_and_report_unresolved()
    {
        var r = A(Auth(AuthKind.Bearer, token: "{{t}}{{missing}}"), vars: ("t", "abc"));
        Assert.Equal("Bearer abc{{missing}}", r.Headers[0].Value);
        Assert.Equal(["missing"], r.Unresolved);
    }

    [Fact]
    public void User_authorization_header_wins()
    {
        var r = A(Auth(AuthKind.Bearer, token: "t"), [new("authorization", "mine")]);
        Assert.Equal([new("authorization", "mine")], r.Headers);
    }

    [Fact]
    public void Disabled_user_header_does_not_win()
    {
        var r = A(Auth(AuthKind.Bearer, token: "t"), [new("Authorization", "mine", false)]);
        Assert.Equal([new("Authorization", "Bearer t")], r.Headers);
    }

    [Fact]
    public void Api_key_in_header_and_skipped_without_key()
    {
        Assert.Equal([new("X-Key", "v")], A(Auth(AuthKind.ApiKey, key: "X-Key", value: "v")).Headers);
        Assert.Empty(A(Auth(AuthKind.ApiKey, value: "v")).Headers);
    }

    [Fact]
    public void Api_key_in_query_goes_after_params_before_fragment()
    {
        var r = R(Req("http://a#f", query: [new("p", "1")]) with { Auth = Auth(AuthKind.ApiKey, key: "k y", value: "v&", loc: ApiKeyLocation.Query) });
        Assert.Equal("http://a?p=1&k%20y=v%26#f", r.Url);
        Assert.Empty(r.Headers);
    }

    [Fact]
    public void None_changes_nothing()
    {
        var r = A(RequestAuth.None);
        Assert.Empty(r.Headers);
        Assert.Equal("http://a", r.Url);
    }

    [Fact]
    public void OAuth2_is_resolved_in_every_field_without_headers()
    {
        var o = OAuth2Settings.Default with
        {
            TokenUrl = "{{t}}/token", AuthorizationUrl = "{{t}}/auth", ClientId = "{{id}}", ClientSecret = "{{s}}", Scope = "{{sc}}",
            Audience = "{{aud}}", Username = "{{u}}", Password = "{{p}}", RedirectUri = "{{ru}}", HeaderPrefix = "{{hp}}", QueryParameterName = "{{q}}",
        };
        var r = A(Auth(AuthKind.OAuth2) with { OAuth2 = o }, vars:
            [("t", "T"), ("id", "I"), ("s", "S"), ("sc", "SC"), ("aud", "AU"), ("u", "U"), ("p", "P"), ("ru", "RU"), ("hp", "HP"), ("q", "Q")]);
        var x = r.PendingAuth!.OAuth2!;
        Assert.Equal(AuthKind.OAuth2, r.PendingAuth.Kind);
        Assert.Equal(["T/token", "T/auth", "I", "S", "SC", "AU", "U", "P", "RU", "HP", "Q"],
            [x.TokenUrl, x.AuthorizationUrl, x.ClientId, x.ClientSecret, x.Scope, x.Audience, x.Username, x.Password, x.RedirectUri, x.HeaderPrefix, x.QueryParameterName]);
        Assert.Empty(r.Headers);
        Assert.Equal("http://a", r.Url);
    }

    [Fact]
    public void Jwt_is_resolved_in_every_field_without_headers()
    {
        var j = JwtSettings.Default with
        {
            Secret = "{{s}}", PrivateKey = "{{k}}", Payload = "{\"a\":\"{{v}}\"}", ExtraHeaders = "{{x}}", HeaderPrefix = "{{hp}}", QueryParameterName = "{{q}}",
        };
        var r = A(Auth(AuthKind.Jwt) with { Jwt = j }, vars: [("s", "S"), ("k", "K"), ("v", "V"), ("x", "X"), ("hp", "HP"), ("q", "Q")]);
        var x = r.PendingAuth!.Jwt!;
        Assert.Equal(["S", "K", "{\"a\":\"V\"}", "X", "HP", "Q"], [x.Secret, x.PrivateKey, x.Payload, x.ExtraHeaders, x.HeaderPrefix, x.QueryParameterName]);
        Assert.Empty(r.Headers);
    }

    [Fact]
    public void Digest_is_resolved()
    {
        var r = A(Auth(AuthKind.Digest, "{{u}}", "{{p}}"), vars: [("u", "bob"), ("p", "pw")]);
        Assert.Equal(new ResolvedAuth(AuthKind.Digest, "bob", "pw", null, null, null), r.PendingAuth);
        Assert.Empty(r.Headers);
    }

    [Fact]
    public void AwsV4_is_resolved()
    {
        var a = new AwsV4Settings("{{a}}", "{{s}}", "{{t}}", "{{r}}", "{{sv}}");
        var r = A(Auth(AuthKind.AwsV4) with { AwsV4 = a }, vars: [("a", "A"), ("s", "S"), ("t", "T"), ("r", "R"), ("sv", "SV")]);
        Assert.Equal(new AwsV4Settings("A", "S", "T", "R", "SV"), r.PendingAuth!.AwsV4);
        Assert.Empty(r.Headers);
    }

    [Theory]
    [InlineData(AuthKind.OAuth2)]
    [InlineData(AuthKind.Jwt)]
    [InlineData(AuthKind.AwsV4)]
    public void Null_settings_are_treated_as_default(AuthKind kind)
    {
        var p = A(Auth(kind)).PendingAuth!;
        Assert.Equal(kind, p.Kind);
        Assert.Equal(kind == AuthKind.OAuth2 ? OAuth2Settings.Default : null, p.OAuth2);
        Assert.Equal(kind == AuthKind.Jwt ? JwtSettings.Default : null, p.Jwt);
        Assert.Equal(kind == AuthKind.AwsV4 ? AwsV4Settings.Default : null, p.AwsV4);
    }

    [Fact]
    public void Advanced_auth_reports_unresolved_names_once()
    {
        var r = A(Auth(AuthKind.OAuth2) with { OAuth2 = OAuth2Settings.Default with { ClientId = "{{m}}", ClientSecret = "{{m}}{{n}}" } });
        Assert.Equal(["m", "n"], r.Unresolved);
    }

    [Theory]
    [InlineData(AuthKind.None)]
    [InlineData(AuthKind.Basic)]
    [InlineData(AuthKind.Bearer)]
    [InlineData(AuthKind.ApiKey)]
    public void Old_kinds_have_no_pending_auth(AuthKind kind) => Assert.Null(A(Auth(kind, "u", "p", "t", "k", "v")).PendingAuth);
}
