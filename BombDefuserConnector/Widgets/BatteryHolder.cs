﻿using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BombDefuserConnector.Widgets;
public class BatteryHolder : WidgetReader<int> {
	public override string Name => "Battery Holder";

	protected internal override float IsWidgetPresent(Image<Rgba32> image, LightsState lightsState, PixelCounts pixelCounts)
		// This is the only widget with yellow pixels.
		=> pixelCounts.Yellow / 2048f;

	private static bool IsRed(HsvColor hsv) => hsv.S >= 0.4f && hsv.H is >= 345 or <= 90;

	protected internal override int Process(Image<Rgba32> image, LightsState lightsState, ref Image<Rgba32>? debugImage) {
		debugImage?.Mutate(c => c.Brightness(0.5f));

		var maxCount = 0;
		for (var i = 0; i < 3; i++) {
			// Do the check at three different locations to account for the holder possibly not being centred.
			var x = 80 + 48 * i;
			var inBattery = false;
			var batteryCount = 0;
			for (var y = 32; y < 240; y++) {
				var color = image[x, y];
				if (debugImage != null)
					debugImage[x, y] = color;
				var hsv = HsvColor.FromColor(color);

				if (IsRed(hsv)) {
					if (debugImage != null)
						debugImage[x, y] = new(255, 0, 0);
					if (!inBattery) {
						batteryCount++;
						inBattery = true;
					}
				} else
					inBattery = false;
			}
			maxCount = Math.Max(maxCount, batteryCount);
		}
		return maxCount is 1 or 2 ? maxCount : throw new InvalidOperationException("Invalid battery count?!");
	}
}
