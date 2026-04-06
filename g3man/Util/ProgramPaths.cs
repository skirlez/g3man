
using g3man.Models;

namespace g3man.Util;

public static class ProgramPaths {
	public static string GetConfigDirectory() {
		#if LINUX
			return GetEnvironmentVariableDirectory("XDG_CONFIG_HOME", [".config"], ["g3man"]);
		#elif WINDOWS
			string? localAppdata = Environment.GetEnvironmentVariable("LOCALAPPDATA");
			if (localAppdata is not null)
				return Path.Combine(localAppdata, "g3man");
			throw new Exception("LOCALAPPDATA is unset. Please have it set.");
		#else
			throw new Exception("Function not implemented for this OS");
		#endif
	}
	public static string GetDataDirectory() {
		#if LINUX
			return GetEnvironmentVariableDirectory("XDG_STATE_HOME", [".local", "state"], ["g3man"]);
		#elif WINDOWS
			return GetConfigDirectory();
		#else
			throw new Exception("Function not implemented for this OS");
		#endif
	}


	/** Gets a directory from an environment variable, using HOME and homeFallback if it is unset, and appending after in both cases.*/
	private static string GetEnvironmentVariableDirectory(string environmentVariable, string[] homeFallback, string[] after) {
		string? xdg = Environment.GetEnvironmentVariable(environmentVariable);
		if (xdg is not null)
			return Path.Combine(xdg, Path.Combine(after));
		string? home = Environment.GetEnvironmentVariable("HOME");
		if (home is not null)
			return Path.Combine(home, Path.Combine(homeFallback), Path.Combine(after));
		throw new Exception($"\"HOME\" and \"{environmentVariable}\" are unset. Please set any one of them.");
	}
	
	public static PathStatus GameMakerDirectoryStatus(string directory) {
		if (GetDatafileFromDirectory(directory) is null)
			return new PathStatus(false, "No data.win or game.unx found at directory");
		return new PathStatus(true, "Directory contains GameMaker game");
	}
	
	public static (string, string)? GetDatafileFromDirectory(string directory) {
		foreach (string name in IO.DatafileNames) {
			string combined = Path.Combine(directory, name);
			if (File.Exists(combined))
				return (name, combined);
		}
		return null;
	}

	/*
	 * Attempt to guess the path of the Steam folder.
	 * If a guess is a folder that actually exists, it is returned. Otherwise an empty string is returned.
	 */
	private static string GuessSteamPath() {
		#if LINUX
			string[] possiblePaths = [GetEnvironmentVariableDirectory("XDG_DATA_HOME", [".local", "share"], ["Steam"]),
							Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "", ".steam", "steam")];
		#elif WINDOWS
			// does Steam have a 64 bit version now? I don't know
			string[] possiblePaths = [Path.Combine("C:", "Program Files (x86)", "Steam"), Path.Combine("C:", "Program Files", "Steam")];
		#else
			string[] possiblePaths = [];
		#endif
		
		foreach (string path in possiblePaths) {
			if (Directory.Exists(path))
				return path;
		}
		return "";
	}
	
	private static string GuessSteamCommonPath() {
		string path = GuessSteamPath();
		if (path == "")
			return "";
		path = Path.Combine(path, "steamapps", "common");
		if (!Directory.Exists(path))
			return "";
		return path;
	}

	public static List<string> GuessPossibleGamePaths() {
		try {
			string path = GuessSteamCommonPath();
			if (path == "")
				return [];
			return Directory.GetDirectories(path).ToList();
		}
		catch (Exception e) {
			Program.Logger.Error($"Game autodetection error: {e}");
			return [];
		}
	}
	
	public static string GuessExecutablePath(string gameDirectory) {
		try {
			string[] files = Directory.GetFiles(gameDirectory);
			foreach (string file in files) {
				if (Path.GetExtension(file) == ".exe") {
					return Path.GetFileName(file);
				}
			}

			return "";
		}
		catch {
			return "";
		}
	}
}