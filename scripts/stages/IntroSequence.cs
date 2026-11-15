namespace ares.scripts.stages;

using System;
using System.Collections.Generic;
using Ares.Systems.Audio;
using ares.scripts.dialogue;
using ares.scripts.mission;
using Godot;

public partial class IntroSequence : Control, ares.scripts.systems.IDevSceneSkipTarget
{
	[Export] public string NextScene { get; set; } = "res://scenes/training/Training.tscn";
	[Export] public DialogueSequence Transmission { get; set; }

	public Func<string, Error> SceneLoader { get; set; }
	public bool Finished { get; private set; }
	public bool CanSkipWithDevCode => IsInsideTree() && !Finished && !_transitionFailed;
	public bool Skipped { get; private set; }
	public string LoadedScene { get; private set; }
	public double Time { get; private set; }
	public int BackdropIndex { get; private set; }
	public int BackdropCount => _backdrops.Count;

	private const double Duration = 23.0;
	private readonly List<(double At, Action Action)> _events = new();
	private readonly List<Label> _telemetry = new();
	private readonly List<Texture2D> _backdrops = new();
	private int _nextEvent;
	private bool _paused;
	private bool _started;
	private bool _backdropAIsFront = true;
	private TextureRect _backdropA;
	private TextureRect _backdropB;
	private Label _telemetryTitle;
	private Label _signalLost;
	private DialogueView _dialogue;
	private Control _objective;
	private Control _title;
	private ColorRect _fade;
	private AudioStreamPlayer _wind;
	private Tween _fadeTween;
	private PanelContainer _loadFailure;
	private bool _transitionFailed;
	private ares.scripts.systems.PauseManager _pauseManager;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		AudioManager.StopMusic(1.0);
		MissionState.Instance.Stage = MissionStage.Intro;
		if (Transmission == null)
			Transmission = GD.Load<DialogueSequence>("res://data/dialogue/intro_transmission.tres");
		LoadBackdrops();
		BuildUi();
		BuildTimeline();
		PlayWind();
		_pauseManager = GetNodeOrNull<ares.scripts.systems.PauseManager>("/root/PauseManager");
		if (_pauseManager != null) _pauseManager.PauseChanged += OnPauseChanged;
		_started = true;
	}

	public override void _Process(double delta)
	{
		if (_started && !_paused && !Finished)
			Advance(delta);
	}

	public void Advance(double delta)
	{
		if (Finished || _transitionFailed || delta <= 0) return;
		Time += delta;
		while (_nextEvent < _events.Count && Time >= _events[_nextEvent].At)
		{
			var next = _events[_nextEvent++];
			next.Action?.Invoke();
			if (Finished) return;
		}
		if (Time >= Duration) Finish(false);
	}

	public void Skip()
	{
		Finish(true);
	}

	public bool SkipWithDevCode()
	{
		if (!CanSkipWithDevCode) return false;
		Skip();
		return Finished;
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;
		if (Time < 0.35) return;
		Key code = key.Keycode != Key.None ? key.Keycode : key.PhysicalKeycode;
		if (code is Key.Escape or Key.E or Key.Space or Key.Enter)
			Skip();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMWindowFocusOut) _paused = true;
		else if (what == NotificationWMWindowFocusIn) _paused = false;
	}

	private void Finish(bool skipped)
	{
		if (Finished) return;
		var error = SceneLoader != null ? SceneLoader(NextScene) : GetTree().ChangeSceneToFile(NextScene);
		if (error == Error.Ok)
		{
			Finished = true;
			Skipped = skipped;
			_wind?.Stop();
			LoadedScene = NextScene;
			MissionState.Instance.CompleteIntro();
			return;
		}
		_transitionFailed = true;
		if (_loadFailure != null) return;
		_loadFailure = new PanelContainer { ZIndex = 100 };
		AddChild(_loadFailure);
		_loadFailure.SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
		_loadFailure.Position -= new Vector2(170, 50);
		var column = new VBoxContainer();
		_loadFailure.AddChild(column);
		column.AddChild(new Label { Text = "Unable to open training. Please try again." });
		var retry = new Button { Text = "Retry connection" };
		column.AddChild(retry);
		retry.Pressed += () => Finish(skipped);
		retry.GrabFocus();
	}

	private void OnPauseChanged(bool paused)
	{
		if (!paused) _wind?.Stop();
	}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(_pauseManager)) _pauseManager.PauseChanged -= OnPauseChanged;
	}

	private void BuildUi()
	{
		_backdropA = MakeBackdrop("BackdropA");
		_backdropB = MakeBackdrop("BackdropB");
		_backdropA.Texture = _backdrops[0];
		_backdropA.Modulate = Colors.White;
		_backdropB.Modulate = new Color(1, 1, 1, 0);
		_backdropB.Hide();
		AddChild(_backdropA);
		AddChild(_backdropB);
		AddChild(MakeScreenOverlay());
		AddChild(MakeTelemetry());
		AddChild(MakeSignalLost());

		_dialogue = new DialogueView { Name = "Dialogue", Visible = false };
		AddChild(_dialogue);

		_objective = MakeObjectives();
		AddChild(_objective);

		_fade = new ColorRect
		{
			Name = "Fade",
			Color = new Color(0, 0, 0, 0),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		AddChild(_fade);

		_title = MakeTitleCard();
		AddChild(_title);
		AddChild(MakeSkipButton());
	}

	private void BuildTimeline()
	{
		_events.Add((0.2, () => FadeIn(_telemetryTitle, 0.3)));
		_events.Add((0.6, () => ShowTelemetry(0)));
		_events.Add((1.0, () => ShowTelemetry(1)));
		_events.Add((1.4, () => ShowTelemetry(2)));
		_events.Add((1.8, () => ShowTelemetry(3)));
		_events.Add((2.1, () => ShowTelemetry(4)));
		_events.Add((2.4, () => ShowTelemetry(5)));
		_events.Add((2.7, () => ShowTelemetry(6)));
		_events.Add((3.2, ShowSignalLost));
		_events.Add((3.8, () => ShowDialogue(0)));
		_events.Add((5.2, () => SetBackdrop(1)));
		_events.Add((5.6, () => ShowDialogue(1)));
		_events.Add((7.2, () => ShowDialogue(2)));
		_events.Add((8.8, () => SetBackdrop(2)));
		_events.Add((9.2, () => ShowDialogue(3)));
		_events.Add((11.4, () => ShowDialogue(4)));
		_events.Add((13.0, () => SetBackdrop(3)));
		_events.Add((13.4, () => ShowDialogue(5)));
		_events.Add((15.0, ShowObjectives));
		_events.Add((17.8, () =>
		{
			_objective.Hide();
			_signalLost.Hide();
		}));
		_events.Add((18.0, () => FadeToBlack(1.2)));
		_events.Add((19.2, ShowTitleCard));
	}

	private void LoadBackdrops()
	{
		var fallback = GD.Load<Texture2D>("res://art/gateway/background/starry sky.jpg");
		foreach (string path in new[]
		{
			"res://art/cinematics/Silent Mars Gateway.png",
			"res://art/cinematics/two sond.png",
			"res://art/cinematics/Rustlit Mars Workshop with Cyan Holograms.png",
			"res://art/cinematics/Indigo Ares launch pad.png",
		})
		{
			var texture = GD.Load<Texture2D>(path) ?? fallback;
			if (texture != null) _backdrops.Add(texture);
		}

		if (_backdrops.Count == 0)
			_backdrops.Add(fallback);
	}

	private static TextureRect MakeBackdrop(string name)
	{
		var backdrop = new TextureRect
		{
			Name = name,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		return backdrop;
	}

	private static ColorRect MakeScreenOverlay()
	{
		var overlay = new ColorRect
		{
			Name = "ScreenOverlay",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		if (DisplayServer.GetName() == "headless")
		{
			overlay.Color = new Color(0, 0, 0, 0);
			return overlay;
		}
		var shader = new Shader
		{
			Code = @"
shader_type canvas_item;
void fragment() {
    vec2 uv = UV;
    vec2 centered = uv - vec2(0.5);
    float vignette = 1.0 - smoothstep(0.18, 0.82, length(centered));
    float line = step(0.5, fract(uv.y * 220.0));
    float sweep = 1.0 - smoothstep(0.0, 0.035, abs(uv.y - fract(TIME * 0.06)));
    float flicker = 0.9 + 0.1 * sin(TIME * 43.0);
    float alpha = (1.0 - vignette) * 0.55 + line * 0.07 + sweep * 0.025;
    COLOR = vec4(0.0, 0.008, 0.02, alpha * flicker);
}"
		};
		overlay.Material = new ShaderMaterial { Shader = shader };
		return overlay;
	}

	private Control MakeTelemetry()
	{
		var panel = new VBoxContainer
		{
			Name = "Telemetry",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		panel.OffsetLeft = 32;
		panel.OffsetTop = 28;
		panel.OffsetRight = 640;
		panel.OffsetBottom = 280;
		panel.AddThemeConstantOverride("separation", 4);

		_telemetryTitle = new Label
		{
			Name = "TelemetryTitle",
			Text = "GATEWAY RELAY // 2067",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_telemetryTitle.AddThemeColorOverride("font_color", new Color(0.55f, 0.9f, 1f));
		_telemetryTitle.AddThemeFontSizeOverride("font_size", 14);
		_telemetryTitle.Modulate = new Color(1, 1, 1, 0);
		panel.AddChild(_telemetryTitle);

		var rule = new ColorRect
		{
			Color = new Color(0.55f, 0.9f, 1f, 0.35f),
			CustomMinimumSize = new Vector2(0, 1),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		panel.AddChild(rule);

		foreach (string text in new[]
		{
			"GATEWAY BASE // MARS",
			"LAST CONTACT: 14 DAYS AGO",
			"LAUNCH WINDOW: 06H",
			"PROBE 01: LOST",
			"PROBE 02: LOST",
			"CREW: 01",
			"ROLE: SYSTEMS ENGINEER // FIRST DEEP-SPACE FLIGHT",
		})
		{
			var label = new Label { Text = text, MouseFilter = MouseFilterEnum.Ignore };
			label.AddThemeColorOverride("font_color", text.Contains("LOST")
				? new Color(1f, 0.45f, 0.38f)
				: new Color(0.72f, 0.84f, 0.9f));
			label.AddThemeFontSizeOverride("font_size", 13);
			label.Modulate = new Color(1, 1, 1, 0);
			panel.AddChild(label);
			_telemetry.Add(label);
		}
		return panel;
	}

	private Label MakeSignalLost()
	{
		_signalLost = new Label
		{
			Name = "SignalLost",
			Text = "SIGNAL LOST",
			Visible = false,
			HorizontalAlignment = HorizontalAlignment.Right,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_signalLost.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		_signalLost.OffsetLeft = -260;
		_signalLost.OffsetTop = 80;
		_signalLost.OffsetRight = -32;
		_signalLost.OffsetBottom = 108;
		_signalLost.AddThemeColorOverride("font_color", new Color(1f, 0.38f, 0.32f));
		_signalLost.AddThemeFontSizeOverride("font_size", 13);
		return _signalLost;
	}

	private static Control MakeObjectives()
	{
		var panel = new PanelContainer
		{
			Name = "Objectives",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.03f, 0.06f, 0.94f),
			BorderColor = new Color(0.55f, 0.9f, 1f, 0.8f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 3,
			CornerRadiusTopRight = 3,
			CornerRadiusBottomLeft = 3,
			CornerRadiusBottomRight = 3,
			ContentMarginLeft = 32,
			ContentMarginTop = 24,
			ContentMarginRight = 32,
			ContentMarginBottom = 24,
		};
		panel.AddThemeStyleboxOverride("panel", style);
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.OffsetLeft = -280;
		panel.OffsetRight = 280;
		panel.OffsetTop = -110;
		panel.OffsetBottom = 190;

		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 8);
		panel.AddChild(column);

		var heading = new Label
		{
			Text = "PRIMARY OBJECTIVES",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		heading.AddThemeColorOverride("font_color", new Color(0.55f, 0.9f, 1f));
		heading.AddThemeFontSizeOverride("font_size", 12);
		column.AddChild(heading);

		var title = new Label
		{
			Text = "MISSION: ARES",
			HorizontalAlignment = HorizontalAlignment.Center,
			ThemeTypeVariation = "MenuHeading",
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		title.AddThemeColorOverride("font_color", new Color(0.98f, 0.94f, 0.86f));
		title.AddThemeFontSizeOverride("font_size", 26);
		column.AddChild(title);

		var rule = new ColorRect
		{
			Color = new Color(0.8f, 0.65f, 0.4f, 0.85f),
			CustomMinimumSize = new Vector2(72, 2),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		column.AddChild(rule);

		foreach (string text in new[] { "REACH MARS", "DIAGNOSE THE FAILURE", "RESTORE THE LINK", "RETURN HOME" })
		{
			var label = new Label
			{
				Text = text,
				HorizontalAlignment = HorizontalAlignment.Center,
				MouseFilter = Control.MouseFilterEnum.Ignore,
			};
			label.AddThemeColorOverride("font_color", new Color(0.9f, 0.92f, 0.96f));
			label.AddThemeFontSizeOverride("font_size", 18);
			column.AddChild(label);
		}
		return panel;
	}

	private static Control MakeTitleCard()
	{
		var column = new VBoxContainer
		{
			Name = "TitleCard",
			Visible = false,
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		column.SetAnchorsPreset(Control.LayoutPreset.Center);
		column.OffsetLeft = -420;
		column.OffsetRight = 420;
		column.OffsetTop = -170;
		column.OffsetBottom = 170;
		column.AddThemeConstantOverride("separation", 12);

		var title = new Label
		{
			Text = "MISSION: ARES",
			HorizontalAlignment = HorizontalAlignment.Center,
			ThemeTypeVariation = "MenuTitle",
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		column.AddChild(title);

		var rule = new ColorRect
		{
			Color = new Color(0.8f, 0.65f, 0.4f, 0.85f),
			CustomMinimumSize = new Vector2(72, 2),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		column.AddChild(rule);

		var line = new Label
		{
			Text = "Bring Gateway back online.",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		line.AddThemeColorOverride("font_color", new Color(0.9f, 0.92f, 0.96f));
		line.AddThemeFontSizeOverride("font_size", 22);
		column.AddChild(line);

		var year = new Label
		{
			Text = "MARS // 2067",
			HorizontalAlignment = HorizontalAlignment.Center,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		year.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 0.9f));
		year.AddThemeFontSizeOverride("font_size", 12);
		column.AddChild(year);
		return column;
	}

	private Button MakeSkipButton()
	{
		var skip = new Button
		{
			Name = "Skip",
			Text = "SKIP  [ESC]",
			FocusMode = Control.FocusModeEnum.None,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		skip.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		skip.OffsetLeft = -190;
		skip.OffsetTop = 22;
		skip.OffsetRight = -28;
		skip.OffsetBottom = 66;
		skip.AddThemeFontSizeOverride("font_size", 12);
		skip.AddThemeColorOverride("font_color", new Color(0.8f, 0.85f, 0.9f));
		skip.Modulate = new Color(1, 1, 1, 0.75f);
		skip.Pressed += Skip;
		return skip;
	}

	private void ShowTelemetry(int index)
	{
		if (index < 0 || index >= _telemetry.Count) return;
		FadeIn(_telemetry[index], 0.18);
	}

	private void SetBackdrop(int index)
	{
		if (index < 0 || index >= _backdrops.Count || index == BackdropIndex) return;
		var incoming = _backdropAIsFront ? _backdropB : _backdropA;
		var outgoing = _backdropAIsFront ? _backdropA : _backdropB;
		incoming.Texture = _backdrops[index];
		incoming.Modulate = new Color(1, 1, 1, 0);
		incoming.Show();

		var tween = CreateTween();
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.TweenProperty(incoming, "modulate:a", 1.0f, 0.8);
		tween.Parallel().TweenProperty(outgoing, "modulate:a", 0.0f, 0.8);
		tween.TweenCallback(Callable.From(() => outgoing.Hide()));

		_backdropAIsFront = !_backdropAIsFront;
		BackdropIndex = index;
	}

	private void ShowSignalLost()
	{
		_signalLost.Show();
		_signalLost.Modulate = new Color(1, 0.35f, 0.3f, 0);
		var tween = CreateTween().SetLoops();
		tween.TweenProperty(_signalLost, "modulate:a", 0.25f, 0.7).SetTrans(Tween.TransitionType.Sine);
		tween.TweenProperty(_signalLost, "modulate:a", 1.0f, 0.7).SetTrans(Tween.TransitionType.Sine);
	}

	private void ShowDialogue(int index)
	{
		if (Transmission?.Lines == null || index < 0 || index >= Transmission.Lines.Count) return;
		_dialogue.ShowLine(Transmission.Lines[index]);
	}

	private void ShowObjectives()
	{
		_dialogue.Clear();
		_objective.Show();
		FadeIn(_objective, 0.5);
	}

	private void ShowTitleCard()
	{
		_title.Show();
		FadeIn(_title, 0.8);
	}

	private void FadeIn(CanvasItem target, double duration)
	{
		if (target == null) return;
		target.Modulate = new Color(1, 1, 1, 0);
		var tween = CreateTween();
		tween.TweenProperty(target, "modulate:a", 1.0f, duration);
	}

	private void FadeToBlack(double duration)
	{
		_fadeTween?.Kill();
		_fadeTween = CreateTween();
		_fadeTween.TweenProperty(_fade, "color:a", 1.0f, duration);
	}

	private void PlayWind()
	{
		if (DisplayServer.GetName() == "headless") return;
		var stream = GD.Load<AudioStream>("res://audio/music/rover_scene_ambient_wind.mp3");
		if (stream == null) return;
		if (stream is AudioStreamMP3 mp3) mp3.Loop = true;
		_wind = new AudioStreamPlayer
		{
			Name = "Wind",
			Stream = stream,
			Bus = AudioManager.MusicBusName,
			ProcessMode = ProcessModeEnum.Always,
			VolumeDb = -24f,
		};
		AddChild(_wind);
		_wind.Play();
		var tween = CreateTween();
		tween.TweenProperty(_wind, "volume_db", -18f, 2.0);
	}
}
