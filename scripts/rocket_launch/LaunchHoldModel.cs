using System;
using System.Collections.Generic;

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

public sealed record LaunchTargetConfig(
    double StartCenter = 50, double MinCenter = 5, double MaxCenter = 95,
    double StartSpeed = 3, double MaxSpeed = 8, double SpeedRampSeconds = 10,
    double StartWidth = 10, double EndWidth = 8,
    int Seed = 20261013, bool RandomDestinations = false, int WaypointCount = 64)
{
    /// <summary>Static band used by the plain center-hold model.</summary>
    public static LaunchTargetConfig Fixed(LaunchHoldConfig hold)
    {
        double center = (hold.Low + hold.High) / 2;
        double width = hold.High - hold.Low;
        return new(center, center, center, 0, 0, 1, width, width);
    }

    public bool IsDynamic => StartSpeed > 0 || MaxSpeed > 0 ||
        Math.Abs(StartWidth - EndWidth) > 1e-9 || Math.Abs(MinCenter - MaxCenter) > 1e-9;

    public void Validate()
    {
        foreach (double value in new[]
        {
            StartCenter, MinCenter, MaxCenter, StartSpeed, MaxSpeed,
            SpeedRampSeconds, StartWidth, EndWidth
        })
            if (!double.IsFinite(value)) throw new ArgumentException("Target configuration must be finite.");

        if (MinCenter < 0 || MaxCenter > 100 || MinCenter > MaxCenter ||
            StartCenter < MinCenter || StartCenter > MaxCenter ||
            StartSpeed < 0 || MaxSpeed < StartSpeed || SpeedRampSeconds <= 0 ||
            StartWidth <= 0 || StartWidth > 100 || EndWidth <= 0 || EndWidth > StartWidth ||
            MinCenter < StartWidth / 2 || MaxCenter > 100 - StartWidth / 2 ||
            WaypointCount <= 0)
            throw new ArgumentException("Invalid moving-target configuration.");
    }

    /// <summary>Band speed at a given active elapsed time. It ramps from start to maximum.</summary>
    public double SpeedAt(double elapsed)
    {
        double ramp = Math.Clamp(elapsed / SpeedRampSeconds, 0, 1);
        return StartSpeed + (MaxSpeed - StartSpeed) * ramp;
    }

    /// <summary>Band width at a given hold progress. It narrows slightly as the task completes.</summary>
    public double WidthAt(double progress)
    {
        double amount = Math.Clamp(progress, 0, 1);
        return StartWidth + (EndWidth - StartWidth) * amount;
    }

    /// <summary>Distance the band travels from the start of the phase until the given time.</summary>
    public double TravelAt(double elapsed)
    {
        double time = Math.Max(0, elapsed);
        double rampTime = SpeedRampSeconds;
        if (time <= rampTime)
            return StartSpeed * time + (MaxSpeed - StartSpeed) * time * time / (2 * rampTime);
        double rampTravel = StartSpeed * rampTime + (MaxSpeed - StartSpeed) * rampTime / 2;
        return rampTravel + MaxSpeed * (time - rampTime);
    }
}

public sealed record LaunchResult(double CenteredSeconds, double RequiredSeconds,
    int IgnitionClicks = 0, int IgnitionTargetClicks = 0, int IgnitionFaults = 0)
{
    public bool Completed => double.IsFinite(CenteredSeconds) && double.IsFinite(RequiredSeconds) &&
        RequiredSeconds > 0 && CenteredSeconds >= RequiredSeconds - 1e-9;
    /// <summary>Share of the ignition taps that were registered. One means a clean start.</summary>
    public double IgnitionRatio => IgnitionTargetClicks > 0
        ? Math.Clamp((double)IgnitionClicks / IgnitionTargetClicks, 0, 1) : 1;
    public bool PerfectIgnition => IgnitionFaults <= 0;
}

/// <summary>Accumulates only time spent inside the launch target band.</summary>
public sealed class LaunchHoldModel
{
    private const double Epsilon = 1e-9;
    private const double DynamicStep = 1.0 / 240.0;
    private readonly List<Leg> _legs = new();
    private readonly bool _randomDestinations;

    public LaunchHoldConfig Config { get; }
    public LaunchTargetConfig TargetConfig { get; }
    public double Heat { get; private set; }
    public double CenteredTime { get; private set; }
    public double TargetCenter { get; private set; }
    public double TargetWidth { get; private set; }
    public double TargetLow => TargetCenter - TargetWidth / 2;
    public double TargetHigh => TargetCenter + TargetWidth / 2;
    public double TargetSpeed => TargetConfig.SpeedAt(ActiveElapsed);
    public double ActiveElapsed { get; private set; }
    public bool InBand => IsWithinBand(Heat, TargetLow, TargetHigh);
    public bool Complete => CenteredTime >= Config.GoalSeconds - Epsilon;
    public LaunchResult Result => Complete ? new(Config.GoalSeconds, Config.GoalSeconds) : null;
    public double Progress => Complete ? 1 : Math.Clamp(CenteredTime / Config.GoalSeconds, 0, 1);
    public int TargetDirection { get; private set; } = 1;

    public LaunchHoldModel(LaunchHoldConfig config = null, LaunchTargetConfig targetConfig = null)
    {
        Config = config ?? new();
        Config.Validate();
        TargetConfig = targetConfig ?? LaunchTargetConfig.Fixed(Config);
        TargetConfig.Validate();
        TargetCenter = TargetConfig.StartCenter;
        TargetWidth = TargetConfig.StartWidth;
        Heat = Config.Initial;
        _randomDestinations = TargetConfig.RandomDestinations && TargetConfig.MaxSpeed > 0;
        BuildLegs();
    }

    public void Advance(double delta, bool holding)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (Complete || delta == 0) return;

        if (!TargetConfig.IsDynamic)
        {
            ActiveElapsed += AdvanceWithinBand(delta, holding, TargetLow, TargetHigh);
            return;
        }

        double remaining = delta;
        while (remaining > Epsilon && !Complete)
        {
            double step = Math.Min(DynamicStep, remaining);
            double middleCenter = PositionAt(ActiveElapsed + step / 2);
            double halfWidth = TargetWidth / 2;
            double elapsed = AdvanceWithinBand(step, holding,
                middleCenter - halfWidth, middleCenter + halfWidth);

            double before = TargetCenter;
            ActiveElapsed += elapsed;
            TargetCenter = PositionAt(ActiveElapsed);
            double moved = TargetCenter - before;
            if (Math.Abs(moved) > Epsilon) TargetDirection = Math.Sign(moved);
            TargetWidth = TargetConfig.WidthAt(Progress);
            remaining -= elapsed;
        }
    }

    /// <summary>Position of the target band at a given active elapsed time.</summary>
    public double PositionAt(double elapsed)
    {
        if (!_randomDestinations)
        {
            double travel = TargetConfig.TravelAt(elapsed);
            double toUpper = TargetConfig.MaxCenter - TargetConfig.StartCenter;
            double toLower = TargetConfig.StartCenter - TargetConfig.MinCenter;
            double period = 2 * (toUpper + toLower);
            if (period <= Epsilon) return TargetConfig.StartCenter;
            double offset = travel % period;
            double position = offset <= toUpper
                ? TargetConfig.StartCenter + offset
                : TargetConfig.StartCenter + toUpper - (offset - toUpper);
            return Math.Clamp(position, TargetConfig.MinCenter, TargetConfig.MaxCenter);
        }

        if (_legs.Count == 0) return TargetConfig.StartCenter;
        double travelled = TargetConfig.TravelAt(elapsed);
        foreach (Leg leg in _legs)
        {
            if (travelled <= leg.EndTravel || leg == _legs[^1])
            {
                double span = leg.EndTravel - leg.StartTravel;
                double amount = span <= Epsilon ? 1 : Math.Clamp((travelled - leg.StartTravel) / span, 0, 1);
                return leg.From + (leg.To - leg.From) * amount;
            }
        }
        return _legs[^1].To;
    }

    private void BuildLegs()
    {
        if (!_randomDestinations) return;
        var random = new Random(TargetConfig.Seed);
        double center = TargetConfig.StartCenter;
        double travelled = 0;
        double minimumTravel = Math.Max(8, (TargetConfig.MaxCenter - TargetConfig.MinCenter) * 0.25);
        for (int i = 0; i < TargetConfig.WaypointCount; i++)
        {
            double destination = NextDestination(random, center, minimumTravel);
            double distance = Math.Abs(destination - center);
            _legs.Add(new Leg(travelled, travelled + distance, center, destination));
            travelled += distance;
            center = destination;
        }
    }

    private double NextDestination(Random random, double current, double minimumTravel)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            double candidate = TargetConfig.MinCenter +
                random.NextDouble() * (TargetConfig.MaxCenter - TargetConfig.MinCenter);
            if (Math.Abs(candidate - current) >= minimumTravel) return candidate;
        }
        double middle = (TargetConfig.MinCenter + TargetConfig.MaxCenter) / 2;
        double opposite = current >= middle ? TargetConfig.MinCenter : TargetConfig.MaxCenter;
        return Math.Clamp(opposite, TargetConfig.MinCenter, TargetConfig.MaxCenter);
    }

    private double AdvanceWithinBand(double delta, bool holding, double low, double high)
    {
        if (Complete || delta == 0) return 0;

        double start = Heat;
        double rate = holding ? Config.Rise : -Config.Fall;
        double tLow = (low - start) / rate;
        double tHigh = (high - start) / rate;
        double entry = rate > 0 ? Math.Max(0, tLow) : Math.Max(0, tHigh);
        double exit = rate > 0 ? Math.Min(delta, tHigh) : Math.Min(delta, tLow);
        double inside = IsWithinBand(start, low, high) ? Math.Max(0, exit) : Math.Max(0, exit - entry);
        double needed = Config.GoalSeconds - CenteredTime;
        if (inside >= needed - Epsilon)
        {
            double finishAt = entry + needed;
            Heat = Math.Clamp(start + rate * finishAt, 0, 100);
            CenteredTime = Config.GoalSeconds;
            return Math.Clamp(finishAt, 0, delta);
        }

        Heat = Math.Clamp(start + rate * delta, 0, 100);
        CenteredTime = Math.Min(Config.GoalSeconds, CenteredTime + inside);
        return delta;
    }

    private static bool IsWithinBand(double value, double low, double high) =>
        value >= low - Epsilon && value <= high + Epsilon;

    private readonly record struct Leg(double StartTravel, double EndTravel, double From, double To);
}
