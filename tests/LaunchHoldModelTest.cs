using System;

public static class LaunchHoldModelTest
{
    public static void Run(Action<bool, string> check)
    {
        foreach (var config in new[]
        {
            new LaunchHoldConfig(Rise: 0), new(Initial: double.NaN), new(Low: 70),
            new(GoalSeconds: 0)
        })
        {
            bool rejected = false;
            try { _ = new LaunchHoldModel(config); } catch (ArgumentException) { rejected = true; }
            check(rejected, "Invalid center-hold configuration rejected");
        }

        var idle = new LaunchHoldModel();
        idle.Advance(100, false);
        check(idle.Heat == 0 && !idle.Complete && Math.Abs(idle.CenteredTime - 1.0 / 3) < 1e-8,
            "Time outside the green band pauses progress without failing the run");

        var center = new LaunchHoldModel();
        center.Advance(2, false);
        check(Math.Abs(center.CenteredTime - 1.0 / 3) < 1e-8 && !center.Complete,
            "Only time actually spent in the center band counts");
        center.Advance(2, true);
        check(center.Heat == 50 && Math.Abs(center.CenteredTime - 2.0 / 3) < 1e-8,
            "Holding and releasing can recenter the marker");

        var complete = Drive(.01);
        check(complete.Complete && complete.Result?.CenteredSeconds == 10 && complete.Progress == 1,
            $"Ten accumulated centered seconds complete the task ({complete.CenteredTime:F9}, {complete.Heat:F9}, {complete.Complete})");
        double finalPosition = complete.Heat;
        complete.Advance(20, true);
        check(complete.Heat == finalPosition && complete.Result?.CenteredSeconds == 10,
            "Completed progress and marker remain fixed");

        LaunchResult baseline = null;
        foreach (int fps in new[] { 30, 60, 144, 1 })
        {
            var driven = Drive(1.0 / fps);
            baseline ??= driven.Result;
            check(driven.Complete && Math.Abs(driven.Result.CenteredSeconds - baseline.CenteredSeconds) < 1e-8,
                $"Center progress is frame-rate independent at {fps}fps");
        }
    }

    private static LaunchHoldModel Drive(double step)
    {
        var model = new LaunchHoldModel();
        while (!model.Complete)
            model.Advance(step, model.Heat < 50);
        return model;
    }
}
