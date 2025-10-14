using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Models;
using g3man.Util;

namespace g3man;

public class Config {
	public static readonly Logger logger = new Logger("CONFIG");
	public List<string> GameDirectories;
	
	#if WINDOWS
		public int Theme;
	#endif
	
	public Config() {
		GameDirectories = [];
	}
	
	public Config(JsonElement root)
	{
		GameDirectories = JsonUtil.GetStringArrayOrThrow(root, "game_directories").ToList();
		#if WINDOWS
			int theme = JsonUtil.GetNumberOrThrow(root, "theme");
			if (theme < 0 || theme > 2)
				throw new InvalidDataException("Field \"theme\" must be between 0 and 2");
			Theme = theme;
		#endif
	}

	public JsonObject ToJson() {
	return new JsonObject() {
			["format_version"] = 1,
			["game_directories"] = new JsonArray(GameDirectories.Select(directory => (JsonNode)directory).ToArray()),
			#if WINDOWS
				["theme"] = Theme
			#endif
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