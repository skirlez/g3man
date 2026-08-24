using System.Diagnostics;
using g3man.Core;
using g3man.Core.Models;
using g3man.GTK.MainUI;
using g3man.Core.Util;
using g3man.GTK.Util;
using GObject;
using Gtk;

#if WINDOWS
	using System.Reflection;
#endif

namespace g3man.GTK;

public static class UI {
	public static GtkTextBufferWriter? GtkLogWriter = null;

	public static Logger Logger = null!;
	public static Config Config = null!;
	public static DataLoader DataLoader = null!;
	public static Initializer InitializedUsing;
	
	private static Game? game;
	private static Profile? profile;
	
	public static string LogFilename = null!;
	
	public static bool Lock = false;
	private static Func<Task>? queued = null!;

	// This stuff looks really not thread safe but since it's only ever the UI thread running this code, I think it is

	private static async void CheckQueue() {
		while (true) {
			if (queued is null) 
				return;
			Func<Task> current = queued;
			queued = null;
			Lock = true;
			await current();
			Lock = false;
			// we could have went through another await there, so queue could be not null again. so we should go over it again
		}
	}

	/**
	 * Create a signal that is either ran, or is set to queue after the lock is released.
	 * There can only be one action queued.
	 */
	public static SignalHandler<T> LockedOrQueue<T>(Action<T, EventArgs> action) where T : NativeObject {
		return (t, eventArgs) => {
			queued = () => {
				RunSignalAction(t, eventArgs, action);
				return Task.CompletedTask;
			};
			if (Lock)
				return;
			CheckQueue();
		};
	}
	/**
	* Create a signal that is either ran, or is set to queue after the lock is released.
	* There can only be one action queued.
	*/
	public static SignalHandler<T> LockedOrQueue<T>(Func<T, EventArgs, Task> action) where T : NativeObject {
		return (t, eventArgs) => {
			queued = async () => {
				await RunSignalActionAsync(t, eventArgs, action);
			};
			if (Lock)
				return;
			CheckQueue();
		};
	}
	
	/**
	* Create a signal that is either ran or is cancelled if the lock is held.
	* This is used for irreversible actions (like deletion), and actions that can open windows.
	*/
	public static SignalHandler<T> LockedOrCancel<T>(Func<T, EventArgs, Task> action) where T : NativeObject {
		return async void (t, eventArgs) => {
			if (Lock)
				return;
			Lock = true;
			await RunSignalActionAsync(t, eventArgs, action);
			Lock = false;
			CheckQueue();
		};
	}
	public static SignalHandler<T> LockedOrCancel<T>(Action<T, EventArgs> action) where T : NativeObject {
		return (t, eventArgs) => {
			if (Lock)
				return;
			RunSignalAction(t, eventArgs, action);
		};
	}


	private static void RunSignalAction<T>(T t, EventArgs eventArgs, Action<T, EventArgs> action) {
		try {
			action(t, eventArgs);
		}
		catch (Exception e) {
			if (t is Button button)
				Logger.Error($"Error while pressing button \"{button.Label ?? "unnamed"}\":\n{e}");
			Logger.Error(e);
		}
	}
	private static async Task RunSignalActionAsync<T>(T t, EventArgs eventArgs, Func<T, EventArgs, Task> action) {
		try {
			await action(t, eventArgs);
		}
		catch (Exception e) {
			if (t is Button button)
				Logger.Error($"Error while pressing button \"{button.Label ?? "unnamed"}\":\n{e}");
			Logger.Error(e);
		}
		CheckQueue();
	}
	
	
	
	public static async Task TryWriteConfig() {
		try {
			await Task.Run(() => Config.Write());
		}
		catch (Exception e) {
			Logger.Error("Failed to write config: " + e);
		}
	}
	
	public static async void AddGameEntry(GameEntry entry) {
		Config.GameEntries.Add(entry);
		await TryWriteConfig();
	}
	
	public static async void RemoveGameEntry(GameEntry entry) {
		Config.GameEntries.Remove(entry);
		await TryWriteConfig();
	}
	
	public static Game? GetGame() {
		return game;
	}
	public static void SetGame(Game? newGame) {
		game = newGame;
	}
	public static Profile? GetProfile() {
		return profile;
	}
	public static void SetProfile(Profile? newProfile) {
		profile = newProfile;
		game!.Entry.ProfileFolderName = profile?.ID ?? "";
	}
	public static string CurrentProfileFolderPath() {
		Debug.Assert(game is not null);
		Debug.Assert(profile is not null);
		return game.GetProfileFolderPath(profile);
	}

	private static Thread MainThread = null!;
	public static int Run(Logger logger, Logger.LoggerPipe mainPipe, Config config) {
		Logger = logger;
		Config = config;
#if WINDOWS
		// force Cairo (fixes black borders around the window on Windows. not sure why this happens)
		// Doesn't happen to me anymore!
		// Environment.SetEnvironmentVariable("GSK_RENDERER", "cairo");
		
		string? executableDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		string? schemaDir = Environment.GetEnvironmentVariable("GSETTINGS_SCHEMA_DIR");
		if (executableDir is not null && (schemaDir is null || schemaDir.Length == 0))
			Environment.SetEnvironmentVariable("GSETTINGS_SCHEMA_DIR", $"{executableDir}\\default-glib-schemas");
		Environment.SetEnvironmentVariable("GTK_CSD", "0");
#endif
		
		try {
			Gtk.Module.Initialize();
		}
		catch (Exception e) {
			logger.Error($"GTK failed to initialize: {e}");
			return 1;
		}
		
		GtkLogWriter = new GtkTextBufferWriter();
		mainPipe.Infos.Add(GtkLogWriter);
		mainPipe.Errors.Add(GtkLogWriter);

		Application application = null!;
		
		// we check this again to specifically check for libadwaita or gtk
		InitializedUsing = config.Initializer;
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
			DataLoader = new DataLoader(mainPipe);
			MainWindow window = new();
			MainThread = Thread.CurrentThread;
			application.AddWindow(window);
			ApplyColorScheme(Config.ColorScheme);

			window.Show();
		};
		application.OnShutdown += (_, _) => {
			//Config.Write();
			//Program.Logfile?.Flush(); TODO
		};
		return application.RunWithSynchronizationContext(null);

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
	
	
	// TODO; I don't really know if this is correct.
	public static void RunOnMainThreadEventually(Action action) {
		GLib.MainContext.Default().InvokeFull((int)GLib.ThreadPriority.Low, () => {
			action.Invoke();
			return false;
		});
	}

	public static void AssertUI() {
		Debug.Assert(Thread.CurrentThread == MainThread);
	}
}