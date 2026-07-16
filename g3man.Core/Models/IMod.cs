using g3man.Core.Util;

namespace g3man.Core.Models;

public interface IMod {
	public string ModId { get; }
	public string DisplayName { get; }
	public string Description { get; }
	public Credit[] Credits { get; }
	public SemVer? MaybeVersion { get; }

	public string Identify() {
		return $"{DisplayName} (ID \"{ModId}\")";
	} 
	
	public bool CreateOldProfileSymlink { get;  }
	
	public List<XdeltaSourcePair> GetXdeltaTargetPairs(string gameFolder, string profileFolder);
	public List<Xdelta>  GetDatafileXdeltaPatches(string profileFolder, string datafileName);
	
	public void Delete(string profileFolder);
}