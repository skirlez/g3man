using System.Diagnostics;

namespace g3man.Models;

public class XdeltaMod : IMod {
	
	public string ModId { get; }
	public string DisplayName { get; }

	public string Description => "";
	public Credit[] Credits => [];
	public SemVer? MaybeVersion => null;

	public string filename;
	
	public XdeltaMod(string path) {
		filename = Path.GetFileName(path);
		DisplayName = filename;
		ModId = Path.GetFileNameWithoutExtension(path).ToLower();
	}
	
	public static List<XdeltaMod> ParseAll(string directory) {
		string[] modFiles;
		try {
			modFiles = Directory.GetFiles(directory).Where(path => Path.GetExtension(path) == ".xdelta").ToArray();
		}
		catch {
			return [];
		}
		return modFiles.Select(path => new XdeltaMod(path)).ToList();
	}


	public string GetXdeltaPath(string profileFolder) {
		return Path.Combine(profileFolder, filename);
	}

	public void Delete(string profileFolder) {
		File.Delete(Path.Combine(profileFolder, filename));
	}
}