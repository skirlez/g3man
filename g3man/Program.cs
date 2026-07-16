
using System.Text.Json;
using g3man.Core;
using g3man.Core.Util;
using DateTime = System.DateTime;

#if WINDOWS
	using System.Reflection;
	using System.Runtime.InteropServices;
#endif

namespace g3man;

public static class Program {
	public const int Version = 9;
	
	public static TextWriter? Logfile = null;
	
	

	public static Thread MainThread = null!;

	public static List<TextWriter> InfoWriters = new List<TextWriter>([Console.Out]);
	public static List<TextWriter> ErrorWriters = new List<TextWriter>([Console.Error]);
	
	#if WINDOWS
		[DllImport("kernel32.dll")]
		static extern bool AttachConsole(int dwProcessId);
		private const int ATTACH_PARENT_PROCESS = -1;
	#endif
	
	public static int Main(string[] args) {
		#if WINDOWS
			AttachConsole(ATTACH_PARENT_PROCESS);
		#endif
		MainThread = Thread.CurrentThread;
		string logFilename = $"log-{DateTime.Now.Year:D4}-{DateTime.Now.Month:D2}-{DateTime.Now.Day:D2}-{DateTime.Now.Hour:D2}-{DateTime.Now.Minute:D2}-{DateTime.Now.Second:D2}.txt";
		Logger.LoggerPipe pipe = new Logger.LoggerPipe(InfoWriters, ErrorWriters);
		if (args.Length == 0) {
			Logger logger = Logger.Make("", pipe);
			try {
				string logs = Path.Combine(ProgramPaths.GetDataDirectory(), "logs");
				Directory.CreateDirectory(logs);
				StreamWriter logfile = new StreamWriter(Path.Combine(logs, logFilename));
				logfile.AutoFlush = true;
				
				Logfile = logfile;
				InfoWriters.Add(Logfile);
				ErrorWriters.Add(Logfile);
			}
			catch (Exception e) {
				logger.Error("Failed to initialize logging to file: " + e);
				logger.Info("This session will not be logged to file.");
			}

			Config config;
			try {
				JsonElement? configJson = Config.Read();
				config = new Config(configJson.Value, logger);
			}
			catch (FileNotFoundException _) {
				config = new Config();
			}
			catch (Exception e) {
				logger.Error("Failed to read config file: " + e);
				config = new Config();
			}
			

			switch (config.Initializer) {
				default:
					return g3man.GTK.UI.Run(logger, pipe, config);
			}
		}
		
		return CLI.Invoke(args, pipe);
	}
}
