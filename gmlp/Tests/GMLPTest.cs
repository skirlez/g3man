using System.Collections.Generic;
using gmlp;

namespace gmlp.Tests;


public abstract class LanguageTest(string name) {
	public readonly string Name = name;
	public abstract string GetCode();
	public abstract string[] GetPatchSections();
	
	public abstract string GetExpected();


	public bool[] GetPatchesCritical() {
		bool[] arr = new bool[GetPatchSections().Length];
		for (int i = 0; i < arr.Length; i++) {
			arr[i] = true;
		}

		return arr;
	}

	public string GetResult() {
		string code = GetCode();
		string[] patchSections = GetPatchSections();
		bool[] patchesCritical = GetPatchesCritical();
		PatchesRecord record = new PatchesRecord();

		int patchIncrement = 0;
		for (int i = patchSections.Length - 1; i >= 0; i--) {
			Language.ExecutePatchSection(patchSections[i], Name, code, patchesCritical[i], new PatchOwner($"{Name}-{i}"), record, ref patchIncrement);
		}

		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		CodeSource source = new DictionaryCodeSource(dictionary);
		Language.ApplyPatches(record, source, []);
		return dictionary[Name];
	}
	
}