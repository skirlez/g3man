using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Patching;
using g3man.Util;

namespace g3man.Models;

public class Game {
	private static Logger logger = new Logger("GAME-PARSER");
	
	public string DisplayName;
	public string InternalName;
	public string Directory;
	public string Hash;
	
	public string DatafileName;
	public string ProfileFolderName;
	
	public Game(string displayName, string internalName, string directory, string datafileName, string hash, string profileFolderName) {
		DisplayName = displayName;
		InternalName = internalName;
		Directory = directory;
		DatafileName = datafileName;
		Hash = hash;
		ProfileFolderName = profileFolderName;
	}
	public Game(JsonElement root, string directory) {
		DisplayName = JsonUtil.GetStringOrThrow(root, "display_name");
		InternalName = JsonUtil.GetStringOrThrow(root, "internal_name");
		Hash = JsonUtil.GetStringOrThrow(root, "hash");
		ProfileFolderName = JsonUtil.GetStringOrThrow(root, "profile_folder_name");
		DatafileName = JsonUtil.GetStringOrThrow(root, "datafile_name");
		Directory = directory;
	}
	
	public bool HasSameData(Game other) {
		return Hash.SequenceEqual(other.Hash);
	}

	public string GetCleanDatafilePath() {
		return Path.Combine(Directory, "g3man", Patcher.CleanDataName);
	}
	public string GetBackupDatafilePath() {
		return Path.Combine(Directory, "g3man", Patcher.CleanDataBackupName);
	}
	public string GetOutputDatafilePath() {
		return Path.Combine(Directory, DatafileName);
	}

	public JsonObject ToJson() {
		return new JsonObject() {
			["format_version"] = 1,
			
			["display_name"] = DisplayName,
			["internal_name"] = InternalName,
			["directory"] = Directory,
			["profile_folder_name"] = ProfileFolderName,
			["datafile_name"] = DatafileName,
			["hash"] = Hash
		};
	}

	public static List<Game> Parse(List<string> gameDirectories) {
		ConcurrentBag<Game> games = new ConcurrentBag<Game>();
		Parallel.ForEach(gameDirectories, gameDirectory =>
		{
			string fullPath = Path.Combine(gameDirectory, "g3man", "game.json");
			JsonDocument jsonDoc;
			try {
				string text = File.ReadAllText(fullPath); 
				jsonDoc = JsonDocument.Parse(text);
			}
			catch (Exception e) {
				logger.Error("Couldn't find or load game.json at " + fullPath + ":\n" + e.Message);
				return;
			}
			try {
				Game game = new Game(jsonDoc.RootElement, gameDirectory);
				games.Add(game);
			}
			catch (InvalidDataException e) {
				logger.Error("Invalid game.json at " + fullPath + ":\n" + e.Message);
			}
		});
		return games.ToList();
	}

	public void Write() {
		string folder = Path.Combine(Directory, "g3man");
		System.IO.Directory.CreateDirectory(folder);
		
		string jsonText = ToJson().ToJsonString();
		File.WriteAllText(Path.Combine(folder, "game.json"), jsonText);
	}


}