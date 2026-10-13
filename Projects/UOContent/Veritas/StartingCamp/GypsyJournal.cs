using System;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Misc;
using Server.Network;

namespace Server.Veritas.StartingCamp;

/// <summary>The gypsy's visitor journal. Signing it lets a new arrival choose their name before leaving the camp.</summary>
[SerializationGenerator(0)]
public partial class GypsyJournal : Item
{
    public const int UseRange = 2;

    [Constructible]
    public GypsyJournal() : base(0xFF2)
    {
        Movable = false;
    }

    public override string DefaultName => "the gypsy's journal";

    public override void OnDoubleClick(Mobile from)
    {
        if (!CanUse(from))
        {
            from.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        GypsyNameGump.DisplayTo(from, this);
    }

    public bool CanUse(Mobile from) =>
        !Deleted && from.Alive && from.Map == Map && from.InRange(GetWorldLocation(), UseRange) &&
        (GypsyCamp.IsInCamp(from) || from.AccessLevel > AccessLevel.Player);

    /// <summary>Format rules from <see cref="NameVerification"/>, plus no two characters may share a name.</summary>
    public static bool TryValidateName(ReadOnlySpan<char> name, Mobile self, out string error)
    {
        if (!NameVerification.ValidatePlayerName(name))
        {
            error = "The ink will not take that name.";
            return false;
        }

        if (IsNameTaken(name, self))
        {
            error = "Someone has already signed that name.";
            return false;
        }

        error = null;
        return true;
    }

    // Cold path (a rename at the camp), so scanning account slots is fine and avoids iterating World.Mobiles.
    private static bool IsNameTaken(ReadOnlySpan<char> name, Mobile self)
    {
        foreach (var account in Accounts.GetAccounts())
        {
            for (var i = 0; i < account.Length; i++)
            {
                var m = account[i];

                if (m != null && m != self && !m.Deleted && m.RawName.InsensitiveEquals(name))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

public class GypsyNameGump : StaticGump<GypsyNameGump>
{
    private const int SignButton = 1;

    private readonly GypsyJournal _journal;

    public override bool Singleton => true;

    private GypsyNameGump(GypsyJournal journal) : base(50, 50) => _journal = journal;

    public static void DisplayTo(Mobile from, GypsyJournal journal)
    {
        if (from?.NetState == null || journal?.Deleted != false)
        {
            return;
        }

        from.SendGump(new GypsyNameGump(journal));
    }

    protected override void BuildLayout(ref StaticGumpBuilder builder)
    {
        builder.AddPage();

        builder.AddImageTiled(10, 10, 300, 135, 2624);
        builder.AddAlphaRegion(10, 10, 300, 135);

        builder.AddHtml(10, 15, 300, 20, "The Gypsy's Journal", "#FFFFFF", align: TextAlignment.Center);
        builder.AddHtml(
            20,
            40,
            280,
            40,
            "Visitors sign their names here before they leave. Write the name you will carry into the world.",
            "#DEC6DE"
        );

        builder.AddBackground(98, 88, 194, 24, 0x2486);
        builder.AddLabel(20, 90, 0x480, "Your name:");
        builder.AddTextEntry(102, 90, 186, 20, 0, 0, "");

        builder.AddButton(20, 117, 4005, 4007, SignButton);
        builder.AddHtml(55, 118, 100, 20, "Sign", "#FFFFFF");
    }

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        var m = state.Mobile;

        if (info.ButtonID != SignButton || m == null || _journal?.CanUse(m) != true)
        {
            return;
        }

        var newName = info.GetTextEntry(0).AsSpan().Trim();

        if (!GypsyJournal.TryValidateName(newName, m, out var error))
        {
            m.SendMessage(error);
            return;
        }

        m.RawName = newName.ToString();
        m.SendMessage($"You sign the journal. You are now known as {newName}.");
    }
}
