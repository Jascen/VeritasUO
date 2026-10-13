using System;
using Server.Engines.Avatar;
using Server.Mobiles;

namespace Server.Veritas.StartingCamp;

/// <summary>
/// Extension points for the Avatar system, which is ported separately. Until it is merged, the Avatar card stays hidden.
/// </summary>
public static class AvatarHooks
{
    public const string CardKey = "avatar";
    public const string CardTitle = "The Avatar";

    //TODO(avatar): Memento's card text is Constants.AVATAR_GYPSY_TEXT; use the port's equivalent.
    public const string CardLore =
        "The Avatar walks a path of many lives, each one a single step on a longer climb. Draw this card to begin that " +
        "climb, and fate will choose the city where your first life starts.";

    //TODO(avatar): return true when the Avatar system is enabled and this player may choose it (Memento hid the card from
    // players who were already Avatars).
    public static bool IsCardAvailable(PlayerMobile pm) => false;

    //TODO(avatar): Memento ran [avatar-enable here instead of leaving the camp. Return true when the draw was handled and
    // the player should stay; return false to depart to the card's (random) city.
    public static bool OnCardDrawn(PlayerMobile pm, TarotCard card) => false;

    // Memento asked for confirmation before an Avatar without a template entered the land.
    public static void BeforeDeparture(PlayerMobile pm, Action onContinue)
    {
        if (!pm.Avatar.Active || pm.Avatar.SelectedTemplate != AvatarStarterTemplates.None)
        {
            onContinue();
            return;
        }

        AvatarConfirm.Send(
            pm,
            "No template selected",
            "You have not selected a template yet. Are you sure you want to enter the land?",
            onContinue
        );
    }

    public static void OnDeparting(PlayerMobile pm) => AvatarEngine.GrantStartingItems(pm);
}
