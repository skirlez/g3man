using gmlp;
using PatchCommon;
using Underanalyzer.Decompiler;
using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Models;

namespace g3man.Core.Patching;

public class GameMakerCodeSource(CompileGroup compileGroup) : CodeSource {
	internal static readonly DecompileSettings Settings = new DecompileSettings {
		UnknownArgumentNamePattern = "arg{0}",
		EmptyLineAroundBranchStatements = true,
		RemoveSingleLineBlockBraces = false,
		EmptyLineBeforeSwitchCases = true
	};

	private Dictionary<string, CodeFile> cache = new();
	
	public override CodeFile? GetCodeFile(string name) {
		if (cache.ContainsKey(name))
			return cache[name];
		UndertaleCode code = compileGroup.Data.Code.ByName(name);
		if (code is null)
			return null;
		string text = new DecompileContext(compileGroup.GlobalContext, code, Settings).DecompileToString();
		CodeFile file = new CodeFile(text);
		cache[name] = file;
		return file;
	}


	public void RemoveFromCache(string name) {
		cache.Remove(name);
	}
}