﻿using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BombDefuserConnector.Components;
public class NeedyVentGas : ComponentProcessor<NeedyVentGas.ReadData> {
	public override string Name => "Needy Vent Gas";
	protected internal override bool UsesNeedyFrame => true;

	private static readonly TextRecogniser displayRecogniser = new(new(TextRecogniser.Fonts.CABIN_MEDIUM, 24), 0, 128, new(256, 64),
		"VENT GAS?", "DETONATE?");

	protected internal override float IsModulePresent(Image<Rgb24> image) {
		// Look for the brown lever frame.
		var count = 0;
		image.ProcessPixelRows(a => {
			for (var y = 80; y < 256; y++) {
				var row = a.GetRowSpan(y);
				for (var x = 16; x < 240; x++) {
					var hsv = HsvColor.FromColor(row[x]);
					if ((hsv.H is >= 15 and <= 60 && hsv.S is >= 0.15f and <= 0.5f) || (hsv.H is >= 90 and <= 135 && hsv.S >= 0.25f))
						count++;
				}
			}
		});
		return count / 36000f;
	}
	protected internal override ReadData Process(Image<Rgb24> image, ref Image<Rgb24>? debugBitmap) {
		var time = ReadNeedyTimer(image, debugBitmap);
		if (time == null) return new(null, null);

		int top = 0, bottom = 0;
		image.ProcessPixelRows(a => {
			for (top = 64; top < 128; top++) {
				var r = a.GetRowSpan(top);
				for (var x = 112; x < 144; x++) {
					var hsv = HsvColor.FromColor(r[x]);
					if (hsv.H is >= 90 and <= 135 && hsv.S >= 0.75f && hsv.V >= 0.5f)
						return;
				}
			}
		});
		top--;  // For the height of the '?'.

		if (top >= 128) return new(null, null);

		image.ProcessPixelRows(a => {
			for (bottom = top + 8; bottom < 144; bottom++) {
				var r = a.GetRowSpan(bottom);
				var found = false;
				for (var x = 112; x < 144; x++) {
					var hsv = HsvColor.FromColor(r[x]);
					if (hsv.H is >= 90 and <= 135 && hsv.S >= 0.75f && hsv.V >= 0.5f) {
						found = true;
						break;
					}
				}
				if (!found) return;
			}
		});
		bottom--;

		var textRect = ImageUtils.FindEdges(image, new(64, top, 128, bottom - top), c => HsvColor.FromColor(c) is HsvColor hsv && hsv.H is >= 90 and <= 135 && hsv.V >= 0.5f);
		debugBitmap?.Mutate(c => c.Draw(Color.Cyan, 1, textRect));
		return new(time, displayRecogniser.Recognise(image, textRect));
	}

	public record ReadData(int? Time, string? Message);
}