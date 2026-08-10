using System.Diagnostics.CodeAnalysis;
using Underanalyzer;
using Underanalyzer.Decompiler;

namespace g3man.Core.Patching;


public class NamespacedGlobalFunctions(IGlobalFunctions origin, List<string> modIds) : IGlobalFunctions {
	public bool FunctionNameExists(string name) {
		foreach (string modId in modIds) {
			if (origin.FunctionNameExists($"@{modId}@{name}"))
				return true;
		}
		return origin.FunctionNameExists(name);
	}
	public bool FunctionExists(IGMFunction function) {
		return origin.FunctionExists(function);
	}
	public bool TryGetFunction(string name, [NotNullWhen(true)] out IGMFunction? function) {
		foreach (string modId in modIds) {
			if (origin.TryGetFunction($"@{modId}@{name}", out IGMFunction? outFunction)) {
				function = outFunction;
				return true;
			}
		}
		return origin.TryGetFunction(name, out function);
	}
	public bool TryGetFunctionName(IGMFunction function, [NotNullWhen(true)] out string? name) {
		return origin.TryGetFunctionName(function, out name);
	}
}