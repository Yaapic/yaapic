using Yaapic.Core.Http;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class RequestEditorViewModelTests
{
    private static RequestSender Sender() =>
        new(new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.Unknown, "unused"))));

    private static HttpRequestDefinition Sample() => new(
        Guid.NewGuid(),
        "Get users",
        HttpMethodKind.Post,
        "https://x.test/users",
        [new KeyValueEntry("page", "1")],
        [new KeyValueEntry("Accept", "*/*", false)],
        RequestBody.Raw(RawContentType.Json, "{}"),
        RequestAuth.None,
        RequestSettings.Default);

    public static TheoryData<string, Action<RequestEditorViewModel>, Action<RequestEditorViewModel>> Edits => new()
    {
        { "name", e => e.Name = "Other", e => e.Name = "Get users" },
        { "url", e => e.Url = "https://y.test", e => e.Url = "https://x.test/users?page=1" },
        { "method", e => e.Method = HttpMethodKind.Get, e => e.Method = HttpMethodKind.Post },
        { "header", e => e.Headers.Rows[0].Value = "text/plain", e => e.Headers.Rows[0].Value = "*/*" },
        { "param", e => e.QueryParams.Rows[0].Key = "p", e => e.QueryParams.Rows[0].Key = "page" },
        { "body kind", e => e.BodyKind = BodyKind.None, e => e.BodyKind = BodyKind.Raw },
        { "auth kind", e => e.Auth.Kind = AuthKind.Bearer, e => e.Auth.Kind = AuthKind.None },
        { "auth username", e => e.Auth.Username = "u", e => e.Auth.Username = "" },
        { "auth password", e => e.Auth.Password = "p", e => e.Auth.Password = "" },
        { "auth token", e => e.Auth.Token = "t", e => e.Auth.Token = "" },
        { "auth key", e => e.Auth.Key = "k", e => e.Auth.Key = "" },
        { "auth value", e => e.Auth.Value = "v", e => e.Auth.Value = "" },
        { "auth location", e => e.Auth.Location = ApiKeyLocation.Query, e => e.Auth.Location = ApiKeyLocation.Header },
        { "follow redirects", e => e.FollowRedirects = false, e => e.FollowRedirects = true },
        { "timeout", e => e.TimeoutSeconds = 30, e => e.TimeoutSeconds = 0 },
        { "body text", e => e.BodyText = "[]", e => e.BodyText = "{}" },
    };

    public static TheoryData<HttpMethodKind> AllMethods => new(RequestEditorViewModel.Methods);

    public static TheoryData<RequestAuth> Auths => new()
    {
        RequestAuth.None,
        new RequestAuth(AuthKind.Basic, "user", "pass", "", "", "", ApiKeyLocation.Header),
        new RequestAuth(AuthKind.Bearer, "", "", "tok", "", "", ApiKeyLocation.Header),
        new RequestAuth(AuthKind.ApiKey, "", "", "", "X-Key", "secret", ApiKeyLocation.Query),
    };

    [Fact]
    public void OffersAllMethodsAndContentTypes()
    {
        Assert.Equal(7, RequestEditorViewModel.Methods.Count);
        Assert.Equal(
            [RawContentType.Json, RawContentType.Text, RawContentType.Xml, RawContentType.Html, RawContentType.JavaScript],
            RequestEditorViewModel.ContentTypes);
    }

    [Theory]
    [MemberData(nameof(AllMethods))]
    public void MethodRoundTripsAndAllowsBodyFollowsIt(HttpMethodKind method)
    {
        var editor = RequestEditorViewModel.FromDefinition(Sample() with { Method = method }, Sender(), new FakeVariableScopeProvider());
        var raised = new List<string?>();
        editor.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Assert.Equal(method, editor.ToDefinition().Method);
        Assert.Equal(method.ToText(), editor.MethodText);
        Assert.Equal(method.AllowsBody(), editor.AllowsBody);

        editor.Method = method == HttpMethodKind.Get ? HttpMethodKind.Post : HttpMethodKind.Get;

        Assert.Contains(nameof(RequestEditorViewModel.AllowsBody), raised);
    }

    [Theory]
    [MemberData(nameof(Auths))]
    public void AuthRoundTripsAndLoadIsClean(RequestAuth auth)
    {
        var definition = Sample() with { Auth = auth };
        var editor = RequestEditorViewModel.FromDefinition(definition, Sender(), new FakeVariableScopeProvider());

        Assert.False(editor.IsDirty);
        Assert.Equal(auth, editor.Auth.ToAuth());
        Assert.Equal(auth.Kind != AuthKind.None, editor.Auth.IsActive);
        Assert.True(editor.ToDefinition().IsEquivalentTo(definition));
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        var definition = Sample() with { Settings = new RequestSettings(false, 15) };
        var editor = RequestEditorViewModel.FromDefinition(definition, Sender(), new FakeVariableScopeProvider());

        Assert.False(editor.FollowRedirects);
        Assert.Equal(15, editor.TimeoutSeconds);
        Assert.True(editor.ToDefinition().IsEquivalentTo(definition));
    }

    [Fact]
    public void NegativeTimeoutBecomesZero()
    {
        var editor = RequestEditorViewModel.FromDefinition(Sample(), Sender(), new FakeVariableScopeProvider());

        editor.TimeoutSeconds = -5;

        Assert.Equal(0, editor.TimeoutSeconds);
        Assert.Equal(0, editor.ToDefinition().Settings.TimeoutSeconds);
    }

    [Fact]
    public void AuthEditorLoadDoesNotRaiseChanged()
    {
        var auth = new AuthEditorViewModel();
        var count = 0;
        auth.Changed += (_, _) => count++;

        auth.Load(new RequestAuth(AuthKind.Basic, "u", "p", "", "", "", ApiKeyLocation.Header));
        Assert.Equal(0, count);

        auth.Username = "x";
        Assert.True(count > 0);
    }

    [Fact]
    public void RoundTripIsEquivalent()
    {
        var definition = Sample();

        Assert.True(RequestEditorViewModel.FromDefinition(definition, Sender(), new FakeVariableScopeProvider()).ToDefinition().IsEquivalentTo(definition));
    }

    [Fact]
    public void LoadedRequestIsClean()
    {
        var editor = RequestEditorViewModel.FromDefinition(Sample(), Sender(), new FakeVariableScopeProvider());

        Assert.False(editor.IsDirty);
        Assert.Equal("Get users", editor.Title);
    }

    [Theory]
    [MemberData(nameof(Edits))]
    public void EditMakesDirtyAndRevertMakesClean(string field, Action<RequestEditorViewModel> edit, Action<RequestEditorViewModel> revert)
    {
        Assert.NotEmpty(field);
        var editor = RequestEditorViewModel.FromDefinition(Sample(), Sender(), new FakeVariableScopeProvider());
        var cleanTitle = editor.Title;
        var raised = new List<string?>();
        editor.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        edit(editor);

        Assert.True(editor.IsDirty);
        Assert.EndsWith(" •", editor.Title);
        Assert.Contains(nameof(RequestEditorViewModel.IsDirty), raised);
        Assert.Contains(nameof(RequestEditorViewModel.Title), raised);

        revert(editor);

        Assert.False(editor.IsDirty);
        Assert.Equal(cleanTitle, editor.Title);
    }

    [Fact]
    public void MarkSavedClearsDirty()
    {
        var editor = RequestEditorViewModel.CreateNew(Sender(), new FakeVariableScopeProvider());
        editor.Url = "https://x.test";

        editor.MarkSaved();

        Assert.False(editor.IsNew);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void CreateNewIsNewAndDirty()
    {
        var editor = RequestEditorViewModel.CreateNew(Sender(), new FakeVariableScopeProvider());

        Assert.True(editor.IsNew);
        Assert.True(editor.IsDirty);
        Assert.Equal("New Request •", editor.Title);
    }

    [Fact]
    public void BlankNameBecomesDefault()
    {
        var editor = RequestEditorViewModel.FromDefinition(Sample(), Sender(), new FakeVariableScopeProvider());
        editor.Name = "   ";

        Assert.Equal("New Request", editor.ToDefinition().Name);
        Assert.StartsWith("New Request", editor.Title);
    }

    [Fact]
    public void DisplayNameIsDefaultForNewRequestWithoutDirtyMarker()
    {
        var editor = RequestEditorViewModel.CreateNew(Sender(), new FakeVariableScopeProvider());

        Assert.Equal("New Request", editor.DisplayName);
    }

    [Fact]
    public void DisplayNameIsTrimmedNameAndChangeIsNotified()
    {
        var editor = RequestEditorViewModel.FromDefinition(Sample(), Sender(), new FakeVariableScopeProvider());
        var raised = new List<string?>();
        editor.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        editor.Name = "  Renamed  ";

        Assert.Equal("Renamed", editor.DisplayName);
        Assert.Contains(nameof(RequestEditorViewModel.DisplayName), raised);
    }
}
