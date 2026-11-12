using System;

public sealed record LaunchIgnitionConfig(int TargetClicks = 25, double TimeLimitSeconds = 5)
{
	public void Validate()
	{
		if (TargetClicks <= 0 || !double.IsFinite(TimeLimitSeconds) || TimeLimitSeconds <= 0)
			throw new ArgumentException("Invalid ignition configuration.");
	}
}

/// <summary>Tracks the timed click sequence that primes the rocket before the hold phase.</summary>
public sealed class LaunchIgnitionModel
{
	public LaunchIgnitionConfig Config { get; }
	public int Clicks { get; private set; }
	public double Elapsed { get; private set; }
	public double Remaining => Math.Max(0, Config.TimeLimitSeconds - Elapsed);
	public bool Complete => Clicks >= Config.TargetClicks;
	public bool Failed => !Complete && Elapsed >= Config.TimeLimitSeconds;
	public double Progress => Math.Clamp((double)Clicks / Config.TargetClicks, 0, 1);

	public LaunchIgnitionModel(LaunchIgnitionConfig config = null)
	{
		Config = config ?? new();
		Config.Validate();
	}

	public void Advance(double delta)
	{
		if (!double.IsFinite(delta) || delta < 0)
			throw new ArgumentOutOfRangeException(nameof(delta));
		if (Complete || Failed || delta == 0) return;
		Elapsed = Math.Min(Config.TimeLimitSeconds, Elapsed + delta);
	}

	public void RegisterClick()
	{
		if (Complete || Failed) return;
		Clicks++;
	}
}
