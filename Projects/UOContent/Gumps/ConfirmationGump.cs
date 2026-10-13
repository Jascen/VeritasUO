using System;
using Server.Network;

namespace Server.Gumps;

public class ConfirmationGump : DynamicGump
{
    private const int Width = 475;
    private const int Height = 280;
    private const int PagePadding = 20;
    private const int BodyWidth = Width - PagePadding * 2;
    private const string TextColor = "#FFFFFF";

    private readonly string _title;
    private readonly string _message;
    private readonly Action _onConfirmed;
    private readonly Action _onDeclined;

    public override bool Singleton => true;

    public ConfirmationGump(string message, Action onConfirmed = null, Action onDeclined = null)
        : this(null, message, onConfirmed, onDeclined)
    {
    }

    public ConfirmationGump(string title, string message, Action onConfirmed = null, Action onDeclined = null) : base(25, 25)
    {
        _title = title;
        _message = message;
        _onConfirmed = onConfirmed;
        _onDeclined = onDeclined;
    }

    /// <summary>
    /// Sends a confirmation gump if <paramref name="shouldPrompt"/> is <c>true</c>.
    /// Otherwise, immediately executes <paramref name="onConfirmed"/>.
    /// </summary>
    public static void PromptIf(Mobile from, bool shouldPrompt, Action onConfirmed, Func<Action, ConfirmationGump> gumpFactory)
    {
        if (shouldPrompt)
        {
            from.SendGump(gumpFactory(onConfirmed));
        }
        else
        {
            onConfirmed();
        }
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, Width, Height, 0x0A3C);

        var y = PagePadding;
        if (!string.IsNullOrWhiteSpace(_title))
        {
            builder.AddBackground(0, 0, Width, 50, 0x0A3C);
            builder.AddHtml(PagePadding, y, BodyWidth, 50, _title, TextColor, align: TextAlignment.Center);
            y += 80;
        }

        builder.AddHtml(PagePadding, y, BodyWidth, 170, _message, TextColor);

        var buttonY = Height - 50;
        var buttonX = 100;
        builder.AddButton(buttonX, buttonY, 4005, 4005, 1);
        builder.AddHtml(buttonX + 30, buttonY + 3, 58, 20, "Yes", TextColor);

        buttonX = Width - 145;
        builder.AddButton(buttonX, buttonY, 4020, 4020, 2);
        builder.AddHtml(buttonX + 30, buttonY + 3, 58, 20, "No", TextColor);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 1)
        {
            _onConfirmed?.Invoke();
        }
        else
        {
            _onDeclined?.Invoke();
        }
    }
}
