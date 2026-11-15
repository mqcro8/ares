using Ares.Systems.Audio;
using Ares.Systems.Controllers;
using Godot;

namespace Ares.Ui.MainMenu;

public partial class ControlsMenu : Control, ares.scripts.systems.IDevSceneSkipTarget
{
	public bool CanSkipWithDevCode => IsInsideTree();
	private ControllerRelay _relay;
	private CheckButton _toggle;
	private Label _status;
	private TextureRect _qr;
	private Label _room;
	private Label _join;
	private LineEdit _relayInput;
	private Button _apply;
	private Button _newCode;
	private Label _hint;
	private string _qrUrl = "";
	private ImageTexture _qrTexture;
	private bool _refreshing;
	private readonly FeedbackSounds _sounds = new();

	public override void _Ready()
	{
		AudioManager.PlayMusic(SoundIds.MenuMusic);
		UiNavigationSounds.Attach(this);

		_toggle = GetNodeOrNull<CheckButton>("Panel/Panel2/Controller/PhoneRow/PhoneToggle");
		_status = GetNodeOrNull<Label>("Panel/Panel2/Controller/PhoneRow/Status");
		_qr = GetNodeOrNull<TextureRect>("Panel/Panel2/Controller/Body/Qr");
		_room = GetNodeOrNull<Label>("Panel/Panel2/Controller/Body/Info/Room");
		_join = GetNodeOrNull<Label>("Panel/Panel2/Controller/Body/Info/Join");
		_relayInput = GetNodeOrNull<LineEdit>("Panel/Panel2/Controller/Body/Info/RelayRow/RelayInput");
		_apply = GetNodeOrNull<Button>("Panel/Panel2/Controller/Body/Info/RelayRow/Apply");
		_newCode = GetNodeOrNull<Button>("Panel/Panel2/Controller/Body/Info/NewCode");
		_hint = GetNodeOrNull<Label>("Panel/Panel2/Controller/Body/Info/Hint");

		_relay = ControllerRelay.Instance;
		if (_relay != null)
		{
			if (_relayInput != null) _relayInput.Text = _relay.RelayUrl;
			_relay.StatusChanged += Refresh;
		}

		if (_toggle != null) _toggle.Toggled += OnToggle;
		if (_apply != null) _apply.Pressed += OnApplyPressed;
		if (_newCode != null) _newCode.Pressed += OnNewCodePressed;

		Refresh();
	}

	public override void _ExitTree()
	{
		if (_relay != null) _relay.StatusChanged -= Refresh;
	}

	public void OnBackPressed()
	{
		_sounds.Play(GetTree().ChangeSceneToFile("res://scenes/main_menu/Options.tscn") == Error.Ok
			? SoundIds.UiBack : SoundIds.UiDenied, 300, true);
	}

	public bool SkipWithDevCode()
	{
		if (!CanSkipWithDevCode) return false;
		OnBackPressed();
		return true;
	}

	private void OnToggle(bool pressed)
	{
		if (_refreshing || _relay == null) return;
		_relay.SetEnabled(pressed);
		_sounds.Play(SoundIds.UiConfirm, 100, true);
		Refresh();
	}

	private void OnApplyPressed()
	{
		if (_relay == null || _relayInput == null) return;

		string normalized = ControllerRelay.NormalizeRelayUrl(_relayInput.Text);
		if (string.IsNullOrEmpty(normalized))
		{
			_sounds.Play(SoundIds.UiDenied, 300, true);
			_hint!.Text = "Enter your Vercel site address, for example https://your-project.vercel.app";
			_relayInput.GrabFocus();
			return;
		}

		_relayInput.Text = normalized;
		_relay.SetRelayUrl(normalized);
		_sounds.Play(SoundIds.UiConfirm, 100, true);
		Refresh();
	}

	private void OnNewCodePressed()
	{
		if (_relay != null) _sounds.Play(SoundIds.UiConfirm, 100, true);
		_relay?.NewRoomCode();
		Refresh();
	}

	private void Refresh()
	{
		if (!IsInsideTree() || _toggle == null) return;

		_refreshing = true;
		try
		{
			if (_relay == null)
			{
				_toggle.Disabled = true;
				_status!.Text = "Unavailable";
				_room!.Text = "ROOM  -----";
				_join!.Text = "Controller relay is not loaded.";
				_hint!.Text = "Restart the game to load the controller relay.";
				UpdateQr("");
				return;
			}

			bool hasRelay = !string.IsNullOrEmpty(_relay.RelayUrl);

			_toggle.Disabled = false;
			_toggle.SetPressedNoSignal(_relay.Enabled);
			_status!.Text = _relay.StatusText;
			_status.AddThemeColorOverride("font_color", StatusColor());

			_room!.Text = _relay.Enabled ? $"ROOM  {_relay.RoomCode}" : "ROOM  -----";
			_join!.Text = _relay.Enabled && hasRelay ? _relay.JoinUrl : "Enable the phone controller to pair.";
			_join.Visible = _relay.Enabled;
			_newCode!.Disabled = !_relay.Enabled;
			_apply!.Disabled = false;

			if (!_relay.Enabled)
				_hint!.Text = "Turn on the phone controller, then scan the QR code with your phone.";
			else if (!hasRelay)
				_hint!.Text = "Set your Vercel site address, then press Apply.";
			else if (_relay.State == ControllerLinkState.ControllerConnected)
				_hint!.Text = $"Phone linked in room {_relay.RoomCode}. Use the field pad or rover wheel.";
			else
				_hint!.Text = "Connect to the hosted relay, then scan the QR or open the link on your phone.";

			UpdateQr(_relay.Enabled && hasRelay ? _relay.JoinUrl : "");
		}
		finally
		{
			_refreshing = false;
		}
	}

	private Color StatusColor()
	{
		if (_relay == null) return new Color(1f, 0.38f, 0.32f);
		return _relay.State switch
		{
			ControllerLinkState.ControllerConnected => new Color(0.43f, 0.95f, 1f),
			ControllerLinkState.Connecting or ControllerLinkState.WaitingForController => new Color(0.8f, 0.65f, 0.4f),
			ControllerLinkState.Reconnecting => new Color(1f, 0.38f, 0.32f),
			_ => new Color(0.7f, 0.66f, 0.82f),
		};
	}

	private void UpdateQr(string url)
	{
		if (_qr == null) return;

		if (url == _qrUrl)
		{
			_qr.Visible = !string.IsNullOrEmpty(url);
			return;
		}

		_qrUrl = url;
		_qrTexture = null;
		_qr.Texture = null;

		if (string.IsNullOrEmpty(url))
		{
			_qr.Visible = false;
			return;
		}

		_qrTexture = ControllerQr.CreateTexture(url, 5, 3);
		_qr.Texture = _qrTexture;
		_qr.Visible = _qrTexture != null;
	}
}
