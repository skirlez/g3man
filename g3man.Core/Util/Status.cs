namespace g3man.Core.Util;

public record Status(bool ok, string message) {
	public static readonly Status OK = new Status(true, "");
}

