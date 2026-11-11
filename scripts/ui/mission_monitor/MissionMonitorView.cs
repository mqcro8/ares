namespace ares.scripts.ui.mission_monitor;

using System.Collections.Generic;
using Godot;

/// <summary>Pixel-art mission monitor. It renders snapshots and owns no gameplay state.</summary>
public partial class MissionMonitorView : CanvasLayer
{
    private const string ArtRoot = "res://art/training/";
    private static readonly Color Cyan = new(0.46f, 0.89f, 0.96f);
    private static readonly Color Amber = new(1.0f, 0.68f, 0.31f);
    private static readonly Color Muted = new(0.56f, 0.67f, 0.72f);

    private sealed record ObjectiveRow(TextureRect Icon, Label Text);

    private NinePatchRect _frame;
    private PanelContainer _collapsedPanel;
    private HBoxContainer _staminaRow;
    private TextureRect _staminaIcon;
    private Label _staminaText;
    private SegmentedBar _staminaBar;
    private TextureRect _compactStaminaIcon;
    private Label _compactStaminaText;
    private Label _compactHint;
    private VBoxContainer _objectiveList;
    private Label _prompt;
    private Label _feedback;
    private readonly List<ObjectiveRow> _rows = new();
    private Texture2D _pendingIcon, _activeIcon, _completeIcon;
    private FontFile _pixelFont;
    private bool _expanded = true;
    private bool _modalVisible = true;
    private bool _hasStamina;

    public bool IsDisplayed => _modalVisible && (_frame?.Visible == true || _collapsedPanel?.Visible == true);
    public bool IsExpanded => _expanded;
    public bool StaminaVisible => _hasStamina && _modalVisible;
    public string PromptText => _prompt?.Text ?? "";
    public string FeedbackText => _feedback?.Text ?? "";
    public IReadOnlyList<string> ObjectiveLabels
    {
        get
        {
            var labels = new List<string>();
            foreach (var row in _rows)
                if (row.Text.Visible) labels.Add(row.Text.Text);
            return labels;
        }
    }

    public override void _Ready()
    {
        Layer = 8;
        _pendingIcon = GD.Load<Texture2D>(ArtRoot + "data/objective_pending.png");
        _activeIcon = GD.Load<Texture2D>(ArtRoot + "data/objective_active.png");
        _completeIcon = GD.Load<Texture2D>(ArtRoot + "data/objective_complete.png");
        _pixelFont = GD.Load<FontFile>("res://art/fonts/PressStart2P-Regular.ttf");
        BuildView();
        GetViewport().SizeChanged += LayoutView;
        LayoutView();
        UpdateVisibility();
    }

    private void BuildView()
    {
        var frameTexture = GD.Load<Texture2D>(ArtRoot + "equipment/mission_monitor_frame.png");
        _frame = new NinePatchRect
        {
            Name = "Frame",
            Texture = frameTexture,
            SelfModulate = new Color(1f, 1f, 1f, 0.72f),
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            Position = new Vector2(12, 12),
            Size = new Vector2(292, 280),
            PatchMarginLeft = 8,
            PatchMarginTop = 8,
            PatchMarginRight = 8,
            PatchMarginBottom = 8,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        AddChild(_frame);

        var margin = new MarginContainer { Name = "Content" };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 13);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        _frame.AddChild(margin);

        var column = new VBoxContainer { Name = "Column", MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 4);
        margin.AddChild(column);

        var title = MakeLabel("TRAINING / GYM", Cyan, 9);
        title.Name = "Title";
        column.AddChild(title);

        _staminaRow = new HBoxContainer { Name = "StaminaRow", MouseFilter = Control.MouseFilterEnum.Ignore };
        _staminaRow.AddThemeConstantOverride("separation", 5);
        _staminaIcon = MakeIcon(GD.Load<Texture2D>(ArtRoot + "data/icon_stamina.png"), 16);
        _staminaText = MakeLabel("STAMINA  --/100", Colors.White, 8);
        _staminaText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _staminaRow.AddChild(_staminaIcon);
        _staminaRow.AddChild(_staminaText);
        column.AddChild(_staminaRow);

        _staminaBar = new SegmentedBar
        {
            Name = "StaminaBar",
            CustomMinimumSize = new Vector2(0, 8),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        column.AddChild(_staminaBar);

        var objectivesTitle = MakeLabel("OBJECTIVES", Cyan, 8);
        objectivesTitle.Name = "ObjectivesTitle";
        column.AddChild(objectivesTitle);

        _objectiveList = new VBoxContainer { Name = "Objectives", MouseFilter = Control.MouseFilterEnum.Ignore };
        _objectiveList.AddThemeConstantOverride("separation", 2);
        column.AddChild(_objectiveList);
        for (int i = 0; i < 8; i++)
        {
            var row = new HBoxContainer { Name = $"Objective{i + 1}", MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 5);
            var icon = MakeIcon(_pendingIcon, 12);
            var text = MakeLabel("", Colors.White, 8);
            text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(icon);
            row.AddChild(text);
            _objectiveList.AddChild(row);
            _rows.Add(new ObjectiveRow(icon, text));
        }

        _prompt = MakeLabel("", Colors.White, 8);
        _prompt.Name = "Prompt";
        _prompt.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_prompt);

        _feedback = MakeLabel("", Amber, 8);
        _feedback.Name = "Feedback";
        _feedback.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        column.AddChild(_feedback);

        _collapsedPanel = new PanelContainer
        {
            Name = "CollapsedStatus",
            CustomMinimumSize = new Vector2(208, 30),
            Size = new Vector2(208, 30),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _collapsedPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.082f, 0.145f, 0.208f, 0.72f),
            BorderColor = new Color(0.278f, 0.482f, 0.545f, 0.82f),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        });
        AddChild(_collapsedPanel);

        var compactMargin = new MarginContainer();
        compactMargin.AddThemeConstantOverride("margin_left", 7);
        compactMargin.AddThemeConstantOverride("margin_right", 7);
        compactMargin.AddThemeConstantOverride("margin_top", 3);
        compactMargin.AddThemeConstantOverride("margin_bottom", 3);
        _collapsedPanel.AddChild(compactMargin);

        var compactRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        compactRow.AddThemeConstantOverride("separation", 5);
        compactMargin.AddChild(compactRow);
        _compactStaminaIcon = MakeIcon(GD.Load<Texture2D>(ArtRoot + "data/icon_stamina.png"), 14);
        _compactStaminaText = MakeLabel("STAMINA --/100", Colors.White, 7);
        _compactStaminaText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _compactHint = MakeLabel("Q · OBJECTIVES", Cyan, 7);
        compactRow.AddChild(_compactStaminaIcon);
        compactRow.AddChild(_compactStaminaText);
        compactRow.AddChild(_compactHint);
    }

    private Label MakeLabel(string text, Color color, int size)
    {
        var label = new Label
        {
            Text = text,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipText = true,
        };
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color(0.015f, 0.035f, 0.055f, 1));
        label.AddThemeConstantOverride("outline_size", 3);
        label.AddThemeFontSizeOverride("font_size", size);
        if (_pixelFont != null) label.AddThemeFontOverride("font", _pixelFont);
        return label;
    }

    private static TextureRect MakeIcon(Texture2D texture, int size) => new()
    {
        Texture = texture,
        CustomMinimumSize = new Vector2(size, size),
        TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    public void Render(MonitorSnapshot snapshot)
    {
        if (_frame == null || snapshot == null) return;
        _modalVisible = snapshot.Visible;
        var title = _frame.GetNode<Label>("Content/Column/Title");
        if (title.Text != snapshot.Title) title.Text = snapshot.Title;

        _hasStamina = snapshot.Stamina.HasValue;
        _staminaRow.Visible = snapshot.Stamina.HasValue;
        _staminaBar.Visible = snapshot.Stamina.HasValue;
        _compactStaminaIcon.Visible = snapshot.Stamina.HasValue;
        _compactStaminaText.Visible = snapshot.Stamina.HasValue;
        if (snapshot.Stamina is float stamina)
        {
            _staminaText.Text = $"STAMINA  {Mathf.RoundToInt(stamina)}/100";
            _staminaBar.Value = stamina / 100f;
            _compactStaminaText.Text = $"STAMINA {Mathf.RoundToInt(stamina)}/100";
        }
        _compactHint.Text = "Q · OBJECTIVES";

        for (int i = 0; i < _rows.Count; i++)
        {
            bool hasObjective = i < snapshot.Objectives.Count;
            var row = _rows[i];
            row.Icon.Visible = hasObjective;
            row.Text.Visible = hasObjective;
            if (!hasObjective) continue;

            var objective = snapshot.Objectives[i];
            row.Text.Text = objective.Text;
            row.Icon.Texture = objective.State switch
            {
                MonitorObjectiveState.Complete => _completeIcon,
                MonitorObjectiveState.Active => _activeIcon,
                _ => _pendingIcon,
            };
            row.Text.Modulate = objective.State switch
            {
                MonitorObjectiveState.Complete => Cyan,
                MonitorObjectiveState.Active => Amber,
                _ => Muted,
            };
        }

        _prompt.Text = snapshot.Prompt;
        _prompt.Visible = !string.IsNullOrWhiteSpace(snapshot.Prompt);
        _feedback.Text = snapshot.Feedback;
        _feedback.Visible = !string.IsNullOrWhiteSpace(snapshot.Feedback);
        UpdateVisibility();
    }

    public override void _UnhandledKeyInput(InputEvent ev)
    {
        if (!_modalVisible || ev is not InputEventKey { Pressed: true, Echo: false } key ||
            (key.PhysicalKeycode != Key.Q && key.Keycode != Key.Q)) return;
        _expanded = !_expanded;
        UpdateVisibility();
        GetViewport().SetInputAsHandled();
    }

    private void LayoutView()
    {
        if (_frame == null || _collapsedPanel == null) return;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        _frame.Position = new Vector2(Mathf.Max(12, viewport.X - _frame.Size.X - 12), 12);
        _collapsedPanel.Position = new Vector2(Mathf.Max(8, viewport.X - _collapsedPanel.CustomMinimumSize.X - 8), 8);
    }

    private void UpdateVisibility()
    {
        if (_frame == null || _collapsedPanel == null) return;
        _frame.Visible = _modalVisible && _expanded;
        _collapsedPanel.Visible = _modalVisible && !_expanded;
    }

    public override void _ExitTree() => GetViewport().SizeChanged -= LayoutView;
}
