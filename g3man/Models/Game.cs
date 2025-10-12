using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Util;

namespace g3man.Models;

public class Game {
	public string DisplayName;
	public string InternalName;
	public string Directory;
	public string Hash;
	public string ProfileFolderName;
	
	public Game(string displayName, string internalName, string directory, string hash, string profileId) {
		DisplayName = displayName;
		InternalName = internalName;
		Directory = directory;
		Hash = hash;
		ProfileFolderName = profileId;
	}
	public Game(JsonElement root) {
		DisplayName = JsonUtil.GetStringOrThrow(root, "display_name");
		InternalName = JsonUtil.GetStringOrThrow(root, "internal_name");
		Directory = JsonUtil.GetStringOrThrow(root, "directory");
		Hash = JsonUtil.GetStringOrThrow(root, "hash");
		ProfileFolderName = JsonUtil.GetStringOrThrow(root, "profile_folder_name");
	}
	
	public bool HasSameData(Game other) {
		return Hash.SequenceEqual(other.Hash);
	}

	public string GetCleanDatafilePath() {
		return Path.Combine(Directory, Patcher.CleanDataName);
	}

	public JsonObject ToJson() {
		return new JsonObject() {
			["format_version"] = 1,
			
			["display_name"] = DisplayName,
			["internal_name"] = InternalName,
			["directory"] = Directory,
			["profile_folder_name"] = ProfileFolderName,
			["hash"] = Hash
		};
	}
}