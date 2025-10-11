using System.Text.Json;
using System.Text.Json.Serialization;

namespace g3man;
using Gtk;
using GObject;

public class State {
	
	public bool SeparateModdedSave;
	
	private State() {
		SeparateModdedSave = false;
	}

	private static readonly State Self = new State();

	public static State Get() {
		return Self;
	}

	public static void Read()
	{
		string config = ProgramPaths.GetConfigDirectory();
		Directory.CreateDirectory(config);
		string text;
		try {
			text = File.ReadAllText(Path.Combine(config, "settings.json"));
		}
		catch (Exception e) {
			// ignored
			return;
		}
		
		JsonDocument jsonDocument = JsonDocument.Parse(text);
	}
}