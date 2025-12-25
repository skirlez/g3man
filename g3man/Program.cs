using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using g3man.Models;
using g3man.UI;
using g3man.Util;
using GLib;
using Gtk;
using UndertaleModLib;
using DateTime = System.DateTime;

namespace g3man;

public static class Program {
	public const int Version = 4;

	public static Logger Logger = null!;
	public static DataLoader DataLoader = null!;
	public static Config Config = null!;
	
	public static Initializer InitializedUsing;

	private static Game? game;
	private static Profile? profile;

	public static TextWriter Logfile = TextWriter.Null;
	
	private static Application application = null!;
	private static MainWindow window = null!;
	
	public static Profile? GetProfile() {
		return profile;
	}
	
	public static void AddGame(Game newGame) {
		Config.GameDirectories.Add(newGame.Directory);
		Config.Write();
	}
	public static void SetGame(Game newGame) {
		game = newGame;
		DataLoader.LoadAsync(newGame);
	}
	public static Game? GetGame() {
		return game;
	}
	public static void SetProfile(Profile newProfile) {
		profile = newProfile;
		Debug.Assert(game is not null);
		game.ProfileFolderName = profile.FolderName;
		Config.Write();
	}
	
	#if WINDOWS
		[DllImport("kernel32.dll")]
		static extern bool AttachConsole(int dwProcessId);
		const int ATTACH_PARENT_PROCESS = -1;
	#endif
	
	public static int Main(string[] args) {
		#if WINDOWS
			AttachConsole(ATTACH_PARENT_PROCESS);
		#endif
		
		if (args.Length == 0) {
			try {
				string logs = Path.Combine(ProgramPaths.GetDataDirectory(), "logs");
				Directory.CreateDirectory(logs);
				string filename = $"log-{DateTime.Now.Year:D4}-{DateTime.Now.Month:D2}-{DateTime.Now.Day:D2}-{DateTime.Now.Hour:D2}-{DateTime.Now.Minute:D2}-{DateTime.Now.Second:D2}.txt";
				StreamWriter logfile = new StreamWriter(Path.Combine(logs, filename));
				logfile.AutoFlush = true;
				Logfile = logfile;
				Logger = Logger.Make("");
			}
			catch (Exception e) {
				Logger = Logger.Make("");
				Logger.Error("Failed to initialize logging to file: " + e);
				Logger.Error("This session will not be logged to file.");
			}
			DataLoader = new DataLoader();
			JsonElement? configJson = Config.Read();
			if (configJson is null)
				Config = new Config();
			else
				Config = new Config(configJson.Value);
			
			#if WINDOWS
				// force Cairo (fixes black borders around the window on Windows. not sure why this happens)
				// Environment.SetEnvironmentVariable("GSK_RENDERER", "cairo"); Doesn't happen to me anymore!
				
				
			#endif
			
			if (Config.Initializer == Initializer.Gtk)
				application = Application.New("com.skirlez.g3man", Gio.ApplicationFlags.FlagsNone);
			else
				application = Adw.Application.New("com.skirlez.g3man", Gio.ApplicationFlags.FlagsNone);
			InitializedUsing = Config.Initializer;

			application.OnActivate += (_, _) => {
				window = new MainWindow();
				application.AddWindow(window);
				window.Show();
			};
			
			
			return application.RunWithSynchronizationContext([]);
		}

		Logger = Logger.Make("");
		return CLI.Invoke(args);
	}
	

	
	// TODO; I don't really know if this is correct.
	// Seems to work, but there's barely any documentation for this stuff.
	public static void RunOnMainThreadEventually(Action action) {
		GLib.MainContext.Default().InvokeFull((int)GLib.ThreadPriority.Low, () => {
			action.Invoke();
			return false;
		});
	}
	
	public enum Initializer {
		Adwaita,
		Gtk
	}
	public enum Theme {
		SystemDefault,
		Light,
		Dark
	}

	public static void OnClose() {
		Logfile.Close();
	}
}

