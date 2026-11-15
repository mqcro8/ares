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
		CustomMinimumSize = new Vector2(0, DialoguePanelStyle.StandardHeight);
		DialoguePanelStyle.ApplyPanel(this);
		FitToViewport();
		GetViewport().SizeChanged += FitToViewport;

		var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
		DialoguePanelStyle.ApplyMargins(margin);
		AddChild(margin);
		var column = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
		DialoguePanelStyle.ApplyColumn(column);
		margin.AddChild(column);

		_speaker = new Label
		{
			Name = "Speaker",
			Text = "MISSION CONTROL",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		DialoguePanelStyle.ApplySpeaker(_speaker);
		column.AddChild(_speaker);

		_text = new Label
		{
			Name = "Text",
			Text = "",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			SizeFlagsVertical = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		DialoguePanelStyle.ApplyBody(_text);
		column.AddChild(_text);
	}

	private void FitToViewport() => DialoguePanelStyle.FitBottom(this, GetViewportRect().Size);

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

	public override void _ExitTree()
	{
		if (GetViewport() != null) GetViewport().SizeChanged -= FitToViewport;
		_tween?.Kill();
	}
}
