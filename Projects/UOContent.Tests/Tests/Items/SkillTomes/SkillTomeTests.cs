using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class SkillTomeTests
{
    private static PlayerMobile CreatePlayer(int x)
    {
        var m = new PlayerMobile(World.NewMobile);
        m.DefaultMobileInit();
        m.MoveToWorld(new Point3D(x, 620, 0), Map.Felucca);
        m.AddItem(new Backpack());
        m.Skills.Swords.Base = 100.0;
        m.Skills.Magery.Base = 50.0;
        return m;
    }

    private static SkillTome GiveTome(Mobile m, SkillName skill, params int[] sections)
    {
        var tome = new SkillTome();
        m.Backpack.DropItem(tome);

        foreach (var section in sections)
        {
            var page = new TomePage(skill, section);
            m.Backpack.DropItem(page);
            Assert.True(tome.TryAddPage(m, page));
            Assert.True(page.Deleted);
        }

        return tome;
    }

    [Fact]
    public void BoundPages_RaiseSkillPastCap_WithoutChangingCap()
    {
        var m = CreatePlayer(4400);

        try
        {
            // Common (1 page) + rare folio (3 pages) + ultra rare folio (5 pages).
            var tome = GiveTome(m, SkillName.Swords, 0, 9, 11);

            Assert.Equal(SkillName.Swords, tome.Skill);
            Assert.Equal(9, tome.PageTotal);
            Assert.Equal(109.0, m.Skills.Swords.Value);
            Assert.Equal(100.0, m.Skills.Swords.Cap);
            Assert.Equal(100.0, m.Skills.Swords.Base);
        }
        finally
        {
            m.Delete();
        }
    }

    [Fact]
    public void Tome_HoldsPagesAsOneItem()
    {
        var m = CreatePlayer(4402);

        try
        {
            GiveTome(m, SkillName.Magery, 0, 1, 2, 3, 4);

            Assert.Single(m.Backpack.Items);
            Assert.Equal(55.0, m.Skills.Magery.Value);
        }
        finally
        {
            m.Delete();
        }
    }

    [Fact]
    public void WrongSkillOrDuplicatePage_IsRejected()
    {
        var m = CreatePlayer(4404);

        try
        {
            var tome = GiveTome(m, SkillName.Swords, 4);

            var duplicate = new TomePage(SkillName.Swords, 4);
            var wrongSkill = new TomePage(SkillName.Magery, 5);
            m.Backpack.DropItem(duplicate);
            m.Backpack.DropItem(wrongSkill);

            Assert.False(tome.TryAddPage(m, duplicate));
            Assert.False(tome.TryAddPage(m, wrongSkill));
            Assert.False(duplicate.Deleted);
            Assert.False(wrongSkill.Deleted);
            Assert.Equal(1, tome.PageTotal);
        }
        finally
        {
            m.Delete();
        }
    }

    [Fact]
    public void Bonus_OnlyAppliesWhileLooseInBackpack()
    {
        var m = CreatePlayer(4406);

        try
        {
            var tome = GiveTome(m, SkillName.Magery, 0, 1);
            Assert.Equal(52.0, m.Skills.Magery.Value);

            var bag = new Bag();
            m.Backpack.DropItem(bag);
            bag.DropItem(tome);
            Assert.Equal(50.0, m.Skills.Magery.Value);

            m.Backpack.DropItem(tome);
            Assert.Equal(52.0, m.Skills.Magery.Value);

            tome.Delete();
            Assert.Equal(50.0, m.Skills.Magery.Value);
        }
        finally
        {
            m.Delete();
        }
    }

    [Fact]
    public void TomesOfSameSkill_UnionPages()
    {
        var m = CreatePlayer(4408);

        try
        {
            GiveTome(m, SkillName.Magery, 0, 1, 9);
            GiveTome(m, SkillName.Magery, 1, 9, 11);

            // Union of sections 0, 1, 9, 11 = 1 + 1 + 3 + 5 pages.
            Assert.Equal(60.0, m.Skills.Magery.Value);
        }
        finally
        {
            m.Delete();
        }
    }

    [Fact]
    public void FullTome_GrantsExactlyTwenty()
    {
        var m = CreatePlayer(4410);

        try
        {
            var all = new int[SkillTomeInfo.Sections.Length];

            for (var i = 0; i < all.Length; i++)
            {
                all[i] = i;
            }

            var tome = GiveTome(m, SkillName.Swords, all);

            Assert.Equal(SkillTomeInfo.PageCount, tome.PageTotal);
            Assert.Equal(120.0, m.Skills.Swords.Value);
        }
        finally
        {
            m.Delete();
        }
    }

    [Fact]
    public void Sections_CoverTwentyPagesWithExpectedRarities()
    {
        var mask = 0;
        var counts = new int[4];

        foreach (var section in SkillTomeInfo.Sections)
        {
            Assert.Equal(0, mask & section.Mask);
            mask |= section.Mask;
            counts[(int)section.Rarity]++;

            var expectedPages = section.Rarity switch
            {
                PageRarity.UltraRare => 5,
                PageRarity.Rare      => 3,
                _                    => 1
            };

            Assert.Equal(expectedPages, section.PageCount);
        }

        Assert.Equal((1 << SkillTomeInfo.PageCount) - 1, mask);
        Assert.Equal([5, 4, 2, 1], counts);

        for (var i = 0; i < 50; i++)
        {
            var page = TomePage.Create(SkillName.Swords, PageRarity.Rare);
            Assert.Equal(PageRarity.Rare, page.Rarity);
            page.Delete();
        }
    }
}
