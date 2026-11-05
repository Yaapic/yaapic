using Yaapic.ViewModels.Palette;

namespace Yaapic.ViewModels.Tests;

public sealed class CommandPaletteViewModelTests
{
    private sealed class FakeSource(params PaletteItem[] items) : IPaletteSource
    {
        public IReadOnlyList<PaletteItem> GetItems() => items;
    }

    private sealed class ThrowingSource : IPaletteSource
    {
        public IReadOnlyList<PaletteItem> GetItems() => throw new InvalidOperationException("source broke");
    }

    private static PaletteItem Item(string title, Func<Task>? run = null) =>
        new("command:" + title, title, null, "Command", null, run ?? (() => Task.CompletedTask));

    private static string[] Titles(CommandPaletteViewModel vm) => [.. vm.Results.Select(r => r.Item.Title)];

    private static CommandPaletteViewModel Open(params PaletteItem[] items)
    {
        var vm = new CommandPaletteViewModel([new FakeSource(items)]);
        vm.OpenCommand.Execute(null);
        return vm;
    }

    [Fact]
    public void Open_collects_items_in_source_order_and_selects_first()
    {
        var vm = new CommandPaletteViewModel([new FakeSource(Item("B"), Item("A")), new FakeSource(Item("C"))]);
        vm.Query = "stale";
        vm.OpenCommand.Execute(null);

        Assert.True(vm.IsOpen);
        Assert.Equal("", vm.Query);
        Assert.Equal(["B", "A", "C"], Titles(vm));
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public void Close_hides_the_palette()
    {
        var vm = Open(Item("A"));
        vm.CloseCommand.Execute(null);
        Assert.False(vm.IsOpen);
    }

    [Fact]
    public async Task Executed_items_come_first_newest_first_on_next_open()
    {
        var vm = Open(Item("A"), Item("B"), Item("C"));
        await vm.ExecuteCommand.ExecuteAsync(vm.Results[2]);
        vm.OpenCommand.Execute(null);
        await vm.ExecuteCommand.ExecuteAsync(vm.Results[2]);
        vm.OpenCommand.Execute(null);

        Assert.Equal(["B", "C", "A"], Titles(vm));
    }

    [Fact]
    public async Task Recents_are_limited_to_five_and_ignore_missing_items()
    {
        var items = Enumerable.Range(1, 7).Select(i => Item("I" + i)).ToArray();
        var vm = Open(items);
        foreach (var item in items)
        {
            await vm.ExecuteCommand.ExecuteAsync(new PaletteResult(item, []));
        }

        await vm.ExecuteCommand.ExecuteAsync(new PaletteResult(Item("Gone"), []));
        vm.OpenCommand.Execute(null);

        Assert.Equal(["I7", "I6", "I5", "I4", "I3", "I1", "I2"], Titles(vm));
    }

    [Fact]
    public void Query_ranks_prefix_before_scattered()
    {
        var vm = Open(Item("Open recent file"), Item("New request"), Item("Rename request"));
        vm.Query = "req";

        Assert.Equal(["New request", "Rename request"], Titles(vm));
    }

    [Fact]
    public void Query_ties_prefer_shorter_title()
    {
        var vm = Open(Item("send all"), Item("send"));
        vm.Query = "send";
        Assert.Equal(["send", "send all"], Titles(vm));
    }

    [Fact]
    public async Task Query_ties_by_score_prefer_recent_then_source_order()
    {
        PaletteItem Dup(string id) => new(id, "dup", null, "Command", null, () => Task.CompletedTask);
        var vm = Open(Dup("x"), Dup("y"), Dup("z"));
        vm.Query = "dup";
        Assert.Equal(["x", "y", "z"], vm.Results.Select(r => r.Item.Id));

        await vm.ExecuteCommand.ExecuteAsync(vm.Results[2]);
        vm.OpenCommand.Execute(null);
        vm.Query = "dup";
        Assert.Equal(["z", "x", "y"], vm.Results.Select(r => r.Item.Id));
    }

    [Fact]
    public void Query_reports_title_match_positions()
    {
        var vm = Open(Item("New request"));
        vm.Query = "nr";
        Assert.Equal([0, 4], vm.Results[0].TitleMatches);
    }

    [Fact]
    public void Results_are_capped_at_fifty()
    {
        var vm = Open([.. Enumerable.Range(0, 80).Select(i => Item("item" + i))]);
        Assert.Equal(50, vm.Results.Count);
        vm.Query = "item";
        Assert.Equal(50, vm.Results.Count);
    }

    [Fact]
    public void Selection_wraps_in_both_directions()
    {
        var vm = Open(Item("A"), Item("B"), Item("C"));
        vm.MovePreviousCommand.Execute(null);
        Assert.Equal(2, vm.SelectedIndex);
        vm.MoveNextCommand.Execute(null);
        Assert.Equal(0, vm.SelectedIndex);
        vm.MoveNextCommand.Execute(null);
        Assert.Equal(1, vm.SelectedIndex);
    }

    [Fact]
    public void Move_is_a_no_op_without_results()
    {
        var vm = Open();
        vm.MoveNextCommand.Execute(null);
        vm.MovePreviousCommand.Execute(null);
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public void Query_change_resets_selection()
    {
        var vm = Open(Item("aa"), Item("ab"), Item("ac"));
        vm.MoveNextCommand.Execute(null);
        vm.Query = "a";
        Assert.Equal(0, vm.SelectedIndex);
    }

    [Fact]
    public async Task ExecuteSelected_closes_then_runs_the_action()
    {
        var ran = false;
        var isOpenDuringRun = true;
        PaletteItem? item = null;
        item = Item("A", () =>
        {
            ran = true;
            isOpenDuringRun = item is not null && vmRef!.IsOpen;
            return Task.CompletedTask;
        });
        var vm = vmRef = Open(Item("Z"), item);
        vm.MoveNextCommand.Execute(null);

        await vm.ExecuteSelectedCommand.ExecuteAsync(null);

        Assert.True(ran);
        Assert.False(isOpenDuringRun);
        Assert.False(vm.IsOpen);
    }

    private CommandPaletteViewModel? vmRef;

    [Fact]
    public async Task Throwing_action_sets_LastError()
    {
        var vm = Open(Item("A", () => throw new InvalidOperationException("boom")));
        await vm.ExecuteSelectedCommand.ExecuteAsync(null);
        Assert.Equal("boom", vm.LastError);
    }

    [Fact]
    public void Throwing_source_is_skipped_and_reported()
    {
        var vm = new CommandPaletteViewModel([new ThrowingSource(), new FakeSource(Item("A"))]);
        vm.OpenCommand.Execute(null);
        Assert.Equal("source broke", vm.LastError);
        Assert.Equal(["A"], Titles(vm));
        Assert.True(vm.IsOpen);
    }

    [Fact]
    public void IsEmpty_only_for_open_non_blank_query_without_result()
    {
        var vm = new CommandPaletteViewModel([new FakeSource(Item("A"))]);
        vm.Query = "zzz";
        Assert.False(vm.IsEmpty);
        vm.OpenCommand.Execute(null);
        Assert.False(vm.IsEmpty);
        vm.Query = "zzz";
        Assert.True(vm.IsEmpty);
        vm.Query = "  ";
        Assert.False(vm.IsEmpty);
    }
}
