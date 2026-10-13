using Server.Gumps;

namespace Server.Veritas.StartingCamp;

/// <summary>The arrival story, shown whenever a player logs in or arrives while still at the camp.</summary>
public class CampWelcomeGump : StaticGump<CampWelcomeGump>
{
    private const string WelcomeText =
        "The day was ordinary, as days go. When the sun finally slipped below the hills you went to bed, but sleep came " +
        "restless and strange. You dreamed of a door of light opening in the dark, and of falling through it.<br><br>" +
        "You woke on cold grass beneath an unfamiliar sky. Your night clothes are gone; you wear rough, old-fashioned " +
        "garb instead, and a light flickers in your hand. There is salt on the wind, and beyond the trees you hear the sea " +
        "on every side. Wherever this is, it is an island, and a small one.<br><br>" +
        "Through the dark you can see the glow of a campfire to the north. A brightly painted tent stands beside it, " +
        "lanterns swaying at its poles, and someone is waiting there as if they expected you.<br><br>" +
        "Whatever life you left behind, it feels very far away. If ever there were a place to begin again, it is here. " +
        "Find the tent, and speak with whoever keeps it.";

    public override bool Singleton => true;

    private CampWelcomeGump() : base(400, 50)
    {
    }

    public static void DisplayTo(Mobile m)
    {
        if (m?.NetState == null)
        {
            return;
        }

        m.SendGump(new CampWelcomeGump());
    }

    protected override void BuildLayout(ref StaticGumpBuilder builder)
    {
        builder.AddPage();

        builder.AddImage(0, 0, GypsyCampArt.WelcomeBackground);
        builder.AddImage(13, 12, GypsyCampArt.WelcomeHeader);

        builder.AddHtml(13, 58, 482, 312, WelcomeText, "#94C541", scrollbar: true);

        builder.AddButton(468, 10, 4017, 4019, 0);
    }
}
