using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Util;

namespace g3man.Models;

public class Profile {
	public string Name;
	public string FolderName;
	public bool SeparateModdedSave;
	public string[] ModOrder;

	private static Logger logger = new Logger("PROFILE-PARSER");

	public Profile(string name, string folderName, bool separateModdedSave, string[] modOrder) {
		Name = name;
		FolderName = folderName;
		SeparateModdedSave = separateModdedSave;
		ModOrder = modOrder;
	}

	public Profile(JsonElement root, string folderName) {
		Name = JsonUtil.GetStringOrThrow(root, "name");
		FolderName = folderName;
		SeparateModdedSave = JsonUtil.GetBooleanOrThrow(root, "separate_modded_save");
		ModOrder = JsonUtil.GetStringArrayOrThrow(root, "mod_order");
	}

	public static List<Profile> ParseProfiles(string directory) {
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
			string text;
			try {
				text = File.ReadAllText(fullPath);
			}
			catch (Exception e) {
				logger.Error("Couldn't find or load profile.json at " + fullPath + ":\n" + e.Message);
				return;
			}

			JsonDocument jsonDoc = JsonDocument.Parse(text);

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
			["mod_order"] = new JsonArray(ModOrder.Select(modId => JsonValue.Create(modId)).ToArray<JsonNode?>())
		};
	}
	
	public void Write(string directory) {
		string profileFolder = Path.Combine(directory, "g3man", FolderName);
		Directory.CreateDirectory(Path.Combine(profileFolder, "mods"));
		
		string jsonText = ToJson().ToJsonString();
		File.WriteAllText(Path.Combine(profileFolder, "profile.json"), jsonText);
	}
}