using gmlp;
using Underanalyzer.Decompiler;
using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;

namespace g3man.Patching;

public class GameMakerCodeSource(UndertaleData data, GlobalDecompileContext context) : CodeSource {
	internal static readonly DecompileSettings Settings = new DecompileSettings {
		UnknownArgumentNamePattern = "arg{0}",
		EmptyLineAroundBranchStatements = true,
		EmptyLineBeforeSwitchCases = true
	};
	
	private readonly CodeImportGroup importGroup = new(data, context);
	
	public override CodeFile? GetCodeFile(string name) {
		UndertaleCode code = data.Code.ByName(name);
		if (code is null)
			return null;
		return new GameMakerCodeFile(code, context);
	}
	public override void Replace(string file, string code) {
		importGroup.QueueReplace(file, code);
		importGroup.Import();
	}
}