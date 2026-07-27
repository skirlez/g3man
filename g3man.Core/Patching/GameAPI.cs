using g3man.Core.Models;
using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Models;

namespace g3man.Core.Patching;

public static class GameAPI {
	public const string ScriptName = "g3man_api";

	public static string GetCode(string[] modOrder, string[] disabledMods, string profileID, string relativeProfilePath, string relativeProfileLivePath) {
		return 
$$"""
global.g3man_6 = {
	disabled_mods : [{{string.Join(",", disabledMods.Select(s => $"\"{s}\""))}}],
	profile_id : "{{profileID}}"
}
global.g3man_7 = {
	disabled_mods : [{{string.Join(",", disabledMods.Select(s => $"\"{s}\""))}}],
	profile_path : "{{relativeProfilePath}}",
	live_path : "{{relativeProfileLivePath}}",
	mod_order : [{{string.Join(",", modOrder.Except(disabledMods).Select(s => $"\"{s}\""))}}],
}
""";
	}

	public static bool IsImportAskingForMe(Import import) {
		return import.Name is "g3man_6" or "g3man_7";
	}

	public static void Inject(UndertaleData data, Profile profile, string relativeProfilePath, string relativeProfileLivePath, CompileGroup group) {
		UndertaleScript? g3manAPIScript = data.Scripts.ByName(GameAPI.ScriptName);
		if (g3manAPIScript is null) {
			g3manAPIScript = new UndertaleScript();
			g3manAPIScript.Name = new UndertaleString(GameAPI.ScriptName);
			g3manAPIScript.Code = new UndertaleCode();
			g3manAPIScript.Code.Name = new UndertaleString($"gml_GlobalScript_{GameAPI.ScriptName}");
				
			data.Code.Add(g3manAPIScript.Code);
			data.Scripts.Add(g3manAPIScript);
				
			UndertaleGlobalInit ginit = new UndertaleGlobalInit();
			ginit.Code = g3manAPIScript.Code;
			data.GlobalInitScripts.Add(ginit);
			data.Strings.Add(g3manAPIScript.Code.Name);
			data.Strings.Add(g3manAPIScript.Name);
		}
		
		group.QueueCodeReplace(g3manAPIScript.Code, GameAPI.GetCode(profile.ModOrder, profile.ModsDisabled, profile.ID, relativeProfilePath, relativeProfileLivePath));
	}
}