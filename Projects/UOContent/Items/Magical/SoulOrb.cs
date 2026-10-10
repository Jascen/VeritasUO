using System;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

public enum SoulOrbType
{
    Default,
    BloodOfVampire,
    CloningCrystalJedi,
    CloningCrystalSyth,
    RestorativeSoil,
    PermadeathPlaceholder
}

/// <summary>
/// Offers its owner a resurrection shortly after death. The permadeath placeholder skips the offer and
/// resurrects directly, which for an Avatar raises the start-over prompt instead.
/// </summary>
[SerializationGenerator(0)]
public partial class SoulOrb : Item
{
    private static readonly TimeSpan _delay = TimeSpan.FromSeconds(10.0);

    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _owner;

    [SerializableField(1, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private SoulOrbType _orbType;

    private TimerExecutionToken _timerToken;

    [Constructible]
    public SoulOrb() : base(0x573E)
    {
        LootType = LootType.Blessed;
        Movable = false;
        Weight = 1.0;
		Hue = 1153;
    }

    public override string DefaultName => _orbType switch
    {
        SoulOrbType.BloodOfVampire     => "blood of a vampire",
        SoulOrbType.CloningCrystalJedi => "replication crystal",
        SoulOrbType.CloningCrystalSyth => "replication crystal",
        SoulOrbType.RestorativeSoil    => "mystical mud",
        _                              => "soul orb"
    };

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile pm) => FindActive(pm)?.StartTimer();

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm) => FindActive(pm)?.StartTimer();

    /// <summary>
    /// Gives the player a new orb, replacing any orb they already carry.
    /// </summary>
    public static SoulOrb Create(Mobile from, SoulOrbType orbType)
    {
        if (from is not PlayerMobile player)
        {
            return null;
        }

        if (player.Avatar.Active && orbType != SoulOrbType.PermadeathPlaceholder)
        {
            from.SendMessage("This item would have no effect.");
            return null;
        }

        FindActive(from)?.Delete();

        var orb = new SoulOrb
        {
            _owner = from,
            _orbType = orbType
        };

        switch (orbType)
        {
            case SoulOrbType.BloodOfVampire:
                {
                    orb.ItemID = 0x122B;
                    break;
                }
            case SoulOrbType.CloningCrystalJedi:
                {
                    orb.ItemID = 0x703;
                    break;
                }
            case SoulOrbType.CloningCrystalSyth:
                {
                    orb.ItemID = 0x705;
                    break;
                }
            case SoulOrbType.RestorativeSoil:
                {
                    orb.ItemID = 0x913;
                    break;
                }
            case SoulOrbType.PermadeathPlaceholder:
                {
                    orb.Visible = false;
                    break;
                }
        }

        from.AddToBackpack(orb);

        if (orb.RootParent == from)
        {
            return orb;
        }

        from.PrivateOverheadMessage(MessageType.Regular, 38, false, "Your pack is full so it did not work!", from.NetState);
        from.SendMessage("Your pack is full so it did not work!");
        orb.Delete();

        return null;
    }

    public static SoulOrb FindActive(Mobile from)
    {
        var orb = from.Backpack?.FindItemByType<SoulOrb>();
        return orb?.Deleted == false && orb._owner == from ? orb : null;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (_orbType == SoulOrbType.PermadeathPlaceholder)
        {
            from.SendMessage("This contains your soul. Are you trying to delete yourself?!");
            return;
        }

        if (!IsChildOf(from.Backpack))
        {
            return;
        }

        from.SendGump(
            new WarningGump(
                $"Delete {Name}?<BR><BR>Are you sure you wish to delete this?",
                420,
                200,
                confirmed =>
                {
                    if (confirmed && !Deleted && IsChildOf(from.Backpack))
                    {
                        Delete();
                    }
                }
            )
        );
    }

    public override void AddNameProperties(IPropertyList list)
    {
        base.AddNameProperties(list);

        var ownerName = _owner?.Name ?? "no one";

        switch (_orbType)
        {
            case SoulOrbType.BloodOfVampire:
                {
                    list.Add(1049644, $"{"Contains vampire blood for"} {ownerName}"); // [~1_stuff~]
                    break;
                }
            case SoulOrbType.CloningCrystalJedi:
            case SoulOrbType.CloningCrystalSyth:
                {
                    list.Add(1049644, $"{"Contains genetic patterns for"} {ownerName}"); // [~1_stuff~]
                    break;
                }
            default:
                {
                    list.Add(1049644, $"{"Contains the Soul of"} {ownerName}"); // [~1_stuff~]
                    break;
                }
        }
    }

    public void StartTimer()
    {
        if (_owner?.Deleted != false || _owner.Alive)
        {
            return;
        }

        _timerToken.Cancel();
        Timer.StartTimer(_delay, OnTimerTick, out _timerToken);
    }

    private void OnTimerTick()
    {
        if (Deleted || _owner?.Deleted != false || _owner.Alive || _owner.NetState == null)
        {
            return;
        }

        if (_orbType == SoulOrbType.PermadeathPlaceholder)
        {
            _owner.Resurrect();
            return;
        }

        _owner.SendSound(0x0F8);
        _owner.CloseGump<WarningGump>();
        _owner.SendGump(new WarningGump("The spirits offer their aid.<BR><BR>Do you accept?", 420, 200, OnOfferResponse));
    }

    private void OnOfferResponse(bool accepted)
    {
        var from = _owner;

        if (Deleted || from?.Deleted != false || from.Alive)
        {
            return;
        }

        if (!accepted)
        {
            StartTimer();
            return;
        }

        if (from.Map == null || !from.Map.CanFit(from.Location, 16, false, false))
        {
            from.SendLocalizedMessage(502391); // Thou can not be resurrected there!
            StartTimer();
            return;
        }

        switch (_orbType)
        {
            case SoulOrbType.BloodOfVampire:
                {
                    from.SendMessage("The blood pours out of the bottle, restoring your life.");
                    break;
                }
            case SoulOrbType.CloningCrystalJedi:
            case SoulOrbType.CloningCrystalSyth:
                {
                    from.SendMessage("The crystal forms a clone of your body, restoring your life.");
                    break;
                }
            default:
                {
                    from.SendMessage("The orb glows, releasing your soul.");
                    break;
                }
        }

        from.Resurrect();
        from.FixedEffect(0x376A, 10, 16);
        Delete();
    }

    public override void OnDelete()
    {
        _timerToken.Cancel();
        base.OnDelete();
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        _owner = null;
    }
}
