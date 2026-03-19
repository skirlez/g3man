namespace g3man.Models;

public interface IMod {
	public string ModId { get; }
	public string DisplayName { get; }
	public string Description { get; }
	public Credit[] Credits { get; }
	public SemVer? MaybeVersion { get; }

	public string GetXdeltaPath(string profileFolder);
	
	public void Delete(string profileFolder);
}