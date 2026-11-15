namespace ares.scripts.dialogue;

using Godot;

/// <summary>Shared visual rules for narrative, briefing and tutorial dialogue panels.</summary>
public static class DialoguePanelStyle
{
	public static readonly Color PanelColor = new(0.025f, 0.04f, 0.06f, 0.97f);
	public static readonly Color BorderColor = new(0.25f, 0.48f, 0.55f, 1f);
	public static readonly Color SpeakerColor = new(0.55f, 0.85f, 0.9f);
	public static readonly Color BodyColor = new(0.94f, 0.95f, 0.97f);
	public const int SpeakerSize = 12;
	public const int BodySize = 18;
	public const int ActionSize = 18;
	public const float StandardHeight = 240f;

	public static void ApplyPanel(PanelContainer panel)
	{
		if (panel == null) return;
		panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
		panel.CustomMinimumSize = new Vector2(panel.CustomMinimumSize.X, StandardHeight);
		// Legacy rover panels had a 175px maximum, which squeezed them below the shared height.
		panel.CustomMaximumSize = new Vector2(-1, -1);
	}

	public static StyleBoxFlat CreatePanelStyle() => new()
	{
		BgColor = PanelColor,
		BorderColor = BorderColor,
		BorderWidthLeft = 2,
		BorderWidthRight = 2,
		BorderWidthTop = 2,
		BorderWidthBottom = 2,
		CornerRadiusTopLeft = 4,
		CornerRadiusTopRight = 4,
		CornerRadiusBottomRight = 4,
		CornerRadiusBottomLeft = 4,
	};

	public static void ApplyMargins(MarginContainer margin)
	{
		if (margin == null) return;
		margin.CustomMinimumSize = Vector2.Zero;
		margin.CustomMaximumSize = new Vector2(-1, -1);
		margin.AddThemeConstantOverride("margin_left", 20);
		margin.AddThemeConstantOverride("margin_right", 20);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
	}

	public static void ApplyColumn(VBoxContainer column)
	{
		if (column != null) column.AddThemeConstantOverride("separation", 12);
	}

	public static void ApplySpeaker(Label speaker)
	{
		if (speaker == null) return;
		speaker.AddThemeColorOverride("font_color", SpeakerColor);
		speaker.AddThemeColorOverride("font_outline_color", Colors.Black);
		speaker.AddThemeConstantOverride("outline_size", 3);
		speaker.AddThemeFontSizeOverride("font_size", SpeakerSize);
		FontFile font = GD.Load<FontFile>("res://art/fonts/PressStart2P-Regular.ttf");
		if (font != null) speaker.AddThemeFontOverride("font", font);
	}

	public static void ApplyBody(Label body)
	{
		if (body == null) return;
		body.AddThemeColorOverride("font_color", BodyColor);
		body.AddThemeColorOverride("font_outline_color", Colors.Black);
		body.AddThemeConstantOverride("outline_size", 3);
		body.AddThemeFontSizeOverride("font_size", BodySize);
		body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
	}

	public static void ApplyAction(Button action)
	{
		if (action == null) return;
		action.CustomMinimumSize = new Vector2(action.CustomMinimumSize.X, 40);
		action.AddThemeFontSizeOverride("font_size", ActionSize);
	}

	/// <summary>Fits a narrative panel to the same bottom-wide safe area at every viewport size.</summary>
	public static void FitBottom(Control panel, Vector2 viewportSize, float preferredHeight = StandardHeight)
	{
		if (panel == null) return;
		float safeHeight = Mathf.Max(120f, viewportSize.Y - 32f);
		float height = Mathf.Min(preferredHeight, safeHeight);
		panel.AnchorLeft = 0.08f;
		panel.AnchorRight = 0.92f;
		panel.AnchorTop = 1f;
		panel.AnchorBottom = 1f;
		panel.OffsetLeft = 0;
		panel.OffsetRight = 0;
		panel.OffsetTop = -height - 16f;
		panel.OffsetBottom = -16f;
	}

	/// <summary>Fits image-led briefings in the center while using the shared panel style.</summary>
	public static void FitCentered(Control panel, Vector2 viewportSize, float preferredHeight = StandardHeight)
	{
		if (panel == null) return;
		float safeHeight = Mathf.Max(120f, viewportSize.Y - 32f);
		float height = Mathf.Min(Mathf.Max(preferredHeight, panel.GetCombinedMinimumSize().Y), safeHeight);
		panel.AnchorLeft = 0.08f;
		panel.AnchorRight = 0.92f;
		panel.AnchorTop = 0.5f;
		panel.AnchorBottom = 0.5f;
		panel.OffsetLeft = 0;
		panel.OffsetRight = 0;
		panel.OffsetTop = -height / 2f;
		panel.OffsetBottom = height / 2f;
	}
}
