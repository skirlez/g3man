using g3man.Util;

namespace g3man.Models;

public interface IMod {
	public string ModId { get; }
	public string DisplayName { get; }
	public string Description { get; }
	public Credit[] Credits { get; }
	public SemVer? MaybeVersion { get; }
	
	public bool CreateOldProfileSymlink { get;  }
	
	public List<XdeltaSourcePair> GetXdeltaTargetPairs(string gameFolder, string profileFolder);
	public List<Xdelta>  GetDatafileXdeltaPatches(string profileFolder);
	
	public void Delete(string profileFolder);
}