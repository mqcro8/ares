using Godot;

namespace ares.scripts.rover;

public partial class RouteForkGraphic : Control
{
	[Export] public bool RiskyRoute { get; set; }

	public override void _Draw()
	{
		float centerX = Size.X * 0.5f;
		float top = 5f;
		float bottom = Size.Y - 5f;
		Vector2 start = new(centerX, top);
		Vector2 end = new(RiskyRoute ? Size.X * 0.75f : Size.X * 0.25f, bottom);
		Color routeColor = RiskyRoute ? new Color("ff794f") : new Color("71e6a0");
		DrawLine(start, end, routeColor, 5f, true);
		DrawCircle(start, 5f, new Color("eaf4ff"));
		DrawCircle(end, 7f, routeColor);

		int markers = RiskyRoute ? 4 : 1;
		for (int i = 1; i <= markers; i++)
		{
			float t = i / (float)(markers + 1);
			Vector2 marker = start.Lerp(end, t);
			DrawCircle(marker, 4f, RiskyRoute ? new Color("ffb24a") : new Color("b9efca"));
		}

		DrawCircle(new Vector2(centerX, 3f), 3f, new Color("cbd8e4"));
	}
}
