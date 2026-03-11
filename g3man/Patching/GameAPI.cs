using g3man.Models;
using UndertaleModLib;
using UndertaleModLib.Models;

namespace g3man.Patching;

public static class GameAPI {
	public static string GetCode(string[] disabledMods, string profileID) {
		return 
$$"""
global.g3man_6 = {
	disabled_mods : [{{string.Join(",", disabledMods.Select(s => $"\"{s}\""))}}],
	profile_id : "{{profileID}}"
}
""";
	}

	public static bool IsImportAskingForMe(Import import) {
		return import.Name == "g3man_6";
	}
}