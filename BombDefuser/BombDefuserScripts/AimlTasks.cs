﻿namespace BombDefuserScripts;
public static class AimlTasks {
	[AimlResponse("OOB Tick *")]
	private static readonly AimlTaskFactory timer = new("<oob><timer duration='{0}'><postback>{ID}</postback></timer></oob>");
	[AimlResponse("OOB DefuserCallback *")]
	private static readonly AimlTaskFactory sendInputs = new("<oob><sendinputs>{0} callback:{ID}</sendinputs></oob>");

	public static AimlTask Delay(TimeSpan timeSpan) => timer.CallAsync(timeSpan.TotalSeconds);
	public static AimlTask Delay(object seconds) => timer.CallAsync(seconds);
	public static AimlTask SendInputsAsync(string inputs) => sendInputs.CallAsync(inputs);
	public static AimlTask SendInputsAsync(this Interrupt interrupt, string inputs) => sendInputs.CallAsync(inputs);
	public static void SendInputs(this Interrupt interrupt, string inputs) => interrupt.Context.Reply($"<oob><sendinputs>{inputs}</sendinputs></oob>");
}
