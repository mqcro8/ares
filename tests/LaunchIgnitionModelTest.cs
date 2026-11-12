using System;

public static class LaunchIgnitionModelTest
{
	public static void Run(Action<bool, string> check)
	{
		var model = new LaunchIgnitionModel();
		check(model.Config.TargetClicks == 25 && model.Config.TimeLimitSeconds == 5,
			"Ignition uses twenty-five taps and a five-second limit");

		for (int i = 0; i < 24; i++) model.RegisterClick();
		check(model.Clicks == 24 && !model.Complete && Math.Abs(model.Progress - .96) < 1e-9,
			"Ignition stays incomplete until the final tap");
		model.RegisterClick();
		check(model.Complete && model.Progress == 1 && !model.Failed,
			"Twenty-five taps complete ignition");
		model.Advance(10);
		check(model.Complete && !model.Failed,
			"Completed ignition cannot time out");

		var failed = new LaunchIgnitionModel();
		failed.RegisterClick();
		failed.Advance(4.9);
		check(!failed.Failed && failed.Remaining > 0,
			"Ignition remains available before its deadline");
		failed.Advance(.1);
		check(failed.Failed && failed.Clicks == 1 && failed.Remaining == 0,
			"Ignition fails at the five-second deadline");
		failed.RegisterClick();
		check(failed.Clicks == 1,
			"Clicks after ignition failure are ignored");

		bool invalidRejected = false;
		try { _ = new LaunchIgnitionModel(new(TargetClicks: 0)); }
		catch (ArgumentException) { invalidRejected = true; }
		check(invalidRejected, "Invalid ignition configuration is rejected");
	}
}
