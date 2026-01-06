using System.Collections.Generic;

namespace gmlp;


/**
 * Represents a container for code files
 */
public abstract class CodeSource {
	
	/**
	 * Return the code file associated with the filename,
	 * or null if it doesn't exist.
	 */
	public abstract CodeFile? GetCodeFile(string file);
	
	/**
	* If code has been replaced for this file, return the string that was
	* given exactly as it was given,
	* If nothing was given, you may return null or a string of your choosing.
	*/
	public abstract string? GetReplacedCodeVerbatim(string file);
	
	/**
	* Replace the code for said this file with the new code string.
	*/
	public abstract void Replace(string file, string code);
}

public class DictionaryCodeSource(Dictionary<string, string> dictionary) : CodeSource {
	public override CodeFile? GetCodeFile(string file) {
		return new StringCodeFile(file);
	}

	public override string? GetReplacedCodeVerbatim(string file) {
		return dictionary[file];
	}
	public override void Replace(string file, string code) {
		dictionary[file] = code;
	}
}