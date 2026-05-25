using System.Collections.Generic;

namespace PatchCommon;

public static class ExtensionMethods {
	public static V GetOrCreate<K, V>(this Dictionary<K, V> dict, K key)
			where K : notnull
			where V : new() {
		if (dict.ContainsKey(key)) {
			return dict[key];
		}

		V newValue = new V();
		dict.Add(key, newValue);
		return newValue;
	}
}