using System;
using System.IO;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.Avatar;
using Server.Items;
using Server.Mobiles;
using Server.Veritas.StartingCamp;
using Xunit;

namespace UOContent.Tests;

[Collection("Sequential UOContent Tests")]
public class AvatarSystemTests
{
    private static PlayerMobile CreatePlayer()
    {
        var m = new PlayerMobile(World.NewMobile);
        m.DefaultMobileInit();
        m.MoveToWorld(new Point3D(1500, 1500, 0), Map.Felucca);
        return m;
    }

    private static void Cleanup(PlayerMobile player)
    {
        AvatarEngine.OnPlayerDeleted(player);
        player.Delete();
    }

    [Fact]
    public void PlayerContextRoundTripsThroughSerialization()
    {
        var player = CreatePlayer();
        var context = AvatarEngine.GetOrCreateContext(player);

        context.PointsSaved = 12345;
        context.StatCapLevel = 7;
        context.RivalSlayerName = SlayerName.Fey;
        context.LifetimeGameTime = TimeSpan.FromHours(3);
        context.Skills[SkillName.Magery] = 850;
        context.SetDraftModeEnabled(player, true);
        context.AddDraftedSkill(SkillName.Swords);
        context.AddDraftBannedSkill(SkillName.Begging);

        var writer = new BufferWriter(true);
        context.Serialize(writer);
        var buffer = writer.Buffer.AsSpan(0, (int)writer.Position).ToArray();
        writer.Close();

        var copy = new PlayerContext(player);
        copy.Deserialize(new BufferReader(buffer));

        Assert.Equal(12345, copy.PointsSaved);
        Assert.Equal(7, copy.StatCapLevel);
        Assert.Equal(SlayerName.Fey, copy.RivalSlayerName);
        Assert.Equal(TimeSpan.FromHours(3), copy.LifetimeGameTime);
        Assert.Equal(850, copy.Skills[SkillName.Magery]);
        Assert.True(copy.DraftModeEnabled);
        Assert.True(copy.IsSkillDrafted(SkillName.Swords));
        Assert.False(copy.IsSkillDrafted(SkillName.Magery));
        Assert.Contains(SkillName.Begging, copy.DraftBannedSkills);

        Cleanup(player);
    }

    [Fact]
    public void DraftLevelMathMatchesMemento()
    {
        var player = CreatePlayer();
        var context = AvatarEngine.GetOrCreateContext(player);

        Assert.Equal(1, context.DraftLevel);
        Assert.Equal(Constants.DRAFT_START_PICK_AMOUNT, context.DraftPicksAvailable);

        // Level 1 needs 500, level 2 needs 540, level 3 needs 580
        context.LifetimePointsGained = 500 + 540 + 580;
        Assert.Equal(4, context.DraftLevel);
        Assert.Equal(Constants.DRAFT_START_PICK_AMOUNT + 1, context.DraftPicksAvailable);

        Cleanup(player);
    }

    [Fact]
    public void StrongerCreaturesAreWorthMoreCoins()
    {
        var rat = new Rat();
        var dragon = new Dragon();

        var ratValue = CoinRewardCalculator.GetKillCoinValue(rat).Total;
        var dragonValue = CoinRewardCalculator.GetKillCoinValue(dragon).Total;

        Assert.True(ratValue > 0);
        Assert.True(dragonValue > ratValue);

        rat.Delete();
        dragon.Delete();
    }

    [Fact]
    public void ReincarnateReplacesMobileAndKeepsHoldings()
    {
        var previousAlgorithm = AccountSecurity.CurrentAlgorithm;
        AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.Argon2;

        var account = Accounts.GetAccount("avatar-reincarnate-user") as Account ??
                      new Account("avatar-reincarnate-user", "avatar-reincarnate-password");
        var player = CreatePlayer();
        PlayerMobile reborn = null;
        try
        {
            player.RawName = "Reborn Avatar";
            player.Female = true;
            player.HairItemID = 0x203B;
            account[0] = player;

            var context = AvatarEngine.GetOrCreateContext(player);
            context.StatCapLevel = 5;

            var box = context.GetOrCreateSafetyDepositBox(player);
            var kept = new Gold(100);
            box.DropItem(kept);

            player.AddItem(new Backpack());
            var lost = new Gold(50);
            player.AddToBackpack(lost);
            player.Skills[SkillName.Swords].Base = 80.0;
            player.Skills[SkillName.Swords].Cap = 110.0;

            var corpse = new Corpse(player, []);
            var looted = new Gold(75);
            corpse.DropItem(looted);
            player.Corpse = corpse;

            reborn = AvatarEngine.Reincarnate(player, true);

            Assert.True(corpse.Deleted);
            Assert.True(looted.Deleted);

            Assert.NotNull(reborn);
            Assert.True(player.Deleted);
            Assert.NotEqual(player.Serial, reborn.Serial);
            Assert.Same(reborn, account[0]);
            Assert.False(AvatarEngine.GetContextOrDefault(player).Active);
            Assert.Same(context, reborn.Avatar);
            Assert.Same(reborn, context.Player);

            Assert.Equal("Reborn Avatar", reborn.RawName);
            Assert.True(reborn.Female);
            Assert.Equal(0x203B, reborn.HairItemID);

            Assert.True(lost.Deleted);
            Assert.False(kept.Deleted);
            Assert.Same(box, reborn.BankBox.FindItemByType<SafetyDepositBox>());
            Assert.Same(reborn, box.Owner);
            Assert.NotNull(reborn.Backpack.FindItemByType<AvatarBook>());

            Assert.Equal(0, reborn.Skills[SkillName.Swords].BaseFixedPoint);
            Assert.Equal(1100, reborn.Skills[SkillName.Swords].CapFixedPoint);
            Assert.Equal(Constants.BASE_STAT_CAP + 5, reborn.StatCap);

            Assert.Equal(Map.Internal, reborn.Map);
            Assert.Equal(GypsyCamp.Arrival, reborn.LogoutLocation);
        }
        finally
        {
            account[0] = null;
            if (reborn != null)
            {
                Cleanup(reborn);
            }

            if (!player.Deleted)
            {
                Cleanup(player);
            }

            account.Delete();
            AccountSecurity.CurrentAlgorithm = previousAlgorithm;
        }
    }

    [Fact]
    public void ReincarnateIgnoresNonAvatars()
    {
        var player = CreatePlayer();

        Assert.Null(AvatarEngine.Reincarnate(player, true));
        Assert.False(player.Deleted);

        Cleanup(player);
    }

    [Fact]
    public void GrantStartingItemsGivesKitAndTemplateItems()
    {
        var player = CreatePlayer();
        var context = AvatarEngine.GetOrCreateContext(player);
        AvatarEngine.InitializePlayer(player);

        // A fresh Avatar life holds only the Avatar book and soul orb until it leaves the sanctuary.
        Assert.Null(player.Backpack.FindItemByType<Gold>());

        context.SelectedTemplate = AvatarStarterTemplates.Warrior;
        AvatarEngine.GrantStartingItems(player);

        Assert.NotNull(player.Backpack.FindItemByType<Gold>());
        var swordsBag = false;
        foreach (var bag in player.Backpack.FindItemsByType<Bag>())
        {
            swordsBag |= bag.Name == player.Skills[SkillName.Swords].Name;
        }

        var books = 0;
        foreach (var _ in player.Backpack.FindItemsByType<AvatarBook>())
        {
            books++;
        }

        Assert.True(swordsBag);
        Assert.Equal(1, books);

        Cleanup(player);
    }

    [Fact]
    public void GrantStartingItemsIgnoresNonAvatars()
    {
        var player = CreatePlayer();
        player.AddItem(new Backpack());

        AvatarEngine.GrantStartingItems(player);

        Assert.Empty(player.Backpack.Items);

        Cleanup(player);
    }

    [Fact]
    public void UndraftedSkillsCannotGain()
    {
        var player = CreatePlayer();
        var context = AvatarEngine.GetOrCreateContext(player);
        context.SetDraftModeEnabled(player, true);
        context.AddDraftedSkill(SkillName.Swords);

        Assert.True(AvatarEngine.CanGainSkill(player, player.Skills[SkillName.Swords]));
        Assert.False(AvatarEngine.CanGainSkill(player, player.Skills[SkillName.Magery]));

        Cleanup(player);
    }
}
