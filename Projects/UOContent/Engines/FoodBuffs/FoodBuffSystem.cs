using System;
using System.Collections.Generic;
using Server.Engines.BuffIcons;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.FoodBuffs;

/// <summary>
/// Timed "Well Fed" bonuses from crafted food and drink. One meal and one drink buff per player.
/// Bonuses feed <see cref="AosAttributes.GetValue"/> and <see cref="AosWeaponAttributes.GetValue"/>,
/// so every system that already reads item attributes (casting, combat, regen caps) honors them.
/// Active buffs live in memory only; a server restart clears them.
/// </summary>
public static class FoodBuffSystem
{
    // A meal stops helping once its eater is hungry again; same for a drink and thirst.
    public const int MinFullness = 5;

    public const int HangoverPeakBac = 20;
    public const int HangoverStamRegen = -2;
    public static readonly TimeSpan HangoverDuration = TimeSpan.FromMinutes(5);

    // ~1_NOTHING~ renders the args verbatim, letting the buff bar show our own text.
    private const int GenericCliloc = 1042971;

    private static readonly string[] _strModNames = ["FoodBuff.Meal.Str", "FoodBuff.Drink.Str"];
    private static readonly string[] _dexModNames = ["FoodBuff.Meal.Dex", "FoodBuff.Drink.Dex"];
    private static readonly string[] _intModNames = ["FoodBuff.Meal.Int", "FoodBuff.Drink.Int"];

    private static readonly Dictionary<Mobile, FoodBuffContext> _contexts = new();

    public static int GetBonus(Mobile m, AosAttribute attribute)
    {
        if (_contexts.Count == 0 || !_contexts.TryGetValue(m, out var context))
        {
            return 0;
        }

        var value = 0;

        if (context.Meal != null && m.Hunger >= MinFullness)
        {
            value += context.Meal.Get(attribute);
        }

        if (context.Drink != null && m.Thirst >= MinFullness)
        {
            value += context.Drink.Get(attribute);
        }

        if (context.Hangover && attribute == AosAttribute.RegenStam)
        {
            value += HangoverStamRegen;
        }

        return value;
    }

    public static int GetBonus(Mobile m, AosWeaponAttribute attribute)
    {
        if (_contexts.Count == 0 || !_contexts.TryGetValue(m, out var context))
        {
            return 0;
        }

        var value = 0;

        if (context.Meal != null && m.Hunger >= MinFullness)
        {
            value += context.Meal.Get(attribute);
        }

        if (context.Drink != null && m.Thirst >= MinFullness)
        {
            value += context.Drink.Get(attribute);
        }

        return value;
    }

    public static bool HasBuff(Mobile m, FoodBuffSlot slot) =>
        _contexts.TryGetValue(m, out var context) && context.Get(slot) != null;

    public static bool HasHangover(Mobile m) => _contexts.TryGetValue(m, out var context) && context.Hangover;

    /// <summary>
    /// Grants the profile's bonuses, replacing whatever occupied the same slot.
    /// </summary>
    public static void Apply(Mobile m, FoodBuffProfile profile, FoodQuality quality)
    {
        if (m is not PlayerMobile pm || profile == null)
        {
            return;
        }

        var slot = profile.Slot;
        var context = GetOrCreateContext(pm);

        RemoveSlot(pm, context, slot);

        var buff = new ActiveFoodBuff(profile, quality);
        context.Set(slot, buff);

        var index = (int)slot;
        AddStatMod(pm, StatType.Str, _strModNames[index], FoodBuffProfile.Scale(profile.Str, quality), profile.Duration);
        AddStatMod(pm, StatType.Dex, _dexModNames[index], FoodBuffProfile.Scale(profile.Dex, quality), profile.Duration);
        AddStatMod(pm, StatType.Int, _intModNames[index], FoodBuffProfile.Scale(profile.Int, quality), profile.Duration);

        Timer.StartTimer(profile.Duration, () => Expire(pm, slot, buff), out buff.TimerToken);

        var slotName = slot == FoodBuffSlot.Meal ? "Well Fed" : "Refreshed";
        pm.AddBuff(
            new BuffInfo(
                GetIcon(slot),
                GenericCliloc,
                GenericCliloc,
                profile.Duration,
                $"{slotName}: {profile.GetDescription(quality)}",
                true
            )
        );

        pm.Delta(MobileDelta.WeaponDamage);
    }

    /// <summary>
    /// Records the highest blood alcohol reached in this bout of drinking, for <see cref="OnSober"/>.
    /// </summary>
    public static void OnAlcoholConsumed(Mobile m)
    {
        if (m is not PlayerMobile pm || m.BAC <= 0)
        {
            return;
        }

        var context = GetOrCreateContext(pm);
        context.PeakBac = Math.Max(context.PeakBac, m.BAC);
    }

    /// <summary>
    /// Called once a drinker's blood alcohol returns to zero. Heavy drinking leaves a hangover.
    /// </summary>
    public static void OnSober(Mobile m)
    {
        if (m is not PlayerMobile pm || !_contexts.TryGetValue(pm, out var context))
        {
            return;
        }

        var peak = context.PeakBac;
        context.PeakBac = 0;

        if (peak < HangoverPeakBac || !pm.Alive)
        {
            CleanupContext(pm, context);
            return;
        }

        context.HangoverToken.Cancel();
        context.Hangover = true;
        Timer.StartTimer(HangoverDuration, () => EndHangover(pm), out context.HangoverToken);

        pm.SendMessage(0x22, "Your head pounds. You have a hangover.");
        pm.AddBuff(
            new BuffInfo(
                BuffIcon.AuraOfNausea,
                GenericCliloc,
                GenericCliloc,
                HangoverDuration,
                $"Hangover: {HangoverStamRegen} Stamina Regeneration",
                true
            )
        );
    }

    public static void Clear(Mobile m)
    {
        if (m is not PlayerMobile pm || !_contexts.TryGetValue(pm, out var context))
        {
            return;
        }

        RemoveSlot(pm, context, FoodBuffSlot.Meal);
        RemoveSlot(pm, context, FoodBuffSlot.Drink);
        context.HangoverToken.Cancel();
        context.Hangover = false;
        context.PeakBac = 0;
        pm.RemoveBuff(BuffIcon.AuraOfNausea);
        _contexts.Remove(pm);
    }

    private static BuffIcon GetIcon(FoodBuffSlot slot) =>
        slot == FoodBuffSlot.Meal ? BuffIcon.FishPie : BuffIcon.BarakoDraftOfMight;

    private static void AddStatMod(Mobile m, StatType type, string name, int offset, TimeSpan duration)
    {
        if (offset != 0)
        {
            m.AddStatMod(new StatMod(type, name, offset, duration));
        }
    }

    private static FoodBuffContext GetOrCreateContext(PlayerMobile pm)
    {
        if (!_contexts.TryGetValue(pm, out var context))
        {
            context = new FoodBuffContext();
            _contexts[pm] = context;
        }

        return context;
    }

    private static void RemoveSlot(PlayerMobile pm, FoodBuffContext context, FoodBuffSlot slot)
    {
        var buff = context.Get(slot);

        if (buff == null)
        {
            return;
        }

        buff.TimerToken.Cancel();
        context.Set(slot, null);

        var index = (int)slot;
        pm.RemoveStatMod(_strModNames[index]);
        pm.RemoveStatMod(_dexModNames[index]);
        pm.RemoveStatMod(_intModNames[index]);
        pm.RemoveBuff(GetIcon(slot));

        pm.Delta(MobileDelta.WeaponDamage);
    }

    private static void Expire(PlayerMobile pm, FoodBuffSlot slot, ActiveFoodBuff buff)
    {
        if (!_contexts.TryGetValue(pm, out var context) || context.Get(slot) != buff)
        {
            return;
        }

        RemoveSlot(pm, context, slot);

        if (!pm.Deleted && pm.NetState != null)
        {
            pm.SendMessage(slot == FoodBuffSlot.Meal ? "You no longer feel well fed." : "You no longer feel refreshed.");
        }

        CleanupContext(pm, context);
    }

    private static void EndHangover(PlayerMobile pm)
    {
        if (!_contexts.TryGetValue(pm, out var context))
        {
            return;
        }

        context.Hangover = false;
        pm.RemoveBuff(BuffIcon.AuraOfNausea);

        if (!pm.Deleted && pm.NetState != null)
        {
            pm.SendMessage("Your hangover fades.");
        }

        CleanupContext(pm, context);
    }

    private static void CleanupContext(PlayerMobile pm, FoodBuffContext context)
    {
        if (pm.Deleted || context.IsEmpty)
        {
            context.HangoverToken.Cancel();
            context.Meal?.TimerToken.Cancel();
            context.Drink?.TimerToken.Cancel();
            _contexts.Remove(pm);
        }
    }

    private sealed class FoodBuffContext
    {
        public ActiveFoodBuff Meal;
        public ActiveFoodBuff Drink;
        public bool Hangover;
        public int PeakBac;
        public TimerExecutionToken HangoverToken;

        public bool IsEmpty => Meal == null && Drink == null && !Hangover && PeakBac == 0;

        public ActiveFoodBuff Get(FoodBuffSlot slot) => slot == FoodBuffSlot.Meal ? Meal : Drink;

        public void Set(FoodBuffSlot slot, ActiveFoodBuff buff)
        {
            if (slot == FoodBuffSlot.Meal)
            {
                Meal = buff;
            }
            else
            {
                Drink = buff;
            }
        }
    }

    private sealed class ActiveFoodBuff
    {
        private readonly (AosAttribute Attribute, int Value)[] _attributes;
        private readonly (AosWeaponAttribute Attribute, int Value)[] _weaponAttributes;

        public TimerExecutionToken TimerToken;

        public ActiveFoodBuff(FoodBuffProfile profile, FoodQuality quality)
        {
            _attributes = new (AosAttribute, int)[profile.Attributes.Length];

            for (var i = 0; i < _attributes.Length; i++)
            {
                var (attr, value) = profile.Attributes[i];
                _attributes[i] = (attr, FoodBuffProfile.Scale(value, quality));
            }

            _weaponAttributes = new (AosWeaponAttribute, int)[profile.WeaponAttributes.Length];

            for (var i = 0; i < _weaponAttributes.Length; i++)
            {
                var (attr, value) = profile.WeaponAttributes[i];
                _weaponAttributes[i] = (attr, FoodBuffProfile.Scale(value, quality));
            }
        }

        public int Get(AosAttribute attribute)
        {
            var value = 0;

            for (var i = 0; i < _attributes.Length; i++)
            {
                if (_attributes[i].Attribute == attribute)
                {
                    value += _attributes[i].Value;
                }
            }

            return value;
        }

        public int Get(AosWeaponAttribute attribute)
        {
            var value = 0;

            for (var i = 0; i < _weaponAttributes.Length; i++)
            {
                if (_weaponAttributes[i].Attribute == attribute)
                {
                    value += _weaponAttributes[i].Value;
                }
            }

            return value;
        }
    }
}
