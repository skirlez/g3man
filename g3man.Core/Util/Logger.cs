namespace g3man.Core.Util;

public class Logger {
	private readonly string infoPrefix;
	private readonly string errorPrefix;
	private readonly string debugPrefix;
	public readonly struct LoggerPipe(List<TextWriter> infos, List<TextWriter> errors) {
		public readonly List<TextWriter> Infos = infos;
		public readonly List<TextWriter> Errors = errors;
	}

	public readonly LoggerPipe Pipe;
	
	/**
	* Create a logger that logs to a pipe.
	*/
	public static Logger Make(string prefix, LoggerPipe pipe) {
		return new Logger(prefix, pipe);
	}
	
	
	/**
	* Create a logger that logs to a pipe's error only.
	*/
	public static Logger MakeWithoutInfo(string prefix, LoggerPipe pipe) {
		return new Logger(prefix, new LoggerPipe([], pipe.Errors));
	}
	
	private Logger(string prefix, LoggerPipe pipe) {
		if (prefix == "")
			infoPrefix = errorPrefix = debugPrefix = "";
		else {
			infoPrefix = $"[{prefix}/INFO] ";
			errorPrefix = $"[{prefix}/ERROR] ";
			debugPrefix = $"[{prefix}/DEBUG] ";
		}

		Pipe = pipe;
	}

	public static readonly Logger Null = new Logger(String.Empty, new LoggerPipe([], []));
	
	public void Info(string str) {
		foreach (TextWriter info in Pipe.Infos) {
			info.WriteLine($"{infoPrefix}{str}");
		}
	}


	public void Error(Exception e) {
		Error(e.ToString());
	}

	public void Error(string str) {
		foreach (TextWriter error in Pipe.Errors) {
			error.WriteLine($"{errorPrefix}{str}");
		}
	}

	public void Debug(string str)
	{
		foreach (TextWriter info in Pipe.Infos) {
			#if DEBUG
				info.WriteLine($"{debugPrefix}{str}");
			#endif
		}
	}

	public void DebugNewline()
	{
		foreach (TextWriter info in Pipe.Infos) {
			#if DEBUG
				info.WriteLine("");
			#endif
		}
	}
}