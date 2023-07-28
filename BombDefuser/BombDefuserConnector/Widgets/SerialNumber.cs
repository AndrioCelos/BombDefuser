﻿using System;
using System.Collections.Generic;
using SixLabors.Fonts;
using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BombDefuserConnector.Widgets;
internal class SerialNumber : WidgetProcessor {
	public override string Name => "SerialNumber";

	private static readonly Font FONT;

	static SerialNumber() {
		using var ms = new MemoryStream(Properties.Resources.AnonymousProBold);
		var fontCollection = new FontCollection();
		var fontFamily = fontCollection.Add(ms);
		FONT = new(fontFamily, 72);

		referenceImages = new (Image<Rgb24> image, char c)[36];
		for (int i = 0; i < 10; i++)
			referenceImages[i] = CreateSampleImage((char) ('0' + i));
		for (int i = 0; i < 26; i++)
			referenceImages[10 + i] = CreateSampleImage((char) ('A' + i));  // I know that the letters 'O' and 'Y' never appear in the serial number normally, but I'm including all letters for simplicity.
	}

	private static (Image<Rgb24> image, char c) CreateSampleImage(char c) {
		var image = new Image<Rgb24>(128, 128, Color.White);
		image.Mutate(ctx => ctx.DrawText(new TextOptions(FONT) { Dpi = 96, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Origin = new(64, 64) }, c.ToString(), Color.Black));
		var charBB = ImageUtils.FindEdges(image, image.Bounds, c => c.B < 128);
		image.Mutate(ctx => ctx.Crop(charBB).Resize(64, 64, KnownResamplers.NearestNeighbor));
		return (image, c);
	}

	private static readonly (Image<Rgb24> image, char c)[] referenceImages;

	public override float IsWidgetPresent(Image<Rgb24> image, LightsState lightsState, PixelCounts pixelCounts)
		// This has many red pixels and white pixels.
		=> Math.Max(0, Math.Min(pixelCounts.Red, pixelCounts.White) - 4096) / 8192f;

	private static bool IsBlack(HsvColor hsv) => hsv.S < 0.2f && hsv.V < 0.2f;

	public override object Process(Image<Rgb24> image, LightsState lightsState, ref Image<Rgb24>? debugBitmap) {
		debugBitmap?.Mutate(c => c.Resize(new ResizeOptions() { Size = new(512, 512), Mode = ResizeMode.BoxPad, Position = AnchorPositionMode.TopLeft, PadColor = Color.Black }));

		// Find the text bounding box.
		var textBB = ImageUtils.FindEdges(image, image.Bounds, c => IsBlack(HsvColor.FromColor(c)));

		// Find out whether the image is upside-down or not.
		bool? isUpsideDown = null;
		image.ProcessPixelRows(a => {
			for (var i = 1; ; i++) {
				for (var rowNumber = 0; rowNumber < 2; rowNumber++) {
					var y = rowNumber == 0 ? textBB.Top - i : textBB.Bottom + i;
					if (y >= 0 && y < a.Height) {
						var r = a.GetRowSpan(y);
						for (var x = textBB.Left; x <= textBB.Right; x++) {
							if (r[x].R >= 96 && r[x].G < 32 && r[x].B < 32) {
								isUpsideDown = rowNumber != 0;
								return;
							}
						}
					}
				}
			}
		});

		debugBitmap?.Mutate(c => c.Draw(isUpsideDown!.Value ? Color.Yellow : Color.Lime, 1, textBB));

		var charImages = new List<Image<Rgb24>>();
		var lastX = 0;
		int? charStart = null;
		for (var i = 0; i < textBB.Width; i++) {
			for (; i < textBB.Width; i++) {
				var x = isUpsideDown!.Value ? textBB.Right - 1 - i : textBB.Left + i;
				var anyPixels = false;
				for (var y = textBB.Top; y <= textBB.Bottom; y++) {
					if (image[x, y].B < 128) {
						anyPixels = true;
						break;
					}
				}
				if (charStart.HasValue) {
					if (!anyPixels)
						break;
				} else {
					if (anyPixels) {
						charStart = x;
					}
				}
				lastX = x;
			}
			if (charStart.HasValue) {
				var charImage = image.Clone(c => c.Crop(new Rectangle(Math.Min(charStart.Value, lastX), textBB.Y, Math.Abs(lastX - charStart.Value) + 1, textBB.Height)).Resize(64, 64, KnownResamplers.NearestNeighbor));
				if (isUpsideDown == true)
					charImage.Mutate(c => c.Rotate(RotateMode.Rotate180));
				charImages.Add(charImage);
				charStart = null;
			}
		}

		if (debugBitmap != null) {
			for (var i = 0; i < charImages.Count; i++) {
				debugBitmap.Mutate(c => c.DrawImage(charImages[i], new Point(64 * i, 256), 1));
			}
		}
		if (charImages.Count != 6) throw new ArgumentException("Found wrong number of characters");

		var chars = new char[6];
		for (int i = 0; i < 6; i++) {
			var bestDist = int.MaxValue;
			foreach (var (refImage, c) in referenceImages) {
				var dist = 0;
				for (var y = 0; y < refImage.Height; y++) {
					for (var x = 0; x < refImage.Width; x++) {
						var cr = refImage[x, y];
						var cs = charImages[i][x, y];
						dist += Math.Abs(cr.B - cs.B);
					}
				}
				if (dist < bestDist) {
					bestDist = dist;
					chars[i] = c;
				}
			}
		}

		return new string(chars);
	}
}
