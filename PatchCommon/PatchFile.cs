namespace PatchCommon;

public readonly struct PatchFile(string content, string identifier) {
	public readonly string Content = content;
	public readonly string Identifier = identifier;
}