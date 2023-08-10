﻿using System.ComponentModel;
using Aiml;
using BombDefuserConnector.Widgets;

namespace BombDefuserScripts;

[AimlInterface]
internal static class Edgework {
	internal static void RegisterWidget(AimlAsyncContext context, WidgetReader? widget, Image<Rgba32> screenshot, LightsState lightsState, Point[] polygon) {
		if (widget is null) return;

		context.RequestProcess.Log(LogLevel.Info, $"Registering widget: {widget.Name}");
		switch (widget) {
			case BatteryHolder batteryHolder:
				GameState.Current.BatteryHolderCount++;
				GameState.Current.BatteryCount += DefuserConnector.Instance.ReadWidget(screenshot, lightsState, batteryHolder, polygon);
				break;
			case Indicator indicator:
				var data = DefuserConnector.Instance.ReadWidget(screenshot, lightsState, indicator, polygon);
				GameState.Current.Indicators.Add(new(data.IsLit, data.Label));
				break;
			case PortPlate portPlate:
				var ports = DefuserConnector.Instance.ReadWidget(screenshot, lightsState, portPlate, polygon);
				GameState.Current.PortPlates.Add((PortTypes) ports.Value);
				break;
			case SerialNumber serialNumber:
				GameState.Current.SerialNumber = DefuserConnector.Instance.ReadWidget(screenshot, lightsState, serialNumber, polygon);
				break;
		}
	}

	internal static void RegisterWidgets(AimlAsyncContext context, bool isSide, Image<Rgba32> screenshot) {
		var lightsState = DefuserConnector.Instance.GetLightsState(screenshot);
		if (lightsState != LightsState.On) throw new ArgumentException($"Can't identify widgets on lights state {lightsState}.");
		Point[][] polygons;
		if (isSide) {
			var adjustment = DefuserConnector.Instance.GetSideWidgetAdjustment(screenshot);
			polygons = new Point[4][];
			for (int i = 0; i < polygons.Length; i++) {
				var polygon = new Point[4];
				for (int j = 0; j < 4; j++) {
					polygon[j] = Utils.sideWidgetPointsLists[i][j];
					polygon[j].X += adjustment;
				}
				polygons[i] = polygon;
			}
		} else
			polygons = Utils.topBottomWidgetPointsLists;
		var widgets = polygons.Select(p => DefuserConnector.Instance.GetWidgetReader(screenshot, p)).ToList();
		for (var i = 0; i < widgets.Count; i++) {
			var widget = widgets[i];
			RegisterWidget(context, widget, screenshot, lightsState, polygons[i]);  // TODO: This assumes the vanilla bomb layout. It will need to be updated for other layouts.
		}
	}

	[AimlCategory("edgework"), EditorBrowsable(EditorBrowsableState.Never)]
	internal static void EdgeworkRequest(AimlAsyncContext context) {
		var batteries = GameState.Current.BatteryCount switch {
			0 => "No batteries.",
			1 => "1 battery in 1 holder.",
			_ => $"{GameState.Current.BatteryCount} batteries in {GameState.Current.BatteryHolderCount} {(GameState.Current.BatteryHolderCount == 1 ? "holder" : "holders")}."
		};
		var indicators = GameState.Current.Indicators.Count > 0
			? $"Indicators: {string.Join(", ", from i in GameState.Current.Indicators select $"{(i.IsLit ? "lit" : "unlit")} {NATO.Speak(i.Label)}")}."
			: "No indicators.";
		string ports;
		if (GameState.Current.PortPlates.Count > 0) {
			var emptyPlates = GameState.Current.PortPlates.Count(p => p == 0);
			var emptyPlatesDesc = emptyPlates switch { 0 => "", 1 => "; an empty port plate", _ => $"{emptyPlates} empty port plates" };
			var list = string.Join("; ", from p in GameState.Current.PortPlates where p != 0 select $"plate: {string.Join(' ', from t in GetPortTypes(p) select t switch { PortTypes.DviD => "DVI", PortTypes.PS2 => "PS2", PortTypes.RJ45 => "RJ45", PortTypes.StereoRCA => "RCA", _ => t.ToString() })}");
			ports = $"Ports: {list}{emptyPlatesDesc}.";
		} else
			ports = "No ports.";
		var serial = NATO.Speak(GameState.Current.SerialNumber);
		context.Reply($"{batteries} {indicators} {ports} Serial number: {serial}.");
	}

	private static IEnumerable<PortTypes> GetPortTypes(PortTypes ports) {
		if (ports.HasFlag(PortTypes.DviD)) yield return PortTypes.DviD;
		if (ports.HasFlag(PortTypes.Parallel)) yield return PortTypes.Parallel;
		if (ports.HasFlag(PortTypes.PS2)) yield return PortTypes.PS2;
		if (ports.HasFlag(PortTypes.RJ45)) yield return PortTypes.RJ45;
		if (ports.HasFlag(PortTypes.Serial)) yield return PortTypes.Serial;
		if (ports.HasFlag(PortTypes.StereoRCA)) yield return PortTypes.StereoRCA;
	}
}