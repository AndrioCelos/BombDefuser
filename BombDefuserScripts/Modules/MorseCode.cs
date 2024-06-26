﻿using System.Collections;
using System.Runtime.CompilerServices;
using System.Text;

namespace BombDefuserScripts.Modules;

[AimlInterface("MorseCode")]
internal class MorseCode : ModuleScript<BombDefuserConnector.Components.MorseCode> {
	public override string IndefiniteDescription => "Morse Code";
	private static Interrupt? interrupt;
	private static CancellationTokenSource? cancellationTokenSource;
	private static readonly int DASH_THRESHOLD = 4;
	private static readonly int WORD_SPACE_THRESHOLD = 10;

	private static readonly Dictionary<MorseLetter, char> decodeMorse = new() {
		{ new(MorseElement.Dot, MorseElement.Dash), 'A' },
		{ new(MorseElement.Dash, MorseElement.Dot, MorseElement.Dot, MorseElement.Dot), 'B' },
		{ new(MorseElement.Dash, MorseElement.Dot, MorseElement.Dash, MorseElement.Dot), 'C' },
		{ new(MorseElement.Dash, MorseElement.Dot, MorseElement.Dot), 'D' },
		{ new(MorseElement.Dot), 'E' },
		{ new(MorseElement.Dot, MorseElement.Dot, MorseElement.Dash, MorseElement.Dot), 'F' },
		{ new(MorseElement.Dash, MorseElement.Dash, MorseElement.Dot), 'G' },
		{ new(MorseElement.Dot, MorseElement.Dot, MorseElement.Dot, MorseElement.Dot), 'H' },
		{ new(MorseElement.Dot, MorseElement.Dot), 'I' },
		{ new(MorseElement.Dot, MorseElement.Dash, MorseElement.Dash, MorseElement.Dash), 'J' },
		{ new(MorseElement.Dash, MorseElement.Dot, MorseElement.Dash), 'K' },
		{ new(MorseElement.Dot, MorseElement.Dash, MorseElement.Dot, MorseElement.Dot), 'L' },
		{ new(MorseElement.Dash, MorseElement.Dash), 'M' },
		{ new(MorseElement.Dash, MorseElement.Dot), 'N' },
		{ new(MorseElement.Dash, MorseElement.Dash, MorseElement.Dash), 'O' },
		{ new(MorseElement.Dot, MorseElement.Dash, MorseElement.Dash, MorseElement.Dot), 'P' },
		{ new(MorseElement.Dash, MorseElement.Dash, MorseElement.Dot, MorseElement.Dash), 'Q' },
		{ new(MorseElement.Dot, MorseElement.Dash, MorseElement.Dot), 'R' },
		{ new(MorseElement.Dot, MorseElement.Dot, MorseElement.Dot), 'S' },
		{ new(MorseElement.Dash), 'T' },
		{ new(MorseElement.Dot, MorseElement.Dot, MorseElement.Dash), 'U' },
		{ new(MorseElement.Dot, MorseElement.Dot, MorseElement.Dot, MorseElement.Dash), 'V' },
		{ new(MorseElement.Dot, MorseElement.Dash, MorseElement.Dash), 'W' },
		{ new(MorseElement.Dash, MorseElement.Dot, MorseElement.Dot, MorseElement.Dash), 'X' },
		{ new(MorseElement.Dash, MorseElement.Dot, MorseElement.Dash, MorseElement.Dash), 'Y' },
		{ new(MorseElement.Dash, MorseElement.Dash, MorseElement.Dot, MorseElement.Dot), 'Z' },
		{ new(MorseElement.Dash, MorseElement.Dash, MorseElement.Dash, MorseElement.Dash, MorseElement.Dash), '0' },
		{ new(MorseElement.Dot, MorseElement.Dash, MorseElement.Dash, MorseElement.Dash, MorseElement.Dash), '1' },
		{ new(MorseElement.Dot, MorseElement.Dot, MorseElement.Dash, MorseElement.Dash, MorseElement.Dash), '2' },
		{ new(MorseElement.Dot, MorseElement.Dot, MorseElement.Dot, MorseElement.Dash, MorseElement.Dash), '3' },
		{ new(MorseElement.Dot, MorseElement.Dot, MorseElement.Dot, MorseElement.Dot, MorseElement.Dash), '4' },
		{ new(MorseElement.Dot, MorseElement.Dot, MorseElement.Dot, MorseElement.Dot, MorseElement.Dot), '5' },
		{ new(MorseElement.Dash, MorseElement.Dot, MorseElement.Dot, MorseElement.Dot, MorseElement.Dot), '6' },
		{ new(MorseElement.Dash, MorseElement.Dash, MorseElement.Dot, MorseElement.Dot, MorseElement.Dot), '7' },
		{ new(MorseElement.Dash, MorseElement.Dash, MorseElement.Dash, MorseElement.Dot, MorseElement.Dot), '8' },
		{ new(MorseElement.Dash, MorseElement.Dash, MorseElement.Dash, MorseElement.Dash, MorseElement.Dot), '9' }
	};

	private static readonly Dictionary<string, int> frequencies = new() {
		{ "505",  0 },
		{ "515",  1 },
		{ "522",  2 },
		{ "532",  3 },
		{ "535",  4 },
		{ "542",  5 },
		{ "545",  6 },
		{ "552",  7 },
		{ "555",  8 },
		{ "565",  9 },
		{ "572", 10 },
		{ "575", 11 },
		{ "582", 12 },
		{ "592", 13 },
		{ "595", 14 },
		{ "600", 15 }
	};

	private int highlight;  // For this script, 0 => down button, 1 => right button, 2 => submit button
	private int selectedFrequency;

	protected internal override void Started(AimlAsyncContext context) => context.AddReply("ready");

	[AimlCategory("read")]
	internal static Task Read(AimlAsyncContext context) {
		context.Reply($"Stand by.");
		cancellationTokenSource = new();
		return GameState.Current.CurrentScript<MorseCode>().ReadAsync(context, cancellationTokenSource.Token);
	}

	private async Task ReadAsync(AimlAsyncContext context, CancellationToken cancellationToken) {
		var interrupt = await this.ModuleInterruptAsync(context);
		try {
			MorseCode.interrupt = interrupt;

			// Wait for a space between letters.
			interrupt.Context.RequestProcess.Log(Aiml.LogLevel.Info, $"[MorseCode] Waiting for letter space");
			while (true) {
				await WaitForStateAsync(interrupt, false, cancellationToken);
				var continuedLetter = await WaitForStateAsync(interrupt, true, DASH_THRESHOLD, cancellationToken);
				if (!continuedLetter) break;
			}
			interrupt.Context.RequestProcess.Log(Aiml.LogLevel.Info, $"[MorseCode] Letter space found");

			for (var lettersRead = 0; lettersRead < 5; lettersRead++) {
				// We've just seen a space between letters. Find out whether it is a space between words.
				interrupt.Context.RequestProcess.Log(Aiml.LogLevel.Info, $"[MorseCode] Waiting for next letter");
				var continuedWord = await WaitForStateAsync(interrupt, true, WORD_SPACE_THRESHOLD - DASH_THRESHOLD, cancellationToken);
				if (cancellationToken.IsCancellationRequested) return;
				if (!continuedWord && !interrupt.IsDisposed) {
					interrupt.Context.RequestProcess.Log(Aiml.LogLevel.Info, $"[MorseCode] Word start");
					interrupt.Context.Reply("Word start.");
					interrupt.Context.Reply("<reply>505</reply><reply>515</reply><reply>522</reply><reply>532</reply><reply>535</reply><reply>542</reply><reply>545</reply><reply>552</reply><reply>555</reply><reply>565</reply><reply>572</reply><reply>575</reply><reply>582</reply><reply>592</reply><reply>595</reply><reply>600</reply>");
					await WaitForStateAsync(interrupt, true, cancellationToken);
				}
				interrupt.Context.RequestProcess.Log(Aiml.LogLevel.Info, $"[MorseCode] Next letter started");

				var currentLetter = new MorseLetter();
				while (true) {
					var isDot = await WaitForStateAsync(interrupt, false, DASH_THRESHOLD, cancellationToken);
					interrupt.Context.RequestProcess.Log(Aiml.LogLevel.Info, $"[MorseCode] {(isDot ? "Dot" : "Dash")}");
					currentLetter.Add(isDot ? MorseElement.Dot : MorseElement.Dash);
					if (!isDot) await WaitForStateAsync(interrupt, false, cancellationToken);
					var continuedLetter = await WaitForStateAsync(interrupt, true, DASH_THRESHOLD, cancellationToken);
					if (!continuedLetter) break;
				}
				if (cancellationToken.IsCancellationRequested || interrupt.IsDisposed) return;
				interrupt.Context.RequestProcess.Log(Aiml.LogLevel.Info, $"[MorseCode] Letter space found");

				if (decodeMorse.TryGetValue(currentLetter, out var c)) {
					interrupt.Context.RequestProcess.Log(Aiml.LogLevel.Info, $"[MorseCode] Decoded letter: {c}");
					interrupt.Context.Reply(NATO.Speak(c.ToString()));
				} else
					interrupt.Context.Reply(string.Join(' ', currentLetter));
				interrupt.Context.Reply("<reply>505</reply><reply>515</reply><reply>522</reply><reply>532</reply><reply>535</reply><reply>542</reply><reply>545</reply><reply>552</reply><reply>555</reply><reply>565</reply><reply>572</reply><reply>575</reply><reply>582</reply><reply>592</reply><reply>595</reply><reply>600</reply>");
			}
		} finally {
			// Only dispose the interrupt if we're not ending due to a cancellation signal, as that indicates we're keeping the interrupt to submit an answer.
			if (!cancellationToken.IsCancellationRequested) {
				interrupt.Dispose();
				MorseCode.interrupt = null;
			}
		}
	}

	private static Task<bool> WaitForStateAsync(Interrupt interrupt, bool state, CancellationToken token) => WaitForStateAsync(interrupt, state, int.MaxValue, token);
	private static async Task<bool> WaitForStateAsync(Interrupt interrupt, bool state, int limit, CancellationToken token) {
		if (token.IsCancellationRequested) return false;
		var count = 0;
		do {
			await Delay(0.075);
			if (token.IsCancellationRequested) return false;
			if (interrupt.IsDisposed || interrupt.Read(Reader).IsLightOn == state) {
				interrupt.Context.RequestProcess.Log(Aiml.LogLevel.Info, $"[MorseCode] Awaited state {state} reached after {count}");
				return true;
			}
			count++;
		} while (count < limit);
		interrupt.Context.RequestProcess.Log(Aiml.LogLevel.Info, $"[MorseCode] Awaited state {state} timed out after {count}");
		return false;
	}

	[AimlCategory("<set>number</set>")]
	[AimlCategory("submit <set>number</set>")]
	[AimlCategory("transmit at <set>number</set>")]
	[AimlCategory("respond at <set>number</set>")]
	internal static async Task Submit1(AimlAsyncContext context, string s) {
		if (!frequencies.TryGetValue(s, out var frequency)) {
			context.Reply("That is not a valid frequency.");
			return;
		}
		var script = GameState.Current.CurrentScript<MorseCode>();
		await script.Submit(context, frequency);
	}

	[AimlCategory("<set>number</set> <set>number</set> <set>number</set>")]
	[AimlCategory("submit <set>number</set> <set>number</set> <set>number</set>")]
	[AimlCategory("transmit at <set>number</set> <set>number</set> <set>number</set>")]
	[AimlCategory("respond at <set>number</set> <set>number</set> <set>number</set>")]
	internal static async Task Submit2(AimlAsyncContext context, string d1, string d2, string d3) {
		if (!frequencies.TryGetValue($"{d1}{d2}{d3}", out var frequency)) {
			context.Reply("That is not a valid frequency.");
			return;
		}
		var script = GameState.Current.CurrentScript<MorseCode>();
		await script.Submit(context, frequency);
	}

	internal async Task Submit(AimlAsyncContext context, int frequency) {
		cancellationTokenSource?.Cancel();
		cancellationTokenSource?.Dispose();
		cancellationTokenSource = null;
		using var interrupt = MorseCode.interrupt ?? await this.ModuleInterruptAsync(context);
		interrupt.Context = context;
		var buttons = new List<Button>();
		if (frequency < this.selectedFrequency) {
			switch (this.highlight) {
				case 1: buttons.Add(Button.Left); break;
				case 2: buttons.Add(Button.Up); break;
			}
			this.highlight = 0;
			do {
				buttons.Add(Button.A);
				this.selectedFrequency--;
			} while (frequency < this.selectedFrequency);
		}
		if (frequency > this.selectedFrequency) {
			if (this.highlight != 1) {
				buttons.Add(Button.Right);
				this.highlight = 1;
			}
			do {
				buttons.Add(Button.A);
				this.selectedFrequency++;
			} while (frequency > this.selectedFrequency);
		}
		if (this.highlight != 2) {
			buttons.Add(Button.Down);
			this.highlight = 2;
		}
		buttons.Add(Button.A);
		await interrupt.SubmitAsync(buttons);
	}

	internal struct MorseLetter : IEquatable<MorseLetter>, IEnumerable<MorseElement> {
		public int Bits;
		public int Length;

		public MorseLetter(MorseElement e1) {
			this.Length = 1;
			this.Bits = (int) e1;
		}
		public MorseLetter(MorseElement e1, MorseElement e2) {
			this.Length = 2;
			this.Bits = (int) e1 | (int) e2 << 1;
		}
		public MorseLetter(MorseElement e1, MorseElement e2, MorseElement e3) {
			this.Length = 3;
			this.Bits = (int) e1 | (int) e2 << 1 | (int) e3 << 2;
		}
		public MorseLetter(MorseElement e1, MorseElement e2, MorseElement e3, MorseElement e4) {
			this.Length = 4;
			this.Bits = (int) e1 | (int) e2 << 1 | (int) e3 << 2 | (int) e4 << 3;
		}
		public MorseLetter(MorseElement e1, MorseElement e2, MorseElement e3, MorseElement e4, MorseElement e5) {
			this.Length = 5;
			this.Bits = (int) e1 | (int) e2 << 1 | (int) e3 << 2 | (int) e4 << 3 | (int) e5 << 4;
		}

		public void Add(MorseElement element) {
			if (element != MorseElement.Dot) this.Bits |= 1 << this.Length;
			this.Length++;
		}

		public readonly bool Equals(MorseLetter other) => this.Bits == other.Bits && this.Length == other.Length;
		public override readonly bool Equals(object? obj) => obj is MorseLetter letter && this.Equals(letter);
		public static bool operator ==(MorseLetter v1, MorseLetter v2) => v1.Equals(v2);
		public static bool operator !=(MorseLetter v1, MorseLetter v2) => !v1.Equals(v2);

		public override readonly int GetHashCode() => HashCode.Combine(this.Bits, this.Length);
		public readonly IEnumerator<MorseElement> GetEnumerator() {
			for (var i = 0; i < this.Length; i++)
				yield return (this.Bits & 1 << i) == 0 ? MorseElement.Dot : MorseElement.Dash;
		}
		readonly IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
	}

	internal enum MorseElement {
		Dot,
		Dash
	}
}
