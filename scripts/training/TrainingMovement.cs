namespace ares.scripts.training;

using Godot;
using ares.scripts.player;

/// <summary>Independent height, driven exclusively by Player's physics tick.</summary>
public partial class TrainingMovement : Node
{
    [Export] public PhysicalTraining Course { get; set; }
    [Export] public Node2D Visual { get; set; }
    [Export] public CollisionShape2D Footprint { get; set; }
    [Export] public Polygon2D Shadow { get; set; }
    [Export] public float JumpImpulse { get; set; } = 240;
    [Export] public float Gravity { get; set; } = 900;
    public float Height { get; private set; }
    public float VerticalSpeed { get; private set; }
    public float Stamina { get; private set; } = 100;
    public bool Grounded { get; private set; } = true;
    public bool IsConfigured { get; private set; }
    private float _rest;
    private Vector2 _visualOrigin;
    private uint _originalMask;
    private bool _bound;
    private bool _waitForJumpRelease;

    public void SuppressDialogueJump() => _waitForJumpRelease = true;

    public sealed record Progress(float Stamina, float Rest);
    public Progress Capture() => new(Stamina, _rest);
    public void Restore(Progress progress)
    {
        Stamina = progress.Stamina;
        _rest = progress.Rest;
        SuppressDialogueJump();
        Course.Step(Stamina);
    }

    public override void _Ready()
    {
        IsConfigured = Course != null && Visual != null && Shadow != null
            && PhysicalTraining.IsValidFootprint(Footprint) && JumpImpulse > 0 && Gravity > 0;
        if (!IsConfigured)
        {
            GD.PushError($"TrainingMovement at {GetPath()}: assign Course, Visual, Shadow and an enabled rectangular Footprint; JumpImpulse and Gravity must be positive. Player will use normal movement.");
            return;
        }
        _visualOrigin = Visual.Position;
    }

    public bool Tick(Player body, float dt)
    {
        if (!IsConfigured || !Course.IsConfigured) return false;
        if (!_bound)
        {
            _originalMask = body.CollisionMask;
            Course.BindFootprint(Footprint, body.GlobalPosition);
            _bound = true;
        }
        Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");
        if (!Input.IsActionPressed("training_jump")) _waitForJumpRelease = false;
        Vector2 before = body.GlobalPosition;
        float oldHeight = Height;
        float support = Course.SupportAt(before, Height);
        if (Height > support + 0.001f) Grounded = false;
        if (Grounded && !_waitForJumpRelease && Input.IsActionJustPressed("training_jump") && Stamina >= 20)
        {
            Course.JumpStarted(before, Height);
            VerticalSpeed = JumpImpulse;
            Grounded = false;
            Stamina -= 20;
            _rest = 0;
        }
        if (!Grounded)
        {
            Height += VerticalSpeed * dt - Gravity * 0.5f * dt * dt;
            VerticalSpeed -= Gravity * dt;
        }
        // Keep the block mask off while supported/overlapping at top height.
        // Walls and original furniture remain solid at EVERY height.
        if (oldHeight >= Course.BlockHeight && Height < Course.BlockHeight && support > 0)
            Height = Course.BlockHeight;
        body.CollisionMask = _originalMask | (Height < Course.BlockHeight ? 2u : 0u);
        body.Velocity = body.Velocity.MoveToward(direction * body.Speed,
            (direction == Vector2.Zero ? body.Friction : body.Acceleration) * dt);
        body.MoveAndSlide();
        Vector2 after = body.GlobalPosition;
        Course.Sweep(before, after);
        support = Course.SupportAt(after, Mathf.Max(oldHeight, Height));
        if (VerticalSpeed <= 0 && Height <= support)
        {
            bool landed = !Grounded;
            Height = support;
            VerticalSpeed = 0;
            Grounded = true;
            if (landed) Course.Landed(after, support);
        }
        else if (Height > support) Grounded = false;
        if (Grounded && direction == Vector2.Zero && body.Velocity.LengthSquared() < 0.01f)
        {
            float previousRest = _rest;
            _rest += dt;
            Stamina = Mathf.Min(100, Stamina + 25 * (Mathf.Max(0, _rest - 1) - Mathf.Max(0, previousRest - 1)));
        }
        else _rest = 0;
        Visual.Position = _visualOrigin + Vector2.Up * Mathf.Round(Height);
        Shadow.Visible = Height > 0;
        Course.Step(Stamina);
        return true;
    }
}
