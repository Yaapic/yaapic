using Yaapic.Domain.Http;

namespace Yaapic.Domain.Tests;

public class RequestBodyTests
{
    static RequestBody Form(string value = "1") => RequestBody.UrlEncoded([new("a", value), new("f", "x.txt", true, FormFieldKind.File)]);

    [Fact]
    public void Equal_field_lists_in_distinct_instances_are_equal()
    {
        var a = Form();
        var b = Form();
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    public static TheoryData<RequestBody> Different => new()
    {
        Form("2"),
        RequestBody.FormData([new("a", "1"), new("f", "x.txt", true, FormFieldKind.File)]),
        RequestBody.UrlEncoded([new("a", "1")]),
        RequestBody.UrlEncoded([new("a", "1", false), new("f", "x.txt", true, FormFieldKind.File)]),
        RequestBody.Binary("a.bin"),
        RequestBody.Raw(RawContentType.Json, "{}"),
    };

    [Theory]
    [MemberData(nameof(Different))]
    public void Differing_member_makes_bodies_unequal(RequestBody other) => Assert.NotEqual(Form(), other);

    [Fact]
    public void Path_and_text_differences_are_unequal()
    {
        Assert.NotEqual(RequestBody.Binary("a"), RequestBody.Binary("b"));
        Assert.NotEqual(RequestBody.Raw(RawContentType.Text, "a"), RequestBody.Raw(RawContentType.Text, "b"));
    }

    [Fact]
    public void Factories_set_kind()
    {
        Assert.Equal(BodyKind.None, RequestBody.None.Kind);
        Assert.Equal(BodyKind.Raw, RequestBody.Raw(RawContentType.Text, "x").Kind);
        Assert.Equal(BodyKind.UrlEncoded, Form().Kind);
        Assert.Equal(BodyKind.FormData, RequestBody.FormData([]).Kind);
        var binary = RequestBody.Binary("p");
        Assert.Equal(BodyKind.Binary, binary.Kind);
        Assert.Equal("p", binary.FilePath);
    }

    [Fact]
    public void Three_argument_constructor_has_empty_fields_and_path()
    {
        var b = new RequestBody(BodyKind.Raw, RawContentType.Json, "{}");
        Assert.Empty(b.Fields);
        Assert.Equal("", b.FilePath);
    }

    [Fact]
    public void IsEquivalentTo_compares_form_fields_by_value()
    {
        var a = HttpRequestDefinition.Create("a") with { Body = Form() };
        Assert.True(a.IsEquivalentTo(a with { Body = Form() }));
        Assert.False(a.IsEquivalentTo(a with { Body = Form("2") }));
    }
}
