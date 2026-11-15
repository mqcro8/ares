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
    private ScrollContainer _bodyScroll;
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
    public float PanelHeight => _panel?.Size.Y ?? 0;
    public float PanelWidth => _panel?.Size.X ?? 0;
    public float MaximumPanelHeight { get; private set; }

    public override void _Ready()
    {
        _panel = GetNode<PanelContainer>("Panel");
        _bodyScroll = GetNode<ScrollContainer>("Panel/Margin/VBox/BodyScroll");
        _text = GetNode<Label>("Panel/Margin/VBox/BodyScroll/Text");
        _continue = GetNode<Button>("Panel/Margin/VBox/Continue");
        _finalScenes = GetNode<Control>("FinalScenes");
        _finalImage = GetNode<TextureRect>("FinalScenes/Image");
        _imageContinue = GetNode<Button>("FinalScenes/Continue");
        _continue.Pressed += Advance;
        _imageContinue.Pressed += Advance;
        _finalScenes.Hide();
        _panel.Hide();
        GetViewport().SizeChanged += FitPanel;
        FitPanel();
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
            FitPanel();
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
                FitPanel();
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
        FitPanel();
        _continue.GrabFocus();
    }

    private void FitPanel()
    {
        if (_panel == null || _bodyScroll == null || _text == null || _continue == null) return;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (viewport.X <= 0 || viewport.Y <= 0) return;

        float width = Mathf.Min(viewport.X - 32f, Mathf.Min(800f, viewport.X * 0.84f));
        width = Mathf.Max(240f, width);
        _panel.AnchorLeft = _panel.AnchorRight = 0.5f;
        _panel.AnchorTop = _panel.AnchorBottom = 1f;
        _panel.OffsetLeft = -width / 2f;
        _panel.OffsetRight = width / 2f;

        const float fixedContentHeight = 112f;
		MaximumPanelHeight = Mathf.Min(viewport.Y - 32f, viewport.Y * 0.55f);
		MaximumPanelHeight = Mathf.Max(150f, MaximumPanelHeight);
		float maxBodyHeight = Mathf.Max(32f, MaximumPanelHeight - fixedContentHeight);
        float maxTextWidth = Mathf.Max(120f, width - 40f);
        int maxCharacters = Mathf.Max(18, Mathf.FloorToInt(maxTextWidth / 11.5f));
        int estimatedLines = EstimateWrappedLines(_text.Text, maxCharacters);
        float bodyContentHeight = Mathf.Max(24f, estimatedLines * 24f);
        float visibleBodyHeight = Mathf.Min(bodyContentHeight, maxBodyHeight);
		float panelHeight = Mathf.Min(MaximumPanelHeight, fixedContentHeight + visibleBodyHeight);

        _bodyScroll.CustomMinimumSize = new Vector2(0, visibleBodyHeight);
        _bodyScroll.CustomMaximumSize = new Vector2(-1, maxBodyHeight);
        _text.CustomMinimumSize = new Vector2(0, bodyContentHeight);
        _text.CustomMaximumSize = new Vector2(maxTextWidth, -1);
        _text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _continue.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        _panel.CustomMinimumSize = new Vector2(0, panelHeight);
        _panel.OffsetTop = -panelHeight - 16f;
        _panel.OffsetBottom = -16f;
    }

    private static int EstimateWrappedLines(string text, int maximumCharacters)
    {
        if (string.IsNullOrEmpty(text)) return 1;
        int lines = 0;
        foreach (string paragraph in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(paragraph)) { lines++; continue; }
            int lineLength = 0;
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                int wordLength = word.Length;
                if (lineLength == 0) lineLength = wordLength;
                else if (lineLength + 1 + wordLength <= maximumCharacters) lineLength += 1 + wordLength;
                else { lines++; lineLength = wordLength; }
                while (lineLength > maximumCharacters)
                {
                    lines++;
                    lineLength -= maximumCharacters;
                }
            }
            if (lineLength > 0) lines++;
        }
        return Mathf.Max(1, lines);
    }

    public override void _ExitTree()
    {
        if (GetViewport() != null) GetViewport().SizeChanged -= FitPanel;
        _playerLock.Release();
    }
}
