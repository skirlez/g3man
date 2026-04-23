using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Lua;

namespace gmlpv2;

public class FileRecord {
	private Dictionary<int, List<PerformedOperation>> changes = new Dictionary<int, List<PerformedOperation>>();
	private List<string> errors = [];
	
	private List<PerformedOperation> getOrCreateLineChanges(int line) {
		if (changes.ContainsKey(line))
			return changes[line];
		List<PerformedOperation> operations = new List<PerformedOperation>();
		changes[line] = operations;
		return operations;
	}
	
	public IEnumerable<KeyValuePair<int, List<PerformedOperation>>> GetChanges() {
		return changes.AsEnumerable();
	}

	public bool HasErrors() {
		return errors.Count != 0;
	}

	public List<string> GetErrors() {
		return errors;
	}
	
	
	public void Add(int line, PerformedOperation operation) {
		getOrCreateLineChanges(line).Add(operation);
	}

	public void AddError(string error) {
		errors.Add(error);
	}
}

