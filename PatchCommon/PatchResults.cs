using System.Collections.Generic;
using System.Diagnostics;
namespace PatchCommon;

public class PatchResults {
	private Dictionary<string, string> dictionary = new();
	private List<string> errors = new();
	/**
	* Replace the code for said this file with the new code string.
	*/
	public bool ResultExists(string file) {
		return dictionary.ContainsKey(file);
	}
	public void AddResult(string file, string code) {
		dictionary[file] = code;
	}

	public string GetResult(string file) {
		Debug.Assert(ResultExists(file));
		return dictionary[file];
	}
	public IEnumerable<KeyValuePair<string, string>> GetAllResults() {
		return dictionary;
	}
	
	public IEnumerable<string> GetAllErrors() {
		return errors;
	}
	
	public void AddErrors(IEnumerable<string> newErrors) {
		errors.AddRange(newErrors);
	}
	public void AddError(string file, string error) {
		errors.Add(error);
	}

	public bool HasErrors() {
		return errors.Count != 0;
	}
}