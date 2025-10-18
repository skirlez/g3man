using Underanalyzer.Decompiler;
using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Decompiler;

namespace g3man.GMLP;

public abstract class PatchApplier {
	public abstract void Apply(string target, string code);
}

public class GMLPatchApplier(UndertaleData data, GlobalDecompileContext context) : PatchApplier {
	private readonly CodeImportGroup importGroup = new(data, context, GMLP.GetSettings());
	public override void Apply(string file, string code) {
		importGroup.QueueReplace(file, code);
		importGroup.Import();
	}
}


public class DictionaryPatchApplier(Dictionary<string, string> dictionary) : PatchApplier {
	public override void Apply(string file, string code) {
		dictionary.Add(file, code);
	}
}