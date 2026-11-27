using Yaapic.Core.LoadTesting;
using Yaapic.Core.Runner;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Tests.LoadTesting;

public class VirtualUserScheduleTests
{
    private static RunItem Item(string url) => new(HttpRequestDefinition.Create("r") with { Url = url }, "Root");

    private static LoadTestPlan Plan(LoadProfile profile = LoadProfile.Fixed, int users = 10, int duration = 60, int ramp = 10, int think = 0, params RunItem[] items)
        => new("t", items.Length == 0 ? [Item("http://a")] : items, users, TimeSpan.FromSeconds(duration), profile, TimeSpan.FromSeconds(ramp), think, []);

    [Fact]
    public void Validate_accepts_the_bounds()
    {
        Plan(users: 1, duration: 10).Validate();
        Plan(LoadProfile.RampUp, users: LoadTestPlan.MaxVirtualUsers, duration: 3600, ramp: 3600, think: 10000).Validate();
    }

    [Theory]
    [InlineData(0, 60, 10, 0)]
    [InlineData(101, 60, 10, 0)]
    [InlineData(10, 9, 10, 0)]
    [InlineData(10, 3601, 10, 0)]
    [InlineData(10, 60, 0, 0)]
    [InlineData(10, 60, 61, 0)]
    [InlineData(10, 60, 10, -1)]
    [InlineData(10, 60, 10, 10001)]
    public void Validate_rejects_out_of_range_values(int users, int duration, int ramp, int think)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Plan(LoadProfile.RampUp, users, duration, ramp, think).Validate());

    [Fact]
    public void Validate_ignores_the_ramp_for_a_fixed_profile()
        => Plan(LoadProfile.Fixed, ramp: 0).Validate();

    [Fact]
    public void Validate_rejects_a_plan_without_item()
        => Assert.Throws<ArgumentOutOfRangeException>(() => (Plan() with { Items = [] }).Validate());

    [Fact]
    public void TargetUsers_fixed_holds_until_the_duration_ends()
    {
        var plan = Plan();
        Assert.Equal(10, VirtualUserSchedule.TargetUsers(plan, TimeSpan.Zero));
        Assert.Equal(10, VirtualUserSchedule.TargetUsers(plan, TimeSpan.FromSeconds(59)));
        Assert.Equal(0, VirtualUserSchedule.TargetUsers(plan, TimeSpan.FromSeconds(60)));
    }

    [Fact]
    public void TargetUsers_ramp_up_follows_the_ramp()
    {
        var plan = Plan(LoadProfile.RampUp, users: 7, ramp: 10);
        Assert.Equal(1, VirtualUserSchedule.TargetUsers(plan, TimeSpan.Zero));
        Assert.Equal(4, VirtualUserSchedule.TargetUsers(plan, TimeSpan.FromSeconds(5)));
        Assert.Equal(7, VirtualUserSchedule.TargetUsers(plan, TimeSpan.FromSeconds(10)));
        Assert.Equal(7, VirtualUserSchedule.TargetUsers(plan, TimeSpan.FromSeconds(59)));
        Assert.Equal(0, VirtualUserSchedule.TargetUsers(plan, TimeSpan.FromSeconds(61)));
        Assert.Equal(1, VirtualUserSchedule.TargetUsers(plan, TimeSpan.FromSeconds(-3)));
    }

    [Fact]
    public void StartOffset_agrees_with_TargetUsers()
    {
        var plan = Plan(LoadProfile.RampUp, users: 7, ramp: 10);
        for (int user = 0; user < 7; user++)
        {
            TimeSpan offset = VirtualUserSchedule.StartOffset(plan, user);
            Assert.True(VirtualUserSchedule.TargetUsers(plan, offset) > user);
            if (offset > TimeSpan.Zero)
            {
                Assert.True(VirtualUserSchedule.TargetUsers(plan, offset - TimeSpan.FromTicks(1)) <= user);
            }
        }
    }

    [Fact]
    public void StartOffset_fixed_is_zero()
        => Assert.Equal(TimeSpan.Zero, VirtualUserSchedule.StartOffset(Plan(), 5));

    [Fact]
    public void DescribeHosts_lists_each_host_once_in_order()
    {
        var plan = Plan(items: [Item("https://b.test/x"), Item("http://a.test:8080/y"), Item("https://b.test/z"), Item("{{nope}}")]);
        Assert.Equal("https://b.test:443, http://a.test:8080, {{nope}}", VirtualUserSchedule.DescribeHosts(plan, u => u));
    }
}
