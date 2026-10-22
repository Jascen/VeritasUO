using System;
using System.Collections.Generic;
using Server.Commands;

namespace Server.Veritas.MeetingSpots;

public static class MeetingSpotsCommand
{
    public static void Configure()
    {
        CommandSystem.Register("MeetingSpots", AccessLevel.GameMaster, OnCommand);
    }

    [Usage("MeetingSpots <status | reroll | clear | regenerate [map]>")]
    [Description("Inspects or resets the town meeting spots and the citizens gathered at them.")]
    private static void OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        switch (e.GetString(0).ToLowerInvariant())
        {
            case "status":
            {
                Status(from);
                break;
            }
            case "reroll":
            {
                var spots = MeetingSpotSystem.Spots;

                for (var i = 0; i < spots.Count; i++)
                {
                    spots[i].NextReroll = Core.Now;
                }

                from.SendMessage($"{spots.Count} meeting spots will reroll shortly (spots a player can see wait until they leave).");
                break;
            }
            case "clear":
            {
                var spots = MeetingSpotSystem.Spots;

                for (var i = 0; i < spots.Count; i++)
                {
                    spots[i].DisbandGroup();
                }

                from.SendMessage($"Cleared the citizens at {spots.Count} meeting spots.");
                break;
            }
            case "regenerate":
            {
                Regenerate(from, e.Length > 1 ? e.GetString(1) : null);
                break;
            }
            default:
            {
                from.SendMessage("Usage: MeetingSpots <status | reroll | clear | regenerate [map]>");
                break;
            }
        }
    }

    private static void Status(Mobile from)
    {
        foreach (var map in MeetingSpotSystem.Facets)
        {
            if (map == null)
            {
                continue;
            }

            var spots = 0;
            var citizens = 0;

            foreach (var spot in MeetingSpotSystem.Spots)
            {
                if (spot.Map == map)
                {
                    spots++;
                    citizens += spot.Population;
                }
            }

            var scanning = MeetingSpotFinder.IsBusy(map) ? " (detection running)" : "";
            from.SendMessage($"{map}: {spots} spots, {citizens} citizens{scanning}");
        }

        from.SendMessage(MeetingSpotSystem.Enabled ? "Meeting spots are enabled." : "Meeting spots are disabled.");
    }

    private static void Regenerate(Mobile from, string mapName)
    {
        var map = from.Map;

        if (mapName != null && !Map.TryParse(mapName, null, out map))
        {
            from.SendMessage($"Unknown map '{mapName}'.");
            return;
        }

        if (map == null || map == Map.Internal || Array.IndexOf(MeetingSpotSystem.Facets, map) < 0)
        {
            from.SendMessage("Meeting spots are only detected on Felucca and Trammel.");
            return;
        }

        if (MeetingSpotFinder.IsBusy(map))
        {
            from.SendMessage($"Detection is already running on {map}.");
            return;
        }

        var doomed = new List<MeetingSpot>();

        foreach (var spot in MeetingSpotSystem.Spots)
        {
            if (spot.Map == map)
            {
                doomed.Add(spot);
            }
        }

        for (var i = 0; i < doomed.Count; i++)
        {
            doomed[i].Delete();
        }

        MeetingSpotFinder.Start(map);
        from.SendMessage($"Deleted {doomed.Count} meeting spots on {map}; detection started. Progress is logged to the console.");
    }
}
