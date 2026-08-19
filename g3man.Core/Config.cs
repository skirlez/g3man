using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Core.Util;

namespace g3man.Core;

public class Config {
	public ColorScheme ColorScheme;
	public Initializer Initializer;
	public List<GameEntry> GameEntries;
	public bool AllowModScripting;
	public bool CheckForUpdates;
	public string SteamExecutable;

	private const int FormatVersion = 2;

	public Config() : this(new JsonElement(), Logger.Null) { }

	public Config(JsonElement root, Logger errorLogger) {
		int formatVersion = JsonUtil.GetOrDefault(root, "format_version", FormatVersion);
		if (formatVersion == 1) {
			List<string> gameDirectories = JsonUtil.GetOrDefaultClass(root, "game_directories", Array.Empty<string>()).ToList();
			GameEntries = gameDirectories.Select(s => new GameEntry(s, "default")).ToList();
		}
		else {
			JsonElement[] gameEntries = JsonUtil.GetOrDefaultClass<JsonElement[]>(root, "game_entries", []);
			GameEntries = new List<GameEntry>();
			foreach (JsonElement gameEntry in gameEntries) {
				try {
					GameEntries.Add(new GameEntry(gameEntry));
				}
				catch (Exception e) {
					errorLogger.Error($"Bad game entry, skipping: {e}");
				}
			}
		}
		int initializer = JsonUtil.GetOrDefault(root, "initializer", 0);
		if (initializer < 0 || initializer > 1)
			initializer = 0;
		Initializer = (Initializer)initializer;
		
		int colorScheme = JsonUtil.GetOrDefault(root, "color_scheme", 0);
		if (colorScheme < 0 || colorScheme > 2)
			colorScheme = 0;
		ColorScheme = (ColorScheme)colorScheme;
		
		int allowModScripting = JsonUtil.GetOrDefault(root, "mod_scripting_permissions", 0);
		if (allowModScripting < 0 || allowModScripting > 1)
			allowModScripting = 0;
		AllowModScripting = allowModScripting == 1;
		
		CheckForUpdates = JsonUtil.GetOrDefault(root, "check_for_updates", true);
		
		SteamExecutable = JsonUtil.GetOrDefaultClass(root, "steam_executable", ProgramPaths.GuessSteamExecutablePath());

	}

	public JsonObject ToJson() {
		return new JsonObject() {
			["format_version"] = 2,
			["game_entries"] = new JsonArray(GameEntries.Select(entry => entry.ToJson()).ToArray()),
			["initializer"] = (int)Initializer,
			["color_scheme"] = (int)ColorScheme,
			["check_for_updates"] = CheckForUpdates,
			["steam_executable"] = SteamExecutable,
			["mod_scripting_permissions"] = AllowModScripting ? 1 : 0,
		};
	}
	
	public void Write() {
		JsonObject obj = ToJson();
		string directory = ProgramPaths.GetConfigDirectory();
		string filePath = Path.Combine(directory, "config.json");
		string jsonText = obj.ToJsonString();
		Directory.CreateDirectory(directory);
		File.WriteAllText(filePath, jsonText);
	}
	
	public static JsonElement Read() {
		string directory = ProgramPaths.GetConfigDirectory();
		string filePath = Path.Combine(directory, "config.json");
		if (!File.Exists(filePath))
			throw new FileNotFoundException();
		string text = File.ReadAllText(filePath);
		return JsonDocument.Parse(text).RootElement;
	}
	
}

public class GameEntry {
	public readonly string Path;
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

public enum Initializer {
	Gtk4,
	Libadwaita
}
public enum ColorScheme {
	SystemDefault,
	Light,
	Dark
}