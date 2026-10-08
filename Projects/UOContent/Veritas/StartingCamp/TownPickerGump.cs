using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Veritas.StartingCamp;

public class TownPickerGump : DynamicGump
{
	private const int CityButtonOffset = 100;
	private const int RowHeight = 25;
	private const int ListTop = 90;

	private readonly CampGypsy _gypsy;
	private readonly CityInfo[] _cities;

	public override bool Singleton => true;

	private TownPickerGump(CampGypsy gypsy, CityInfo[] cities) : base(100, 100)
	{
		_gypsy = gypsy;
		_cities = cities;
	}

	public static void DisplayTo(PlayerMobile pm, CampGypsy gypsy)
	{
		if (pm?.NetState == null || gypsy?.Deleted != false)
		{
			return;
		}

		var cities = CampDeparture.GetAvailableCities(pm);

		if (cities.Length == 0)
		{
			gypsy.Say("The cards are silent today. Come back later.");
			return;
		}

		pm.SendGump(new TownPickerGump(gypsy, cities));
	}

	protected override void BuildLayout(ref DynamicGumpBuilder builder)
	{
		builder.AddPage();

		var height = ListTop + _cities.Length * RowHeight + 50;

		// builder.AddBackground(0, 0, 420, height, 5054);
		builder.AddImage(10, 10, GypsyCampArt.Header);

		builder.AddHtml(10, 10, 400, 20, "Where shall your journey begin?", "#FFFFFF", align: TextAlignment.Center);
		builder.AddHtml(
			10,
			40,
			400,
			40,
			"Choose the place where fate will set you down. Once you leave, you cannot return.",
			"#FFFFFF"
		);

		for (var i = 0; i < _cities.Length; i++)
		{
			var city = _cities[i];
			var y = ListTop + i * RowHeight;

			builder.AddButton(10, y, 4005, 4007, CityButtonOffset + i);
			builder.AddHtml(45, y + 2, 365, 20, $"{city.City} - {city.Building}", "#FFFFFF");
		}

		var cancelY = ListTop + _cities.Length * RowHeight + 15;
		builder.AddButton(10, cancelY, 4017, 4019, 0);
		builder.AddHtmlLocalized(45, cancelY + 2, 140, 20, 1011012); // CANCEL
	}

	public override void OnResponse(NetState state, in RelayInfo info)
	{
		if (state.Mobile is not PlayerMobile pm)
		{
			return;
		}

		var index = info.ButtonID - CityButtonOffset;

		if (index < 0 || index >= _cities.Length)
		{
			return;
		}

		CampDeparture.TryDepart(pm, _gypsy, _cities[index]);
	}
}
