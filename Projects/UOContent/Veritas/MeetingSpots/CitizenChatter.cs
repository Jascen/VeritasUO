using System.Collections.Generic;
using Server.Regions;

namespace Server.Veritas.MeetingSpots;

/// <summary>
/// What <see cref="TownCitizen"/>s say. Each line calls <c>Say($"...")</c> directly so the interpolation
/// handler writes straight into the packet without building a string.
/// </summary>
public static class CitizenChatter
{
    public const int SpeakLines = 40;
    public const int ReplyLines = 10;
    public const int TellRumorLines = 15;
    public const int BrushOffLines = 4;

    private static readonly Dictionary<Map, string[]> _townNames = [];

    /// <summary>Ambient gossip, said when a player passes by.</summary>
    public static void Speak(Mobile m) => Speak(m, Utility.Random(SpeakLines));

    internal static void Speak(Mobile m, int line)
    {
        switch (line)
        {
            case 0:
            {
                m.Say($"Have you seen {Person()} lately? Still owes me {Coin()} gold.");
                break;
            }
            case 1:
            {
                m.Say($"The {Trade()} raised prices again. Robbery, I tell you.");
                break;
            }
            case 2:
            {
                m.Say($"I hear {Beast()} have been sighted on the road to {Town(m)}.");
                break;
            }
            case 3:
            {
                m.Say($"My cousin moved to {Town(m)}. Says the ale is better there.");
                break;
            }
            case 4:
            {
                m.Say("Mind your purse in the crowd today.");
                break;
            }
            case 5:
            {
                m.Say($"{Person()} swears there is treasure buried near {Dungeon()}.");
                break;
            }
            case 6:
            {
                m.Say("Rain's coming. I can feel it in my knees.");
                break;
            }
            case 7:
            {
                m.Say($"Did you hear? {Person()} and {Person()} are to be wed!");
                break;
            }
            case 8:
            {
                m.Say("The guards were in a foul mood this morning.");
                break;
            }
            case 9:
            {
                m.Say($"I'd not go near {Dungeon()} for all the gold in Britannia.");
                break;
            }
            case 10:
            {
                m.Say($"The {Trade()}'s apprentice set half the workshop alight last week.");
                break;
            }
            case 11:
            {
                m.Say("These boots were not made for cobblestones.");
                break;
            }
            case 12:
            {
                m.Say($"A ship from {Town(m)} came in with a hold full of strange cargo.");
                break;
            }
            case 13:
            {
                m.Say("Is it just me, or have the moongates been flickering lately?");
                break;
            }
            case 14:
            {
                m.Say($"{Person()} lost a small fortune at dice last night.");
                break;
            }
            case 15:
            {
                m.Say("Lord British ought to hear about the state of these roads.");
                break;
            }
            case 16:
            {
                m.Say($"I sold a sword to an adventurer bound for {Dungeon()}. Never saw them again.");
                break;
            }
            case 17:
            {
                m.Say("Have you tried the stew at the inn? Don't.");
                break;
            }
            case 18:
            {
                m.Say($"They say the {Trade()} keeps a dragon's tooth under the counter.");
                break;
            }
            case 19:
            {
                m.Say("Another quiet day. I don't trust quiet days.");
                break;
            }
            case 20:
            {
                m.Say($"My brother went hunting {Beast()} and came back with half a beard.");
                break;
            }
            case 21:
            {
                m.Say($"Prices in {Town(m)} are half what they are here, I'd wager.");
                break;
            }
            case 22:
            {
                m.Say("I wish the town crier would find some better news.");
                break;
            }
            case 23:
            {
                m.Say($"{Person()} says the healers are charging double this season.");
                break;
            }
            case 24:
            {
                m.Say("Watch where you step; the horses have been busy.");
                break;
            }
            case 25:
            {
                m.Say($"Someone saw lights deep in {Dungeon()} last night.");
                break;
            }
            case 26:
            {
                m.Say("I'm saving for a little house by the sea. Someday.");
                break;
            }
            case 27:
            {
                m.Say($"The road past {Town(m)} is crawling with brigands.");
                break;
            }
            case 28:
            {
                m.Say($"Old {Person()} claims to have fought a dragon. Old fool.");
                break;
            }
            case 29:
            {
                m.Say("I swear that bard only knows three songs.");
                break;
            }
            case 30:
            {
                m.Say($"If you need a good {Trade()}, ask for {Person()}.");
                break;
            }
            case 31:
            {
                m.Say("The fish have not been biting all week.");
                break;
            }
            case 32:
            {
                m.Say($"I hear {Beast()} overran a farm just outside {Town(m)}.");
                break;
            }
            case 33:
            {
                m.Say("Did the bank lose your gold too, or only mine?");
                break;
            }
            case 34:
            {
                m.Say("My mother says folk don't keep to the virtues like they used to.");
                break;
            }
            case 35:
            {
                m.Say($"{Person()} owes half this town money.");
                break;
            }
            case 36:
            {
                m.Say("I'd give a week's wages for a decent pair of gloves.");
                break;
            }
            case 37:
            {
                m.Say($"A mage from {Town(m)} turned a man into a frog. Or so they say.");
                break;
            }
            case 38:
            {
                m.Say("Keep your voice down. The walls have ears.");
                break;
            }
            default:
            {
                m.Say($"I saw {Person()} sneaking out of the tavern before dawn.");
                break;
            }
        }
    }

    /// <summary>A short reaction from another member of the circle.</summary>
    public static void Reply(Mobile m) => Reply(m, Utility.Random(ReplyLines));

    internal static void Reply(Mobile m, int line)
    {
        switch (line)
        {
            case 0:
            {
                m.Say("Truly?");
                break;
            }
            case 1:
            {
                m.Say("Bah! I'll believe it when I see it.");
                break;
            }
            case 2:
            {
                m.Say("You don't say.");
                break;
            }
            case 3:
            {
                m.Say($"That's what {Person()} told me too.");
                break;
            }
            case 4:
            {
                m.Say("Aye, I heard the same.");
                break;
            }
            case 5:
            {
                m.Say("Hmph.");
                break;
            }
            case 6:
            {
                m.Say("Ha! Not likely.");
                break;
            }
            case 7:
            {
                m.Say("Keep your voice down.");
                break;
            }
            case 8:
            {
                m.Say("Is that so?");
                break;
            }
            default:
            {
                m.Say("I'd stay out of it, if I were you.");
                break;
            }
        }
    }

    /// <summary>A rumor for a player who stops to talk.</summary>
    public static void TellRumor(Mobile m) => TellRumor(m, Utility.Random(TellRumorLines));

    internal static void TellRumor(Mobile m, int line)
    {
        switch (line)
        {
            case 0:
            {
                m.Say($"They say {Beast()} have been gathering near {Dungeon()}. Steer clear, friend.");
                break;
            }
            case 1:
            {
                m.Say($"An adventurer stumbled out of {Dungeon()} last week, raving about a hoard of gold.");
                break;
            }
            case 2:
            {
                m.Say($"Word is the {Trade()} in {Town(m)} pays well for rare reagents.");
                break;
            }
            case 3:
            {
                m.Say($"I heard a wyrm has been seen circling the hills near {Town(m)}.");
                break;
            }
            case 4:
            {
                m.Say($"{Person()} the {Trade()} found an old map in a shipwreck. It points somewhere near {Dungeon()}.");
                break;
            }
            case 5:
            {
                m.Say($"Folk say the deeper halls of {Dungeon()} have gone quiet. Too quiet.");
                break;
            }
            case 6:
            {
                m.Say($"Merchants from {Town(m)} are hiring guards. Something has them spooked.");
                break;
            }
            case 7:
            {
                m.Say($"A band of {Beast()} raided a caravan bound for {Town(m)}.");
                break;
            }
            case 8:
            {
                m.Say($"There's talk of a hermit near {Town(m)} who trades in strange relics.");
                break;
            }
            case 9:
            {
                m.Say($"If you're headed into {Dungeon()}, bring more torches than you think you need.");
                break;
            }
            case 10:
            {
                m.Say($"They found a strange idol near {Dungeon()}. No one will say who has it now.");
                break;
            }
            case 11:
            {
                m.Say($"The {Trade()} in {Town(m)} swears a ghost walks the shop at midnight.");
                break;
            }
            case 12:
            {
                m.Say($"{Person()} came back from {Dungeon()} with a blade that hums. Won't sell it at any price.");
                break;
            }
            case 13:
            {
                m.Say($"Pirates were seen off the coast near {Town(m)}. Watch the docks.");
                break;
            }
            default:
            {
                m.Say($"I heard the {Beast()} around {Dungeon()} have a new leader. Bigger than the last.");
                break;
            }
        }
    }

    /// <summary>Said when asked again before the rumor cooldown is up.</summary>
    public static void BrushOff(Mobile m) => BrushOff(m, Utility.Random(BrushOffLines));

    internal static void BrushOff(Mobile m, int line)
    {
        switch (line)
        {
            case 0:
            {
                m.Say("I've told you all I know, friend.");
                break;
            }
            case 1:
            {
                m.Say("Ask someone else. I've said my piece.");
                break;
            }
            case 2:
            {
                m.Say("Again? Go bother the town crier.");
                break;
            }
            default:
            {
                m.Say("I've nothing more for you today.");
                break;
            }
        }
    }

    private static string Person() => NameList.RandomName(Utility.RandomBool() ? "male" : "female");

    private static int Coin() => Utility.RandomMinMax(5, 200);

    private static string Dungeon() =>
        Utility.RandomList(
            "Covetous", "Deceit", "Despise", "Destard", "Hythloth", "Shame", "Wrong",
            "the Ice dungeon", "the Fire dungeon", "Terathan Keep", "the orc caves"
        );

    private static string Beast() =>
        Utility.RandomList(
            "orcs", "ratmen", "lizardmen", "ettins", "trolls", "harpies", "brigands",
            "ogres", "liches", "gargoyles", "terathans", "ophidians", "headless", "mongbats"
        );

    private static string Trade() =>
        Utility.RandomList(
            "smith", "baker", "tailor", "tinker", "fisherman", "carpenter", "healer",
            "mage", "bard", "jeweler", "provisioner", "innkeeper", "tanner", "alchemist"
        );

    /// <summary>A random top-level town on the speaker's facet.</summary>
    private static string Town(Mobile m)
    {
        var map = m.Map;

        if (map == null || map == Map.Internal)
        {
            return "Britain";
        }

        if (!_townNames.TryGetValue(map, out var names))
        {
            var found = new List<string>();

            foreach (var region in Region.Regions)
            {
                if (region is TownRegion { Parent: null } && region.Map == map && !string.IsNullOrEmpty(region.Name) &&
                    !found.Contains(region.Name))
                {
                    found.Add(region.Name);
                }
            }

            names = found.Count > 0 ? found.ToArray() : ["Britain"];
            _townNames[map] = names;
        }

        return names.RandomElement();
    }
}
