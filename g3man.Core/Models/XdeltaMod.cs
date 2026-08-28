using g3man.Core.Util;

namespace g3man.Core.Models;

public class XdeltaMod : IMod {
	
	public string ModId { get; }
	public string DisplayName { get; }

	public string Description => "";
	public Credit[] Credits => [];
	public SemVer? MaybeVersion => null;
	
	public bool CreateOldProfileSymlink
	{
		get => false;
	}

	public string Filename;
	
	public XdeltaMod(string path) {
		Filename = Path.GetFileName(path);
		DisplayName = Filename;
		ModId = Path.GetFileNameWithoutExtension(path).ToLower().Replace(" ", "_");
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


	public List<XdeltaSourcePair> GetXdeltaTargetPairs(string gameFolder, string profileFolder) {
		return [];
	}

	public List<Xdelta> GetDatafileXdeltaPatches(string profileFolder, string _)
	{
		return [new Xdelta(profileFolder, Filename)];
	}

	public void Delete(string profileFolder) {
		File.Delete(Path.Combine(profileFolder, Filename));
	}
}