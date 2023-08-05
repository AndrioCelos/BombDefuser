﻿namespace BombDefuserScripts;
internal static class Timer {
	internal static async Task ReadTimerAsync(Image<Rgb24> screenshot) {
		if (GameState.Current.TimerSlot is null) throw new InvalidOperationException("Don't know where the timer is.");
		var polygon = Utils.GetPoints(GameState.Current.TimerSlot.Value);
		int? lastSeconds = null;
		// Keep watching the timer until it ticks over to get sub-second precision.
		while (true) {
			var data = DefuserConnector.Instance.ReadComponent(screenshot, DefuserConnector.TimerReader, polygon);
			GameState.Current.GameMode = data.GameMode;
			if (lastSeconds is not null && data.Time != lastSeconds.Value) {
				GameState.Current.TimerBaseTime = data.GameMode is BombDefuserConnector.Components.Timer.GameMode.Zen or BombDefuserConnector.Components.Timer.GameMode.Training
					? TimeSpan.FromSeconds(data.Time) - TimeSpan.FromMilliseconds(50)
					: TimeSpan.FromSeconds(lastSeconds.Value) + TimeSpan.FromMilliseconds(50);
				GameState.Current.TimerStopwatch.Restart();
				return;
			}
			lastSeconds = data.Time;
			await AimlTasks.Delay(0.1);
			screenshot = DefuserConnector.Instance.TakeScreenshot();
		}
	}

	public static async Task WaitForDigitInTimerAsync(int digit) {
		var time = GameState.Current.Time;
		if (time.Seconds % 10 == digit || time.Seconds / 10 == digit) return;
		if (time.Minutes > 0 ? (time.Minutes % 10 == digit || time.Minutes / 10 == digit) : digit == 0) return;

		// Find out how long to wait for.
		TimeSpan timeSpan;
		if (GameState.Current.GameMode is BombDefuserConnector.Components.Timer.GameMode.Zen or BombDefuserConnector.Components.Timer.GameMode.Training) {
			if (digit < time.Seconds % 10)
				timeSpan = TimeSpan.FromTicks((digit + 10) * TimeSpan.TicksPerSecond + TimeSpan.TicksPerSecond / 2 - time.Ticks % (TimeSpan.TicksPerSecond * 10));
			else
				timeSpan = TimeSpan.FromTicks(digit * TimeSpan.TicksPerSecond + TimeSpan.TicksPerSecond / 2 - time.Ticks % (TimeSpan.TicksPerSecond * 10));
		} else {
			if (digit > time.Seconds % 10)
				timeSpan = TimeSpan.FromTicks(time.Ticks % (TimeSpan.TicksPerSecond * 10) + 10 * TimeSpan.TicksPerSecond - digit * TimeSpan.TicksPerSecond + TimeSpan.TicksPerSecond / 2);
			else
				timeSpan = TimeSpan.FromTicks(time.Ticks % (TimeSpan.TicksPerSecond * 10) - digit * TimeSpan.TicksPerSecond + TimeSpan.TicksPerSecond / 2);
		}
		await AimlTasks.Delay(timeSpan);
	}
}
