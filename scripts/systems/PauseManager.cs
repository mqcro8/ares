using Godot;
using Ares.Systems.Audio;

namespace ares.scripts.systems;

// Autoload global: pausa el arbol con Esc usando la escena de opciones existente.
public partial class PauseManager : Node
{
	private const string OptionsScenePath = "res://scenes/main_menu/Options.tscn";
	private const string VolumeScenePath = "res://scenes/main_menu/Volume_menu.tscn";
	private const string ControlsScenePath = "res://scenes/main_menu/Control_menu.tscn";

	private static readonly string[] BlockedScenes =
	{
		"res://scenes/main_menu/main_menu_take3.tscn",
		"res://scenes/main_menu/MenuShell.tscn",
		"res://scenes/main_menu/Options.tscn",
		"res://scenes/main_menu/Volume_menu.tscn",
		"res://scenes/main_menu/Control_menu.tscn"
	};

	private CanvasLayer _layer;
	private Control _optionsScreen;
	private Control _subScreen;
	private bool _isOpen;
	private Input.MouseModeEnum _savedMouseMode;
	public bool IsOpen => _isOpen;

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		_layer = new CanvasLayer
		{
			Name = "PauseLayer",
			Layer = 100,
			ProcessMode = ProcessModeEnum.Always
		};
		AddChild(_layer);
	}

	public override void _ExitTree()
	{
		if (_isOpen)
		{
			var tree = GetTree();
			if (tree != null) tree.Paused = false;
			AudioManager.SetMusicPaused(false);
			Input.MouseMode = _savedMouseMode;
			_isOpen = false;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!@event.IsActionPressed("ui_cancel")) return;

		if (_isOpen)
		{
			if (_subScreen != null)
				BackToOptions();
			else
				Close();
		}
		else
		{
			Open();
		}

		GetViewport().SetInputAsHandled();
	}

	private void Open()
	{
		if (_isOpen || IsBlockedScene()) return;

		var screen = LoadScreen(OptionsScenePath);
		if (screen == null) return;

		_savedMouseMode = Input.MouseMode;
		Input.MouseMode = Input.MouseModeEnum.Visible;

		_isOpen = true;
		GetTree().Paused = true;
		AudioManager.SetMusicPaused(true);

		_optionsScreen = screen;
		_layer.AddChild(screen);
		ConfigureOptions(screen);
		ShowScreen(screen);
	}

	public void Close()
	{
		if (!_isOpen) return;
		_isOpen = false;

		DestroyScreen(_subScreen);
		_subScreen = null;

		DestroyScreen(_optionsScreen);
		_optionsScreen = null;

		GetTree().Paused = false;
		AudioManager.SetMusicPaused(false);
		Input.MouseMode = _savedMouseMode;
	}

	public void OpenPauseMenu() => Open();

	private void OpenSub(string path)
	{
		if (!_isOpen) return;

		DestroyScreen(_subScreen);
		_subScreen = null;

		var screen = LoadScreen(path);
		if (screen == null) return;

		_subScreen = screen;
		_layer.AddChild(screen);
		ConfigureSub(screen);
		ShowScreen(screen);
	}

	private void BackToOptions()
	{
		DestroyScreen(_subScreen);
		_subScreen = null;

		if (_optionsScreen != null)
			ShowScreen(_optionsScreen);
	}

	private void ShowScreen(Control screen)
	{
		if (_optionsScreen != null)
			_optionsScreen.Visible = screen == _optionsScreen;
		if (_subScreen != null)
			_subScreen.Visible = screen == _subScreen;

		screen.Visible = true;
		screen.ProcessMode = ProcessModeEnum.Always;
	}

	private void ConfigureOptions(Control screen)
	{
		if (screen.GetNodeOrNull<Label>("Panel/Panel2/Label") is { } label)
			label.Text = "PAUSA";

		if (screen.GetNodeOrNull<Button>("Panel/Panel2/Back") is { } back)
		{
			ClearPressed(back);
			back.Text = "Reanudar";
			back.Pressed += Close;
		}

		if (screen.GetNodeOrNull<Button>("Panel/Panel2/Volume") is { } volume)
		{
			ClearPressed(volume);
			volume.Pressed += () => OpenSub(VolumeScenePath);
		}

		if (screen.GetNodeOrNull<Button>("Panel/Panel2/Controls") is { } controls)
		{
			ClearPressed(controls);
			controls.Pressed += () => OpenSub(ControlsScenePath);
		}
	}

	private void ConfigureSub(Control screen)
	{
		if (screen.GetNodeOrNull<Button>("Panel/Panel2/Back") is { } back)
		{
			ClearPressed(back);
			back.Pressed += BackToOptions;
		}
	}

	private static void ClearPressed(Button button)
	{
		foreach (var connection in button.GetSignalConnectionList("pressed"))
		{
			var callable = connection["callable"].AsCallable();
			if (callable.Target != null)
				button.Disconnect("pressed", callable);
		}
	}

	private static Control LoadScreen(string path)
	{
		var packed = GD.Load<PackedScene>(path);
		if (packed == null)
		{
			GD.PushError($"PauseManager: no se pudo cargar \"{path}\".");
			return null;
		}

		return packed.Instantiate<Control>();
	}

	private static void DestroyScreen(Control screen)
	{
		if (screen == null) return;
		screen.Visible = false;
		screen.QueueFree();
	}

	private bool IsBlockedScene()
	{
		var scene = GetTree().CurrentScene;
		if (scene == null) return true;

		string path = scene.SceneFilePath;
		foreach (string blocked in BlockedScenes)
			if (path == blocked) return true;

		return false;
	}
}
