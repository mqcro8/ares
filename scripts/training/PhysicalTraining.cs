namespace ares.scripts.training;

using Godot;

/// <summary>Session progress only. Scene rectangles are the sole footprint source.</summary>
public partial class PhysicalTraining : Node2D
{
    /// <summary>Emitted once, after the final valid ground landing.</summary>
    [Signal] public delegate void CompletedEventHandler();
    [Export] public float BlockHeight { get; set; } = 18;
    [Export] public Godot.Collections.Array<CollisionShape2D> Blocks { get; set; } = new();
    [Export] public TrainingHUD Hud { get; set; }
    public int ClearedCount { get; private set; }
    public bool Congratulated { get; private set; }
    public bool IsConfigured { get; private set; }
    private int[] _side;
    private bool[] _crossed;
    private int _direction;
    private Vector2 _halfBody, _offset;

    public override void _Ready()
    {
        IsConfigured = Blocks.Count == 3 && BlockHeight > 0;
        foreach (CollisionShape2D block in Blocks)
            IsConfigured &= IsValidFootprint(block)
                && block.GetParent() is StaticBody2D body && body.CollisionLayer == 2;
        if (!IsConfigured)
        {
            GD.PushError($"PhysicalTraining at {GetPath()}: assign three enabled rectangular block footprints on StaticBody2D layer 2 and a positive BlockHeight. Training movement is disabled.");
            return;
        }
        _side = new int[Blocks.Count];
        _crossed = new bool[Blocks.Count];
    }

    internal static bool IsValidFootprint(CollisionShape2D collider) =>
        collider != null && !collider.Disabled && collider.Shape is RectangleShape2D rectangle
        && rectangle.Size.X > 0 && rectangle.Size.Y > 0;

    public static Rect2 WorldRect(CollisionShape2D collider)
    {
        RectangleShape2D rectangle = (RectangleShape2D)collider.Shape;
        Vector2 half = rectangle.Size / 2;
        Transform2D transform = collider.GlobalTransform;
        Rect2 bounds = new(transform * -half, Vector2.Zero);
        bounds = bounds.Expand(transform * new Vector2(half.X, -half.Y));
        bounds = bounds.Expand(transform * half);
        return bounds.Expand(transform * new Vector2(-half.X, half.Y));
    }

    public void BindFootprint(CollisionShape2D collider, Vector2 origin)
    {
        Rect2 bounds = WorldRect(collider);
        _halfBody = bounds.Size / 2;
        _offset = bounds.GetCenter() - origin;
    }

    public float SupportAt(Vector2 point, float height)
    {
        if (height < BlockHeight) return 0;
        Rect2 body = new(point + _offset - _halfBody, _halfBody * 2);
        foreach (CollisionShape2D block in Blocks)
            if (WorldRect(block).Intersects(body)) return BlockHeight;
        return 0;
    }

    public void JumpStarted(Vector2 point, float height)
    {
        Vector2 center = point + _offset;
        for (int i = 0; i < Blocks.Count; i++)
        {
            Rect2 r = WorldRect(Blocks[i]);
            _side[i] = height != 0 ? 0 : center.Y + _halfBody.Y <= r.Position.Y ? -1 : center.Y - _halfBody.Y >= r.End.Y ? 1 : 0;
            _crossed[i] = false;
        }
    }

    /// <summary>Require full width through the whole swept footprint.</summary>
    public void Sweep(Vector2 from, Vector2 to)
    {
        from += _offset;
        to += _offset;
        for (int i = 0; i < Blocks.Count; i++)
        {
            if (_side[i] == 0) continue;
            Rect2 r = WorldRect(Blocks[i]);
            float near = r.Position.Y - _halfBody.Y, far = r.End.Y + _halfBody.Y;
            float dy = to.Y - from.Y;
            float enter = 0, exit = 1;
            if (Mathf.Abs(dy) < 0.00001f)
            {
                if (from.Y < near || from.Y > far) continue;
            }
            else
            {
                float a = (near - from.Y) / dy, b = (far - from.Y) / dy;
                enter = Mathf.Max(0, Mathf.Min(a, b));
                exit = Mathf.Min(1, Mathf.Max(a, b));
                if (enter > exit) continue;
            }
            Vector2 p = from.Lerp(to, enter), q = from.Lerp(to, exit);
            // Only width violations cancel: ascent below 18 is fine, because layer 2
            // stays solid under 18 and MoveAndSlide blocks body entry into the footprint.
            if (Mathf.Min(p.X, q.X) - _halfBody.X < r.Position.X || Mathf.Max(p.X, q.X) + _halfBody.X > r.End.X)
            { _side[i] = 0; continue; }
            // Reaching here already proves entry into the swept footprint;
            // crossing its far face needs no separate "entered" flag.
            if ((_side[i] < 0 && to.Y >= far) || (_side[i] > 0 && to.Y <= near)) _crossed[i] = true;
        }
    }

    public void Landed(Vector2 point, float support)
    {
        int candidate = -1, crossings = 0;
        for (int i = 0; i < Blocks.Count; i++)
        {
            float y = point.Y + _offset.Y;
            Rect2 r = WorldRect(Blocks[i]);
            bool opposite = _side[i] < 0 ? y - _halfBody.Y >= r.End.Y : y + _halfBody.Y <= r.Position.Y;
            if (support == 0 && _side[i] != 0 && _crossed[i] && opposite)
            { candidate = i; crossings++; }
            _side[i] = 0;
        }
        if (crossings != 1 || Congratulated) return;
        if (ClearedCount == 0)
        {
            if (candidate == 0) _direction = 1;
            else if (candidate == Blocks.Count - 1) _direction = -1;
            else return;
        }
        int expected = _direction == 1 ? ClearedCount : Blocks.Count - 1 - ClearedCount;
        if (candidate != expected) return;
        ClearedCount++;
        if (ClearedCount == Blocks.Count)
        {
            Congratulated = true;
            EmitSignal(SignalName.Completed);
        }
    }

    public void Step(float stamina) => Hud?.UpdateState(stamina);
}
