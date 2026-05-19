using System.Collections.Generic;

namespace PatchCommon;


/**
 * Represents a source of code files
 */
public abstract class CodeSource {
	
	/**
	 * Return the code file associated with the filename,
	 * or null if it doesn't exist.
	 */
	public abstract CodeFile? GetCodeFile(string file);
}

/**
* Code source backed by a dictionary
*/
public class DictionaryCodeSource(Dictionary<string, string> dictionary) : CodeSource {
	public override CodeFile GetCodeFile(string file) {
		return new CodeFile(dictionary[file]);
	}
	public string GetReplacedFile(string file) {
		return dictionary[file];
	}
}