using Yaapic.Core.Http;
using Yaapic.Domain.Http;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Tests;

public sealed class RequestEditorHttpVersionTests
{
    private sealed class Support(bool http3) : IHttpVersionSupport
    {
        public bool IsHttp3Supported => http3;
    }

    private static RequestSender Sender() =>
        new(new FakeHttpTransport((_, _) => Task.FromResult(SendResult.Fail(SendFailureKind.Unknown, "unused"))));

    private static RequestEditorViewModel Load(HttpVersionPreference version, IHttpVersionSupport? support = null)
    {
        var definition = HttpRequestDefinition.Create("r") with { Settings = new RequestSettings(true, 0, version) };
        return RequestEditorViewModel.FromDefinition(definition, Sender(), new FakeVariableScopeProvider(), versionSupport: support);
    }

    [Theory]
    [InlineData(HttpVersionPreference.Default)]
    [InlineData(HttpVersionPreference.Http1)]
    [InlineData(HttpVersionPreference.Http2)]
    [InlineData(HttpVersionPreference.Http3)]
    public void RoundTripsEachValue(HttpVersionPreference version)
    {
        var editor = Load(version);

        Assert.Equal(version, editor.HttpVersion);
        Assert.Equal(version, editor.ToDefinition().Settings.HttpVersion);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void EditingMarksTheTabDirtyAndRevertingClearsIt()
    {
        var editor = Load(HttpVersionPreference.Default);

        editor.HttpVersion = HttpVersionPreference.Http2;
        Assert.True(editor.IsDirty);
        editor.HttpVersion = HttpVersionPreference.Default;
        Assert.False(editor.IsDirty);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Http3AvailabilityFollowsTheSupport(bool supported, bool expected)
    {
        var choices = Load(HttpVersionPreference.Default, new Support(supported)).HttpVersionChoices;

        Assert.Equal(expected, choices.Single(c => c.Value == HttpVersionPreference.Http3).IsAvailable);
        Assert.All(choices.Where(c => c.Value != HttpVersionPreference.Http3), c => Assert.True(c.IsAvailable));
        Assert.All(choices, c => Assert.NotEmpty(c.Label));
    }

    [Fact]
    public void Http3IsUnavailableWithoutSupportObject() =>
        Assert.False(Load(HttpVersionPreference.Default).HttpVersionChoices.Single(c => c.Value == HttpVersionPreference.Http3).IsAvailable);

    [Theory]
    [InlineData(HttpVersionPreference.Http3, false, true)]
    [InlineData(HttpVersionPreference.Http3, true, false)]
    [InlineData(HttpVersionPreference.Http2, false, false)]
    public void NoteShowsOnlyForUnavailableHttp3(HttpVersionPreference version, bool supported, bool noteShown)
    {
        var editor = Load(version, new Support(supported));

        Assert.Equal(noteShown, editor.HttpVersionNote.Length > 0);
        Assert.Equal(version, editor.ToDefinition().Settings.HttpVersion);
    }
}
