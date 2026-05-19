using System.Collections.Generic;

namespace gmlp;


public class UnitOperations() {
	private readonly Dictionary<int, List<PatchOperation>> unitPatches = new();
	public List<PatchOperation> GetPatchOperationsOrCreate(int target) {
		if (unitPatches.ContainsKey(target))
			return unitPatches[target];
		List<PatchOperation> operations = new List<PatchOperation>();
		unitPatches[target] = operations;
		return operations;
	}

	public Dictionary<int, List<PatchOperation>> GetData() {
		return unitPatches;
	}
}

public class PatchOperation(string text, bool critical, OperationType type, int increment) {
	public string Text = text;
	public readonly bool Critical = critical;
	public readonly OperationType Type = type;
	

	// gets incremented for each patch operation in a patch file, so they can sort by each other.
	public int Increment = increment;
	
	public int IsHigherPriorityThan(PatchOperation other) {
		return int.Sign(other.Increment - Increment);
	}
	
	public static readonly Dictionary<string, OperationType> WriteOperationTypes = new Dictionary<string, OperationType> {
		{ "write_replace", OperationType.WriteReplace },
		{ "write_before", OperationType.WriteBefore },
		{ "write_before_last", OperationType.WriteBefore }, // deprecated
		{ "write", OperationType.Write },
		{ "write_last", OperationType.Write }, // deprecated
		{ "write_else_if",  OperationType.WriteElseIf },
		{ "write_else", OperationType.WriteElse },
		{ "write_and_condition",  OperationType.WriteAndCondition },
		{ "write_or_condition", OperationType.WriteOrCondition },
	};
}

// this is really dumb. this is the only operation with a subclass, since it's the only one that needs more than a string
public class ReplaceSubstringPatchOperation(string oldStr, string newStr, bool regex, bool critical, int increment)
	: PatchOperation(newStr, critical, OperationType.WriteReplaceSubstring, increment) {

	public string OldText = oldStr;
	public bool Regex = regex;
}

public enum OperationType {
	WriteReplace,
	WriteReplaceSubstring,
	WriteBefore,
	Write,
	WriteElseIf,
	WriteElse,
	
	WriteAndCondition,
	WriteOrCondition
}

