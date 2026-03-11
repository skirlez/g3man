namespace g3man.Util;

public class Logger {
	private readonly string infoPrefix;
	private readonly string errorPrefix;
	private readonly string debugPrefix;
	
	private readonly TextWriter[] infos;
	private readonly TextWriter[] errors;


	/**
	* Create a logger that logs to standard out, standard error, and the log file.
	*/
	public static Logger Make(string prefix) {
		return new Logger(prefix, Program.InfoWriters(), Program.ErrorWriters());
	}
	
	/**
	* Create a logger that only logs to standard error and the log file.
	*/
	public static Logger MakeWithoutInfo(string prefix) {
		return new Logger(prefix, [], Program.ErrorWriters());
	}
	
	private Logger(string prefix, TextWriter[] infos, TextWriter[] errors) {
		if (prefix == "")
			infoPrefix = errorPrefix = debugPrefix = "";
		else {
			infoPrefix = $"[{prefix}/INFO] ";
			errorPrefix = $"[{prefix}/ERROR] ";
			debugPrefix = $"[{prefix}/DEBUG] ";
		}

		this.infos = infos;
		this.errors = errors;
	}

	public static readonly Logger Null = new Logger(String.Empty, [TextWriter.Null], [TextWriter.Null]);
	
	public void Info(string str) {
		foreach (TextWriter info in infos) {
			info.WriteLine($"{infoPrefix}{str}");
		}
	}


	public void Error(Exception e) {
		Error(e.ToString());
	}

	public void Error(string str) {
		foreach (TextWriter error in errors) {
			error.WriteLine($"{errorPrefix}{str}");
		}
	}

	public void Debug(string str)
	{
		foreach (TextWriter info in infos) {
			#if DEBUG
				info.WriteLine($"{debugPrefix}{str}");
			#endif
		}
	}

	public void DebugNewline()
	{
		foreach (TextWriter info in infos) {
			#if DEBUG
				info.WriteLine("");
			#endif
		}
	}
}