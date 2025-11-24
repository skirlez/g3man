
namespace g3man.Util;

public static class ProgramPaths {
	public static string GetConfigDirectory() {
		#if LINUX
			string? xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
			if (xdg is not null)
				return Path.Combine(xdg, "g3man");
			string? home = Environment.GetEnvironmentVariable("HOME");
			if (home is not null)
				return Path.Combine(home, ".config", "g3man");
			throw new Exception("HOME and XDG_DATA_HOME are unset. Please set any one of them.");
		#elif WINDOWS
			string? localAppdata = Environment.GetEnvironmentVariable("LOCALAPPDATA");
			if (localAppdata is not null)
				return Path.Combine(localAppdata, "g3man");
			throw new Exception("LOCALAPPDATA is unset. Please have it set.");
		#else
			throw new Exception("Function not implemented for this OS");
		#endif
	}

	public static PathStatus GameMakerDirectoryStatus(string directory) {
		if (GetDatafileFromDirectory(directory) is null)
			return new PathStatus(false, "No data.win or game.unx found at directory");
		return new PathStatus(true, "Directory contains GameMaker game");
	}
	
	public static (string, string)? GetDatafileFromDirectory(string directory) {
		// all technically valid gamemaker data filenames
		string[] datafileNames = ["data.win", "game.unx", "game.ios", "game.droid"];
		foreach (string name in datafileNames) {
			string combined = Path.Combine(directory, name);
			if (File.Exists(combined))
				return (name, combined);
		}
		return null;
	}
}