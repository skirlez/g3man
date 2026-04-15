using g3man.Models;

namespace g3man.Patching;

public static class GameAPI {
	public const string ScriptName = "g3man_api";

	public static string GetCode(string[] modOrder, string[] disabledMods, string profileID, string relativeProfilePath) {
		return 
$$"""
global.g3man_6 = {
	disabled_mods : [{{string.Join(",", disabledMods.Select(s => $"\"{s}\""))}}],
	profile_id : "{{profileID}}"
}
global.g3man_7 = {
	disabled_mods : [{{string.Join(",", disabledMods.Select(s => $"\"{s}\""))}}],
	profile_path : {{relativeProfilePath}},
	mod_order : [{{string.Join(",", modOrder.Except(disabledMods).Select(s => $"\"{s}\""))}}],
}
""";
	}

	public static bool IsImportAskingForMe(Import import) {
		return import.Name is "g3man_6" or "g3man_7";
	}
}