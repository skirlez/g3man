using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Patching;
using g3man.Util;

namespace g3man.Models;

public class Game {
	private static Logger logger = Logger.Make("GAME-PARSER");
	
	public string DisplayName;
	public string InternalName;
	public string DatafileName;

	public int ExecutableType;
	public string ExecutablePath;
	public int ExecutableSteamAppId;
	
	public string OutputDatafileName;
	
	private const int LatestVersion = 2;
	public int FormatVersion;

	public string Directory => Entry.Path;
	public GameEntry Entry;
	
	public Game(GameEntry entry, string displayName, string internalName, string datafileName, int executableType, string executablePath, int executableSteamAppId,
		string outputDatafileName) {
		Entry = entry;
		DisplayName = displayName;
		InternalName = internalName;
		DatafileName = datafileName;
		ExecutableType = executableType;
		ExecutablePath = executablePath;
		ExecutableSteamAppId = executableSteamAppId;
		OutputDatafileName = outputDatafileName;
		FormatVersion = LatestVersion;
	}
	public Game(JsonElement root, GameEntry entry) {
		Entry = entry;
		FormatVersion = JsonUtil.GetNumberOrThrow(root, "format_version");
		if (FormatVersion > LatestVersion)
			throw new InvalidDataException($"Game in {entry.Path} has a format version too new: {FormatVersion} > {LatestVersion}.");
		
		DisplayName = JsonUtil.GetStringOrThrow(root, "display_name");
		InternalName = JsonUtil.GetStringOrThrow(root, "internal_name");
		DatafileName = JsonUtil.GetStringOrThrow(root, "datafile_name");

		ExecutableType = JsonUtil.GetOrDefault(root, "executable_type", 0);
		ExecutablePath = JsonUtil.GetOrDefaultClass(root, "executable_path", "");
		ExecutableSteamAppId = JsonUtil.GetOrDefault(root, "executable_steam_app_id", -1);
		OutputDatafileName = JsonUtil.GetStringOrThrow(root, "output_datafile_name", "g3man_" + DatafileName);
	}

	public string GetCleanDatafilePath() {
		return Path.Combine(Directory, "g3man", DatafilePatcher.CleanDataName);
	}
	public string GetBackupDatafilePath() {
		return Path.Combine(Directory, "g3man", DatafilePatcher.CleanDataBackupName);
	}
	public string GetProfileFolderPath(Profile profile) {
		Debug.Assert(profile.ID != "");
		return Path.Combine(Directory, "g3man", "profiles", profile.ID);
	}
	public string GetOutputDatafilePath() {
		return Path.Combine(Directory, DatafileName);
	}
	public bool HasExecutable() {
		switch (ExecutableType) {
			case 0:
				return ExecutablePath != "";
			case 1:
				return ExecutableSteamAppId != -1;
		}
		return false;
	}

	
	public JsonObject ToJson() {
		return new JsonObject() {
			["format_version"] = 2,
			["display_name"] = DisplayName,
			["internal_name"] = InternalName,
			["datafile_name"] = DatafileName,
			["executable_type"] = ExecutableType,
			["executable_path"] = ExecutablePath,
			["executable_steam_app_id"] = ExecutableSteamAppId,
			["output_datafile_name"] = OutputDatafileName
		};
	}

	public static List<Game> Parse(List<GameEntry> gameEntries) {
		ConcurrentBag<Game> games = new ConcurrentBag<Game>();
		Parallel.ForEach(gameEntries, gameEntry =>
		{
			string fullPath = Path.Combine(gameEntry.Path, "g3man", "game.json");
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
				Game game = new Game(jsonDoc.RootElement, gameEntry);
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

public interface Executable {
	public void Start();
}

public class FileExecutable(string path) : Executable {
	public string Path = path;
	public void Start() {
		throw new NotImplementedException();
	}
}
public class SteamExecutable(int appId) : Executable {
	public int AppId = appId;
	public void Start() {
		throw new NotImplementedException();
	}
}
