using System;

public static class LaunchIgnitionModelTest
{
	public static void Run(Action<bool, string> check)
	{
		var model = new LaunchIgnitionModel();
		check(model.Config.TargetClicks == 25 && model.Config.TimeLimitSeconds == 5,
			"Ignition uses twenty-five taps and a five-second limit");
		check(model.Config.MinimumRatio == 0.85 && model.Config.MinimumClicks == 22,
			"Ignition requires eighty-five percent of the taps before the rocket may fly");

		for (int i = 0; i < 24; i++) model.RegisterClick();
		check(model.Clicks == 24 && !model.Complete && Math.Abs(model.Progress - .96) < 1e-9,
			"Ignition stays incomplete until the final tap");
		model.RegisterClick();
		check(model.Complete && model.Progress == 1 && !model.Failed,
			"Twenty-five taps complete ignition");
		model.Advance(10);
		check(model.Complete && !model.Failed,
			"Completed ignition cannot time out");

		var minimum = new LaunchIgnitionModel();
		for (int i = 0; i < minimum.Config.MinimumClicks; i++) minimum.RegisterClick();
		check(minimum.MeetsMinimum && !minimum.Complete && minimum.ClicksMissing == 3,
			"Twenty-two taps still meet the launch minimum with three taps missing");
		minimum.Advance(5);
		check(minimum.TimedOut && !minimum.Failed,
			"Meeting the minimum before the deadline keeps the launch available");
		var partial = minimum.Outcome();
		check(partial.Outcome == LaunchIgnitionOutcome.Partial && partial.CanProceed && partial.Faults == 3,
			"A partial ignition may proceed and reports the missing taps as faults");
		check(Math.Abs(partial.Ratio - .88) < 1e-9,
			"Partial ignition keeps the registered share of the taps");

		var failed = new LaunchIgnitionModel();
		failed.RegisterClick();
		failed.Advance(4.9);
		check(!failed.Failed && failed.Remaining > 0,
			"Ignition remains available before its deadline");
		failed.Advance(.1);
		check(failed.Failed && failed.Clicks == 1 && failed.Remaining == 0,
			"Ignition fails at the five-second deadline");
		check(!failed.Outcome().CanProceed && failed.Outcome().Faults == 0,
			"An ignition below the minimum cannot launch and leaves no repairable faults");
		failed.RegisterClick();
		check(failed.Clicks == 1,
			"Clicks after ignition failure are ignored");

		var perfect = new LaunchIgnitionModel();
		for (int i = 0; i < 25; i++) perfect.RegisterClick();
		var clean = perfect.Outcome();
		check(clean.IsPerfect && clean.Faults == 0 && clean.CanProceed,
			"A full ignition is perfect and causes no faults");

		bool invalidRejected = false;
		try { _ = new LaunchIgnitionModel(new(TargetClicks: 0)); }
		catch (ArgumentException) { invalidRejected = true; }
		check(invalidRejected, "Invalid ignition configuration is rejected");
	}
}
