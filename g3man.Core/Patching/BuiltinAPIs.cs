using g3man.Core.Models;
using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Models;

namespace g3man.Core.Patching;

public static class BuiltinAPIs {
	public const string ScriptName = "g3man_api";
	
	// TODO: this works fine, however, if there are duplicate assets this will cause issues.
	
	private static string GetCommaSeparatedAssetStructVariables(DatafilePatcher.Assets assets) {
		return string.Join(",", 
			assets.Set.Select(x => $"{x} : {x}")
			.Concat(
			assets.Functions.Select(x => $"{x.Key} : {x.Value}")
			)
		);
	}

	private static string GetAssetsCode(Dictionary<string, DatafilePatcher.Assets> modIndicesMap) {
		return
$"global.assets_1 = {{ {
	string.Join(",",
		modIndicesMap.Keys.Select(modId => 
			$"\"{modId}\":"
			+ "{ "
			+ $"{GetCommaSeparatedAssetStructVariables(modIndicesMap[modId])}"
			+ "}"))
} }}";
	}
	private static string GetVanillaAssetsCode(DatafilePatcher.Assets vanillaAssets) {
		return
$"global.vanilla_assets_1 = {{ {
	GetCommaSeparatedAssetStructVariables(vanillaAssets)
} }}";
	}
	
	private static string GetCodeG3man(string[] modOrder, string[] disabledMods, string relativeProfilePath, string relativeProfileLivePath) {
		return
$$"""
global.g3man_7 = {
	disabled_mods : [{{string.Join(",", disabledMods.Select(s => $"\"{s}\""))}}],
	profile_path : "{{relativeProfilePath}}",
	live_path : "{{relativeProfileLivePath}}",
	mod_order : [{{string.Join(",", modOrder.Except(disabledMods).Select(s => $"\"{s}\""))}}],
}
""";
	}


	private static string[] us = ["g3man_7", "assets_1", "vanilla_assets_1"];
	public static bool IsImportAskingForUs(Import import) {
		return us.Contains(import.Name);
	}

	public static string[] GetRequestedAPIs(IEnumerable<Import> imports) {
		return imports.Select(i => i.Name).ToArray().Distinct().Union(us).ToArray();	
	}
	
	public static void Inject(
			UndertaleData data, 
			string[] requestedAPIs,
			Profile profile, 
			string relativeProfilePath, 
			string relativeProfileLivePath, 
			DatafilePatcher.Assets vanillaAssets, 
			Dictionary<string, DatafilePatcher.Assets> indicesMap, 
			CompileGroup group) {
		
		UndertaleScript? APIScript = data.Scripts.ByName(ScriptName);
		if (APIScript is null)
			APIScript = CreateScript(data, ScriptName);

		string code = "";

		if (requestedAPIs.Contains("g3man_7"))
			code += GetCodeG3man(profile.ModOrder, profile.ModsDisabled, relativeProfilePath, relativeProfileLivePath);
		if (requestedAPIs.Contains("assets_1"))
			code += "\n" + GetAssetsCode(indicesMap);
		if (requestedAPIs.Contains("vanilla_assets_1"))
			code += "\n" + GetVanillaAssetsCode(vanillaAssets);
		
		group.QueueCodeReplace(APIScript.Code, code);
	}
	
	
	
	
	// TODO: a script might not be needed. maybe it just works without and we can just add a code entry to global init
	private static UndertaleScript CreateScript(UndertaleData data, string scriptName) {
		UndertaleScript script = new();
		script.Name = new UndertaleString(scriptName);
		script.Code = new UndertaleCode();
		script.Code.Name = new UndertaleString($"gml_GlobalScript_{scriptName}");
				
		data.Code.Add(script.Code);
		data.Scripts.Add(script);
				
		UndertaleGlobalInit ginit = new();
		ginit.Code = script.Code;
		data.GlobalInitScripts.Insert(0, ginit);
		data.Strings.Add(script.Code.Name);
		data.Strings.Add(script.Name);
		return script;
	}
	
}