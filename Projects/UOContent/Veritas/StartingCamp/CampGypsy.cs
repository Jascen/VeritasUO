using ModernUO.Serialization;
using Server.Engines.Quests;
using Server.Items;
using Server.Mobiles;

namespace Server.Veritas.StartingCamp;

[SerializationGenerator(0)]
public partial class CampGypsy : BaseQuester
{
    [Constructible]
    public CampGypsy() : base("the gypsy")
    {
        CantWalk = true;
        Direction = Direction.South;
    }

    public override void InitBody()
    {
        InitStats(100, 100, 25);

        SpeechHue = Utility.RandomDyedHue();
        Hue = Race.Human.RandomSkinHue();
        Female = true;
        Body = 0x191;
        Name = NameList.RandomName("female");
    }

    public override void InitOutfit()
    {
        AddItem(new FancyDress(Utility.RandomBrightHue()));
        AddItem(new BodySash(Utility.RandomBrightHue()));
        AddItem(new Bandana(Utility.RandomBrightHue()));
        AddItem(new Sandals());
        AddItem(new GoldNecklace());

        Utility.AssignRandomHair(this);
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile pm && from.Alive && InRange(from, CampDeparture.TalkRange) && CanTalkTo(pm))
        {
            OnTalk(pm, false);
            return;
        }

        base.OnDoubleClick(from);
    }

    public override void OnTalk(PlayerMobile player, bool contextMenu)
    {
        if (!player.Alive)
        {
            Say("I read fates for the living, spirit. Find a healer first.");
            return;
        }

        PlaySound(778);
        TownPickerGump.DisplayTo(player, this);
    }
}
