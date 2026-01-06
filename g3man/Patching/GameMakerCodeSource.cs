using gmlp;
using Underanalyzer.Decompiler;
using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;

namespace g3man.Patching;

public class GameMakerCodeSource(CompileGroup compileGroup) : CodeSource {
	internal static readonly DecompileSettings Settings = new DecompileSettings {
		UnknownArgumentNamePattern = "arg{0}",
		EmptyLineAroundBranchStatements = true,
		RemoveSingleLineBlockBraces = false,
		EmptyLineBeforeSwitchCases = true
	};
	
	public override CodeFile? GetCodeFile(string name) {
		UndertaleCode code = compileGroup.Data.Code.ByName(name);
		if (code is null)
			return null;
		return new GameMakerCodeFile(code, compileGroup.GlobalContext);
	}

	public override string? GetReplacedCodeVerbatim(string file) {
		if (!replaced.ContainsKey(file))
			return null;
		return replaced[file];
	}

	private readonly Dictionary<string, string> replaced = new Dictionary<string, string>();
	public override void Replace(string file, string code) {
		compileGroup.QueueCodeReplace(compileGroup.Data.Code.ByName(file), code);
		replaced[file] = code;
	}
}