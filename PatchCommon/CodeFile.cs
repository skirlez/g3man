using System.Linq;

namespace PatchCommon;

public class CodeFile(string code) {
	private string[] lines = code.Split("\n");
	public string GetAsString() {
		return code;
	}
	public string[] GetAsLines() {
		return lines;
	}
}