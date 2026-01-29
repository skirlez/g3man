using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Models;
using g3man.Util;

namespace g3man;

public class Config {
	public static readonly Logger logger = Logger.Make("CONFIG");
	public List<string> GameDirectories;
	
	public Program.ColorScheme ColorScheme;
	public Program.Initializer Initializer;
	public bool AllowModScripting;
	public bool CheckForUpdates;

	public Config() {
		GameDirectories = [];
		Initializer = Program.Initializer.Gtk4;
		ColorScheme = Program.ColorScheme.SystemDefault;
	}
	
	public Config(JsonElement root) {
		GameDirectories = JsonUtil.GetOrDefaultClass(root, "game_directories", Array.Empty<string>()).ToList();

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
			["format_version"] = 1,
			["game_directories"] = new JsonArray(GameDirectories.Select(directory => (JsonNode)directory).ToArray()),
			["initializer"] = (int)Initializer,
			["color_scheme"] = (int)ColorScheme,
			["check_for_updates"] = CheckForUpdates,
			["mod_scripting_permissions"] = AllowModScripting ? 1 : 0
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

	public void UpdateGameDirectories(List<Game> games) {
		GameDirectories = games.Select(game => game.Directory).ToList();
	}
}