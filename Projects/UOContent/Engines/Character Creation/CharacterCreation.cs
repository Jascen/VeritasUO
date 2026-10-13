using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Engines.Affixes;
using Server.Items;
using Server.Logging;
using Server.Maps;
using Server.Misc;
using Server.Mobiles;
using Server.Network;

namespace Server.Engines.CharacterCreation;

public static partial class CharacterCreation
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(CharacterCreation));

    // Allowed skills that are not race or era specific
    private static readonly HashSet<SkillName> _allowedStartingSkills =
    [
        SkillName.Alchemy,
        SkillName.Anatomy,
        SkillName.AnimalLore,
        SkillName.AnimalTaming,
        SkillName.Archery,
        SkillName.ArmsLore,
        SkillName.Begging,
        SkillName.Blacksmith,
        SkillName.Fletching,
        SkillName.Bushido,
        SkillName.Camping,
        SkillName.Carpentry,
        SkillName.Cartography,
        SkillName.Chivalry,
        SkillName.Cooking,
        SkillName.DetectHidden,
        SkillName.Discordance,
        SkillName.EvalInt,
        SkillName.Fencing,
        SkillName.Fishing,
        SkillName.Focus,
        SkillName.Forensics,
        SkillName.Healing,
        SkillName.Herding,
        SkillName.Hiding,
        SkillName.Imbuing,
        SkillName.Inscribe,
        SkillName.ItemID,
        SkillName.Lockpicking,
        SkillName.Lumberjacking,
        SkillName.Macing,
        SkillName.Magery,
        SkillName.Meditation,
        SkillName.Mining,
        SkillName.Musicianship,
        SkillName.Mysticism,
        SkillName.Necromancy,
        SkillName.Ninjitsu,
        SkillName.Parry,
        SkillName.Peacemaking,
        SkillName.Poisoning,
        SkillName.Provocation,
        SkillName.MagicResist,
        SkillName.Snooping,
        SkillName.SpiritSpeak,
        SkillName.Stealing,
        SkillName.Swords,
        SkillName.Tactics,
        SkillName.Tailoring,
        SkillName.TasteID,
        SkillName.Throwing,
        SkillName.Tinkering,
        SkillName.Tracking,
        SkillName.Veterinary,
        SkillName.Wrestling
    ];

    private static readonly TimeSpan BadStartMessageDelay = TimeSpan.FromSeconds(3.5);

    public static readonly CityInfo[] OldHavenStartingCities =
    [
        new("Haven", "The Bountiful Harvest Inn", 3677, 2625, 0, Map.Trammel),
        new("Britain", "Sweet Dreams Inn", 1075074, 1496, 1628, 10, Map.Trammel),
        new("Magincia", "The Great Horns Tavern", 1075077, 3734, 2222, 20, Map.Trammel),
    ];

    public static readonly CityInfo[] FeluccaStartingCities =
    [
        new("Yew", "The Empath Abbey", 1075072, 633, 858, 0, Map.Felucca),
        new("Minoc", "The Barnacle", 1075073, 2476, 413, 15, Map.Felucca),
        new("Britain", "Sweet Dreams Inn", 1075074, 1496, 1628, 10, Map.Felucca),
        new("Moonglow", "The Scholars Inn", 1075075, 4408, 1168, 0, Map.Felucca),
        new("Trinsic", "The Traveler's Inn", 1075076, 1845, 2745, 0, Map.Felucca),
        new("Magincia", "The Great Horns Tavern", 1075077, 3734, 2222, 20, Map.Felucca),
        new("Jhelom", "The Mercenary Inn", 1075078, 1374, 3826, 0, Map.Felucca),
        new("Skara Brae", "The Falconer's Inn", 1075079, 618, 2234, 0, Map.Felucca),
        new("Vesper", "The Ironwood Inn", 1075080, 2771, 976, 0, Map.Felucca)
    ];

    public static readonly CityInfo[] TrammelStartingCities =
    [
        new("Yew", "The Empath Abbey", 1075072, 633, 858, 0, Map.Trammel),
        new("Minoc", "The Barnacle", 1075073, 2476, 413, 15, Map.Trammel),
        new("Moonglow", "The Scholars Inn", 1075075, 4408, 1168, 0, Map.Trammel),
        new("Trinsic", "The Traveler's Inn", 1075076, 1845, 2745, 0, Map.Trammel),
        new("Jhelom", "The Mercenary Inn", 1075078, 1374, 3826, 0, Map.Trammel),
        new("Skara Brae", "The Falconer's Inn", 1075079, 618, 2234, 0, Map.Trammel),
        new("Vesper", "The Ironwood Inn", 1075080, 2771, 976, 0, Map.Trammel),
    ];

    public static readonly CityInfo[] NewHavenStartingCities =
    [
        new("New Haven", "The Bountiful Harvest Inn", 1150168, 3503, 2574, 14, Map.Trammel),
        new("Britain", "The Wayfarer's Inn", 1075074, 1602, 1591, 20, Map.Trammel)
        // Magincia removed because it burned down.
    ];

    public static readonly CityInfo[] StartingCitiesSA =
    [
        new("Royal City", "Royal City Inn", 1150169, 738, 3486, -19, Map.TerMur)
    ];

    // Temporary: every new player character starts in New Haven regardless of client selection or profession.
    private const bool ForceNewHavenStart = true;

    private static CityInfo[] _availableStartingCities;

    public static CityInfo[] GetStartingCities() =>
        _availableStartingCities ??= ConstructAvailableStartingCities();

    private static CityInfo[] ConstructAvailableStartingCities()
    {
        if (ForceNewHavenStart)
        {
            return [NewHavenStartingCities[0]];
        }

        var pre6000ClientSupport = TileMatrix.Pre6000ClientSupport;
        var availableMaps = ExpansionInfo.CoreExpansion.MapSelectionFlags;
        var trammelAvailable = availableMaps.Includes(MapSelectionFlags.Trammel);
        var terMerAvailable = availableMaps.Includes(MapSelectionFlags.TerMur);

        if (trammelAvailable)
        {
            if (pre6000ClientSupport)
            {
                return [..OldHavenStartingCities, ..TrammelStartingCities];
            }

            if (terMerAvailable)
            {
                return [..NewHavenStartingCities, ..TrammelStartingCities, ..StartingCitiesSA];
            }

            return [..NewHavenStartingCities, ..TrammelStartingCities];
        }

        if (availableMaps.Includes(MapSelectionFlags.Felucca))
        {
            return FeluccaStartingCities;
        }

        logger.Error("No starting cities are available.");
        return [];
    }

    [GeneratedEvent(nameof(CharacterCreatedEvent))]
    public static partial void CharacterCreatedEvent(CharacterCreatedEventArgs e);

    public static void AddBackpack(this Mobile m)
    {
        var pack = m.Backpack;

        if (pack == null)
        {
            pack = new Backpack();
            pack.Movable = false;

            m.AddItem(pack);
        }

        m.PackItem(new RedBook("a book", m.Name, 20, true));
        m.PackItem(new Gold(1000)); // Starting gold can be customized here
        m.PackItem(new Dagger());
        m.PackItem(new Candle());
    }

    private static Mobile CreateMobile(Account a)
    {
        if (a.Count >= a.Limit)
        {
            return null;
        }

        for (var i = 0; i < a.Length; ++i)
        {
            if (a[i] == null)
            {
                return a[i] = new PlayerMobile();
            }
        }

        return null;
    }

    [OnEvent(nameof(CharacterCreatedEvent))]
    private static void OnCharacterCreated(CharacterCreatedEventArgs args)
    {
        if (!ProfessionInfo.GetProfession(args.Profession, out var profession))
        {
            args.Profession = 0;
        }

        var state = args.State;

        if (state == null)
        {
            return;
        }

        var newChar = CreateMobile(args.Account as Account);

        if (newChar == null)
        {
            logger.Information("Login: {NetState}: Character creation failed, account full", state);
            return;
        }

        args.Mobile = newChar;

        newChar.Player = true;
        newChar.AccessLevel = args.Account.AccessLevel;
        newChar.Female = args.Female;
        newChar.Hue = newChar.Race.ClipSkinHue(args.Hue & 0x3FFF) | 0x8000;
        newChar.Hunger = 20;

        SetName(newChar, args.Name);
        newChar.AddBackpack();

        if (newChar.AccessLevel == AccessLevel.Player)
        {
            var race = Core.Expansion >= args.Race.RequiredExpansion ? args.Race : Race.DefaultRace;
            newChar.Race = race;

            if (newChar is PlayerMobile pm)
            {
                if (((Account)pm.Account).Young)
                {
                    pm.Young = true;

                    newChar.BankBox.DropItem(new NewPlayerTicket
                    {
                        Owner = newChar
                    });
                }
            }

            SetStats(newChar, state, profession?.Stats ?? args.Stats);
            SetSkills(newChar, profession?.Skills ?? args.Skills);
            GiveProfessionItems(newChar, profession, args.ShirtHue, args.PantsHue);

            if (race.ValidateHair(newChar, args.HairID))
            {
                newChar.HairItemID = args.HairID;
                newChar.HairHue = race.ClipHairHue(args.HairHue & 0x3FFF);
            }

            if (race.ValidateFacialHair(newChar, args.BeardID))
            {
                newChar.FacialHairItemID = args.BeardID;
                newChar.FacialHairHue = race.ClipHairHue(args.BeardHue & 0x3FFF);
            }

            if (TestCenter.Enabled)
            {
                TestCenter.FillBankbox(newChar);
            }
        }
        else
        {
            newChar.Str = 100;
            newChar.Int = 100;
            newChar.Dex = 100;

            for (var i = 0; i < newChar.Skills.Length; i++)
            {
                newChar.Skills[i].BaseFixedPoint = 1000;
            }

            newChar.Race = Race.Human;
            newChar.Blessed = true;
            newChar.AddItem(new StaffRobe(newChar.AccessLevel));
        }

        var city = GetStartLocation(args);
        newChar.MoveToWorld(city.Location, city.Map);

        logger.Information(
            "Login: {0}: New character being created (account={1}, character={2}, serial={3}, started.city={4}, started.location={5}, started.map={6})",
            state,
            args.Account.Username,
            newChar.Name,
            newChar.Serial,
            city.City,
            city.Location,
            city.Map);

        new WelcomeTimer(newChar).Start();
    }

    private static CityInfo GetStartLocation(CharacterCreatedEventArgs args)
    {
        var availableMaps = ExpansionInfo.CoreExpansion.MapSelectionFlags;
        var m = args.Mobile;

        if (m.AccessLevel > AccessLevel.Player)
        {
            var map = availableMaps.Includes(MapSelectionFlags.Felucca) ? Map.Felucca : Map.Trammel;
            if (availableMaps.Includes(MapSelectionFlags.Felucca))
            {
                return new CityInfo("Green Acres", "Green Acres", 5445, 1153, 0, map);
            }
        }

        if (ForceNewHavenStart)
        {
            return NewHavenStartingCities[0];
        }

        if (Core.SA)
        {
            return args.City;
        }

        var flags = args.State?.Flags ?? ClientFlags.None;
        if (ProfessionInfo.GetProfession(args.Profession, out var profession))
        {
            switch (profession.Name.ToLowerInvariant())
            {
                case "necromancer":
                    {
                        if ((flags & ClientFlags.Malas) != 0 && availableMaps.Includes(MapSelectionFlags.Malas))
                        {
                            return new CityInfo("Umbra", "Mardoth's Tower", 2114, 1301, -50, Map.Malas);
                        }

                        /*
                         * Unfortunately you are playing on a *NON-Age-Of-Shadows* game
                         * installation and cannot be transported to Malas.
                         * You will not be able to take your new player quest in Malas
                         * without an AOS client.  You are now being taken to the city of
                         * Haven on the Trammel facet.
                         */
                        Timer.StartTimer(BadStartMessageDelay, () => m.SendLocalizedMessage(1062205));
                        return GetStartingCities()[0];
                    }
                case "paladin":
                    {
                        return GetStartingCities()[0];
                    }
                case "samurai":
                    {
                        var haotisAndTokunoAccessible =
                            (flags & ClientFlags.Tokuno) == ClientFlags.Tokuno &&
                            (flags & ClientFlags.Malas) == ClientFlags.Malas &&
                            availableMaps.Includes(MapSelectionFlags.Malas | MapSelectionFlags.Tokuno);

                        if (haotisAndTokunoAccessible)
                        {
                            return new CityInfo("Samurai DE", "Haoti's Grounds", 368, 780, -1, Map.Malas);
                        }

                        /*
                         * Unfortunately you are playing on a *NON-Samurai-Empire* game
                         * installation and cannot be transported to Tokuno.
                         * You will not be able to take your new player quest in Tokuno
                         * without an SE client. You are now being taken to the city of
                         * Haven on the Trammel facet.
                         */
                        Timer.StartTimer(BadStartMessageDelay, () => m.SendLocalizedMessage(1063487));
                        return GetStartingCities()[0];
                    }
                case "ninja":
                    {
                        var enimosAndTokunoAccessible =
                            (flags & ClientFlags.Tokuno) == ClientFlags.Tokuno &&
                            (flags & ClientFlags.Malas) == ClientFlags.Malas &&
                            availableMaps.Includes(MapSelectionFlags.Malas | MapSelectionFlags.Tokuno);

                        if (enimosAndTokunoAccessible)
                        {
                            return new CityInfo("Ninja DE", "Enimo's Residence", 414, 823, -1, Map.Malas);
                        }

                        /*
                         * Unfortunately you are playing on a *NON-Samurai-Empire* game
                         * installation and cannot be transported to Tokuno.
                         * You will not be able to take your new player quest in Tokuno
                         * without an SE client. You are now being taken to the city of
                         * Haven on the Trammel facet.
                         */
                        Timer.StartTimer(BadStartMessageDelay, () => m.SendLocalizedMessage(1063487));
                        return GetStartingCities()[0];
                    }
            }
        }

        return args.City;
    }

    private static void SetStats(Mobile m, NetState state, byte[] stats)
    {
        var maxStats = state.NewCharacterCreation ? 90 : 80;

        var str = stats[0];
        var dex = stats[1];
        var intel = stats[2];

        if (str is < 10 or > 60 || dex is < 10 or > 60 || intel is < 10 or > 60 || str + dex + intel != maxStats)
        {
            str = 10;
            dex = 10;
            intel = 10;
        }

        m.InitStats(str, dex, intel);
    }

    private static void SetName(Mobile m, string name)
    {
        name = name.Trim();

        if (!NameVerification.ValidatePlayerName(name))
        {
            name = "Generic Player";
        }

        m.Name = name;
    }

    private static bool ValidateSkills(int raceFlag, (SkillName, byte)[] skills)
    {
        var total = 0;

        for (var i = 0; i < skills.Length; ++i)
        {
            var (name, value) = skills[i];

            if (value > 50 || !_allowedStartingSkills.Contains(name))
            {
                return false;
            }

            /**
             * Note: Change to Alchemy @ 0 skill if something invalid is chosen.
             * To avoid this, modify the client to only show the skills allowed by your shard.
             */
            switch (name)
            {
                case SkillName.Necromancy or SkillName.Chivalry or SkillName.Focus when !Core.AOS:
                case SkillName.Ninjitsu or SkillName.Bushido when !Core.SE:
                case SkillName.Throwing or SkillName.Imbuing when !Core.SA:
                case SkillName.Archery when raceFlag == Race.AllowGargoylesOnly:
                case SkillName.Throwing when raceFlag != Race.AllowGargoylesOnly:
                    {
                        skills[i] = default;
                        break;
                    }
            }

            total += value;

            // Do not allow a skill to be listed twice
            for (var j = i + 1; j < skills.Length; ++j)
            {
                var (nameCheck, valueCheck) = skills[j];

                if (valueCheck > 0 && nameCheck == name)
                {
                    return false;
                }
            }
        }

        return total is 100 or 120;
    }

    private static void SetSkills(Mobile m, (SkillName, byte)[] skills)
    {
        if (!ValidateSkills(m.Race.RaceFlag, skills))
        {
            return;
        }

        Span<SkillName> chosen = stackalloc SkillName[skills.Length];
        var count = 0;

        for (var i = 0; i < skills.Length; ++i)
        {
            var (name, value) = skills[i];
            if (value <= 0)
            {
                continue;
            }

            var skill = m.Skills[name];

            if (skill != null)
            {
                skill.BaseFixedPoint = value * 10;
                chosen[count++] = name;
            }
        }

        // Kits depend on the whole selection (e.g. Arms Lore rolls a weapon only with a weapon skill).
        AddStarterSkillItems(m, chosen[..count]);
    }

    private static void GiveProfessionItems(Mobile m, ProfessionInfo profession, int shirtHue, int pantsHue)
    {
        var elf = m.Race == Race.Elf;
        var gargoyle = m.Race == Race.Gargoyle;

        switch (profession?.Name.ToLowerInvariant())
        {
            case "necromancer":
                {
                    Container regs = new BagOfNecroReagents { LootType = LootType.Regular };

                    if (!Core.AOS)
                    {
                        foreach (var item in regs.Items)
                        {
                            item.LootType = LootType.Newbied;
                        }
                    }

                    m.PackItem(regs);

                    EquipItem(m, new BoneHelm());

                    if (elf)
                    {
                        EquipItem(m, new ElvenMachete());
                        EquipItem(m, NecroHue(new LeafChest()));
                        EquipItem(m, NecroHue(new LeafArms()));
                        EquipItem(m, NecroHue(new LeafGloves()));
                        EquipItem(m, NecroHue(new LeafGorget()));
                        EquipItem(m, NecroHue(new LeafLegs()));
                        EquipItem(m, new ElvenBoots());
                    }
                    else if (gargoyle)
                    {
                        EquipItem(m, new GlassSword());
                        EquipItem(m, NecroHue(m.Female ? new GargishLeatherChestType2() : new GargishLeatherChestType1()));
                        EquipItem(m, NecroHue(m.Female ? new GargishLeatherArmsType2() : new GargishLeatherArmsType1()));
                        EquipItem(m, NecroHue(m.Female ? new GargishLeatherKiltType2() : new GargishLeatherKiltType1()));
                        EquipItem(m, NecroHue(m.Female ? new GargishLeatherLegsType2() : new GargishLeatherLegsType1()));
                    }
                    else
                    {
                        EquipItem(m, new BoneHarvester());
                        EquipItem(m, NecroHue(new LeatherChest()));
                        EquipItem(m, NecroHue(new LeatherArms()));
                        EquipItem(m, NecroHue(new LeatherGloves()));
                        EquipItem(m, NecroHue(new LeatherGorget()));
                        EquipItem(m, NecroHue(new LeatherLegs()));
                        EquipItem(m, NecroHue(new Skirt()));
                        EquipItem(m, new Sandals(0x8FD));
                    }

                    // animate dead, evil omen, pain spike, summon familiar, wraith form
                    m.PackItem(new NecromancerSpellbook(0x8981ul));
                    return;
                }
            case "paladin":
                {
                    if (elf)
                    {
                        EquipItem(m, new ElvenMachete());
                        EquipItem(m, new WingedHelm());
                        EquipItem(m, new LeafGorget());
                        EquipItem(m, new LeafArms());
                        EquipItem(m, new LeafChest());
                        EquipItem(m, new LeafLegs());
                        EquipItem(m, new LeafGloves());
                        EquipItem(m, new ElvenBoots()); // Verify hue
                    }
                    else if (gargoyle)
                    {
                        EquipItem(m, new GlassSword());
                        EquipItem(m, m.Female ? new GargishStoneChestType2() : new GargishStoneChestType1());
                        EquipItem(m, m.Female ? new GargishStoneArmsType2() : new GargishStoneArmsType1());
                        EquipItem(m, m.Female ? new GargishStoneKiltType2() : new GargishStoneKiltType1());
                        EquipItem(m, m.Female ? new GargishStoneLegsType2() : new GargishStoneLegsType1());
                    }
                    else
                    {
                        EquipItem(m, new Broadsword());
                        EquipItem(m, new Helmet());
                        EquipItem(m, new PlateGorget());
                        EquipItem(m, new RingmailArms());
                        EquipItem(m, new RingmailChest());
                        EquipItem(m, new RingmailLegs());
                        EquipItem(m, new RingmailGloves());
                        EquipItem(m, new ThighBoots(0x748));
                        EquipItem(m, new Cloak(0xCF));
                        EquipItem(m, new BodySash(0xCF));
                    }

                    m.PackItem(new BookOfChivalry());
                    return;
                }
            case "samurai":
                {
                    if (elf)
                    {
                        EquipItem(m, new RavenHelm());
                        EquipItem(m, new HakamaShita(0x2C3));
                        EquipItem(m, new Hakama(0x2C3));
                        EquipItem(m, new SamuraiTabi(0x2C3));
                        EquipItem(m, new TattsukeHakama(0x22D));
                        EquipItem(m, new Bokuto());
                    }
                    else if (gargoyle)
                    {
                        EquipItem(m, new GargishTalwar());
                        EquipItem(m, m.Female ? new GargishLeatherChestType2() : new GargishLeatherChestType1());
                        EquipItem(m, m.Female ? new GargishLeatherArmsType2() : new GargishLeatherArmsType1());
                        EquipItem(m, m.Female ? new GargishLeatherKiltType2() : new GargishLeatherKiltType1());
                        EquipItem(m, m.Female ? new GargishLeatherLegsType2() : new GargishLeatherLegsType1());
                    }
                    else
                    {
                        EquipItem(m, new LeatherJingasa());
                        EquipItem(m, new HakamaShita(0x2C3));
                        EquipItem(m, new Hakama(0x2C3));
                        EquipItem(m, new SamuraiTabi(0x2C3));
                        EquipItem(m, new TattsukeHakama(0x22D));
                        EquipItem(m, new Bokuto());
                    }

                    m.PackItem(new Scissors());
                    m.PackItem(new Bandage(50));
                    m.PackItem(new BookOfBushido());

                    return;
                }
            case "ninja":
                {
                    ReadOnlySpan<int> hues = [0x1A8, 0xEC, 0x99, 0x90, 0xB5, 0x336, 0x89];
                    // TODO: Verify that's ALL the hues for that above.

                    if (elf)
                    {
                        EquipItem(m, new AssassinSpike());
                        EquipItem(m, new TattsukeHakama(hues.RandomElement()));
                        EquipItem(m, new HakamaShita(0x2C3));
                        EquipItem(m, new NinjaTabi(0x2C3));
                        EquipItem(m, new Kasa());
                    }
                    else if (gargoyle)
                    {
                        EquipItem(m, new DualPointedSpear());
                        EquipItem(m, m.Female ? new GargishLeatherChestType2() : new GargishLeatherChestType1());
                        EquipItem(m, m.Female ? new GargishLeatherArmsType2() : new GargishLeatherArmsType1());
                        EquipItem(m, m.Female ? new GargishLeatherKiltType2() : new GargishLeatherKiltType1());
                        EquipItem(m, m.Female ? new GargishLeatherLegsType2() : new GargishLeatherLegsType1());
                    }
                    else
                    {
                        EquipItem(m, new Tekagi());
                        EquipItem(m, new TattsukeHakama(hues.RandomElement()));
                        EquipItem(m, new HakamaShita(0x2C3));
                        EquipItem(m, new NinjaTabi(0x2C3));
                        EquipItem(m, new Kasa());
                    }

                    m.PackItem(new SmokeBomb());
                    m.PackItem(new SmokeBomb());
                    m.PackItem(new SmokeBomb());
                    m.PackItem(new SmokeBomb());
                    m.PackItem(new SmokeBomb());
                    m.PackItem(new BookOfNinjitsu());

                    return;
                }
            case "swordsman":
            case "fencer":
            case "warrior":
            case "mace fighter":
                {
                    if (elf)
                    {
                        EquipItem(m, new Circlet());
                        EquipItem(m, new HideGorget());
                        EquipItem(m, new HideChest());
                        EquipItem(m, new HidePauldrons());
                        EquipItem(m, new HideGloves());
                        EquipItem(m, new HidePants());
                        EquipItem(m, new ElvenBoots());
                    }
                    else if (gargoyle)
                    {
                        EquipItem(m, m.Female ? new GargishLeatherChestType2() : new GargishLeatherChestType1());
                        EquipItem(m, m.Female ? new GargishLeatherArmsType2() : new GargishLeatherArmsType1());
                        EquipItem(m, m.Female ? new GargishLeatherKiltType2() : new GargishLeatherKiltType1());
                        EquipItem(m, m.Female ? new GargishLeatherLegsType2() : new GargishLeatherLegsType1());
                    }
                    else
                    {
                        EquipItem(m, new Bascinet());
                        EquipItem(m, new StuddedGorget());
                        EquipItem(m, new StuddedChest());
                        EquipItem(m, new StuddedArms());
                        EquipItem(m, new StuddedGloves());
                        EquipItem(m, new StuddedLegs());
                        EquipItem(m, new ThighBoots());
                    }
                    break;
                }
        }

        m.AddShirt(shirtHue);
        m.AddPants(pantsHue);
        m.AddShoes();

        // All elves get a wild staff
        if (elf)
        {
            EquipItem(m, new WildStaff());
        }
    }

    private static void EquipItem(Mobile m, Item item, bool mustEquip = false)
    {
        if (item == null)
        {
            return;
        }

        if (!Core.AOS && item.LootType == LootType.Regular)
        {
            item.LootType = LootType.Newbied;
        }

        if (m?.EquipItem(item) == true)
        {
            return;
        }

        var pack = m?.Backpack;

        if (!mustEquip && pack != null)
        {
            pack.DropItem(item);
        }
        else
        {
            item.Delete();
        }
    }

    private static void PackItem(this Mobile m, Item item)
    {
        if (!Core.AOS && item.LootType == LootType.Regular)
        {
            item.LootType = LootType.Newbied;
        }

        var pack = m.Backpack;

        if (pack != null)
        {
            pack.DropItem(item);
        }
        else
        {
            item.Delete();
        }
    }

    private static void AddShirt(this Mobile m, int shirtHue)
    {
        var hue = Utility.ClipDyedHue(shirtHue & 0x3FFF);
        var raceFlag = m.Race.RaceFlag;

        var shirt = raceFlag switch
        {
            Race.AllowElvesOnly                   => new ElvenShirt(hue),
            Race.AllowGargoylesOnly when m.Female => new GargishClothChestType2 { Hue = hue },
            Race.AllowGargoylesOnly               => new GargishClothChestType1 { Hue = hue },
            // Humans
            _ => (Item)(Utility.Random(3) switch
            {
                0 => new Shirt(hue),
                1 => new FancyShirt(hue),
                _ => new Doublet(hue)
            })
        };

        EquipItem(m, shirt);
    }

    private static void AddPants(this Mobile m, int pantsHue)
    {
        var hue = Utility.ClipDyedHue(pantsHue & 0x3FFF);
        var raceFlag = m.Race.RaceFlag;
        var female = m.Female;

        var pants = raceFlag switch
        {
            Race.AllowElvesOnly                 => new ElvenPants(hue),
            Race.AllowGargoylesOnly when female => new GargishClothLegsType2 { Hue = hue },
            Race.AllowGargoylesOnly             => new GargishClothLegsType1 { Hue = hue },
            // Humans
            _ => (Item)(Utility.RandomBool() switch
            {
                true when female  => new Skirt(hue),
                true              => new LongPants(hue),
                false when female => new Kilt(hue),
                false             => new ShortPants(hue)
            })
        };

        EquipItem(m, pants);
    }

    private static void AddShoes(this Mobile m)
    {
        if (m.Race == Race.Elf)
        {
            EquipItem(m, new ElvenBoots());
        }
        else if (m.Race == Race.Human)
        {
            EquipItem(m, new Shoes(Utility.RandomYellowHue()));
        }
    }

    private static Item NecroHue(Item item)
    {
        item.Hue = 0x2C3;

        return item;
    }

    public static void AddStarterSkillItems(Mobile m, ReadOnlySpan<SkillName> skills)
    {
        for (var i = 0; i < skills.Length; i++)
        {
            m.AddSkillItems(skills[i], skills);
        }
    }

    // Each skill's kit goes into its own bag named after the skill.
    private static void AddSkillItems(this Mobile m, SkillName skill, ReadOnlySpan<SkillName> skills)
    {
        var gargoyle = m.Race == Race.Gargoyle;
        var bag = new Bag { Name = m.Skills[skill].Name };

        switch (skill)
        {
            case SkillName.Alchemy:
                {
                    bag.Drop(new MortarPestle());
                    bag.Drop(new Bottle(15));
                    break;
                }
            case SkillName.Anatomy:
                {
                    bag.Drop(SkillBonusItem(m, skill, 5, 5));
                    bag.Drop(new Bandage(200));
                    break;
                }
            case SkillName.ArmsLore:
                {
                    var item = RandomArmsLoreItem(skills);
                    if (item != null)
                    {
                        LootAffixGenerator.Apply(item, ItemLevel.Min, 0.0);
                        bag.Drop(item);
                    }
                    break;
                }
            case SkillName.Parry:
                {
                    bag.Drop(gargoyle ? new GargishWoodenShield() : new Buckler());
                    break;
                }
            case SkillName.Blacksmith:
                {
                    bag.Drop(new SmithHammer());
                    bag.Drop(new IronIngot(50));
                    break;
                }
            case SkillName.Fletching:
                {
                    bag.Drop(new FletcherTools());
                    bag.Drop(new Board(50));
                    bag.Drop(new Feather(50));
                    break;
                }
            case SkillName.Camping:
                {
                    bag.Drop(new Bedroll());
                    bag.Drop(new Kindling(10));
                    break;
                }
            case SkillName.Carpentry:
                {
                    bag.Drop(new Saw());
                    bag.Drop(new Board(50));
                    break;
                }
            case SkillName.Cartography:
                {
                    bag.Drop(new MapmakersPen());
                    bag.Drop(10, () => new BlankMap());
                    break;
                }
            case SkillName.Cooking:
                {
                    bag.Drop(new Skillet());
                    bag.Drop(new RawBird(3));
                    break;
                }
            case SkillName.Healing:
            case SkillName.Veterinary:
                {
                    bag.Drop(new Scissors());
                    bag.Drop(new Bandage(200));
                    break;
                }
            case SkillName.Fishing:
                {
                    bag.Drop(new FishingPole());
                    bag.Drop(new Fish());
                    bag.Drop(new RawFishSteak(3));
                    break;
                }
            case SkillName.Forensics:
                {
                    bag.Drop(new SkinningKnife());
                    bag.Drop(SkillBonusItem(m, skill, 5, 5));
                    break;
                }
            case SkillName.Herding:
                {
                    bag.Drop(new ShepherdsCrook());
                    break;
                }
            case SkillName.Inscribe:
                {
                    bag.Drop(new ScribesPen());
                    bag.Drop(new BlankScroll(50));
                    break;
                }
            case SkillName.Lockpicking:
                {
                    bag.Drop(new Lockpick(10));
                    break;
                }
            case SkillName.Magery:
                {
                    bag.Drop(new Spellbook());
                    bag.Drop(new HealScroll());
                    bag.Drop(new MagicArrowScroll());
                    bag.Drop(
                        Utility.Random(8) switch
                        {
                            0 => new AgilityScroll(),
                            1 => new CunningScroll(),
                            2 => new CureScroll(),
                            3 => new HarmScroll(),
                            4 => new MagicTrapScroll(),
                            5 => new MagicUnTrapScroll(),
                            6 => new ProtectionScroll(),
                            _ => (Item)new StrengthScroll()
                        }
                    );
                    bag.Drop(new BagOfReagents());
                    break;
                }
            case SkillName.Musicianship:
                {
                    bag.Drop(Loot.RandomInstrument());
                    break;
                }
            case SkillName.Peacemaking:
            case SkillName.Discordance:
            case SkillName.Provocation:
                {
                    bag.Drop(SkillBonusItem(m, skill, 5, 5));

                    if (ShouldPackInstrument(skill, skills))
                    {
                        bag.Drop(Loot.RandomInstrument());
                    }

                    break;
                }
            case SkillName.Poisoning:
                {
                    bag.Drop(5, () => new LesserPoisonPotion());
                    break;
                }
            case SkillName.Archery:
                {
                    bag.Drop(new Bow());
                    bag.Drop(new RepeatingCrossbow());
                    bag.Drop(new Arrow(50));
                    bag.Drop(new Bolt(50));
                    break;
                }
            case SkillName.Tailoring:
                {
                    bag.Drop(new Scissors());
                    bag.Drop(new SewingKit());
                    bag.Drop(new Cloth(50));
                    break;
                }
            case SkillName.AnimalTaming:
            case SkillName.DetectHidden:
            case SkillName.MagicResist:
            case SkillName.Stealing:
                {
                    bag.Drop(SkillBonusItem(m, skill, 5, 5));
                    break;
                }
            case SkillName.AnimalLore:
            case SkillName.Begging:
            case SkillName.EvalInt:
            case SkillName.Focus:
            case SkillName.Hiding:
            case SkillName.ItemID:
            case SkillName.Meditation:
            case SkillName.Snooping:
            case SkillName.SpiritSpeak:
            case SkillName.Stealth:
            case SkillName.Tactics:
            case SkillName.Tracking:
                {
                    bag.Drop(SkillBonusItem(m, skill, 5, 10));
                    break;
                }
            case SkillName.TasteID:
                {
                    bag.Drop(SkillBonusItem(m, skill, 5, 10));
                    bag.Drop(3, Loot.RandomPotion);
                    break;
                }
            case SkillName.Tinkering:
                {
                    bag.Drop(new TinkerTools());
                    bag.Drop(new IronIngot(50));
                    break;
                }
            case SkillName.Swords:
                {
                    bag.Drop(
                        gargoyle
                            ? new DreadSword()
                            : Utility.Random(3) switch
                            {
                                0 => new Bokuto(),
                                1 => new Cleaver(),
                                _ => (Item)new Cutlass()
                            }
                    );
                    break;
                }
            case SkillName.Macing:
                {
                    bag.Drop(
                        gargoyle
                            ? new DiscMace()
                            : Utility.Random(4) switch
                            {
                                0 => new Tessen(),
                                1 => new Club(),
                                2 => new WildStaff(),
                                _ => (Item)new Mace()
                            }
                    );
                    break;
                }
            case SkillName.Fencing:
                {
                    bag.Drop(
                        gargoyle
                            ? new BloodBlade()
                            : Utility.Random(4) switch
                            {
                                0 => new Dagger(),
                                1 => new Kryss(),
                                2 => new AssassinSpike(),
                                _ => (Item)new Sai()
                            }
                    );
                    break;
                }
            case SkillName.Wrestling:
                {
                    bag.Drop(
                        m.Race.RaceFlag switch
                        {
                            Race.AllowElvesOnly                   => new LeafGloves(),
                            Race.AllowGargoylesOnly when m.Female => new GargishLeatherArmsType2(),
                            Race.AllowGargoylesOnly               => new GargishLeatherArmsType1(),
                            _                                     => (Item)new LeatherGloves()
                        }
                    );
                    break;
                }
            case SkillName.Lumberjacking:
                {
                    bag.Drop(2, gargoyle ? () => new DualShortAxes() : () => new Hatchet());
                    break;
                }
            case SkillName.Mining:
                {
                    bag.Drop(2, () => new Pickaxe());
                    break;
                }
            case SkillName.Necromancy:
                {
                    bag.Drop(new NecromancerSpellbook());
                    bag.Drop(new PainSpikeScroll());
                    bag.Drop(new CurseWeaponScroll());
                    bag.Drop(new BagOfNecroReagents());
                    break;
                }
            // Nether Bolt and Healing Stone (1st circle) are not registered, so the scrolls start at 2nd circle.
            case SkillName.Mysticism:
                {
                    bag.Drop(new MysticSpellbook());
                    bag.Drop(new EagleStrikeScroll());
                    bag.Drop(new AnimatedWeaponScroll());
                    bag.Drop(new StoneFormScroll());
                    bag.Drop(new BagOfReagents());
                    bag.Drop(new Bone(30));
                    bag.Drop(new FertileDirt(30));
                    break;
                }
            case SkillName.Chivalry:
                {
                    bag.Drop(new BookOfChivalry());
                    break;
                }
            case SkillName.Bushido:
                {
                    bag.Drop(new BookOfBushido());
                    break;
                }
            case SkillName.Ninjitsu:
                {
                    bag.Drop(new BookOfNinjitsu());
                    break;
                }
            case SkillName.Throwing:
                {
                    if (gargoyle)
                    {
                        bag.Drop(new Boomerang());
                    }

                    break;
                }
        }

        if (bag.Items.Count == 0)
        {
            bag.Delete();
            return;
        }

        m.PackItem(bag);
    }

    private static void Drop(this Container bag, Item item)
    {
        if (item == null)
        {
            return;
        }

        if (!Core.AOS && item.LootType == LootType.Regular)
        {
            item.LootType = LootType.Newbied;
        }

        bag.DropItem(item);
    }

    private static void Drop(this Container bag, int count, Func<Item> factory)
    {
        for (var i = 0; i < count; i++)
        {
            bag.Drop(factory());
        }
    }

    private static bool HasSkill(ReadOnlySpan<SkillName> skills, SkillName skill)
    {
        for (var i = 0; i < skills.Length; i++)
        {
            if (skills[i] == skill)
            {
                return true;
            }
        }

        return false;
    }

    // Musicianship supplies the instrument; without it, only the first bard skill does.
    private static bool ShouldPackInstrument(SkillName skill, ReadOnlySpan<SkillName> skills)
    {
        for (var i = 0; i < skills.Length; i++)
        {
            var s = skills[i];

            if (s == SkillName.Musicianship)
            {
                return false;
            }

            if (s is SkillName.Peacemaking or SkillName.Discordance or SkillName.Provocation)
            {
                return s == skill;
            }
        }

        return false;
    }

    // Weapons and shields only roll when the character has a skill to use them.
    private static Item RandomArmsLoreItem(ReadOnlySpan<SkillName> skills)
    {
        var hasMelee = HasSkill(skills, SkillName.Swords) || HasSkill(skills, SkillName.Macing) ||
                       HasSkill(skills, SkillName.Fencing) || HasSkill(skills, SkillName.Wrestling);

        return Utility.Random(4) switch
        {
            0 when HasSkill(skills, SkillName.Archery) => Loot.RandomRangedWeapon(),
            1 when HasSkill(skills, SkillName.Parry)   => Loot.RandomShield(),
            2 when hasMelee                            => Loot.RandomWeapon(),
            _                                          => Loot.RandomArmor()
        };
    }

    // Gargoyles cannot wear human clothing, so they only roll jewelry.
    private static Item SkillBonusItem(Mobile m, SkillName skill, int min, int max)
    {
        var item = Utility.Random(m.Race == Race.Gargoyle ? 4 : 13) switch
        {
            0  => Utility.RandomBool() ? new GoldRing() : (Item)new SilverRing(),
            1  => Utility.RandomBool() ? new GoldBracelet() : (Item)new SilverBracelet(),
            2  => Utility.RandomBool() ? new GoldNecklace() : (Item)new SilverNecklace(),
            3  => Utility.RandomBool() ? new GoldEarrings() : (Item)new SilverEarrings(),
            4  => Utility.Random(4) switch
            {
                0 => new Sandals(),
                1 => new Shoes(),
                2 => new Boots(),
                _ => (Item)new ThighBoots()
            },
            5  => Utility.Random(3) switch
            {
                0 => new ShortPants(),
                1 => new LongPants(),
                _ => (Item)new TattsukeHakama()
            },
            6  => Utility.RandomBool() ? new Shirt() : (Item)new FancyShirt(),
            7  => Utility.Random(4) switch
            {
                0 => new FeatheredHat(),
                1 => new WideBrimHat(),
                2 => new TricorneHat(),
                _ => (Item)new WizardsHat()
            },
            8  => Utility.RandomBool() ? new HalfApron() : (Item)new Obi(),
            9  => Utility.Random(4) switch
            {
                0 => new BodySash(),
                1 => new Doublet(),
                2 => new JesterSuit(),
                _ => (Item)new Surcoat()
            },
            10 => new Cloak(),
            11 => Utility.Random(3) switch
            {
                0 => new Robe(),
                1 => new FancyDress(),
                _ => (Item)new Kamishimo()
            },
            _ => Utility.Random(3) switch
            {
                0 => new Skirt(),
                1 => new Kilt(),
                _ => (Item)new Hakama()
            }
        };

        var amount = Utility.RandomMinMax(min, max);

        switch (item)
        {
            case BaseClothing clothing:
                {
                    clothing.SkillBonuses.SetValues(0, skill, amount);
                    break;
                }
            case BaseJewel jewel:
                {
                    jewel.SkillBonuses.SetValues(0, skill, amount);
                    break;
                }
        }

        return item;
    }
}
