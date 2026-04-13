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

	public enum ExecutableType {
		File,
		Steam,
		Size
	}
	
	public ExecutableType ChosenExecutableType;
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
		ChosenExecutableType = (ExecutableType)executableType;
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
		
		int executableType = JsonUtil.GetOrDefault(root, "executable_type", 0);
		if (executableType >= (int)ExecutableType.Size || executableType < 0)
			executableType = 0;
		ChosenExecutableType = (ExecutableType)executableType;
		ExecutablePath = JsonUtil.GetOrDefaultClass(root, "executable_path", "");
		ExecutableSteamAppId = JsonUtil.GetOrDefault(root, "executable_steam_app_id", -1);
		OutputDatafileName = JsonUtil.GetStringOrThrow(root, "output_datafile_name", $"g3man_{DatafileName}");
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
	public Status ExecutableStatus(Config config) {
		switch (ChosenExecutableType) {
			case ExecutableType.File:
				if (ExecutablePath == "")
					return new Status(false, "Game has no executable file set");
				return Status.OK;
			case ExecutableType.Steam:
				if (ExecutableSteamAppId == -1)
					return new Status(false, "Game has no Steam App ID set");
				if (config.SteamExecutable == "")
					return new Status(false, "To launch games via Steam, supply the path to the Steam executable in Settings.");
				return Status.OK;
		}
		throw new UnreachableException();
	}
	

	public void Launch(Config config) {
		Debug.Assert(ExecutableStatus(config).ok);
		switch (ChosenExecutableType) {
			case ExecutableType.File:
				
				break;
			case ExecutableType.Steam:
				Process.Start(new ProcessStartInfo {
					FileName =  config.SteamExecutable,
					ArgumentList = { "-applaunch", ExecutableSteamAppId.ToString(), "--", "-game", OutputDatafileName },
					RedirectStandardOutput = true,
					UseShellExecute = false,
					CreateNoWindow = true
				});
				break;
		}
	}
	
	public JsonObject ToJson() {
		return new JsonObject() {
			["format_version"] = 2,
			["display_name"] = DisplayName,
			["internal_name"] = InternalName,
			["datafile_name"] = DatafileName,
			["executable_type"] = (int)ChosenExecutableType,
			["executable_path"] = ExecutablePath,
			["executable_steam_app_id"] = ExecutableSteamAppId,
			["output_datafile_name"] = OutputDatafileName
		};
	}


	public static List<Game> ParseAll(List<GameEntry> gameEntries, Action<Exception, GameEntry>? errorHandler = null) {
		ConcurrentBag<Game> games = new ConcurrentBag<Game>();
		
		Action<Exception, GameEntry> onError = (errorHandler) ?? ((e, entry) => {
			logger.Error($"Error reading game at {entry.Path}:\n{e.Message}");
		});
		Parallel.ForEach(gameEntries, gameEntry =>
		{
			try {
				string fullPath = Path.Combine(gameEntry.Path, "g3man", "game.json");
				string text = File.ReadAllText(fullPath); 
				JsonDocument jsonDoc = JsonDocument.Parse(text);
				Game game = new Game(jsonDoc.RootElement, gameEntry);
				games.Add(game);
			}
			catch (Exception e) {
				onError(e, gameEntry);
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
