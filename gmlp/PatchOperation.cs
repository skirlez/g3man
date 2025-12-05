using System.Collections.Generic;

namespace gmlp;


public class PatchesRecord {
	private readonly Dictionary<string, UnitOperations> record = new Dictionary<string, UnitOperations>();
	public Dictionary<string, UnitOperations> GetData() {
		return record;
	}
	public UnitOperations GetUnitOperationsOrCreate(string target, string code) {
		if (record.ContainsKey(target))
			return record[target];
		UnitOperations unitOperations = new UnitOperations(code);
		record[target] = unitOperations;
		return unitOperations;
	}
}

public class UnitOperations(string code) {
	public readonly string Code = code;
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

public class PatchOperation(string text, bool critical, OperationType type, PatchOwner owner, int increment) {
	public string Text = text;
	public readonly bool Critical = critical;
	public readonly OperationType Type = type;
	
	public readonly PatchOwner Owner = owner;

	// gets incremented for each patch operation in a patch file, so they can sort by each other.
	public int Increment = increment;
	
	public int IsHigherPriorityThan(PatchOperation other, List<PatchOwner> mods) {
		int ownerComparison = Owner.IsHigherPriorityThan(other.Owner, mods);
		if (ownerComparison != 0)
			return ownerComparison;
		
		return int.Sign(other.Increment - Increment);
	}
	
	public static readonly Dictionary<string, OperationType> WriteOperationTypes = new Dictionary<string, OperationType> {
		{ "write_replace", OperationType.WriteReplace },
		{ "write_before", OperationType.WriteBefore },
		{ "write_before_last", OperationType.WriteBeforeLast },
		{ "write", OperationType.Write },
		{ "write_last", OperationType.WriteLast },
		{ "write_else_if",  OperationType.WriteElseIf },
		{ "write_else", OperationType.WriteElse },
		{ "write_and_condition",  OperationType.WriteAndCondition },
		{ "write_or_condition", OperationType.WriteOrCondition },
	};
	public static readonly Dictionary<OperationType, ReversibleOperationClass> ReversibleWriteOperationClasses = new Dictionary<OperationType, ReversibleOperationClass> {
		{ OperationType.WriteLast, ReversibleOperationClass.WriteLast },
		{ OperationType.WriteBeforeLast, ReversibleOperationClass.WriteBeforeLast },
		{ OperationType.WriteAndCondition,  ReversibleOperationClass.WriteCondition },
		{ OperationType.WriteOrCondition, ReversibleOperationClass.WriteCondition },
	};
}

// this is really dumb. this is the only operation with a subclass, since it's the only one that needs more than a string
public class ReplaceSubstringPatchOperation(string oldStr, string newStr, bool regex, bool critical, PatchOwner owner, int increment)
	: PatchOperation(newStr, critical, OperationType.WriteReplaceSubstring, owner, increment) {

	public string OldText = oldStr;
	public bool Regex = regex;
}

public enum OperationType {
	WriteReplace,
	WriteReplaceSubstring,
	WriteBefore,
	WriteBeforeLast,
	Write,
	WriteLast,
	WriteElseIf,
	WriteElse,
	
	WriteAndCondition,
	WriteOrCondition
}

/** Used for reversing operations that write in the opposite order of their statements */
public enum ReversibleOperationClass {
	WriteLast,
	WriteBeforeLast,
	WriteCondition,
}


