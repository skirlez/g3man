using System;

namespace PatchCommon;


public class RecursiveException : Exception {
	private Exception? Next;
	private Func<Exception, string> PrintNext;

	private static Func<Exception, string> PrintNextDefault = (e) => e.ToString();
	public RecursiveException(string message, Exception? next = null, Func<Exception, string>? howToPrintNext = null) : base(message) {
		Next = next;
		PrintNext = howToPrintNext ?? PrintNextDefault;
	}
	
	public override string ToString() {
		string a = (Next is null) ? "" : PrintNext(Next);
		return $"- {this.Message}:\n{a}";
	}
}


public class PatchBadIntentionsException(string message, Exception? next = null) : RecursiveException(message, next);
public class PatchRealizationException(string message, Exception? next = null) : RecursiveException(message, next);
