using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Core.Util;

namespace g3man.Core.Models;

public class Profile {
	public readonly string Name;
	public readonly string ID;
	
	// this may seem like redundant state, however i did want users to be able to experiment with these variables on/off without it removing what was written in the fields
	public readonly bool SeparateModdedSave;
	public readonly string ModdedSaveName;


	public readonly string Description;
	public readonly string Version;
	public readonly string[] Credits;
	public readonly string[] Links;
	
	public string[] ModOrder;
	public string[] ModsDisabled;
	
	
	public Profile(string name, string id, bool separateModdedSave, string moddedSaveName, string[] modOrder) {
		Name = name;
		ID = id;
		SeparateModdedSave = separateModdedSave;
		ModdedSaveName = moddedSaveName;
		ModOrder = modOrder;

		ModsDisabled = [];
		Description = "";
		Version = "";
		Credits = [];
		Links = [];
	}

	private Profile(JsonElement root, string folderName) {
		int version = JsonUtil.GetNumberOrThrow(root, "format_version");
		Name = JsonUtil.GetStringOrThrow(root, "name");
		ID = version == 1 ? folderName : JsonUtil.GetStringOrThrow(root, "id");
		if (ID == "")
			throw new InvalidDataException($"Profile \"{Name}\" has an empty ID");
		
		SeparateModdedSave = JsonUtil.GetBooleanOrThrow(root, "separate_modded_save");
		ModdedSaveName = JsonUtil.GetStringOrThrow(root, "modded_save_name");
		if (SeparateModdedSave && ModdedSaveName == "")
			throw new InvalidDataException($"Profile \"{Name}\" (ID \"{ID}\" has \"separate_modded_save\" set to true, but \"modded_save_name\" is blank");
		
		ModOrder = JsonUtil.GetOrDefaultClass(root, "mod_order", Array.Empty<string>());
		ModsDisabled = JsonUtil.GetOrDefaultClass(root, "mods_disabled", Array.Empty<string>());
		Description = JsonUtil.GetOrDefaultClass(root, "description", "");
		Version = JsonUtil.GetOrDefaultClass(root, "version", "");
		Credits = JsonUtil.GetOrDefaultClass(root, "credits", Array.Empty<string>());
		Links = JsonUtil.GetOrDefaultClass(root, "links", Array.Empty<string>());
	}
	
	
	public static List<Profile> ParseAll(string directory, Action<Exception, string> errorHandler) {
		ConcurrentBag<Profile> profiles = new ConcurrentBag<Profile>();
		string[] profileFolders;
		try {
			profileFolders = Directory.GetDirectories(directory);
		}
		catch {
			return [];
		}


		Parallel.ForEach(profileFolders, profileFolder => {

			try {
				Profile profile = ParseFolder(profileFolder);
				profiles.Add(profile);
			}
			catch (Exception e) {
				errorHandler(e, profileFolder);
			}
		});

		return profiles.ToList();
	}
	
	public static Profile ParseFolder(string profileFolder, bool doFolderCheck = true) {
		string fullPath = Path.Combine(profileFolder, "profile.json");
		string folderName = Path.GetFileName(profileFolder);
		Profile profile;
		{
			using FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
			profile = Parse(stream, folderName);
		}
		if (doFolderCheck && folderName != profile.ID)
			throw new InvalidDataException($"Profile's ID does not match with its folder name. ID is \"{profile.ID}\", but found it in folder \"{folderName}\"");
		return profile;
	}
	public static Profile Parse(Stream stream, string v1folderName) {
		JsonDocument jsonDoc = JsonDocument.Parse(stream);
		Profile profile = new Profile(jsonDoc.RootElement, v1folderName);
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
			// don't care
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

	public string Identify() {
		return $"{Name} (ID \"{ID}\")";
	}
}