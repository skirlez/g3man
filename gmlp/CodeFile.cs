namespace gmlp;

public abstract class CodeFile {
	public abstract string GetAsString();
}

public class StringCodeFile(string code) : CodeFile {
	public override string GetAsString() {
		return code;
	}
}