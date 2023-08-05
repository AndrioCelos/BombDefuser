﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace BombDefuserConnector.Components;
public class NeedyCapacitor : ComponentReader<NeedyCapacitor.ReadData> {
	public override string Name => "Needy Capacitor";
	protected internal override bool UsesNeedyFrame => true;

	protected internal override float IsModulePresent(Image<Rgb24> image) {
		// Look for the brown lever frame.
		var count = 0;
		image.ProcessPixelRows(a => {
			for (var y = 80; y < 256; y++) {
				var row = a.GetRowSpan(y);
				for (var x = 144; x < 240; x++) {
					var hsv = HsvColor.FromColor(row[x]);
					if (hsv.H is >= 15 and <= 60 && hsv.S is >= 0.15f and <= 0.5f)
						count++;
				}
				for (var x = 16; x < 112; x++) {
					var hsv = HsvColor.FromColor(row[x]);
					if (hsv.H is >= 15 and <= 60 && hsv.S is >= 0.15f and <= 0.5f)
						count--;  // Prevent Needy Vent Gas from matching.
				}
			}
		});
		return count / 6000f;
	}
	protected internal override ReadData Process(Image<Rgb24> image, ref Image<Rgb24>? debugImage) {
		// Find the timer.
		return new(ReadNeedyTimer(image, debugImage));
	}

	public record ReadData(int? Time);
}
