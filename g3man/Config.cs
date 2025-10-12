using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using g3man.Models;
using g3man.Util;

namespace g3man;

public class Config {
	public static readonly Logger logger = new Logger("CONFIG");
	public List<Game> Games;
	
	public Config() {
		Games = [];
	}
	
	public Config(JsonElement root) {
		Games = JsonUtil.GetObjectArrayOrThrow(root, "games").Select(element => new Game(element)).ToList();
	}

	public JsonObject ToJson() {
		JsonNode?[] gameObjects = Games.Select(game => game.ToJson()).ToArray<JsonNode?>();
		return new JsonObject() {
			["format_version"] = 1,
			["games"] = new JsonArray(gameObjects)
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