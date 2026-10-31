using Yaapic.Domain.Workspace;

namespace Yaapic.Domain.Tests;

public class SessionStateTests
{
    [Fact]
    public void Empty_has_no_open_requests_and_null_ids()
    {
        Assert.Empty(SessionState.Empty.OpenRequestIds);
        Assert.Null(SessionState.Empty.ActiveRequestId);
        Assert.Null(SessionState.Empty.ActiveEnvironmentId);
    }
}
