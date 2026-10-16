using System.Collections.Generic;
using System.Linq;
using AdminMenu;
using Xunit;

public class ItemSearchTests
{
    private static readonly List<ItemEntry> Items = new List<ItemEntry>
    {
        new ItemEntry { Name = "Wrench", Rarity = "Common", PrefabName = "Item_Wrench" },
        new ItemEntry { Name = "Duct Tape", Rarity = "Uncommon", PrefabName = "Item_DuctTape" },
        new ItemEntry { Name = "Golden Wrench", Rarity = "Rare", PrefabName = "Item_GoldWrench" },
    };

    [Fact]
    public void EmptyOrNullQueryReturnsEverythingSortedByName()
    {
        var expected = new[] { "Duct Tape", "Golden Wrench", "Wrench" };
        Assert.Equal(expected, ItemSearch.Filter(Items, "").Select(i => i.Name));
        Assert.Equal(expected, ItemSearch.Filter(Items, null).Select(i => i.Name));
        Assert.Equal(expected, ItemSearch.Filter(Items, "   ").Select(i => i.Name));
    }

    [Fact]
    public void MatchesSubstringsOfTheNameCaseInsensitively()
    {
        Assert.Equal(new[] { "Golden Wrench", "Wrench" }, ItemSearch.Filter(Items, "WRENCH").Select(i => i.Name));
        Assert.Equal(new[] { "Duct Tape" }, ItemSearch.Filter(Items, "t ta").Select(i => i.Name));
    }

    [Fact]
    public void TrimsTheQuery()
    {
        Assert.Single(ItemSearch.Filter(Items, "  duct  "));
    }

    [Fact]
    public void MatchesTheDisplayNameOnlyNotThePrefabName()
    {
        Assert.Empty(ItemSearch.Filter(Items, "Item_"));
    }

    [Fact]
    public void KeepsDuplicateNames()
    {
        var items = new List<ItemEntry>
        {
            new ItemEntry { Name = "Bolt", PrefabName = "Bolt_B" },
            new ItemEntry { Name = "Bolt", PrefabName = "Bolt_A" },
        };
        var result = ItemSearch.Filter(items, "bolt");
        Assert.Equal(new[] { "Bolt_A", "Bolt_B" }, result.Select(i => i.PrefabName));
    }

    [Fact]
    public void IgnoresNullEntriesAndNames()
    {
        var items = new List<ItemEntry> { null, new ItemEntry { Name = null, PrefabName = "x" }, new ItemEntry { Name = "Saw" } };
        Assert.Equal(new[] { "Saw" }, ItemSearch.Filter(items, "saw").Select(i => i.Name));
        Assert.Equal(2, ItemSearch.Filter(items, "").Count);
        Assert.Empty(ItemSearch.Filter(null, "saw"));
    }
}
