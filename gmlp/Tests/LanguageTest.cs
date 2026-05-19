using System;
using System.Collections.Generic;
using System.Linq;
using PatchCommon;

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
		Dictionary<string, string> dictionary = new Dictionary<string, string> {
			["only"] = code,
		};
		CodeSource source = new DictionaryCodeSource(dictionary);
		string[] patchSections = GetPatchSections();
		bool[] patchesCritical = GetPatchesCritical();
		
		string patch = "";
		int i = 0;
		foreach (string patchSection in patchSections) {
			patch += $"meta:\ntargets='only'\ncritical={patchesCritical[i].ToString().ToLowerInvariant()}\npatch:\n{patchSection}\n";
		}

		PatchIntentionAggregate<UnitOperations> aggregate = new();
		gmlp.Language.FindIntentions(patch, "test", aggregate);
	
		
		RecordAggregate<UnitOperations> record = aggregate.RealizeAll(source);
		PatchResults results = gmlp.Language.Apply(record, source);
		
		return results.GetResult("only");
	}
	
}