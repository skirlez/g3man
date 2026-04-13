namespace g3man;

public record Status(bool ok, string message) {
	public static readonly Status OK = new Status(true, "");
}

