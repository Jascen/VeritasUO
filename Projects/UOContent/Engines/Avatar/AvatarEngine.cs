using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Collections;
using Server.Engines.Craft;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Multis;
using Server.Multis.Boats;
using Server.Network;

namespace Server.Engines.Avatar;

public class AvatarEngine : GenericPersistence
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(AvatarEngine));

    private static readonly Dictionary<PlayerMobile, PlayerContext> _contexts = [];

    private static AvatarEngine _instance;

    // Set while a template is applied so the skill churn does not feed the archive or count as gains
    public static bool DisableSkillGains { get; set; }

    public static bool IsEnabled { get; private set; }

    public AvatarEngine() : base("Avatar", 10)
    {
    }

    public static void Configure()
    {
        IsEnabled = ServerConfiguration.GetOrUpdateSetting("avatar.enabled", true);
        _instance = new AvatarEngine();
    }

    public static void InitializePlayer(PlayerMobile player)
    {
        player.InitStats(10, 10, 10);
        InitializePlayerItems(player);
    }

    /// <summary>
    /// Gives the starter kit and the selected template's items as the Avatar leaves the sanctuary. Choosing a template
    /// in the sanctuary only sets stats and skills, so switching templates never hands out extra items.
    /// </summary>
    public static void GrantStartingItems(PlayerMobile player)
    {
        var context = GetContextOrDefault(player);
        if (!context.Active)
        {
            return;
        }

        CharacterCreation.CharacterCreation.AddBackpack(player);
        InitializePlayerItems(player);
        context.ApplyTemplate(player, context.SelectedTemplate);
    }

    private static void InitializePlayerItems(PlayerMobile player)
    {
        if (player.Backpack == null)
        {
            player.AddItem(new Backpack { Movable = false });
        }

        if (player.Backpack.FindItemByType<AvatarBook>() == null)
        {
            player.Backpack.AddItem(new AvatarBook());
        }

        if (SoulOrb.FindActive(player) == null)
        {
            SoulOrb.Create(player, SoulOrbType.PermadeathPlaceholder);
        }
    }

    public static void ApplyContext(PlayerMobile player, PlayerContext context)
    {
        if (!context.Active)
        {
            return;
        }

        player.StatCap = Constants.BASE_STAT_CAP + context.StatCapLevel * Constants.STAT_CAP_PER_LEVEL;
        player.Skills.Cap = Constants.SKILL_CAP_BASE + context.SkillCapLevel * Constants.SKILL_CAP_PER_LEVEL * 10;
    }

    public static PlayerContext GetContextOrDefault(Mobile mobile) =>
        mobile is PlayerMobile pm && _contexts.TryGetValue(pm, out var context) ? context : PlayerContext.Default;

    public static PlayerContext GetOrCreateContext(PlayerMobile player)
    {
        if (!_contexts.TryGetValue(player, out var context))
        {
            _contexts[player] = context = new PlayerContext(player);
        }

        return context;
    }

    /// <summary>
    /// Starts a new life on a new mobile in the same account slot and deletes the old one, so every system that holds
    /// the old mobile (quests, murder counts, virtues, titles, timers) lets go of it. The Avatar context, Safety Deposit
    /// Box, houses and player vendors move to the new mobile; the corpse and boats are lost with the rest of the old life's
    /// possessions.
    /// Deleting the old mobile disconnects its client, and the player logs back in as the new one in the sanctuary.
    /// </summary>
    /// <returns>The new mobile, or null when the old one is not an Avatar in an account slot.</returns>
    public static PlayerMobile Reincarnate(PlayerMobile oldPlayer, bool keepSkillCaps)
    {
        if (!_contexts.TryGetValue(oldPlayer, out var context))
        {
            return null;
        }

        var account = oldPlayer.Account as Account;
        var slot = -1;
        for (var i = 0; i < account?.Length; i++)
        {
            if (account[i] == oldPlayer)
            {
                slot = i;
                break;
            }
        }

        if (slot < 0)
        {
            logger.Warning("Avatar {Player} has no account slot and cannot be reincarnated", oldPlayer);
            return null;
        }

        // Race sets the body, so it must follow Female
        var newPlayer = new PlayerMobile
        {
            Player = true,
            AccessLevel = oldPlayer.AccessLevel,
            Name = oldPlayer.RawName,
            Female = oldPlayer.Female,
            Race = oldPlayer.Race,
            Hue = oldPlayer.Hue,
            HairItemID = oldPlayer.HairItemID,
            HairHue = oldPlayer.HairHue,
            FacialHairItemID = oldPlayer.FacialHairItemID,
            FacialHairHue = oldPlayer.FacialHairHue,
            Hunger = 20
        };

        account[slot] = newPlayer;

        if (keepSkillCaps)
        {
            for (var i = 0; i < newPlayer.Skills.Length; i++)
            {
                newPlayer.Skills[i].CapFixedPoint = oldPlayer.Skills[i].CapFixedPoint;
            }
        }

        if (context.UnlockRecordRecipes)
        {
            foreach (var recipeId in Recipe.Recipes.Keys)
            {
                if (oldPlayer.HasRecipe(recipeId))
                {
                    newPlayer.AcquireRecipe(recipeId);
                }
            }
        }

        context.LifetimeGameTime += context.GetRunGameTime(oldPlayer);
        _contexts.Remove(oldPlayer);
        context.TransferTo(newPlayer);
        _contexts[newPlayer] = context;
        context.RunStartGameTime = newPlayer.GameTime;

        TransferHoldings(oldPlayer, newPlayer, context);

        context.RewardCache = null;
        context.BoostedTemplateCache = null;
        context.SelectedTemplate = AvatarStarterTemplates.None;
        context.GenerateRivalry();

        AvatarSanctuary.MoveTo(newPlayer);
        InitializePlayer(newPlayer);
        ApplyContext(newPlayer, context);

        if (context.DraftModeEnabled)
        {
            context.SetDraftModeEnabled(newPlayer, true);
        }

        newPlayer.Hits = newPlayer.HitsMax;
        newPlayer.Stam = newPlayer.StamMax;
        newPlayer.Mana = newPlayer.ManaMax;

        // Parked like any logged-out character until the player logs in to it
        newPlayer.LogoutLocation = newPlayer.Location;
        newPlayer.LogoutMap = newPlayer.Map;
        newPlayer.Internalize();

        logger.Information("Avatar {OldPlayer} reincarnated as {NewPlayer}", oldPlayer, newPlayer);

        // The old life's gear goes with it rather than lying out for others to loot
        oldPlayer.Corpse?.Delete();
        oldPlayer.Delete();

        return newPlayer;
    }

    // Must run before the old mobile is deleted, which would take its bank box and houses with it
    private static void TransferHoldings(PlayerMobile oldPlayer, PlayerMobile newPlayer, PlayerContext context)
    {
        if (context.HasSafetyDepositBox)
        {
            var box = context.SafetyDepositBox;
            box.Owner = newPlayer;
            newPlayer.BankBox.AddItem(box);
            box.Location = new Point3D(0, 0, 0);
        }

        foreach (var house in BaseHouse.GetHouses(oldPlayer))
        {
            house.Owner = newPlayer;

            foreach (var vendor in house.PlayerVendors)
            {
                if (vendor.Owner == oldPlayer)
                {
                    vendor.Owner = newPlayer;
                }
            }
        }

        using var boats = PooledRefQueue<BaseBoat>.Create();
        foreach (var boat in BaseBoat.Boats)
        {
            if (boat.Owner == oldPlayer)
            {
                boats.Enqueue(boat);
            }
        }

        while (boats.Count > 0)
        {
            boats.Dequeue().Delete();
        }
    }

    public static bool CanGainSkill(Mobile from, Skill skill)
    {
        if (DisableSkillGains)
        {
            return false;
        }

        var context = GetContextOrDefault(from);
        return !context.Active || context.IsSkillDrafted(skill.SkillName);
    }

    public static double GetSkillGainMultiplier(Mobile from)
    {
        var context = GetContextOrDefault(from);
        return context.Active && context.SkillGainRateLevel > 0
            ? 1 + Constants.SKILL_GAIN_RATE_PER_LEVEL * context.SkillGainRateLevel * 0.01
            : 1.0;
    }

    public static void OnSkillGain(Mobile from, Skill skill)
    {
        if (DisableSkillGains || from is not PlayerMobile player)
        {
            return;
        }

        var context = GetContextOrDefault(player);
        if (!context.Active)
        {
            return;
        }

        context.Skills[skill.SkillID] = Math.Max(context.Skills[skill.SkillID], skill.BaseFixedPoint);
    }

    private static int GetBonusCoinsAmount(int value, PlayerContext context) =>
        (int)(value * context.PointGainRateLevel * Constants.POINT_GAIN_RATE_PER_LEVEL * 0.01);

    private static void GrantCoins(PlayerMobile player, int value, PlayerContext context)
    {
        context.PointsFarmed += value;

        player.SendMessage($"You have gained {value} coins.");
    }

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature creature)
    {
        if (!IsEnabled || _contexts.Count == 0)
        {
            return;
        }

        if (creature.AI == AIType.AI_Vendor || creature.NoKillAwards || creature.Summoned || creature.Controlled ||
            creature.IsDeadPet)
        {
            return;
        }

        var rights = BaseCreature.GetLootingRights(creature.DamageEntries, creature.HitsMax);

        var damagerCount = 0;
        for (var i = 0; i < rights.Count; i++)
        {
            if (rights[i].m_HasRight)
            {
                damagerCount++;
            }
        }

        if (damagerCount == 0)
        {
            return;
        }

        for (var i = 0; i < rights.Count; i++)
        {
            var ds = rights[i];
            if (ds.m_HasRight && ds.m_Mobile is PlayerMobile player)
            {
                OnKilledBy(creature, player, damagerCount);
            }
        }
    }

    private static void OnKilledBy(BaseCreature creature, PlayerMobile player, int damagerCount)
    {
        var context = GetContextOrDefault(player);
        if (!context.Active)
        {
            return;
        }

        var value = CoinRewardCalculator.GetKillCoinValue(creature).Total;
        if (value < 1)
        {
            return;
        }

        if (damagerCount > 1)
        {
            value /= 2;
        }

        context.LifetimeCreatureKills += 1;

        // Apply bonus coin multiplier
        value += GetBonusCoinsAmount(value, context);

        // Apply rivalry bonus
        if (context.HasRivalFaction)
        {
            var slayer = SlayerGroup.GetEntryByName(context.RivalSlayerName);
            if (slayer?.Slays(creature) == true)
            {
                context.LifetimeEnemyFactionKills += 1;

                if (context.RivalBonusEnabled)
                {
                    var bonus = (int)(value * Constants.RIVAL_BONUS_PERCENT * 0.01);
                    if (bonus > 0)
                    {
                        context.RivalBonusPoints += bonus;
                        value += bonus;

                        if (context.RivalBonusPoints >= Constants.RIVAL_BONUS_MAX_POINTS)
                        {
                            context.RivalBonusEnabled = false;
                            player.SendMessage(
                                $"You have avenged your family by vanquishing all members of '{context.RivalFactionName}'."
                            );
                        }
                        else
                        {
                            player.SendMessage($"You have eliminated another member of '{context.RivalFactionName}'.");
                        }
                    }
                }
            }
        }

        GrantCoins(player, value, context);
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile player)
    {
        var context = GetContextOrDefault(player);
        if (!context.Active)
        {
            return;
        }

        DeathContext.TrySave(player, context);

        context.LifetimeDeaths += 1;
        context.LifetimePointsGained += context.PointsFarmed;
        context.PointsSaved += context.PointsFarmed;
        context.PointsFarmed = 0;
        context.RivalBonusPoints = 0;
    }

    [OnEvent(nameof(PlayerMobile.PlayerDeletedEvent))]
    public static void OnPlayerDeleted(Mobile m)
    {
        if (m is PlayerMobile pm && _contexts.Remove(pm))
        {
            logger.Information("Removed Avatar context for player {Player}", pm);
        }
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version

        writer.WriteEncodedInt(_contexts.Count);
        foreach (var (player, context) in _contexts)
        {
            writer.Write(player);
            context.Serialize(writer);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version

        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var player = reader.ReadEntity<PlayerMobile>();
            var context = new PlayerContext(player);
            context.Deserialize(reader);

            if (player?.Deleted == false)
            {
                _contexts[player] = context;
            }
        }

        logger.Information("Loaded Avatar data for {Count} characters", _contexts.Count);
    }
}
