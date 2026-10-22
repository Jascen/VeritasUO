using System.Collections.Generic;
using Server.Commands;
using Server.Spells;

namespace Server.Veritas.Instancing;

public static class DungeonInstanceCommands
{
    public static void Configure()
    {
        CommandSystem.Register("LeaveDungeon", AccessLevel.Player, LeaveDungeon_OnCommand);
        CommandSystem.Register("DungeonInstance", AccessLevel.GameMaster, DungeonInstance_OnCommand);
    }

    [Usage("LeaveDungeon")]
    [Description("Leaves the dungeon instance you are in and returns you to where you entered it.")]
    public static void LeaveDungeon_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var type = InstanceManager.FindType(from.Map);

        if (type == null)
        {
            from.SendMessage("You are not in a dungeon instance.");
            return;
        }

        if (SpellHelper.CheckCombat(from))
        {
            from.SendLocalizedMessage(1005564, "", 0x22); // Wouldst thou flee during the heat of battle??
            return;
        }

        type.Eject(from, type.GetLiveInstance(from.Map));
    }

    [Usage("DungeonInstance list|status|enter <name>|clear")]
    [Description("Lists instanceable dungeons, shows live instances, enters an instance, or frees every instance.")]
    public static void DungeonInstance_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var action = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

        switch (action)
        {
            case "list":
                {
                    var all = DungeonDefinition.All;
                    for (var i = 0; i < all.Count; i++)
                    {
                        from.SendMessage($"{all[i].Name} ({all[i].Map})");
                    }

                    from.SendMessage($"{all.Count} instanceable dungeons.");
                    break;
                }
            case "status":
                {
                    var types = InstanceManager.Types;
                    for (var i = 0; i < types.Count; i++)
                    {
                        var type = types[i];
                        var live = 0;
                        foreach (var inst in type.GetLiveInstances())
                        {
                            live++;
                            var idle = (int)(Core.Now - inst.LastTouched).TotalMinutes;
                            from.SendMessage(
                                $"{inst.LiveMap}: {inst.Definition?.Name} owner {inst.OwnerKey}, {inst.Visitors.Count} visitors, idle {idle}m"
                            );
                        }

                        from.SendMessage($"{type.Key}: {live} of {type.PoolSize} pool maps live.");
                    }

                    break;
                }
            case "enter":
                {
                    var name = e.Length > 1 ? string.Join(" ", e.Arguments, 1, e.Length - 1) : null;
                    var definition = DungeonDefinition.Find(name, InstanceManager.GetBaseMap(from.Map));

                    if (definition == null)
                    {
                        from.SendMessage("No instanceable dungeon has that name. Use [DungeonInstance list.");
                        break;
                    }

                    DungeonInstanceType.Instance.Enter(from, definition);
                    break;
                }
            case "clear":
                {
                    var freed = 0;
                    var types = InstanceManager.Types;
                    for (var i = 0; i < types.Count; i++)
                    {
                        var type = types[i];
                        foreach (var inst in new List<Instance>(type.GetLiveInstances()))
                        {
                            type.FreeInstance(inst);
                            freed++;
                        }
                    }

                    from.SendMessage($"Freed {freed} instances.");
                    break;
                }
            default:
                {
                    from.SendMessage("Usage: [DungeonInstance list|status|enter <name>|clear");
                    break;
                }
        }
    }
}
