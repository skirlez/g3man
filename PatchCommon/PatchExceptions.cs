using System;

namespace PatchCommon;

public class PatchException(string message, string filename = "unknown") : Exception(message) {
	public string Filename = filename;
	public override string ToString() {
		return $"In patch \"{Filename}\":\n{this.Message}";
	}
}

public class PatchRealizationException(string message) : PatchException(message);
public class PatchBadIntentionsException(string message) : PatchException(message);
