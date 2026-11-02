namespace ares.scripts.training;

using Godot;

/// <summary>Scene-local briefing; owns only the player's temporary physics lock.</summary>
public partial class TrainingIntro : Node2D
{
	[Export] public string AstronautName { get; set; } = "Comandante";
	[Export] public NodePath PlayerPath { get; set; } = "Tilemaps_layers/Player";
	[Export] public NodePath PanelPath { get; set; } = "IntroUI/Panel";
	[Export] public NodePath SpeakerPath { get; set; } = "IntroUI/Panel/Margin/VBox/Speaker";
	[Export] public NodePath TextPath { get; set; } = "IntroUI/Panel/Margin/VBox/Text";
	[Export] public NodePath ContinuePath { get; set; } = "IntroUI/Panel/Margin/VBox/Continue";

	private CharacterBody2D _player;
	private PanelContainer _panel;
	private Label _speaker;
	private Label _text;
	private Button _continue;
	private bool _previousPhysicsProcessing;
	private bool _ownsPhysicsLock;
	private bool _active;
	private int _line;
	private ulong _lastAdvanceFrame = ulong.MaxValue;

	public override void _Ready()
	{
		_player = GetNodeOrNull<CharacterBody2D>(PlayerPath);
		_panel = GetNodeOrNull<PanelContainer>(PanelPath);
		_speaker = GetNodeOrNull<Label>(SpeakerPath);
		_text = GetNodeOrNull<Label>(TextPath);
		_continue = GetNodeOrNull<Button>(ContinuePath);
		if (_player == null || _panel == null || _speaker == null || _text == null || _continue == null)
		{
			GD.PushError("TrainingIntro: missing player or briefing UI; movement was not locked.");
			_panel?.Hide();
			return;
		}

		_previousPhysicsProcessing = _player.IsPhysicsProcessing();
		_ownsPhysicsLock = true;
		_player.SetPhysicsProcess(false);
		_continue.Pressed += Advance;
		_active = true;
		ShowLine();
		_panel.Show();
		_continue.GrabFocus();
	}

	private void ShowLine()
	{
		_speaker.Text = _line == 1 ? "PROTAGONISTA" : "CONTROL";
		_text.Text = _line switch
		{
			0 => "Bienvenido, [Nombre]. Nuestra Gateway lleva dos semanas en silencio y tú vas a ser quien la reactive. Antes de salir, necesitas dominar tu cuerpo, tu equipo y tu nave."
				.Replace("[Nombre]", AstronautName),
			1 => "Entendido. Empecemos.",
			_ => "Aquí aprenderás a moverte. Los ejercicios son solo práctica, nada es permanente."
		};
		_continue.Text = _line == 2 ? "Iniciar entrenamiento" : "Continuar";
	}

	private void Advance()
	{
		// The focused Button is the only input route: no parallel ui_accept handler.
		ulong frame = Engine.GetProcessFrames();
		if (!_active || frame == _lastAdvanceFrame)
			return;
		_lastAdvanceFrame = frame;
		_line++;
		if (_line < 3)
		{
			ShowLine();
			return;
		}

		_active = false;
		_continue.ReleaseFocus();
		_panel.Hide();
		RestorePhysics();
	}

	private void RestorePhysics()
	{
		if (!_ownsPhysicsLock)
			return;
		if (GodotObject.IsInstanceValid(_player))
			_player.SetPhysicsProcess(_previousPhysicsProcessing);
		_ownsPhysicsLock = false;
	}

	public override void _ExitTree()
	{
		if (GodotObject.IsInstanceValid(_continue))
			_continue.Pressed -= Advance;
		_active = false;
		RestorePhysics();
	}
}
