namespace ares.scripts.dialogue;

using Godot;

public partial class DialogueView : PanelContainer
{
	private Label _speaker;
	private Label _text;
	private Tween _tween;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		CustomMinimumSize = new Vector2(0, 150);

		var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 6);
		AddChild(column);

		_speaker = new Label
		{
			Name = "Speaker",
			Text = "MISSION CONTROL",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_speaker.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 0.9f));
		_speaker.AddThemeFontSizeOverride("font_size", 12);
		column.AddChild(_speaker);

		_text = new Label
		{
			Name = "Text",
			Text = "",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_text.AddThemeColorOverride("font_color", new Color(0.94f, 0.95f, 0.97f));
		_text.AddThemeFontSizeOverride("font_size", 20);
		column.AddChild(_text);
	}

	public void ShowLine(DialogueLine line, bool animate = true)
	{
		if (line == null) return;
		_speaker.Text = line.Speaker;
		_text.Text = line.Text;
		Visible = true;
		_tween?.Kill();
		if (!animate || !IsInsideTree())
		{
			Modulate = Colors.White;
			return;
		}
		Modulate = new Color(1, 1, 1, 0);
		_tween = CreateTween();
		_tween.TweenProperty(this, "modulate:a", 1.0f, 0.22f);
	}

	public void Clear()
	{
		_tween?.Kill();
		_tween = null;
		Visible = false;
	}
}
