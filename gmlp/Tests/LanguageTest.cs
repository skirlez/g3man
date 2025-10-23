using System.Collections.Generic;
using System.Linq;

namespace gmlp.Tests;


public abstract class LanguageTest(string name) {
	public readonly string Name = name;
	public abstract string GetCode();
	public abstract string[] GetPatchSections();
	
	public abstract string GetExpected();


	public virtual bool[] GetPatchesCritical() {
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

		List<PatchOwner> owners = patchSections.Select((_, i) => new PatchOwner($"{Name}-{i}")).ToList();
		
		
		int patchIncrement = 0;
		for (int i = 0; i < patchSections.Length; i++) {
			Language.ExecutePatchSection(patchSections[i], Name, code, patchesCritical[i], owners[i], record, ref patchIncrement);
			patchIncrement = 0;
		}

		owners.Reverse();
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		CodeSource source = new DictionaryCodeSource(dictionary);
		Language.ApplyPatches(record, source, owners);
		return dictionary[Name];
	}
	
}