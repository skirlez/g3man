using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Core.Patching;
using g3man.Core.Util;

namespace g3man.Core.Models;

public class Game {
	public string DisplayName;
	public string InternalName;
	
	public string DatafilePath;

	private string DatafileExtension;
	private string DatafileFolder;
	
	public enum ExecutableType {
		File,
		Steam,
		Size
	}
	
	public ExecutableType ChosenExecutableType;
	public string ExecutablePath;
	public int ExecutableSteamAppId;
	
	private const int LatestFormatVersion = 3;
	public int FormatVersion;

	public string Directory => Entry.Path;
	public GameEntry Entry;

	public bool OverwriteGameFiles;
	
	public Game(GameEntry entry, string displayName, string internalName, string datafilePath, int executableType, string executablePath, int executableSteamAppId,
		bool overwriteGameFiles) {
		Entry = entry;
		DisplayName = displayName;
		InternalName = internalName;
		
		DatafilePath = datafilePath;
		DatafileExtension = Path.GetExtension(DatafilePath);
		DatafileFolder = Path.GetDirectoryName(DatafilePath) ?? "";
		
		ChosenExecutableType = (ExecutableType)executableType;
		ExecutablePath = executablePath;
		ExecutableSteamAppId = executableSteamAppId;
		OverwriteGameFiles = overwriteGameFiles;
		FormatVersion = LatestFormatVersion;
	}
	public Game(JsonElement root, GameEntry entry) {
		Entry = entry;
		FormatVersion = JsonUtil.GetNumberOrThrow(root, "format_version");
		if (FormatVersion > LatestFormatVersion)
			throw new InvalidDataException($"Game in {entry.Path} has a format version too new: {FormatVersion} > {LatestFormatVersion}.");
		
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

		if (FormatVersion <= 2) {
			string outputDatafilePath = JsonUtil.GetStringOrThrow(root, "output_datafile_name", "");
			if (outputDatafilePath != DatafilePath)
				OverwriteGameFiles = false;
			else
				OverwriteGameFiles = true;
		}
		else {
			OverwriteGameFiles = JsonUtil.GetOrDefault(root, "overwrite_game_files", false);
		}
	}

	public string GetCleanDatafilePath() {
		return Path.Combine(Directory, GetCleanDatafileRelativePath());
	}
	public string GetCleanDatafileRelativePath() {
		return Path.Combine("g3man", DatafilePatcher.CleanDataName);
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
	public string GetOutputDatafilePath(Profile profile) {
		return Path.Combine(Directory, GetOutputDatafileRelativePath(profile));
	}
	
	public string GetInputDatafileRelativePath() {
		return DatafilePath;
	}
	public string GetOutputDatafileRelativePath(Profile profile) {
		return GetOutputDatafileRelativePath(profile.ID);
	}
	public string GetOutputDatafileRelativePath(string profileID) {
		if (!OverwriteGameFiles) {
			return Path.Combine("g3man", "stages", profileID, GetInputDatafileRelativePath());
		}
		return GetInputDatafileRelativePath();
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

	public List<string> GetBaseLaunchArguments(string profileId) {
		List<string> gameArguments = ["-game", GetOutputDatafileRelativePath(profileId)];
		
		// at least one version of the linux runner always appended "-game game.unx" at the end of its arguments, overriding
		// our choice of datafile. including a single " character (appears to) make the runner listen to us,
		// so i'm assuming it breaks the argument parser. 
		
		// it's probably more trouble than it's worth...
		//gameArguments.Add("\"");
		
		return gameArguments;
	}
	
	public Process Launch(Config config, Profile profile) {
		Debug.Assert(ExecutableStatus(config).ok);


		List<string> gameArguments = GetBaseLaunchArguments(profile.ID);
		
		switch (ChosenExecutableType) {
			case ExecutableType.File: {
				Process? p = Process.Start(new ProcessStartInfo(Path.Combine(Directory, ExecutablePath), gameArguments) {
					UseShellExecute = false,
					CreateNoWindow = true,
					WorkingDirectory = Directory
				});
				return p;
			}
			case ExecutableType.Steam: {
				List<string> steamArguments = ["-applaunch", ExecutableSteamAppId.ToString(), "--"];
				Process? p = Process.Start(
					new ProcessStartInfo(config.SteamExecutable, steamArguments.Concat(gameArguments).ToList()) {
						UseShellExecute = false,
						CreateNoWindow = true
					});
				return p;
			}
			case ExecutableType.Size:
			default:
				throw new UnreachableException();
		}

		
	}
	
	public JsonObject ToJson() {
		return new JsonObject() {
			["format_version"] = Game.LatestFormatVersion,
			["display_name"] = DisplayName,
			["internal_name"] = InternalName,
			["datafile_name"] = DatafilePath,
			["executable_type"] = (int)ChosenExecutableType,
			["executable_path"] = ExecutablePath,
			["executable_steam_app_id"] = ExecutableSteamAppId,
			["overwrite_game_files"] = OverwriteGameFiles
		};
	}


	public static Game Parse(Stream stream, GameEntry entry) {
		JsonDocument jsonDoc = JsonDocument.Parse(stream);
		Game game = new Game(jsonDoc.RootElement, entry);
		return game;
	}
	
	public static List<Game> ParseAll(List<GameEntry> gameEntries, Action<Exception, GameEntry> errorHandler) {
		ConcurrentBag<Game> games = new ConcurrentBag<Game>();
		

		Parallel.ForEach(gameEntries, gameEntry =>
		{
			try {
				string fullPath = Path.Combine(gameEntry.Path, "g3man", "game.json");
				using FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
				Game game = Parse(stream, gameEntry);
				games.Add(game);
			}
			catch (Exception e) {
				errorHandler(e, gameEntry);
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
