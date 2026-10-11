namespace Server.Mobiles;

public abstract partial class BaseAI
{
    /// <summary>
    /// Drops the current fight, including the last-known-position memory, so the creature picks its
    /// next target by its fight mode instead of resuming the old one.
    /// </summary>
    public void Peel()
    {
        ClearLastKnown();
        Mobile.FocusMob = null;
        Mobile.Combatant = null;
        Mobile.Warmode = false;

        if (!Mobile.Controlled)
        {
            Action = ActionType.Wander;
        }
    }

    /// <summary>
    /// Runs ahead of Think/Obey: a driven creature walks to its herding destination and does nothing
    /// else until it arrives, the drive times out, or damage ends it.
    /// </summary>
    internal bool TryDrive()
    {
        if (!Mobile.IsDriven)
        {
            return false;
        }

        if (Core.TickCount - Mobile.DriveEnd < 0 && CheckHerding())
        {
            return true;
        }

        Mobile.EndDrive();
        return false;
    }
}
