namespace g3man.Util;

public class Logger(string prefix)
{
	private string prefix = prefix;
	public void Info(string str) {
		Console.WriteLine($"[{prefix}/INFO] {str}");
	}
	
	public void Error(string str) {
		Console.WriteLine($"[{prefix}/ERROR] {str}");
	}

	public void Debug(string str)
	{
		#if DEBUG
			Console.WriteLine($"[{prefix}/DEBUG] {str}");
		#endif
	}

	public void DebugNewline(string empty)
	{
		#if DEBUG
			Console.WriteLine("");
		#endif
	}
}