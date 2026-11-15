namespace ares.scripts.mars;

using Godot;
using ares.scripts.mission;
using Ares.Systems.Audio;

/// <summary>One of the four door-access switches in the second building.</summary>
public partial class DoorSwitch : Interactable
{
	/// <summary>0-3. The matching index is stored in MissionState.</summary>
	[Export(PropertyHint.Range, "0,3,1")]
	public int SwitchIndex { get; set; }

	private AnimatedSprite2D _sprite;
	private Label _indexLabel;

	public override void _Ready()
	{
		base._Ready();
		_sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
		CreateIndexLabel();
		Apply(MissionState.Instance?.IsDoorAccessOn(SwitchIndex) ?? false);
		MissionState.Instance.DoorAccessChanged += OnDoorAccessChanged;
	}

	protected override void Interact()
	{
		if (MissionState.Instance == null)
			return;

		MissionState.Instance.SetDoorAccess(SwitchIndex, true);
		if (MissionState.Instance.AllDoorAccessOn)
			AudioManager.PlaySfx(SoundIds.OpenDoor);
		Apply(true);
	}

	private void Apply(bool on)
	{
		_sprite.Play(on ? "on" : "off");
		Enabled = !on;
		if (on)
			HidePrompt();
		else
			ShowPrompt();

		if (_indexLabel != null)
			_indexLabel.AddThemeColorOverride("font_color", on ? new Color("70ff8c") : Colors.White);
	}

	private void OnDoorAccessChanged(int index, bool on)
	{
		if (index == SwitchIndex) Apply(on);
	}

	public override void _ExitTree()
	{
		if (MissionState.Instance != null)
			MissionState.Instance.DoorAccessChanged -= OnDoorAccessChanged;
	}

	private void CreateIndexLabel()
	{
		_indexLabel = new Label
		{
			Text = (SwitchIndex + 1).ToString(),
			Position = new Vector2(-7, -54),
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		_indexLabel.AddThemeFontSizeOverride("font_size", 12);
		_indexLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
		_indexLabel.AddThemeConstantOverride("outline_size", 4);
		AddChild(_indexLabel);
	}
}
