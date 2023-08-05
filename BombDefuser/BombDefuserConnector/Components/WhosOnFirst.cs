﻿using System;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BombDefuserConnector.Components;
public class WhosOnFirst : ComponentProcessor<WhosOnFirst.ReadData> {
	public override string Name => "Who's on First";
	protected internal override bool UsesNeedyFrame => false;

	private static readonly TextRecogniser displayRecogniser = new(new(TextRecogniser.Fonts.CABIN_MEDIUM, 24), 88, 255, new(256, 64),
		"YES", "FIRST", "DISPLAY", "OKAY", "SAYS", "NOTHING", "BLANK", "NO", "LED", "LEAD", "READ", "RED", "REED", "LEED",
		"HOLD ON", "YOU", "YOU ARE", "YOUR", "YOU'RE", "UR", "THERE", "THEY'RE", "THEIR", "THEY ARE", "SEE", "C", "CEE");
	private static readonly TextRecogniser keyRecogniser = new(new(TextRecogniser.Fonts.OSTRICH_SANS_HEAVY, 24), 160, 44, new(128, 64),
		"READY", "FIRST", "NO", "BLANK", "NOTHING", "YES", "WHAT", "UHHH", "LEFT", "RIGHT", "MIDDLE", "OKAY", "WAIT", "PRESS",
		"YOU", "YOU ARE", "YOUR", "YOU’RE", "UR", "U", "UH HUH", "UH UH", "WHAT?", "DONE", "NEXT", "HOLD", "SURE", "LIKE");

	protected internal override float IsModulePresent(Image<Rgb24> image) {
		// Who's on First: look for the display and keys
		var referenceColour = new Rgb24(71, 91, 104);
		var referenceColour2 = new Rgb24(200, 154, 140);
		var referenceColour3 = new Rgb24(51, 46, 37);
		var count = 0f;
		var count2 = 0f;

		for (var x = 48; x < 144; x++) {
			var pixel = image[x, 48];
			var dist = Math.Abs(pixel.R - referenceColour.R) + Math.Abs(pixel.G - referenceColour.G) + Math.Abs(pixel.B - referenceColour.B);
			count += Math.Max(0, 1 - dist * dist / 1000f);
		}

		for (var y = 96; y < 224; y += 4) {
			for (var x = 32; x < 172; x += 4) {
				var pixel = image[x, y];
				var dist = pixel.R < 64
					? Math.Abs(pixel.R - referenceColour3.R) + Math.Abs(pixel.G - referenceColour3.G) + Math.Abs(pixel.B - referenceColour3.B)
					: Math.Abs(pixel.R - referenceColour2.R) + Math.Abs(pixel.G - referenceColour2.G) + Math.Abs(pixel.B - referenceColour2.B);
				count2 += Math.Max(0, 1 - dist * dist / 10000f);
			}
		}

		return count / 192 + count2 / 2240;
	}
	protected internal override ReadData Process(Image<Rgb24> image, ref Image<Rgb24>? debugBitmap) {
		var displayBorderRect = ImageUtils.FindEdges(image, new(28, 22, 140, 50), c => c.R < 44 && c.G < 44 && c.B < 44);
		displayBorderRect.Inflate(-4, -4);
		var textRect = ImageUtils.FindEdges(image, displayBorderRect, c => c.B >= 192);

		debugBitmap?.Mutate(c => c.Draw(Color.Red, 1, displayBorderRect).Draw(Color.Lime, 1, textRect));
		var displayText = textRect.Height == 0 ? "" : displayRecogniser.Recognise(image, textRect);

		static bool isKeyBackground(Rgb24 c) => HsvColor.FromColor(c) is HsvColor hsv && hsv.H is >= 30 and <= 45 && hsv.S is >= 0.2f and <= 0.4f;

		var baseRects = new[] {
			ImageUtils.FindEdges(image, new(31, 90, 80, 48), isKeyBackground),
			ImageUtils.FindEdges(image, new(105, 90, 80, 48), isKeyBackground),
			ImageUtils.FindEdges(image, new(31, 134, 80, 48), isKeyBackground),
			ImageUtils.FindEdges(image, new(105, 134, 80, 48), isKeyBackground),
			ImageUtils.FindEdges(image, new(31, 175, 80, 48), isKeyBackground),
			ImageUtils.FindEdges(image, new(105, 175, 80, 48), isKeyBackground),
		};
		var keyLabels = new string[6];
		for (int i = 0; i < 6; i++) {
			var keyRect = i switch {
				0 => new Rectangle(24, 90, 74, 44),
				1 => new Rectangle(104, 90, 74, 44),
				2 => new Rectangle(24, 134, 74, 44),
				3 => new Rectangle(104, 134, 74, 44),
				4 => new Rectangle(24, 178, 74, 44),
				_ => new Rectangle(104, 178, 74, 44)
			};
			keyRect = ImageUtils.FindEdges(image, keyRect, isKeyBackground);
			textRect = Rectangle.Inflate(keyRect, -2, -5);
			textRect = ImageUtils.FindEdges(image, textRect, c => c.R < 128);
			debugBitmap?.Mutate(c => c.Draw(Color.Yellow, 1, keyRect).Draw(Color.Green, 1, textRect));
			keyLabels[i] = keyRecogniser.Recognise(image, textRect);
		}

		return new(ReadStageIndicator(image), displayText, keyLabels);
	}

	public record ReadData(int StagesCleared, string Display, string[] Keys) {
		public override string ToString() => $"ReadData {{ StagesCleared = {this.StagesCleared}, Display = {this.Display}, Keys = {{ {string.Join(", ", this.Keys)} }} }}";
	}
}
