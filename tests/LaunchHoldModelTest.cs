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

        var movingTarget = new LaunchTargetConfig(
            StartCenter: 50, MinCenter: 5, MaxCenter: 95,
            StartSpeed: 3, MaxSpeed: 8, SpeedRampSeconds: 10,
            StartWidth: 10, EndWidth: 8);
        var moving = new LaunchHoldModel(targetConfig: movingTarget);
        check(moving.TargetCenter == 50 && moving.TargetWidth == 10 && moving.TargetSpeed == 3,
            "Moving band starts centered at its original width and a gentle speed");
        moving.Advance(5, true);
        check(Math.Abs(moving.TargetSpeed - 5.5) < 1e-8 && moving.TargetCenter > 50 && moving.TargetCenter < 95,
            "Band moves and accelerates according to active elapsed time");

        var shrinkingHold = new LaunchHoldConfig(Rise: .1, Fall: .1, GoalSeconds: 10);
        var shrinkingTarget = new LaunchTargetConfig(
            StartCenter: 50, MinCenter: 5, MaxCenter: 95,
            StartSpeed: 0, MaxSpeed: 0, SpeedRampSeconds: 10,
            StartWidth: 10, EndWidth: 8);
        var shrinking = new LaunchHoldModel(shrinkingHold, shrinkingTarget);
        shrinking.Advance(5, true);
        double halfProgressWidth = shrinking.TargetWidth;
        shrinking.Advance(5, true);
        check(Math.Abs(halfProgressWidth - 9) < .01 && Math.Abs(shrinking.TargetWidth - 8) < .01,
            "Band narrows gradually by only two gauge points as hold progress completes");

        var bouncingTarget = new LaunchTargetConfig(
            StartCenter: 50, MinCenter: 5, MaxCenter: 95,
            StartSpeed: 20, MaxSpeed: 20, SpeedRampSeconds: 10,
            StartWidth: 10, EndWidth: 10);
        var bouncing = new LaunchHoldModel(shrinkingHold, bouncingTarget);
        bouncing.Advance(2.25, true);
        check(Math.Abs(bouncing.TargetCenter - 95) < .01,
            "Band reaches the far edge of the rail without clipping");
        bouncing.Advance(4.5, true);
        check(Math.Abs(bouncing.TargetCenter - 5) < .01,
            "Band traverses the full rail and comes back to the near edge");

        var randomTarget = new LaunchTargetConfig(
            StartCenter: 50, MinCenter: 5, MaxCenter: 95,
            StartSpeed: 6, MaxSpeed: 6, SpeedRampSeconds: 10,
            StartWidth: 10, EndWidth: 10,
            Seed: 4242, RandomDestinations: true);
        var random = new LaunchHoldModel(shrinkingHold, randomTarget);
        var visited = new System.Collections.Generic.HashSet<double>();
        bool insideRail = true;
        for (int i = 0; i < 400; i++)
        {
            random.Advance(.05, false);
            if (random.TargetCenter < randomTarget.MinCenter - .001 ||
                random.TargetCenter > randomTarget.MaxCenter + .001) insideRail = false;
            visited.Add(Math.Round(random.TargetCenter, 1));
        }
        check(insideRail, "Random band never leaves the rail");
        check(visited.Count > 12,
            $"Random band visits many different positions along the rail ({visited.Count})");
        check(Math.Abs(random.PositionAt(0) - randomTarget.StartCenter) < .001,
            "Random band starts from its configured center");

        var second = new LaunchHoldModel(shrinkingHold, randomTarget);
        check(Math.Abs(second.PositionAt(3.5) - random.PositionAt(3.5)) < 1e-6,
            "The same seed reproduces the same random path");
        var different = new LaunchHoldModel(shrinkingHold, randomTarget with { Seed = 777 });
        bool diverges = false;
        for (double time = 0.5; time <= 20 && !diverges; time += 0.5)
            diverges = Math.Abs(different.PositionAt(time) - random.PositionAt(time)) > .5;
        check(diverges,
            "A different seed produces a different random path");

        (double center, double width, double heat, double progress) dynamicBaseline = default;
        foreach (int fps in new[] { 30, 60, 144, 1 })
        {
            var frameModel = new LaunchHoldModel(shrinkingHold, movingTarget);
            int frames = fps * 5;
            for (int i = 0; i < frames; i++) frameModel.Advance(1.0 / fps, true);
            if (dynamicBaseline == default)
                dynamicBaseline = (frameModel.TargetCenter, frameModel.TargetWidth, frameModel.Heat, frameModel.CenteredTime);
            check(Math.Abs(frameModel.TargetCenter - dynamicBaseline.center) < .01 &&
                Math.Abs(frameModel.TargetWidth - dynamicBaseline.width) < .01 &&
                Math.Abs(frameModel.Heat - dynamicBaseline.heat) < .01 &&
                Math.Abs(frameModel.CenteredTime - dynamicBaseline.progress) < .03,
                $"Moving target and scoring remain stable at {fps}fps");
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
