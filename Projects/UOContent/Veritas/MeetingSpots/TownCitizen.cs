using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;

namespace Server.Veritas.MeetingSpots;

/// <summary>
/// An idle townsperson standing in a <see cref="MeetingSpot"/> circle. Never moves, cannot be harmed,
/// gossips when players pass and shares a rumor when spoken to.
/// </summary>
[SerializationGenerator(0)]
public partial class TownCitizen : BaseCreature
{
    private const int ChatterRange = 8;
    private const int TalkRange = 3;
    private const int ReplyChance = 40;
    private static readonly TimeSpan QuestionCooldown = TimeSpan.FromMinutes(15);

    // Horse, ostard and llama mount item IDs.
    private static readonly int[] MountItemIds = [0x3E9F, 0x3EA0, 0x3EA1, 0x3EA2, 0x3EA3, 0x3EA5, 0x3EA6];

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private MeetingSpot _spot;

    // Used only when the citizen was placed by hand and has no spot to share a cooldown with.
    private long _nextChatterTick = Core.TickCount;
    private DateTime _questionedUntil;

    [Constructible(AccessLevel.GameMaster)]
    public TownCitizen() : this(false)
    {
    }

    public TownCitizen(bool mounted) : base(AIType.AI_Animal, FightMode.None)
    {
        InitStats(50, 50, 50);
        SetSpeed(0.2, 0.4);

        SpeechHue = Utility.RandomDyedHue();
        Hue = Race.Human.RandomSkinHue();
        Blessed = true;
        CantWalk = true;

        if (Female = Utility.RandomBool())
        {
            Body = 0x191;
            Name = NameList.RandomName("female");
        }
        else
        {
            Body = 0x190;
            Name = NameList.RandomName("male");
        }

        Utility.AssignRandomHair(this);

        if (!Female)
        {
            Utility.AssignRandomFacialHair(this, HairHue);
        }

        Dress();

        if (mounted)
        {
            AddItem(new VirtualMountItem(this) { ItemID = MountItemIds.RandomElement() });
        }
    }

    public override bool DisallowAllMoves => true;

    public override bool CanTeach => false;

    public override bool BardImmune => true;

    public override void OnMovement(Mobile m, Point3D oldLocation)
    {
        base.OnMovement(m, oldLocation);

        if (m is not PlayerMobile || !m.InRange(this, ChatterRange) || !TryClaimChatter() || !Utility.RandomBool())
        {
            return;
        }

        CitizenChatter.Speak(this);

        if (Utility.Random(100) < ReplyChance && _spot?.PickReplier(this) is { } replier)
        {
            Timer.StartTimer(TimeSpan.FromSeconds(Utility.RandomMinMax(2, 4)), replier.Reply);
        }
    }

    private bool TryClaimChatter()
    {
        if (_spot is { Deleted: false })
        {
            return _spot.TryClaimChatter();
        }

        if (Core.TickCount - _nextChatterTick < 0)
        {
            return false;
        }

        _nextChatterTick = Core.TickCount + Utility.RandomMinMax(15_000, 45_000);
        return true;
    }

    private void Reply()
    {
        if (!Deleted && Map != null && Map != Map.Internal)
        {
            CitizenChatter.Reply(this);
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (CanTalkTo(from))
        {
            Talk(from);
            return;
        }

        base.OnDoubleClick(from);
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from is PlayerMobile && from.Alive)
        {
            list.Add(new CitizenTalkEntry());
        }
    }

    private bool CanTalkTo(Mobile from) => from is PlayerMobile && from.Alive && from.InRange(this, TalkRange);

    public void Talk(Mobile from)
    {
        if (Core.Now < _questionedUntil)
        {
            CitizenChatter.BrushOff(this);
            return;
        }

        _questionedUntil = Core.Now + QuestionCooldown;
        CitizenChatter.TellRumor(this);
    }

    public override void OnAfterDelete()
    {
        if (_spot is { Deleted: false })
        {
            _spot.RemoveCitizen(this);
        }

        _spot = null;

        base.OnAfterDelete();
    }

    private void Dress()
    {
        switch (Utility.Random(5))
        {
            case 0:
            {
                DressCommoner();
                break;
            }
            case 1:
            {
                DressNoble();
                break;
            }
            case 2:
            {
                DressFighter();
                break;
            }
            case 3:
            {
                DressMage();
                break;
            }
            default:
            {
                DressRogue();
                break;
            }
        }
    }

    private void DressCommoner()
    {
        Title = Utility.RandomList("the townsperson", "the farmer", "the laborer", "the shopkeeper", "the fishmonger");

        if (Female && Utility.RandomBool())
        {
            AddItem(new PlainDress(Utility.RandomNeutralHue()));
        }
        else
        {
            AddItem(new Shirt(Utility.RandomDyedHue()));

            if (Female)
            {
                AddItem(new Skirt(Utility.RandomNeutralHue()));
            }
            else
            {
                AddItem(Utility.RandomBool() ? new LongPants(Utility.RandomNeutralHue()) : new ShortPants(Utility.RandomNeutralHue()));
            }
        }

        if (Utility.Random(3) == 0)
        {
            AddItem(new HalfApron(Utility.RandomNeutralHue()));
        }

        if (Utility.Random(4) == 0)
        {
            AddItem(new StrawHat());
        }

        AddItem(Utility.RandomBool() ? new Shoes(Utility.RandomNeutralHue()) : new Sandals());
    }

    private void DressNoble()
    {
        Title = Female ? "the noblewoman" : "the nobleman";

        if (Female)
        {
            AddItem(new FancyDress(Utility.RandomDyedHue()));
        }
        else
        {
            AddItem(new FancyShirt(Utility.RandomDyedHue()));
            AddItem(new LongPants(Utility.RandomNeutralHue()));

            if (Utility.RandomBool())
            {
                AddItem(new Doublet(Utility.RandomDyedHue()));
            }
        }

        AddItem(new Cloak(Utility.RandomDyedHue()));

        if (Utility.Random(3) == 0)
        {
            AddItem(new FeatheredHat(Utility.RandomDyedHue()));
        }

        AddItem(Female ? new ThighBoots() : new Boots());
    }

    private void DressFighter()
    {
        Title = Utility.RandomList("the mercenary", "the sellsword", "the adventurer", "the veteran");

        if (Utility.RandomBool())
        {
            AddItem(new StuddedChest());
        }
        else
        {
            AddItem(new LeatherChest());
        }

        AddItem(new LeatherLegs());
        AddItem(new Boots());

        if (Utility.RandomBool())
        {
            AddItem(new Surcoat(Utility.RandomDyedHue()));
        }

        Item weapon = Utility.Random(4) switch
        {
            0 => new Longsword(),
            1 => new Broadsword(),
            2 => new Mace(),
            _ => new Club()
        };

        AddItem(weapon);
    }

    private void DressMage()
    {
        Title = Utility.RandomList("the mage", "the scholar", "the alchemist", "the sage");

        AddItem(new Robe(Utility.RandomDyedHue()));
        AddItem(new Sandals());

        if (Utility.RandomBool())
        {
            AddItem(new WizardsHat(Utility.RandomDyedHue()));
        }

        Item staff = Utility.Random(4) switch
        {
            0 => new GnarledStaff(),
            1 => new BlackStaff(),
            2 => new QuarterStaff(),
            _ => null
        };

        if (staff != null)
        {
            AddItem(staff);
        }
    }

    private void DressRogue()
    {
        Title = Utility.RandomList("the traveler", "the wanderer", "the gambler", "the rogue");

        AddItem(new Shirt(Utility.RandomNeutralHue()));
        AddItem(Female ? new Skirt(Utility.RandomNeutralHue()) : new LongPants(Utility.RandomNeutralHue()));
        AddItem(new Cloak(Utility.RandomNeutralHue()));
        AddItem(new Boots(Utility.RandomNeutralHue()));

        if (Utility.RandomBool())
        {
            AddItem(new Bandana(Utility.RandomDyedHue()));
        }

        if (Utility.RandomBool())
        {
            AddItem(new Dagger());
        }
    }

    private class CitizenTalkEntry : ContextMenuEntry
    {
        public CitizenTalkEntry() : base(6146, TalkRange) // Talk
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is TownCitizen citizen && citizen.CanTalkTo(from))
            {
                citizen.Talk(from);
            }
        }
    }
}
