using g3man.Models;
using UndertaleModLib;
using UndertaleModLib.Models;

namespace g3man.Patching;

public static class GameAPI {
	public static string GetCode(string[] modOrder, string[] disabledMods, string profileID) {
		return 
$$"""
global.g3man_6 = {
	disabled_mods : [{{string.Join(",", disabledMods.Select(s => $"\"{s}\""))}}],
	profile_id : "{{profileID}}"
}
global.g3man_7 = {
	disabled_mods : [{{string.Join(",", disabledMods.Select(s => $"\"{s}\""))}}],
	profile_id : "{{profileID}}",
	mod_order : [{{string.Join(",", modOrder.Select(s => $"\"{s}\""))}}],
}
""";
	}

	public static bool IsImportAskingForMe(Import import) {
		return import.Name is "g3man_6" or "g3man_7";
	}
}