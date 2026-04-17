using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Patching;
using g3man.UI;
using g3man.Util;

namespace g3man.Models;

public class Game {
	private static Logger logger = Logger.Make("GAME-PARSER");
	
	public string DisplayName;
	public string InternalName;
	
	public string DatafilePath;

	private string DatafileExtension;
	private string DatafileFolder;

	public string GetDatafileName() {
		return Path.GetFileName(DatafilePath);
	}
	
	public static string GetDefaultOutputDatafilePath(string inputDatafilePath) {
		return $"{Path.GetDirectoryName(inputDatafilePath)}/g3man_{Path.GetFileName(inputDatafilePath)}";
	}

	public enum ExecutableType {
		File,
		Steam,
		Size
	}
	
	public ExecutableType ChosenExecutableType;
	public string ExecutablePath;
	public int ExecutableSteamAppId;
	
	private string OutputDatafilePath;
	
	private const int LatestVersion = 2;
	public int FormatVersion;

	public string Directory => Entry.Path;
	public GameEntry Entry;
	
	public Game(GameEntry entry, string displayName, string internalName, string datafilePath, int executableType, string executablePath, int executableSteamAppId,
		string outputDatafilePath) {
		Entry = entry;
		DisplayName = displayName;
		InternalName = internalName;
		
		DatafilePath = datafilePath;
		DatafileExtension = Path.GetExtension(DatafilePath);
		DatafileFolder = Path.GetDirectoryName(DatafilePath) ?? "";
		
		ChosenExecutableType = (ExecutableType)executableType;
		ExecutablePath = executablePath;
		ExecutableSteamAppId = executableSteamAppId;
		OutputDatafilePath = outputDatafilePath;
		FormatVersion = LatestVersion;
	}
	public Game(JsonElement root, GameEntry entry) {
		Entry = entry;
		FormatVersion = JsonUtil.GetNumberOrThrow(root, "format_version");
		if (FormatVersion > LatestVersion)
			throw new InvalidDataException($"Game in {entry.Path} has a format version too new: {FormatVersion} > {LatestVersion}.");
		
		DisplayName = JsonUtil.GetStringOrThrow(root, "display_name");
		InternalName = JsonUtil.GetStringOrThrow(root, "internal_name");
		DatafilePath = JsonUtil.GetStringOrThrow(root, "datafile_name");
		
		DatafileExtension = Path.GetExtension(DatafilePath);
		DatafileFolder = Path.GetDirectoryName(DatafilePath) ?? "";
		
		int executableType = JsonUtil.GetOrDefault(root, "executable_type", 0);
		if (executableType >= (int)ExecutableType.Size || executableType < 0)
			executableType = 0;
		ChosenExecutableType = (ExecutableType)executableType;
		ExecutablePath = JsonUtil.GetOrDefaultClass(root, "executable_path", "");
		ExecutableSteamAppId = JsonUtil.GetOrDefault(root, "executable_steam_app_id", -1);
		OutputDatafilePath = JsonUtil.GetStringOrThrow(root, "output_datafile_name", GetDefaultOutputDatafilePath(DatafilePath));
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
	public string GetProfileLiveFolderPath(Profile profile) {
		Debug.Assert(profile.ID != "");
		return Path.Combine(Directory, "g3man", "live", profile.ID);
	}
	

	public string GetInputDatafilePath() {
		return Path.Combine(Directory, GetInputDatafileRelativePath());
	}
	public string GetInputDatafileRelativePath() {
		return DatafilePath;
	}
	public string GetOutputDatafileRelativePath(string profileID) {
		return $"{DatafileFolder}/{profileID}{DatafileExtension}";
	}
	public string GetOutputDatafileRelativePath(Profile profile) {
		if (profile.EnableOutputOverride)
			return GetOutputDatafileRelativePath(profile.ID);
		return GetOutputDatafileRelativePath() ;
	}
	public string GetOutputDatafileRelativePath() {
		return OutputDatafilePath;
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
	

	public void Launch(Config config, Profile profile) {
		Debug.Assert(ExecutableStatus(config).ok);
		
		switch (ChosenExecutableType) {
			case ExecutableType.File: 
				Process.Start(new ProcessStartInfo {
					FileName = Path.Combine(Directory, ExecutablePath),
					// at least one version of the linux runner always appended "-game game.unx" at the end of its arguments, overriding
					// our choice of datafile. including a single " character (appears to) make the runner listen to us, so i'm assuming it breaks the argument parser.
					ArgumentList = { "-game", GetOutputDatafileRelativePath(profile), "\"" },
					UseShellExecute = false,
					CreateNoWindow = true
				});
				break;
			case ExecutableType.Steam:
				Process.Start(new ProcessStartInfo {
					FileName = config.SteamExecutable,
					ArgumentList = { "-applaunch", ExecutableSteamAppId.ToString(), "--", "-game", GetOutputDatafileRelativePath(profile) },
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
			["datafile_name"] = DatafilePath,
			["executable_type"] = (int)ChosenExecutableType,
			["executable_path"] = ExecutablePath,
			["executable_steam_app_id"] = ExecutableSteamAppId,
			["output_datafile_name"] = OutputDatafilePath
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

	public LaunchParadigm GetLaunchParadigm() {
		return (OutputDatafilePath == DatafilePath) ? LaunchParadigm.Modify : LaunchParadigm.Launch;
	}
	
	public enum LaunchParadigm {
		Launch,
		Modify
	}
}
