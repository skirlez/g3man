using gmlp;
using Underanalyzer.Decompiler;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;

namespace g3man.Patching;

public class GameMakerCodeFile(UndertaleCode code, GlobalDecompileContext context) : CodeFile {
	public override string GetAsString() {
		return new DecompileContext(context, code, GameMakerCodeSource.Settings).DecompileToString();
	}
}