using System;

namespace Server.Commands;

public static class UseSkillCommand
{
    public static void Configure()
    {
        CommandSystem.Register("UseSkill", AccessLevel.Player, UseSkill_OnCommand);
    }

    // The client only offers a use button for skills it considers active, so skills made usable
    // server-side (e.g. Dominance/Herding) need another way in.
    [Usage("UseSkill <skill name>")]
    [Description("Uses a skill by name, e.g. [UseSkill Dominance or [UseSkill animal taming.")]
    private static void UseSkill_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (e.Length == 0)
        {
            from.SendMessage("Usage: [UseSkill <skill name>");
            return;
        }

        if (!TryFindSkill(e.ArgString, out var skill))
        {
            from.SendMessage("Invalid skill name.");
            return;
        }

        from.UseSkill(skill);
    }

    // Matches the enum name or the display name (skills.json), ignoring case and spaces.
    private static bool TryFindSkill(string input, out SkillName skill)
    {
        var name = input.Replace(" ", "");

        if (Enum.TryParse(name, true, out skill) && Enum.IsDefined(skill))
        {
            return true;
        }

        var table = SkillInfo.Table;

        for (var i = 0; i < table.Length; i++)
        {
            if (string.Equals(table[i].Name.Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase))
            {
                skill = (SkillName)table[i].SkillID;
                return true;
            }
        }

        skill = default;
        return false;
    }
}
