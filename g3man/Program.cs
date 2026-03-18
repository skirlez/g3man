using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using g3man.Models;
using g3man.Util;
using Gdk;
using Gtk;
using DateTime = System.DateTime;
using MainWindow = g3man.UI.Main.MainWindow;

#if WINDOWS
	using GdkWin32;
	using Win32;
#endif

namespace g3man;

public static class Program {
	public const int Version = 6;

	public static Logger Logger = null!;
	public static DataLoader DataLoader = null!;
	public static Config Config = null!;

	public static Initializer InitializedUsing;

	private static Game? game;
	private static Profile? profile;

	public static string LogFilename = null!;
	private static TextWriter? Logfile = null;
	public static GtkTextBufferWriter? GtkLogWriter = null;
	
	public static List<TextWriter> InfoWriters = new List<TextWriter>([Console.Out]);
	public static List<TextWriter> ErrorWriters = new List<TextWriter>([Console.Error]);
	
	private static Application application = null!;

	public static Profile? GetProfile() {
		return profile;
	}

	public static void AddGame(Game newGame) {
		Config.GameDirectories.Add(newGame.Directory);
		Config.Write();
	}
	public static void SetGame(Game newGame) {
		game = newGame;
	}
	public static Game? GetGame() {
		return game;
	}
	public static void SetProfile(Profile newProfile) {
		profile = newProfile;
	}
	public static string CurrentProfileFolderPath() {
		Debug.Assert(game is not null);
		Debug.Assert(profile is not null);
		return game.GetProfileFolderPath(profile);
	}

	public static Thread MainThread = null!;

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
		LogFilename = $"log-{DateTime.Now.Year:D4}-{DateTime.Now.Month:D2}-{DateTime.Now.Day:D2}-{DateTime.Now.Hour:D2}-{DateTime.Now.Minute:D2}-{DateTime.Now.Second:D2}.txt";
		if (args.Length == 0) {
			Logger = Logger.Make("");
			try {
				string logs = Path.Combine(ProgramPaths.GetDataDirectory(), "logs");
				Directory.CreateDirectory(logs);
				StreamWriter logfile = new StreamWriter(Path.Combine(logs, LogFilename));
				logfile.AutoFlush = true;
				
				Logfile = logfile;
				InfoWriters.Add(Logfile);
				ErrorWriters.Add(Logfile);
			}
			catch (Exception e) {
				Logger.Error("Failed to initialize logging to file: " + e);
				Logger.Error("This session will not be logged to file.");
			}

			try {
				Gtk.Module.Initialize();
			}
			catch (Exception e) {
				Logger.Error($"GTK failed to initialize: {e}");
				return 1;
			}
			
			GtkLogWriter = new GtkTextBufferWriter();
			InfoWriters.Add(GtkLogWriter);
			ErrorWriters.Add(GtkLogWriter);
	
			
			#if WINDOWS
				// force Cairo (fixes black borders around the window on Windows. not sure why this happens)
				// Doesn't happen to me anymore!
				// Environment.SetEnvironmentVariable("GSK_RENDERER", "cairo");

				string? schemaDir = Environment.GetEnvironmentVariable("GSETTINGS_SCHEMA_DIR");
				if (schemaDir is null || schemaDir.Length == 0)
					Environment.SetEnvironmentVariable("GSETTINGS_SCHEMA_DIR", "./default-glib-schemas");
				Environment.SetEnvironmentVariable("GTK_CSD", "0");
			#endif
			
			JsonElement? configJson = Config.Read();
			if (configJson is null)
				Config = new Config();
			else
				Config = new Config(configJson.Value);
			InitializedUsing = Config.Initializer;
			if (InitializedUsing == Initializer.Libadwaita) {
				try {
					application = Adw.Application.New("com.skirlez.g3man", Gio.ApplicationFlags.FlagsNone);
				}
				catch (Exception e) {
					Logger.Error($"Failed to create Libadwaita application instance: {e}\nTrying GTK instead...");
					InitializedUsing = Initializer.Gtk4;
				}
			}

			if (InitializedUsing == Initializer.Gtk4) {
				try {
					application = Application.New("com.skirlez.g3man", Gio.ApplicationFlags.FlagsNone);
				}
				catch (Exception e) {
					Logger.Error($"Failed to create GTK application instance: {e}");
					return 1;
				}
			}
			#if WINDOWS
				GdkWin32.Module.Initialize();
			#endif
			
			application.OnActivate += (_, _) => {
				DataLoader = new DataLoader();
				MainWindow window = new MainWindow();
				application.AddWindow(window);
				ApplyColorScheme(Config.ColorScheme);
				
				window.Show();
			};
			application.OnShutdown += (_, _) => {
				//Config.Write();
				Logfile?.Flush();
			};
			return application.RunWithSynchronizationContext([]);
		}

		Logger = Logger.Make("");
		return CLI.Invoke(args);
	}
	

	// TODO; I don't really know if this is correct.
	public static void RunOnMainThreadEventually(Action action) {
		GLib.MainContext.Default().InvokeFull((int)GLib.ThreadPriority.Low, () => {
			action.Invoke();
			return false;
		});
	}

	public enum Initializer {
		Gtk4,
		Libadwaita
	}
	public enum ColorScheme {
		SystemDefault,
		Light,
		Dark
	}
	
	public enum Theme {
		SystemDefault,
		None,
	}


	public static void ApplyColorScheme(ColorScheme colorScheme) {
		if (InitializedUsing == Initializer.Gtk4) {
			Settings? settings = Settings.GetDefault();
			if (settings is null)
				return;
			settings.GtkInterfaceColorScheme = colorScheme switch {
				ColorScheme.SystemDefault => InterfaceColorScheme.Default,
				ColorScheme.Light => InterfaceColorScheme.Light,
				ColorScheme.Dark => InterfaceColorScheme.Dark,
				_ => throw new UnreachableException()
			};
		}
		else {
			Adw.StyleManager.GetDefault().SetColorScheme(colorScheme switch {
				ColorScheme.SystemDefault => Adw.ColorScheme.Default,
				ColorScheme.Light => Adw.ColorScheme.ForceLight,
				ColorScheme.Dark => Adw.ColorScheme.ForceDark,
				_ => throw new UnreachableException()
			});
		}
	}


}
