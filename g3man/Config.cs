using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Util;

namespace g3man;

public class Config {
	private static readonly Logger logger = Logger.Make("CONFIG");
	public Program.ColorScheme ColorScheme;
	public Program.Initializer Initializer;
	public List<GameEntry> GameEntries;
	public bool AllowModScripting;
	public bool CheckForUpdates;

	private const int LatestVersion = 2;
	
	public Config() {
		GameEntries = new List<GameEntry>();
		Initializer = Program.Initializer.Gtk4;
		ColorScheme = Program.ColorScheme.SystemDefault;
		AllowModScripting = false;
		CheckForUpdates = true;
	}
	
	public Config(JsonElement root) {
		int formatVersion = JsonUtil.GetOrDefault(root, "format_version", LatestVersion);
		if (formatVersion == 1) {
			List<string> gameDirectories = JsonUtil.GetOrDefaultClass(root, "game_directories", Array.Empty<string>()).ToList();
			GameEntries = gameDirectories.Select(s => new GameEntry(s, "default")).ToList();
		}
		else {
			JsonElement[] gameEntries = JsonUtil.GetOrDefaultClass<JsonElement[]>(root, "game_entries", null!);
			GameEntries = new List<GameEntry>();
			foreach (JsonElement gameEntry in gameEntries) {
				try {
					GameEntries.Add(new GameEntry(gameEntry));
				}
				catch (Exception e) {
					logger.Error($"Bad game entry, skipping: {e}");
				}
			}
		}
		int initializer = JsonUtil.GetOrDefault(root, "initializer", 0);
		if (initializer < 0 || initializer > 1)
			initializer = 0;
		Initializer = (Program.Initializer)initializer;
		
		int colorScheme = JsonUtil.GetOrDefault(root, "color_scheme", 0);
		if (colorScheme < 0 || colorScheme > 2)
			colorScheme = 0;
		ColorScheme = (Program.ColorScheme)colorScheme;
		
		int allowModScripting = JsonUtil.GetOrDefault(root, "mod_scripting_permissions", 0);
		if (allowModScripting < 0 || allowModScripting > 1)
			allowModScripting = 0;
		AllowModScripting = allowModScripting == 1;
		
		CheckForUpdates = JsonUtil.GetOrDefault(root, "check_for_updates", true);

	}

	public JsonObject ToJson() {
		return new JsonObject() {
			["format_version"] = 2,
			["game_entries"] = new JsonArray(GameEntries.Select(entry => entry.ToJson()).ToArray()),
			["initializer"] = (int)Initializer,
			["color_scheme"] = (int)ColorScheme,
			["check_for_updates"] = CheckForUpdates,
			["mod_scripting_permissions"] = AllowModScripting ? 1 : 0,
		};
	}
	
	public void Write() {
		JsonObject obj = ToJson();
		string directory = ProgramPaths.GetConfigDirectory();
		string filePath = Path.Combine(directory, "config.json");
		string jsonText = obj.ToJsonString();
		try {
			Directory.CreateDirectory(directory);
			logger.Debug("Writing config file");
			File.WriteAllText(filePath, jsonText);
		}
		catch (Exception e) {
			logger.Error("Failed to write config file: " + e);
		}
	}
	
	public static JsonElement? Read() {
		string directory = ProgramPaths.GetConfigDirectory();
		string filePath = Path.Combine(directory, "config.json");
	
		if (!File.Exists(filePath)) 
			return null;
		try {
			string text = File.ReadAllText(filePath);
			return JsonDocument.Parse(text).RootElement;
		}
		catch (Exception e) {
			logger.Error("Failed to read config file: " + e);
			return null;
		}
	}
	
}

public class GameEntry {
	public string Path;
	public string ProfileFolderName;

	public GameEntry(string path, string profileFolderName) {
		Path = path;
		ProfileFolderName = profileFolderName;
	}
	
	public GameEntry(JsonElement root) {
		Path = JsonUtil.GetStringOrThrow(root, "path");
		ProfileFolderName = JsonUtil.GetStringOrThrow(root, "last_selected_profile");
	}
	
	public JsonObject ToJson() {
		return new JsonObject() {
			["path"] = Path,
			["last_selected_profile"] = ProfileFolderName,
		};
	}
}