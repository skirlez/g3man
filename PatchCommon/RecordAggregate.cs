using System.Collections.Generic;
using System.Linq;
using common;


namespace PatchCommon;

public class RecordAggregate<T> where T : new() {
	private Dictionary<string, T> dict = new();
	
	public T GetOrCreate(string str) {
		return dict.GetOrCreate(str);

	}
	public IEnumerable<KeyValuePair<string, T>> GetChanges() {
		return dict.AsEnumerable();
	}
}