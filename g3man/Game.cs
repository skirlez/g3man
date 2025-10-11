namespace g3man;

public class Game
{
	public string DisplayName;
	public string InternalName;
	public string Directory;
	public byte[] Hash;

	public Game(string displayName, string internalName, string directory, byte[] hash) {
		DisplayName = displayName;
		InternalName = internalName;
		Directory = directory;
		Hash = hash;
	}

	public bool HasSameData(Game other) {
		return Hash.SequenceEqual(other.Hash);
	}

	public string GetCleanDatafilePath() {
		return Path.Combine(Directory, Patcher.CleanDataName);
	}
}