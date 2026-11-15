using Ares.Systems.Audio;
using Godot;

namespace ares.scripts.ending;

public partial class Credits : Control
{
	private const string MainMenuScene = "res://scenes/main_menu/main_menu_take3.tscn";
	private const float ScrollSpeed = 38f;
	private VBoxContainer _roll;
	private Button _pauseButton;
	private Button _skipButton;
	private Button _menuButton;
	private bool _paused;
	private bool _finished;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Pausable;
		AudioManager.PlayMusic(SoundIds.MenuMusic, 1.0);
		BuildCredits();
		BuildControls();
		_roll.Position = new Vector2(0, GetViewportRect().Size.Y);
		_roll.Size = new Vector2(GetViewportRect().Size.X, Mathf.Max(1200, _roll.GetCombinedMinimumSize().Y));
		_pauseButton.GrabFocus();
	}

	public override void _Process(double delta)
	{
		if (_paused || _finished) return;
		_roll.Position += Vector2.Up * ScrollSpeed * (float)delta;
		if (_roll.Position.Y + _roll.Size.Y < 0) FinishCredits();
	}

	private void BuildCredits()
	{
		var background = new TextureRect
		{
			Name = "Background",
			Texture = GD.Load<Texture2D>("res://art/cinematics/Silent Mars Gateway.png"),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(background);

		var shade = new ColorRect
		{
			Name = "TextShade",
			Color = new Color(0.025f, 0.015f, 0.07f, 0.28f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(shade);

		_roll = new VBoxContainer
		{
			Name = "CreditRoll",
			AnchorRight = 1,
			Position = Vector2.Zero,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
			Theme = GD.Load<Theme>("res://scenes/main_menu/MenuTheme.tres"),
		};
		_roll.AddThemeConstantOverride("separation", 0);
		AddChild(_roll);

		AddCreditLabel("MISSION: ARES", 38, new Color(0.46f, 0.88f, 0.94f), 36);
		AddCreditLabel("C R E D I T S", 20, new Color(0.96f, 0.72f, 0.42f), 76);
		AddCreditLabel("PROJECT TEAM", 18, new Color(0.46f, 0.88f, 0.94f), 44);
		AddCreditLabel("Jose Miguel Ramirez", 14, Colors.White, 8);
		AddCreditLabel("Creative Director · Developer", 10, new Color(0.82f, 0.84f, 0.91f), 0);
		AddCreditLabel("Miguel Ceballos", 14, Colors.White, 12);
		AddCreditLabel("Developer · Architecture", 10, new Color(0.82f, 0.84f, 0.91f), 0);
		AddCreditLabel("Nikita Serna", 14, Colors.White, 12);
		AddCreditLabel("Digital Art · Developer · History Helper", 10, new Color(0.82f, 0.84f, 0.91f), 0);
		AddCreditLabel("Diego Alberto Pasaye", 14, Colors.White, 12);
		AddCreditLabel("Developer · Digital Art", 10, new Color(0.82f, 0.84f, 0.91f), 0);
		AddCreditLabel("Luis Fernando Nieto", 14, Colors.White, 12);
		AddCreditLabel("Developer · Writer", 10, new Color(0.82f, 0.84f, 0.91f), 0);
		AddCreditLabel("MUSIC & AUDIO", 18, new Color(0.46f, 0.88f, 0.94f), 44);
		AddCreditLabel("Sergio Alfredo Gutierrez · Music", 12, Colors.White, 8);
		AddCreditLabel("BigSoundBank — Joseph SARDIN", 12, Colors.White, 0);
		AddCreditLabel("Switches and electrical effects · CC0 1.0", 10, new Color(0.82f, 0.84f, 0.91f), 0);
		AddCreditLabel("bigsoundbank.com · CC0 1.0", 10, new Color(0.82f, 0.84f, 0.91f), 0);
		AddCreditLabel("ART & THIRD-PARTY RESOURCES", 18, new Color(0.46f, 0.88f, 0.94f), 44);
		AddCreditLabel("Digital Art · Nikita Serna · Diego Alberto Pasaye", 10, Colors.White, 8);
		AddCreditLabel("Free art assets from itch.io", 12, Colors.White, 8);
		AddCreditLabel("Generated placeholder icons · OpenAI · 2026", 10, Colors.White, 0);
		AddCreditLabel("Net.Codecrete.QrCodeGenerator", 12, Colors.White, 0);
		AddCreditLabel("Manuel Bleichenbacher · MIT License", 10, new Color(0.82f, 0.84f, 0.91f), 0);
		AddCreditLabel("THANK YOU FOR PLAYING", 20, new Color(0.96f, 0.72f, 0.42f), 36);
		AddCreditLabel("MISSION CONTROL OUT", 12, new Color(0.82f, 0.84f, 0.91f), 0);
	}

	private void AddCreditLabel(string text, int fontSize, Color color, int topSpacing)
	{
		if (topSpacing > 0)
		{
			_roll.AddChild(new Control { CustomMinimumSize = new Vector2(0, topSpacing), MouseFilter = MouseFilterEnum.Ignore });
		}
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			AutowrapMode = TextServer.AutowrapMode.Word,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_outline_color", new Color(0.025f, 0.015f, 0.07f, 1));
		label.AddThemeConstantOverride("outline_size", 5);
		_roll.AddChild(label);
	}

	private void BuildControls()
	{
		var controls = new HBoxContainer { Name = "Controls" };
		controls.SetAnchorsPreset(LayoutPreset.BottomRight);
		controls.OffsetLeft = -400;
		controls.OffsetTop = -62;
		controls.OffsetRight = -20;
		controls.OffsetBottom = -20;
		controls.AddThemeConstantOverride("separation", 10);
		AddChild(controls);

		_pauseButton = MakeButton("PAUSE", 120);
		_pauseButton.Pressed += TogglePause;
		controls.AddChild(_pauseButton);

		_skipButton = MakeButton("SKIP", 110);
		_skipButton.Pressed += FinishCredits;
		controls.AddChild(_skipButton);

		_menuButton = MakeButton("MAIN MENU", 140);
		_menuButton.Visible = false;
		_menuButton.Pressed += ReturnToMenu;
		controls.AddChild(_menuButton);
	}

	private static Button MakeButton(string text, float width)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(width, 40), FocusMode = FocusModeEnum.All };
		button.AddThemeFontSizeOverride("font_size", 11);
		return button;
	}

	private void TogglePause()
	{
		if (_finished) return;
		_paused = !_paused;
		_pauseButton.Text = _paused ? "RESUME" : "PAUSE";
	}

	private void FinishCredits()
	{
		if (_finished) return;
		_finished = true;
		_roll.Hide();
		_pauseButton.Hide();
		_skipButton.Hide();
		_menuButton.Show();
		_menuButton.GrabFocus();
	}

	private void ReturnToMenu() => GetTree().ChangeSceneToFile(MainMenuScene);

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
		if (key.Keycode == Key.Escape)
		{
			FinishCredits();
			GetViewport().SetInputAsHandled();
		}
		else if (key.Keycode is Key.Space or Key.P)
		{
			TogglePause();
			GetViewport().SetInputAsHandled();
		}
	}
}
