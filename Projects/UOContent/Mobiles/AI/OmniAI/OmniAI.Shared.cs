using System;
using Server.Items;
using Server.Spells;
using Server.Spells.Chivalry;
using Server.Spells.Fifth;
using Server.Spells.First;
using Server.Spells.Fourth;
using Server.Spells.Second;
using Server.Spells.Sixth;

namespace Server.Mobiles;

public partial class OmniAI
{
    private const int HealCooldownMs = 20000;
    private const int SpiritSpeakCooldownMs = 10000;
    private const int WeaponSwapCooldownMs = 15000;

    private long _nextWeaponSwap;

    /// <summary>
    ///     Rolls a heal with a chance equal to the percentage of hit points missing.
    ///     Returns true when a heal was started, which ends this combat tick.
    /// </summary>
    public bool TryToHeal()
    {
        if (Mobile.Summoned || Mobile.Hits > Mobile.HitsMax - 10 || Core.TickCount - _nextHealTime < 0)
        {
            return false;
        }

        var missingPercent = 100 - Mobile.Hits * 100 / Math.Max(1, Mobile.HitsMax);

        if (Utility.Random(100) >= missingPercent)
        {
            return false;
        }

        _nextHealTime = Core.TickCount + HealCooldownMs;

        Spell spell = null;

        if (CanUseMagery)
        {
            if (Mobile.Poisoned)
            {
                spell = new CureSpell(Mobile);
            }
            else if (Mobile.Hits < Mobile.HitsMax - 50)
            {
                spell = new GreaterHealSpell(Mobile);
            }
            else
            {
                spell = new HealSpell(Mobile);
            }
        }
        else if (CanUseNecromancy)
        {
            _nextHealTime = Core.TickCount + SpiritSpeakCooldownMs;
            return Mobile.UseSkill(SkillName.SpiritSpeak);
        }
        else if (CanUseChivalry)
        {
            spell = Mobile.Poisoned ? new CleanseByFireSpell(Mobile) : new CloseWoundsSpell(Mobile);
        }
        else if (Mobile.Skills.Healing.Value > 10.0)
        {
            var context = BandageContext.BeginHeal(Mobile, Mobile);

            if (context == null)
            {
                return false;
            }

            _nextHealTime = Core.TickCount + (long)context.Duration.TotalMilliseconds + 1000;
            return true;
        }

        return spell?.Cast() == true;
    }

    /// <summary>
    ///     Equips a melee weapon from the pack when unarmed. With <paramref name="swap"/>, stows the
    ///     current weapon first and draws a random one, so Bushido and Ninjitsu users rotate weapons.
    /// </summary>
    public void CheckArmed(bool swap)
    {
        if (!SwapWeapons || Core.TickCount - _nextWeaponSwap < 0)
        {
            return;
        }

        var pack = Mobile.Backpack;

        if (pack == null)
        {
            Mobile.EquipItem(new Backpack());
            pack = Mobile.Backpack;

            if (pack == null)
            {
                return;
            }
        }

        // Mobile.Weapon falls back to Fists, so read the hand layers directly.
        if (GetEquippedWeapon() is { } weapon)
        {
            if (!swap)
            {
                return;
            }

            pack.DropItem(weapon);
        }

        DebugSay("Searching my pack for a weapon");

        BaseMeleeWeapon pick = null;
        var seen = 0;

        foreach (var candidate in pack.FindItemsByType<BaseMeleeWeapon>())
        {
            if (Utility.Random(++seen) == 0)
            {
                pick = candidate;
            }
        }

        if (pick != null)
        {
            Mobile.EquipItem(pick);
        }

        _nextWeaponSwap = Core.TickCount + WeaponSwapCooldownMs;
    }

    private BaseWeapon GetEquippedWeapon() =>
        Mobile.FindItemOnLayer(Layer.OneHanded) as BaseWeapon ?? Mobile.FindItemOnLayer(Layer.TwoHanded) as BaseWeapon;

    public void UseWeaponStrike()
    {
        if (GetEquippedWeapon() is not { } weapon)
        {
            return;
        }

        DebugSay("Picking a weapon move");

        var skill = Mobile.Skills[weapon.Skill].Value;

        if (Utility.RandomBool() && skill >= 90.0)
        {
            WeaponAbility.SetCurrentAbility(Mobile, weapon.PrimaryAbility);
        }
        else if (skill >= 60.0)
        {
            WeaponAbility.SetCurrentAbility(Mobile, weapon.SecondaryAbility);
        }
    }

    /// <summary>
    ///     Steps off a poison, paralyze or fire field. Returns true while still standing in one.
    /// </summary>
    public bool CheckForFieldSpells()
    {
        var map = Mobile.Map;

        if (map == null || map == Map.Internal || Mobile.DisallowAllMoves)
        {
            return false;
        }

        var inField = false;

        foreach (var item in map.GetItemsAt(Mobile.Location))
        {
            if (item.Z == Mobile.Z && IsFieldSpell(item))
            {
                inField = true;
                break;
            }
        }

        if (!inField)
        {
            return false;
        }

        DebugSay("I am standing in a field, moving out");

        return !DoMove((Direction)Utility.Random(8));
    }

    public static bool IsFieldSpell(Item item) => item is FireFieldItem or PoisonField or ParalyzeField;
}
