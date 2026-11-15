namespace ares.scripts.training;

using Godot;
using ares.scripts.player;
using ares.scripts.mission;

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
    private float _fallbackStamina = MissionState.MaxStamina;
    public float Stamina => MissionState.Instance?.Stamina ?? _fallbackStamina;
    public bool Grounded { get; private set; } = true;
    public bool IsConfigured { get; private set; }
    private Vector2 _visualOrigin;
    private uint _originalMask;
    private bool _bound;
    private bool _waitForJumpRelease;

    public void SuppressDialogueJump() => _waitForJumpRelease = true;

    public sealed record Progress(float Stamina, float Rest);
    public Progress Capture() => new(Stamina, MissionState.Instance?.StaminaRestSeconds ?? 0);
    public void Restore(Progress progress)
    {
        if (MissionState.Instance != null)
            MissionState.Instance.RestoreStamina(progress.Stamina, progress.Rest);
        else
            _fallbackStamina = Mathf.Clamp(progress.Stamina, 0, MissionState.MaxStamina);
        SuppressDialogueJump();
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
        if (Grounded && !_waitForJumpRelease && Input.IsActionJustPressed("training_jump") && TrySpendStamina(20))
        {
            Course.JumpStarted(before, Height);
            VerticalSpeed = JumpImpulse;
            Grounded = false;
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
        Visual.Position = _visualOrigin + Vector2.Up * Mathf.Round(Height);
        // Shadow is a sibling of Visual: stays at the ground footprint even in flight.
        Shadow.Visible = true;
        return true;
    }

    private bool TrySpendStamina(float amount)
    {
        if (MissionState.Instance != null)
            return MissionState.Instance.TrySpendStamina(amount);
        if (_fallbackStamina < amount)
            return false;
        _fallbackStamina -= amount;
        return true;
    }
}
