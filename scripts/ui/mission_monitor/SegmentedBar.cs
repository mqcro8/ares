namespace ares.scripts.ui.mission_monitor;

using Godot;

public partial class SegmentedBar : Control
{
    public int Segments { get; set; } = 12;
    private float _value = 1f;
    public float Value
    {
        get => _value;
        set
        {
            float clamped = Mathf.Clamp(value, 0, 1);
            if (Mathf.IsEqualApprox(_value, clamped)) return;
            _value = clamped;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        if (Segments <= 0 || Size.X <= 0 || Size.Y <= 0) return;
        const float gap = 2;
        float segmentWidth = (Size.X - gap * (Segments - 1)) / Segments;
        int filled = Mathf.RoundToInt(_value * Segments);
        var emptyColor = new Color(0.07f, 0.14f, 0.20f, 1);
        var fillColor = new Color(0.31f, 0.82f, 0.88f, 1);
        for (int i = 0; i < Segments; i++)
        {
            var rect = new Rect2(i * (segmentWidth + gap), 0, segmentWidth, Size.Y);
            DrawRect(rect, i < filled ? fillColor : emptyColor);
        }
    }
}
