using System;

public sealed record LaunchIgnitionConfig(int TargetClicks = 25, double TimeLimitSeconds = 5,
    double MinimumRatio = 0.85)
{
    /// <summary>Smallest number of taps that still allows the rocket to leave the pad.</summary>
    public int MinimumClicks => (int)Math.Ceiling(TargetClicks * MinimumRatio - 1e-9);
    public void Validate()
    {
        if (TargetClicks <= 0 || !double.IsFinite(TimeLimitSeconds) || TimeLimitSeconds <= 0 ||
            !double.IsFinite(MinimumRatio) || MinimumRatio <= 0 || MinimumRatio > 1)
            throw new ArgumentException("Invalid ignition configuration.");
        if (MinimumClicks > TargetClicks)
            throw new ArgumentException("Ignition minimum cannot exceed the target clicks.");
    }
}

public enum LaunchIgnitionOutcome { Pending, Perfect, Partial, Failed }

/// <summary>Outcome of the timed ignition sequence, including the penalty it causes.</summary>
public sealed record LaunchIgnitionResult(int Clicks, int TargetClicks, int MinimumClicks, LaunchIgnitionOutcome Outcome)
{
    public double Ratio => TargetClicks > 0 ? Math.Clamp((double)Clicks / TargetClicks, 0, 1) : 0;
    public bool IsPerfect => Outcome == LaunchIgnitionOutcome.Perfect;
    public bool CanProceed => Outcome is LaunchIgnitionOutcome.Perfect or LaunchIgnitionOutcome.Partial;
    /// <summary>Ignition faults caused by an imperfect start. Zero for a clean ignition.</summary>
    public int Faults => Outcome switch
    {
        LaunchIgnitionOutcome.Perfect => 0,
        LaunchIgnitionOutcome.Partial => Math.Max(1, TargetClicks - Clicks),
        _ => 0,
    };
}

/// <summary>Tracks the timed click sequence that primes the rocket before the hold phase.</summary>
public sealed class LaunchIgnitionModel
{
    public LaunchIgnitionConfig Config { get; }
    public int Clicks { get; private set; }
    public double Elapsed { get; private set; }
    public double Remaining => Math.Max(0, Config.TimeLimitSeconds - Elapsed);
    /// <summary>Every target tap was registered.</summary>
    public bool Complete => Clicks >= Config.TargetClicks;
    /// <summary>The minimum ratio of taps was reached, so the rocket may still leave the pad.</summary>
    public bool MeetsMinimum => Clicks >= Config.MinimumClicks;
    /// <summary>The deadline passed.</summary>
    public bool TimedOut => Elapsed >= Config.TimeLimitSeconds - 1e-9;
    /// <summary>The deadline passed without enough taps to launch.</summary>
    public bool Failed => TimedOut && !MeetsMinimum;
    public double Progress => Math.Clamp((double)Clicks / Config.TargetClicks, 0, 1);
    public double MinimumProgress => Math.Clamp((double)Config.MinimumClicks / Config.TargetClicks, 0, 1);
    public int ClicksMissing => Math.Max(0, Config.TargetClicks - Clicks);

    public LaunchIgnitionModel(LaunchIgnitionConfig config = null)
    {
        Config = config ?? new();
        Config.Validate();
    }

    public void Advance(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0)
            throw new ArgumentOutOfRangeException(nameof(delta));
        if (Complete || TimedOut || delta == 0) return;
        Elapsed = Math.Min(Config.TimeLimitSeconds, Elapsed + delta);
    }

	public void RegisterClick()
	{
		if (Complete || TimedOut) return;
		Clicks = Math.Min(Config.TargetClicks, Clicks + 1);
	}

    public LaunchIgnitionResult Outcome() => Outcome(Clicks);

    public LaunchIgnitionResult Outcome(int clicks)
    {
        LaunchIgnitionOutcome outcome = clicks >= Config.TargetClicks
            ? LaunchIgnitionOutcome.Perfect
            : clicks >= Config.MinimumClicks ? LaunchIgnitionOutcome.Partial : LaunchIgnitionOutcome.Failed;
        return new(clicks, Config.TargetClicks, Config.MinimumClicks, outcome);
    }
}
