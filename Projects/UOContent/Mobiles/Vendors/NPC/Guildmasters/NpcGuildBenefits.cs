using System;
using System.Linq;

namespace Server.Mobiles;

/// <summary>
/// Per-guild perks for NPC guild members: the skills a guild trains faster and the
/// bonuses its <see cref="Server.Items.GuildRing"/> grants.
/// </summary>
public static class NpcGuildBenefits
{
    // Indexed by guild for an allocation-free lookup on the skill gain path.
    private static readonly (SkillName Skill, double Bonus)[][] _skillBonuses = BuildSkillBonuses();

    private static (SkillName Skill, double Bonus)[][] BuildSkillBonuses()
    {
        var guilds = Enum.GetValues<NpcGuild>();
        var table = new (SkillName Skill, double Bonus)[(int)guilds.Max() + 1][];

        foreach (var guild in guilds)
        {
            table[(int)guild] = DefineSkillBonuses(guild);
        }

        return table;
    }

    private static (SkillName Skill, double Bonus)[] DefineSkillBonuses(NpcGuild guild) =>
        guild switch
        {
            NpcGuild.MagesGuild => [(SkillName.EvalInt, 10), (SkillName.Magery, 10), (SkillName.Meditation, 10)],
            NpcGuild.WarriorsGuild =>
            [
                (SkillName.Fencing, 10), (SkillName.Macing, 10), (SkillName.Parry, 10), (SkillName.Swords, 10),
                (SkillName.Tactics, 10)
            ],
            NpcGuild.ThievesGuild =>
            [
                (SkillName.Hiding, 10), (SkillName.Lockpicking, 10), (SkillName.Snooping, 10),
                (SkillName.Stealing, 10), (SkillName.Stealth, 10)
            ],
            NpcGuild.RangersGuild => [(SkillName.Camping, 25), (SkillName.Tracking, 25)],
            NpcGuild.HealersGuild => [(SkillName.Anatomy, 15), (SkillName.Healing, 15), (SkillName.Veterinary, 15)],
            NpcGuild.MinersGuild => [(SkillName.Mining, 30)],
            NpcGuild.MerchantsGuild => [(SkillName.ItemID, 15), (SkillName.ArmsLore, 15), (SkillName.TasteID, 15)],
            NpcGuild.TinkersGuild => [(SkillName.Tinkering, 30)],
            NpcGuild.TailorsGuild => [(SkillName.Tailoring, 30)],
            NpcGuild.FishermensGuild => [(SkillName.Fishing, 30)],
            NpcGuild.BardsGuild =>
            [
                (SkillName.Discordance, 10), (SkillName.Musicianship, 10), (SkillName.Peacemaking, 10),
                (SkillName.Provocation, 10)
            ],
            NpcGuild.BlacksmithsGuild => [(SkillName.Blacksmith, 20), (SkillName.ArmsLore, 20)],
            NpcGuild.NecromancersGuild =>
                [(SkillName.Forensics, 10), (SkillName.Necromancy, 10), (SkillName.SpiritSpeak, 10)],
            NpcGuild.AlchemistsGuild => [(SkillName.Alchemy, 15), (SkillName.Cooking, 15), (SkillName.TasteID, 15)],
            NpcGuild.DruidsGuild =>
            [
                (SkillName.AnimalLore, 10), (SkillName.AnimalTaming, 10), (SkillName.Herding, 10),
                (SkillName.Veterinary, 10), (SkillName.Cooking, 5)
            ],
            NpcGuild.ArchersGuild => [(SkillName.Archery, 10), (SkillName.Fletching, 20), (SkillName.Tactics, 10)],
            NpcGuild.CarpentersGuild => [(SkillName.Carpentry, 20), (SkillName.Lumberjacking, 20)],
            NpcGuild.CartographersGuild => [(SkillName.Cartography, 30)],
            NpcGuild.LibrariansGuild => [(SkillName.ItemID, 20), (SkillName.Inscribe, 20)],
            NpcGuild.CulinariansGuild => [(SkillName.Cooking, 20), (SkillName.TasteID, 20)],
            NpcGuild.AssassinsGuild =>
            [
                (SkillName.Fencing, 10), (SkillName.Hiding, 10), (SkillName.Poisoning, 15), (SkillName.Stealth, 10)
            ],
            _ => []
        };

    public static ReadOnlySpan<(SkillName Skill, double Bonus)> GetSkillBonuses(NpcGuild guild) =>
        (uint)guild < (uint)_skillBonuses.Length ? _skillBonuses[(int)guild] : [];

    public static bool IsGuildSkill(NpcGuild guild, SkillName skill)
    {
        foreach (var (guildSkill, _) in GetSkillBonuses(guild))
        {
            if (guildSkill == skill)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Divisor for <see cref="Server.Misc.SkillCheck"/>'s gain chance on a guild skill, rolled per check.
    /// Non-guild skills use 2.0; members roll 1.0-1.5 in 0.1 steps, matching Memento.
    /// </summary>
    public static double RollGuildGainDivisor() => 1.0 + Utility.Random(6) * 0.1;

    public static string GetName(NpcGuild guild) =>
        guild switch
        {
            NpcGuild.MagesGuild         => "Mages Guild",
            NpcGuild.WarriorsGuild      => "Warriors Guild",
            NpcGuild.ThievesGuild       => "Thieves Guild",
            NpcGuild.RangersGuild       => "Rangers Guild",
            NpcGuild.HealersGuild       => "Healers Guild",
            NpcGuild.MinersGuild        => "Miners Guild",
            NpcGuild.MerchantsGuild     => "Merchants Guild",
            NpcGuild.TinkersGuild       => "Tinkers Guild",
            NpcGuild.TailorsGuild       => "Tailors Guild",
            NpcGuild.FishermensGuild    => "Fishermen's Guild",
            NpcGuild.BardsGuild         => "Bards Guild",
            NpcGuild.BlacksmithsGuild   => "Blacksmiths Guild",
            NpcGuild.NecromancersGuild  => "Necromancers Guild",
            NpcGuild.AlchemistsGuild    => "Alchemists Guild",
            NpcGuild.DruidsGuild        => "Druids Guild",
            NpcGuild.ArchersGuild       => "Archers Guild",
            NpcGuild.CarpentersGuild    => "Carpenters Guild",
            NpcGuild.CartographersGuild => "Cartographers Guild",
            NpcGuild.LibrariansGuild    => "Librarians Guild",
            NpcGuild.CulinariansGuild   => "Culinarians Guild",
            NpcGuild.AssassinsGuild     => "Assassins Guild",
            _                           => "no guild"
        };
}
