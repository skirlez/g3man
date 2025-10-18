namespace g3man.GMLP.Tests;


public abstract class GMLPTest(string name) {
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
			GMLP.ExecutePatchSection(patchSections[i], Name, code, patchesCritical[i], new TestPatchOwner(i, $"{Name}-{i}"), record, ref patchIncrement);
		}

		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		GMLP.ApplyPatches(record, new DictionaryPatchApplier(dictionary), []);
		return dictionary[Name];
	}
	
}