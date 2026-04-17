using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Util;

namespace g3man.Models;

public class Profile {
	public string Name;
	public string ID;
	
	// this may seem like redundant state, however i did want users to be able to experiment with these variables on/off without it removing what was written in the fields
	public bool SeparateModdedSave;
	public string ModdedSaveName;

	public bool EnableOutputOverride;
	public string OutputDatafileOverride;
	
	public string[] ModOrder;
	public string[] ModsDisabled;
	public string Description;
	public string Version;
	public string[] Credits;
	public string[] Links;

	private static readonly Logger logger = Logger.Make("PROFILE-PARSER");

	public Profile(string name, string id, bool separateModdedSave, string moddedSaveName, bool enableOutputOverride, string outputDatafileOverride, string[] modOrder) {
		Name = name;
		ID = id;
		SeparateModdedSave = separateModdedSave;
		ModdedSaveName = moddedSaveName;
		ModOrder = modOrder;
		EnableOutputOverride = enableOutputOverride;
		OutputDatafileOverride = outputDatafileOverride;

		ModsDisabled = [];
		Description = "";
		Version = "";
		Credits = [];
		Links = [];
	}

	public Profile(JsonElement root, string folderName) {
		int version = JsonUtil.GetNumberOrThrow(root, "format_version");
		ID = version == 1 ? folderName : JsonUtil.GetStringOrThrow(root, "id");
		Name = JsonUtil.GetStringOrThrow(root, "name");
		
		SeparateModdedSave = JsonUtil.GetBooleanOrThrow(root, "separate_modded_save");
		ModdedSaveName = JsonUtil.GetStringOrThrow(root, "modded_save_name");
		if (SeparateModdedSave && ModdedSaveName == "")
			throw new InvalidDataException($"Profile \"{Name}\" (ID \"{ID}\" has \"separate_modded_save\" set to true, but \"modded_save_name\" is blank");

		EnableOutputOverride = JsonUtil.GetOrDefault(root, "enable_output_override", false);
		OutputDatafileOverride = JsonUtil.GetOrDefaultClass(root, "output_datafile_override", "");
		if (EnableOutputOverride && OutputDatafileOverride == "")
			throw new InvalidDataException($"Profile \"{Name}\" (ID \"{ID}\" has \"enable_output_override\" set to true, but \"output_datafile_override\" is blank");
		
		
		ModOrder = JsonUtil.GetOrDefaultClass(root, "mod_order", Array.Empty<string>());
		ModsDisabled = JsonUtil.GetOrDefaultClass(root, "mods_disabled", Array.Empty<string>());
		Description = JsonUtil.GetOrDefaultClass(root, "description", "");
		Version = JsonUtil.GetOrDefaultClass(root, "version", "");
		Credits = JsonUtil.GetOrDefaultClass(root, "credits", Array.Empty<string>());
		Links = JsonUtil.GetOrDefaultClass(root, "links", Array.Empty<string>());
	}
	
	
	public static List<Profile> ParseAll(string directory, Action<Exception, string>? errorHandler = null) {
		ConcurrentBag<Profile> profiles = new ConcurrentBag<Profile>();
		string[] profileFolders;
		try {
			profileFolders = Directory.GetDirectories(directory);
		}
		catch {
			return [];
		}

		Action<Exception, string> onError = (errorHandler) ?? ((e, path) => {
			logger.Error($"Profile at {path} failed to parse:\n{e.Message}");
		});
		Parallel.ForEach(profileFolders, profileFolder => {

			try {
				Profile profile = Parse(profileFolder);
				profiles.Add(profile);
			}
			catch (Exception e) {
				onError(e, profileFolder);
			}
		});

		return profiles.ToList();
	}
	
	public static Profile Parse(string profileFolder, bool doFolderCheck = true) {
		string fullPath = Path.Combine(profileFolder, "profile.json");
		string text = File.ReadAllText(fullPath); 
		JsonDocument jsonDoc = JsonDocument.Parse(text);
		string folderName = Path.GetFileName(profileFolder);
		Profile profile = new Profile(jsonDoc.RootElement, folderName);
		if (doFolderCheck && folderName != profile.ID)
			throw new InvalidDataException($"Profile's ID does not match with its folder name. ID is \"{profile.ID}\", but found it in folder \"{folderName}\"");
		return profile;
	}

	public JsonObject ToJson() {
		return new JsonObject() {
			["format_version"] = 2,
			["name"] = Name,
			["id"] = ID,
			["separate_modded_save"] = SeparateModdedSave,
			["modded_save_name"] = ModdedSaveName,
			["mod_order"] = new JsonArray(ModOrder.Select(modId => JsonValue.Create(modId)).ToArray<JsonNode?>()),
			["mods_disabled"] = new JsonArray(ModsDisabled.Select(modId => JsonValue.Create(modId)).ToArray<JsonNode?>()),
			["description"] = Description,
			["version"] = Version,
			["credits"] = new JsonArray(Credits.Select(credit => JsonValue.Create(credit)).ToArray<JsonNode?>()),
			["links"] = new JsonArray(Links.Select(link => JsonValue.Create(link)).ToArray<JsonNode?>()),
			["enable_output_override"] = EnableOutputOverride,
			["output_datafile_override"] = OutputDatafileOverride
		};
	}
	
	
	public void Write(Game game) {
		string profileFolder = game.GetProfileFolderPath(this);
		Directory.CreateDirectory(profileFolder);

		string jsonText = ToJson().ToJsonString(new JsonSerializerOptions() {
			WriteIndented = true
		});
		File.WriteAllText(Path.Combine(profileFolder, "profile.json"), jsonText);
	}

	public void Delete(Game game) {
		string profileFolder = game.GetProfileFolderPath(this);
		Directory.Delete(profileFolder, true);
		try {
			string profileLiveFolder = game.GetProfileLiveFolderPath(this);
			Directory.Delete(profileLiveFolder, true);
		}
		catch {
			// i don't even care
		}
	}

	public void UpdateModsStatus(List<IMod> modsList, Dictionary<IMod, bool> enabledMods) {
		ModOrder = modsList.Select(mod => mod.ModId).ToArray();
		List<string> disabledIds = [];
		foreach (var kvp in enabledMods) {
			if (!kvp.Value)
				disabledIds.Add(kvp.Key.ModId);
		}
		ModsDisabled = disabledIds.ToArray();
	}
}