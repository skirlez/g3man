using System.Collections.Generic;

namespace gmlp;

public abstract class CodeSource {
	public abstract CodeFile? GetCodeFile(string file);
	public abstract void Replace(string file, string code);
}

public class DictionaryCodeSource(Dictionary<string, string> dictionary) : CodeSource {
	public override CodeFile? GetCodeFile(string file) {
		return new TestCodeFile(file);
	}

	public override void Replace(string file, string code) {
		dictionary[file] = code;
	}
}