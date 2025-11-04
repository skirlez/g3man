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

		Description = "";
		Version = "";
		Credits = [];
		Links = [];
	}

	public Profile(JsonElement root, string folderName) {
		Name = JsonUtil.GetStringOrThrow(root, "name");
		FolderName = folderName;
		ModdedSaveName = JsonUtil.GetStringOrThrow(root, "modded_save_name");
		ModOrder = JsonUtil.GetStringArrayOrThrow(root, "mod_order");
		Description = JsonUtil.GetStringOrThrow(root, "description");
		Version = JsonUtil.GetStringOrThrow(root, "version");
		Credits = JsonUtil.GetStringArrayOrThrow(root, "credits");
		Links = JsonUtil.GetStringArrayOrThrow(root, "links");
	}

	public static List<Profile> Parse(string directory) {
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
			string fullPath = Path.Combine(profileFolder, "profile.json");
			JsonDocument jsonDoc;
			try {
				string text = File.ReadAllText(fullPath); 
				jsonDoc = JsonDocument.Parse(text);
			}
			catch (Exception e) {
				logger.Error("Couldn't find or load profile.json at " + fullPath + ":\n" + e.Message);
				return;
			}
			try {
				Profile profile = new Profile(jsonDoc.RootElement, Path.GetFileName(profileFolder));
				profiles.Add(profile);
			}
			catch (InvalidDataException e) {
				logger.Error("Invalid profile.json at " + fullPath + ":\n" + e.Message);
			}
		});

		return profiles.ToList();
	}

	public JsonObject ToJson() {
		return new JsonObject() {
			["format_version"] = 1,
			["name"] = Name,
			["separate_modded_save"] = SeparateModdedSave,
			["modded_save_name"] = ModdedSaveName,
			["mod_order"] = new JsonArray(ModOrder.Select(modId => JsonValue.Create(modId)).ToArray<JsonNode?>()),
			
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

	public void UpdateOrder(List<Mod> modsList) {
		ModOrder = modsList.Select(mod => mod.ModId).ToArray();
	}
}