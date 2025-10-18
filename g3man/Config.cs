using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Models;
using g3man.Util;

namespace g3man;

public class Config {
	public static readonly Logger logger = new Logger("CONFIG");
	public List<string> GameDirectories;
	
	public Program.Theme Theme;
	public Program.Initializer Initializer;
	
	public Config() {
		GameDirectories = [];
		Initializer = Program.Initializer.Adwaita;
		Theme = Program.Theme.SystemDefault;
	}
	
	public Config(JsonElement root) {
		GameDirectories = JsonUtil.GetStringArrayOrThrow(root, "game_directories").ToList();
		int initializer = JsonUtil.GetNumberOrThrow(root, "initializer");
		if (initializer < 0 || initializer > 1)
			throw new InvalidDataException("Field \"initializer\" must be in the range of 0-1 (inclusive)");
		Initializer = (Program.Initializer)initializer;
			int theme = JsonUtil.GetNumberOrThrow(root, "theme");
			if (theme < 0 || theme > 2)
				throw new InvalidDataException("Field \"theme\" must be in the range of 0-2 (inclusive)");
		Theme = (Program.Theme)theme;
	}

	public JsonObject ToJson() {
		return new JsonObject() {
			["format_version"] = 1,
			["game_directories"] = new JsonArray(GameDirectories.Select(directory => (JsonNode)directory).ToArray()),
			["initializer"] = (int)Initializer,
			["theme"] = (int)Theme
		};
	}
	
	public void Write() {
		JsonObject obj = ToJson();
		string directory = ProgramPaths.GetConfigDirectory();
		string filePath = Path.Combine(directory, "config.json");
		string jsonText = obj.ToJsonString();
		try {
			Directory.CreateDirectory(directory);
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