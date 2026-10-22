using System;
using System.Numerics;

namespace Server.Items;

/// <summary>
/// Applies tome page bonuses as skill mods that ignore the skill cap. Pages of the same skill across
/// several tomes in one backpack are unioned, so duplicates never stack past 20.
/// </summary>
public static class SkillTomeBonus
{
    private static string[] _modNames;

    private static string[] ModNames
    {
        get
        {
            if (_modNames == null)
            {
                var table = SkillInfo.Table;
                _modNames = new string[table.Length];

                for (var i = 0; i < table.Length; i++)
                {
                    _modNames[i] = $"SkillTome:{table[i].Name}";
                }
            }

            return _modNames;
        }
    }

    /// <summary>The mobile whose backpack is <paramref name="parent"/>, or null if it is not a backpack.</summary>
    public static Mobile GetBackpackOwner(IEntity parent) =>
        parent is Container { Parent: Mobile m } pack && m.Backpack == pack ? m : null;

    public static void Refresh(Mobile m)
    {
        if (m?.Deleted != false)
        {
            return;
        }

        var names = ModNames;
        Span<int> masks = stackalloc int[names.Length];
        masks.Clear();

        var pack = m.Backpack;

        if (pack != null)
        {
            var items = pack.Items;

            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is SkillTome { Deleted: false, IsBound: true } tome && (int)tome.Skill < masks.Length)
                {
                    masks[(int)tome.Skill] |= tome.Pages;
                }
            }
        }

        for (var i = 0; i < names.Length; i++)
        {
            var bonus = BitOperations.PopCount((uint)masks[i]);
            var mod = m.GetSkillMod(names[i]);

            if (bonus == 0)
            {
                if (mod != null)
                {
                    m.RemoveSkillMod(mod);
                }
            }
            else if (mod == null)
            {
                m.AddSkillMod(new DefaultSkillMod((SkillName)i, names[i], true, bonus) { ObeyCap = false });
            }
            else if (mod.Value != bonus)
            {
                mod.Value = bonus;
            }
        }
    }
}
