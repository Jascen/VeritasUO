using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Veritas.StartingCamp;

/// <summary>
/// The gypsy's reading: an intro page, then one client-side page per card. Browsing needs no server round-trips;
/// only drawing a card replies.
/// </summary>
public class TarotGump : DynamicGump
{
    private const int DrawButtonOffset = 100;
    private const int IntroPage = 1;
    private const int FirstCardPage = 2;
    private const string TextColor = "#DEC6DE";

    private const string IntroText =
        "Sit, sit. The fire is warm and the cards are patient, even if you are not.<br><br>" +
        "You have come a long way to reach this camp, though I suspect you do not remember the journey. That is the way " +
        "of it. The road behind you is closed; the roads ahead are many. My cards will show you where each one leads.<br><br>" +
        "Look through the deck as long as you like. Each card is a city, and each city is a different life: scholars and " +
        "soldiers, miners and merchants, healers and duelists. When one speaks to you, draw it, and it will carry you there. " +
        "Choose with care. Once you leave this island, you will not find your way back.<br><br>" +
        "Now. Let us see what the cards have to say.";

    private readonly CampGypsy _gypsy;
    private readonly TarotCard[] _cards;

    public override bool Singleton => true;

    private TarotGump(CampGypsy gypsy, TarotCard[] cards) : base(50, 50)
    {
        _gypsy = gypsy;
        _cards = cards;
    }

    public static void DisplayTo(PlayerMobile pm, CampGypsy gypsy)
    {
        if (pm?.NetState == null || gypsy?.Deleted != false)
        {
            return;
        }

        var cards = TarotDeck.BuildFor(pm);

        if (cards.Length == 0)
        {
            gypsy.Say("The cards are silent today. Come back later.");
            return;
        }

        pm.SendGump(new TarotGump(gypsy, cards));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage(IntroPage);
		builder.AddBackground(250, 0, 650, 400, 0x06DB);
        builder.AddHtml(278, 73, 604, 320, IntroText, TextColor, scrollbar: true);
        builder.AddButton(819, 14, 4011, 4013, 0, GumpButtonType.Page, FirstCardPage);

        for (var i = 0; i < _cards.Length; i++)
        {
            AddCardPage(ref builder, i);
        }
    }

    private void AddCardPage(ref DynamicGumpBuilder builder, int index)
    {
        var card = _cards[index];
        var count = _cards.Length;
        var prevPage = FirstCardPage + (index + count - 1) % count;
        var nextPage = FirstCardPage + (index + 1) % count;

        builder.AddPage(FirstCardPage + index);

		builder.AddBackground(250, 0, 400, 410, 0x06DB);

        builder.AddHtml(269, 12, 240, 20, card.Title, TextColor);
        builder.AddHtml(271, 47, 356, 297, $"{card.Destination.City} - {card.Destination.Building}<br><br>{card.Lore}", TextColor, scrollbar: true);

        builder.AddButton(317, 375, 4014, 4016, 0, GumpButtonType.Page, prevPage);
        builder.AddButton(552, 375, 4005, 4007, 0, GumpButtonType.Page, nextPage);

        builder.AddButton(599, 11, 4023, 4025, DrawButtonOffset + index);
    }

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        if (state.Mobile is not PlayerMobile pm)
        {
            return;
        }

        var index = info.ButtonID - DrawButtonOffset;

        if (index < 0 || index >= _cards.Length)
        {
            return;
        }

        CampDeparture.TryDepart(pm, _gypsy, _cards[index]);
    }
}
