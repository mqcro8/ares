namespace ares.scripts.gateway;

using Godot;
using System.Collections.Generic;
using System;
using ares.scripts.player;
using ares.scripts.training;

/// <summary>Training-style modal transmission with one focused Continue input route.</summary>
public partial class GatewayMissionControl : CanvasLayer
{
    private readonly Queue<string> _messages = new();
    private readonly TrainingPlayerLock _playerLock = new();
    private PanelContainer _panel;
    private Label _text;
    private Button _continue;
    private Player _player;
    private TrainingStations _stations;
    private Control _finalScenes;
    private TextureRect _finalImage;
    private Button _imageContinue;
    private bool _completionPending, _endingComplete;
    internal Func<string, Error> SceneLoader { get; set; }
    [Export] public Texture2D FirstImage { get; set; }
    [Export] public Texture2D SecondImage { get; set; }
    public int EndingFrame { get; private set; }
    public bool IsOpen => _panel?.Visible == true || EndingFrame > 0;
    public string Message => _text?.Text ?? "";

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("Panel");
        _text = GetNode<Label>("Panel/Margin/VBox/Text");
        _continue = GetNode<Button>("Panel/Margin/VBox/Continue");
        _finalScenes = GetNode<Control>("FinalScenes");
        _finalImage = GetNode<TextureRect>("FinalScenes/Image");
        _imageContinue = GetNode<Button>("FinalScenes/Continue");
        _continue.Pressed += Advance;
        _imageContinue.Pressed += Advance;
        _finalScenes.Hide();
        _panel.Hide();
    }

    public void Bind(Player player, TrainingStations stations)
    {
        _player = player;
        _stations = stations;
    }

    public void Enqueue(string message)
    {
        if (string.IsNullOrEmpty(message)) return;
        _messages.Enqueue(message);
        if (!IsOpen)
        {
            _playerLock.Acquire(_player);
            _stations.InteractionLocked = true;
            ShowNext();
        }
    }

    public void Advance()
    {
        if (_endingComplete || !IsOpen) return;
        if (EndingFrame == 1)
        {
            EndingFrame = 2;
            _finalImage.Texture = SecondImage;
            _imageContinue.Hide();
            _imageContinue.ReleaseFocus();
            _text.Text = GatewayMission.ConnectionRecovered;
            _panel.Show();
            _continue.GrabFocus();
            return;
        }
        if (EndingFrame == 2)
        {
            var error = SceneLoader != null
                ? SceneLoader("res://scenes/rover/rover.tscn")
                : GetTree().ChangeSceneToFile("res://scenes/rover/rover.tscn");
            if (error != Error.Ok)
            {
                _text.Text = "Unable to transfer control to the rover. Continue to retry.";
                _continue.Text = "Retry transfer";
                _panel.Show();
                _continue.GrabFocus();
                return;
            }
            _endingComplete = true;
            _panel.Hide();
            _continue.ReleaseFocus();
            return;
        }
        if (_completionPending)
        {
            _completionPending = false;
            EndingFrame = 1;
            _panel.Hide();
            _continue.ReleaseFocus();
            _finalImage.Texture = FirstImage;
            _finalScenes.Show();
            _imageContinue.Show();
            _imageContinue.GrabFocus();
            return;
        }
        if (_messages.Count > 0) { ShowNext(); return; }
        _panel.Hide();
        _continue.ReleaseFocus();
        _playerLock.Release();
        _stations.InteractionLocked = false;
    }

    public bool SkipToRover()
    {
        if (_endingComplete) return false;
        _messages.Clear();
        _completionPending = false;
        EndingFrame = 2;
        _finalImage.Texture = SecondImage;
        _finalScenes.Show();
        _imageContinue.Hide();
        _panel.Hide();
        Advance();
        return true;
    }

    private void ShowNext()
    {
        _text.Text = _messages.Dequeue();
        _completionPending = _text.Text == GatewayMission.Complete;
        _panel.Show();
        _continue.GrabFocus();
    }

    public override void _ExitTree() => _playerLock.Release();
}
