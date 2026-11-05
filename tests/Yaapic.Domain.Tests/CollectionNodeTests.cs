using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;

namespace Yaapic.Domain.Tests;

public class CollectionNodeTests
{
    static CollectionNode Req(string name, Guid? id = null) =>
        CollectionNode.ForRequest(HttpRequestDefinition.Create(name) with { Id = id ?? Guid.NewGuid() });

    static CollectionNode Dir(string name, params CollectionNode[] children) =>
        CollectionNode.ForFolder(FolderDefinition.Create(name), children);

    [Fact]
    public void Folder_create_trims_name()
    {
        var f = FolderDefinition.Create("  Docs ");
        Assert.Equal("Docs", f.Name);
        Assert.Equal("", f.Description);
        Assert.Empty(f.Variables);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Folder_create_rejects_blank_name(string? name) =>
        Assert.ThrowsAny<ArgumentException>(() => FolderDefinition.Create(name!));

    [Fact]
    public void Folder_IsEquivalentTo_compares_variables_by_sequence()
    {
        var a = FolderDefinition.Create("a") with { Variables = [new("x", "1"), new("y", "2")] };
        Assert.True(a.IsEquivalentTo(a with { Variables = [new("x", "1"), new("y", "2")] }));
        Assert.False(a.IsEquivalentTo(a with { Variables = [new("y", "2"), new("x", "1")] }));
        Assert.False(a.IsEquivalentTo(a with { Description = "d" }));
    }

    [Fact]
    public void Factories_set_the_right_members()
    {
        var r = Req("r");
        Assert.Equal(CollectionNodeKind.Request, r.Kind);
        Assert.Equal(r.Request!.Id, r.Id);
        Assert.Equal("r", r.Name);
        Assert.Null(r.Folder);
        Assert.Empty(r.Children);

        var d = Dir("d", r);
        Assert.Equal(CollectionNodeKind.Folder, d.Kind);
        Assert.Equal(d.Folder!.Id, d.Id);
        Assert.Null(d.Request);
        Assert.Same(r, Assert.Single(d.Children));
    }

    [Fact]
    public void Factories_reject_null()
    {
        Assert.Throws<ArgumentNullException>(() => CollectionNode.ForFolder(null!, []));
        Assert.Throws<ArgumentNullException>(() => CollectionNode.ForFolder(FolderDefinition.Create("a"), null!));
        Assert.Throws<ArgumentNullException>(() => CollectionNode.ForRequest(null!));
    }

    [Fact]
    public void Sort_puts_folders_first_and_ignores_case()
    {
        var sorted = CollectionNode.Sort([Req("b"), Req("A"), Dir("z"), Dir("Y")]);
        Assert.Equal(["Y", "z", "A", "b"], sorted.Select(n => n.Name));
    }

    [Fact]
    public void Sort_breaks_ties_by_id()
    {
        var lo = new Guid("00000000-0000-0000-0000-000000000001");
        var hi = new Guid("00000000-0000-0000-0000-000000000002");
        var sorted = CollectionNode.Sort([Req("a", hi), Req("A", lo)]);
        Assert.Equal([lo, hi], sorted.Select(n => n.Id));
    }

    [Fact]
    public void ForFolder_sorts_children() =>
        Assert.Equal(["sub", "a"], Dir("d", Req("a"), Dir("sub")).Children.Select(n => n.Name));

    [Fact]
    public void Descendants_and_Flatten_are_depth_first()
    {
        var tree = Dir("root", Dir("f1", Req("r1")), Req("r2"));
        Assert.Equal(["f1", "r1", "r2"], tree.Descendants().Select(n => n.Name));
        Assert.Equal(["root", "f1", "r1", "r2"], CollectionNode.Flatten([tree]).Select(n => n.Name));
        Assert.Empty(Req("x").Descendants());
    }
}
