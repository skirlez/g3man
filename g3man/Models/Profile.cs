using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Util;

namespace g3man.Models;

public class Profile {
	public string Name;
	public string FolderName;
	public bool SeparateModdedSave;
	public string ModdedSaveName;
	public string[] ModOrder;
	public string[] ModsDisabled;
	public string Description;
	public string Version;
	public string[] Credits;
	public string[] Links;

	private static readonly Logger logger = new Logger("PROFILE-PARSER");

	public Profile(string name, string folderName, bool separateModdedSave, string moddedSaveName, string[] modOrder) {
		Name = name;
		FolderName = folderName;
		SeparateModdedSave = separateModdedSave;
		ModdedSaveName = moddedSaveName;
		ModOrder = modOrder;

		ModsDisabled = [];
		Description = "";
		Version = "";
		Credits = [];
		Links = [];
	}

	public Profile(JsonElement root, string folderName) {
		Name = JsonUtil.GetStringOrThrow(root, "name");
		FolderName = folderName;
		SeparateModdedSave = JsonUtil.GetBooleanOrThrow(root, "separate_modded_save");
		ModdedSaveName = JsonUtil.GetStringOrThrow(root, "modded_save_name");
		ModOrder = JsonUtil.GetStringArrayOrThrow(root, "mod_order");
		ModsDisabled = JsonUtil.GetStringArrayOrThrow(root, "mods_disabled");
		Description = JsonUtil.GetStringOrThrow(root, "description");
		Version = JsonUtil.GetStringOrThrow(root, "version");
		Credits = JsonUtil.GetStringArrayOrThrow(root, "credits");
		Links = JsonUtil.GetStringArrayOrThrow(root, "links");
	}

	public static List<Profile> ParseAll(string directory) {
		ConcurrentBag<Profile> profiles = new ConcurrentBag<Profile>();
		string[] profileFolders;
		try {
			profileFolders = Directory.GetDirectories(directory);
		}
		catch (Exception e) {
			// ignored
			return [];
		}

		Parallel.ForEach(profileFolders, profileFolder => {
			Profile? profile = Parse(profileFolder);
			if (profile is not null)
				profiles.Add(profile);
		});

		return profiles.ToList();
	}
	
	public static Profile? Parse(string profileFolder) {
		string fullPath = Path.Combine(profileFolder, "profile.json");
		JsonDocument jsonDoc;
		try {
			string text = File.ReadAllText(fullPath); 
			jsonDoc = JsonDocument.Parse(text);
		}
		catch (Exception e) {
			logger.Error("Couldn't find or load profile.json at " + fullPath + ":\n" + e.Message);
			return null;
		}
		try {
			Profile profile = new Profile(jsonDoc.RootElement, Path.GetFileName(profileFolder));
			return profile;
		}
		catch (InvalidDataException e) {
			logger.Error("Invalid profile.json at " + fullPath + ":\n" + e.Message);
		}
		return null;
	}

	public JsonObject ToJson() {
		return new JsonObject() {
			["format_version"] = 1,
			["name"] = Name,
			["separate_modded_save"] = SeparateModdedSave,
			["modded_save_name"] = ModdedSaveName,
			["mod_order"] = new JsonArray(ModOrder.Select(modId => JsonValue.Create(modId)).ToArray<JsonNode?>()),
			["mods_disabled"] = new JsonArray(ModsDisabled.Select(modId => JsonValue.Create(modId)).ToArray<JsonNode?>()),
			["description"] = Description,
			["version"] = Version,
			["credits"] = new JsonArray(Credits.Select(credit => JsonValue.Create(credit)).ToArray<JsonNode?>()),
			["links"] = new JsonArray(Links.Select(link => JsonValue.Create(link)).ToArray<JsonNode?>())
		};
	}
	
	public bool Write(string directory) {
		try {
			string profileFolder = Path.Combine(directory, "g3man", FolderName);
			Directory.CreateDirectory(Path.Combine(profileFolder, "mods"));

			string jsonText = ToJson().ToJsonString();
			File.WriteAllText(Path.Combine(profileFolder, "profile.json"), jsonText);
			return true;
		}
		catch (Exception e) {
			logger.Error("Failed to write profile.json at " + directory + ":\n" + e.Message);
		}
		return false;
	}

	public bool Delete(string directory) {
		try {
			string profileFolder = Path.Combine(directory, "g3man", FolderName);
			Directory.Delete(profileFolder, true);
			return true;
		}
		catch (Exception e) {
			logger.Error("Failed to delete profile.json at " + directory + ":\n" + e.Message);
		}

		return false;
	}

	public void UpdateModsStatus(List<Mod> modsList, Dictionary<Mod, bool> enabledMods) {
		ModOrder = modsList.Select(mod => mod.ModId).ToArray();
		List<string> disabledIds = [];
		foreach (var kvp in enabledMods) {
			if (!kvp.Value)
				disabledIds.Add(kvp.Key.ModId);
		}

		ModsDisabled = disabledIds.ToArray();
	}
}