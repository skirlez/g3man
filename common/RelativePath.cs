namespace common;

public readonly struct RelativePath(string relativePath) {
	private readonly string str = relativePath;
	public static implicit operator RelativePath(string str) {
		return new RelativePath(str);
	}
	public static implicit operator string(RelativePath path) {
		return path.str;
	}
}