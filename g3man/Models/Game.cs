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
	public string ProfileFolderName;
	
	public Game(string displayName, string internalName, string directory, string hash, string profileId) {
		DisplayName = displayName;
		InternalName = internalName;
		Directory = directory;
		Hash = hash;
		ProfileFolderName = profileId;
	}
	public Game(JsonElement root, string directory) {
		DisplayName = JsonUtil.GetWithContingency<string>(root, "display_name", JsonUtil.Contingency.ThrowOnAnything);
		InternalName = JsonUtil.GetWithContingency<string>(root, "internal_name", JsonUtil.Contingency.ThrowOnAnything);
		Hash = JsonUtil.GetWithContingency<string>(root, "hash", JsonUtil.Contingency.ThrowOnAnything);
		ProfileFolderName = JsonUtil.GetWithContingency<string>(root, "profile_folder_name", JsonUtil.Contingency.ThrowOnAnything);

		Directory = directory;
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