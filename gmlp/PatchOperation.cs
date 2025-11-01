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
	public readonly string Text = text;
	public readonly bool Critical = critical;
	public readonly OperationType Type = type;
	
	public readonly PatchOwner Owner = owner;

	// gets incremented for each patch operation in a patch file, so they can sort by each other.
	private readonly int increment = increment;
	
	public int IsHigherPriorityThan(PatchOperation other, List<PatchOwner> mods) {
		int ownerComparison = Owner.IsHigherPriorityThan(other.Owner, mods);
		if (ownerComparison != 0)
			return ownerComparison;
		
		return int.Sign(other.increment - increment);
	}
}

public enum OperationType {
	WriteBefore,
	WriteReplace,
	WriteAfter,
	WriteAfterElseIf,
	WriteAfterElse,
}
