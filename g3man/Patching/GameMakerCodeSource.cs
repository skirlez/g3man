using gmlp;
using PatchCommon;
using Underanalyzer.Decompiler;
using UndertaleModLib;
using UndertaleModLib.Compiler;
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
		string text = new DecompileContext(compileGroup.GlobalContext, code, Settings).DecompileToString();
		return new CodeFile(text);
	}
	
	/*
	public override void Replace(string file, string code) {
		compileGroup.QueueCodeReplace(compileGroup.Data.Code.ByName(file), code);
	}
	*/
}