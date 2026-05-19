using System;
using System.Collections.Generic;

namespace PatchCommon;



public class PatchStep<T>(Func<PatchResults> function, Dictionary<string, List<PatchInfo>> blameMap, T owner) {
	public T Owner = owner;
	public PatchResults Apply() {
		return function();
	}

	public List<PatchInfo> WhoTouches(string target) {
		return blameMap.GetValueOrDefault(target, []);
	}
}