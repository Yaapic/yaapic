using Yaapic.Domain.Http;

namespace Yaapic.Domain.Tests;

public class HttpResponseDataTests
{
    static HttpResponseData Make(int status = 200, params KeyValueEntry[] headers) =>
        new(status, "OK", headers, [1, 2, 3], TimeSpan.FromMilliseconds(5), "http://x");

    [Fact]
    public void ContentType_is_case_insensitive_and_first_match() =>
        Assert.Equal("a/b", Make(200, new KeyValueEntry("X", "1"), new KeyValueEntry("cOnTeNt-TyPe", "a/b"), new KeyValueEntry("Content-Type", "c/d")).ContentType);

    [Fact]
    public void ContentType_is_null_when_absent() => Assert.Null(Make(200, new KeyValueEntry("X", "1")).ContentType);

    [Fact]
    public void SizeBytes_is_body_length() => Assert.Equal(3, Make().SizeBytes);

    [Theory]
    [InlineData(199, false)]
    [InlineData(200, true)]
    [InlineData(299, true)]
    [InlineData(300, false)]
    public void IsSuccessStatusCode_range(int status, bool expected) =>
        Assert.Equal(expected, Make(status).IsSuccessStatusCode);

    [Fact]
    public void Success_sets_response_only()
    {
        var r = SendResult.Success(Make(500));
        Assert.True(r.IsSuccess);
        Assert.NotNull(r.Response);
        Assert.Null(r.Failure);
    }

    [Fact]
    public void Fail_sets_failure_only()
    {
        var r = SendResult.Fail(SendFailureKind.Timeout, "slow");
        Assert.False(r.IsSuccess);
        Assert.Null(r.Response);
        Assert.Equal(new SendFailure(SendFailureKind.Timeout, "slow"), r.Failure);
    }
}
