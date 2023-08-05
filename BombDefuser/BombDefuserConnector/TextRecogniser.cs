﻿using System;
using System.IO;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BombDefuserConnector;
internal class TextRecogniser {
	internal static class Fonts {
		private static readonly FontCollection fontCollection = new();

		internal static readonly FontFamily CABIN_MEDIUM = LoadFontFamily(Properties.Resources.CabinMedium);
		internal static readonly FontFamily OSTRICH_SANS_HEAVY = LoadFontFamily(Properties.Resources.OstrichSansHeavy);

		private static FontFamily LoadFontFamily(byte[] fontFile) {
			using var ms = new MemoryStream(fontFile);
			return fontCollection.Add(ms);
		}
	}

	private readonly (Image<L8> image, float aspectRatio, string s)[] samples;

	public TextRecogniser(Font font, byte backgroundValue, byte foregroundValue, Size resolution, params string[] strings) {
		var compareValue = (backgroundValue + foregroundValue) / 2;
		Predicate<L8> predicate = foregroundValue > backgroundValue ? c => c.PackedValue >= compareValue : c => c.PackedValue < compareValue;

		this.samples = new (Image<L8>, float, string)[strings.Length];
		var textOptions = new TextOptions(font) { Dpi = 96, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Origin = new(resolution.Width / 2, resolution.Height / 2) };
		for (var i = 0; i < strings.Length; i++) {
			var image = new Image<L8>(resolution.Width, resolution.Height, new(backgroundValue));
			image.Mutate(c => c.DrawText(textOptions, strings[i], new Color(new L16((ushort) (foregroundValue << 8)))));
			var textBoundingBox = ImageUtils.FindEdges(image, image.Bounds, predicate);
			if (textBoundingBox.Top <= 0 || textBoundingBox.Bottom >= image.Height)
				throw new ArgumentException("Sample text height went out of the specified bounds.");
			if (textBoundingBox.Left <= 0 || textBoundingBox.Right >= image.Width)
				throw new ArgumentException("Sample text width went out of the specified bounds.");
			image.Mutate(c => c.Crop(textBoundingBox).Resize(resolution, KnownResamplers.NearestNeighbor, false));
			this.samples[i] = (image, (float) textBoundingBox.Width / textBoundingBox.Height, strings[i]);
		}
	}

	public string Recognise(Image<Rgba32> image, Rectangle rectangle) {
		string? result = null;
		var bestDist = int.MaxValue;
		var checkRatio = (float) rectangle.Width / rectangle.Height;

		foreach (var (refImage, refRatio, s) in this.samples) {
			if (Math.Abs(checkRatio - refRatio) > 1) continue;  // Skip strings that are way too narrow or too wide to match this rectangle.
			refImage.ProcessPixelRows(image, (ar, ac) => {
				var dist = 0;
				for (var y = 0; y < ar.Height; y++) {
					var rr = ar.GetRowSpan(y);
					var rc = ac.GetRowSpan(rectangle.Y + y * rectangle.Height / ar.Height);
					for (var x = 0; x < ar.Width; x++) {
						var pc = rc[rectangle.X + x * rectangle.Width / ar.Width];
						var lc = Math.Min(Math.Min(pc.R, pc.G), pc.B) + Math.Max(Math.Max(pc.R, pc.G), pc.B);
						dist += Math.Abs(lc - rr[x].PackedValue * 2);
						if (dist >= bestDist) return;
					}
				}
				// If we got here, this is the best match so far.
				bestDist = dist;
				result = s;
			});
		}

		return result ?? throw new ArgumentException("Couldn't recognise the text.");
	}
}
