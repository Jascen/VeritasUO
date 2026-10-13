using Server.Items;

namespace Server.Commands;

public static class BandageCommands
{
    private const int LowBandageWarning = 5;

    public static void Configure()
    {
        CommandSystem.Register("BandSelf", AccessLevel.Player, BandSelf_OnCommand);
        CommandSystem.Register("BandOther", AccessLevel.Player, BandOther_OnCommand);
    }

    [Usage("BandSelf")]
    [Description("Applies a bandage from your backpack to yourself.")]
    private static void BandSelf_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (FindBandage(from) is not { } bandage)
        {
            return;
        }

        Bandage.BandageTargetRequest(from, bandage, from);
        WarnIfLow(from, bandage);
    }

    [Usage("BandOther")]
    [Description("Targets someone to apply a bandage from your backpack to.")]
    private static void BandOther_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (FindBandage(from) is not { } bandage)
        {
            return;
        }

        bandage.OnDoubleClick(from);
        WarnIfLow(from, bandage);
    }

    private static Bandage FindBandage(Mobile from)
    {
        if (!from.Alive)
        {
            from.SendMessage("You cannot do that while dead.");
            return null;
        }

        var bandage = from.Backpack?.FindItemByType<Bandage>();

        if (bandage == null)
        {
            from.SendMessage("You have no bandages left to use!");
        }

        return bandage;
    }

    private static void WarnIfLow(Mobile from, Bandage bandage)
    {
        // Bandage may have been consumed down to nothing by the heal attempt.
        var amount = bandage.Deleted ? 0 : bandage.Amount;

        if (amount <= LowBandageWarning)
        {
            from.SendMessage($"Warning, your bandage count is currently {amount}!");
        }
    }
}
