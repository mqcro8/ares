using System;

public sealed record LaunchHoldConfig(
    double Initial = 50, double Rise = 15, double Fall = 15,
    double Low = 45, double High = 55, double GoalSeconds = 10)
{
    public void Validate()
    {
        foreach (double value in new[] { Initial, Rise, Fall, Low, High, GoalSeconds })
            if (!double.IsFinite(value)) throw new ArgumentException("Configuration must be finite.");
        if (Initial < 0 || Initial > 100 || Rise <= 0 || Fall <= 0 || Low < 0 ||
            High > 100 || Low >= High || GoalSeconds <= 0)
            throw new ArgumentException("Invalid center-hold configuration.");
    }
}

public sealed record LaunchResult(double CenteredSeconds, double RequiredSeconds)
{
    public bool Completed => double.IsFinite(CenteredSeconds) && double.IsFinite(RequiredSeconds) &&
        RequiredSeconds > 0 && CenteredSeconds >= RequiredSeconds - 1e-9;
}

/// <summary>Accumulates only time spent inside the launch center band.</summary>
public sealed class LaunchHoldModel
{
    private const double Epsilon = 1e-9;
    public LaunchHoldConfig Config { get; }
    public double Heat { get; private set; }
    public double CenteredTime { get; private set; }
    public bool InBand => Heat >= Config.Low - Epsilon && Heat <= Config.High + Epsilon;
    public bool Complete => CenteredTime >= Config.GoalSeconds - Epsilon;
    public LaunchResult Result => Complete ? new(Config.GoalSeconds, Config.GoalSeconds) : null;
    public double Progress => Complete ? 1 : Math.Clamp(CenteredTime / Config.GoalSeconds, 0, 1);

    public LaunchHoldModel(LaunchHoldConfig config = null)
    {
        Config = config ?? new();
        Config.Validate();
        Heat = Config.Initial;
    }

    public void Advance(double delta, bool holding)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (Complete || delta == 0) return;

        double start = Heat;
        double rate = holding ? Config.Rise : -Config.Fall;
        double tLow = (Config.Low - start) / rate;
        double tHigh = (Config.High - start) / rate;
        double entry = rate > 0 ? Math.Max(0, tLow) : Math.Max(0, tHigh);
        double exit = rate > 0 ? Math.Min(delta, tHigh) : Math.Min(delta, tLow);
        double inside = InBand ? Math.Max(0, exit) : Math.Max(0, exit - entry);
        double needed = Config.GoalSeconds - CenteredTime;
        if (inside >= needed - Epsilon)
        {
            double finishAt = entry + needed;
            Heat = Math.Clamp(start + rate * finishAt, 0, 100);
            CenteredTime = Config.GoalSeconds;
            return;
        }

        Heat = Math.Clamp(start + rate * delta, 0, 100);
        CenteredTime = Math.Min(Config.GoalSeconds, CenteredTime + inside);
    }
}
