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

	public static HashSet<object> Running = new();

	public enum Operation {
		TouchingGames,
		TouchingProfiles,
		TouchingMods,
		ChangePage,
		OpenWindow,
		ApplyOrLaunch,
		SaveConfig,
	}

	private static List<List<Operation>> mutuallyExclusiveOperations = [
		[Operation.TouchingGames, Operation.TouchingMods, Operation.TouchingProfiles, Operation.ApplyOrLaunch],
		[Operation.OpenWindow, Operation.ChangePage],
	];

	private static bool isConflicting(Operation operation1, Operation operation2) {
		return operation1 == operation2 
			|| mutuallyExclusiveOperations.Any(x => x.Contains(operation1) && x.Contains(operation2));
	}
	public static bool CanDo(Operation operation) {
		foreach (Operation operation2 in ongoingOperations) {
			if (isConflicting(operation, operation2))
				return false;
		}

		return true;
	}
	
	private static List<Operation> ongoingOperations = new();


	/**
	 * Return a signal handler that might be cancelled if another mutually exclusive operation is taking place.
	 * operations is the list of *possible* operations this signal could perform.
	 */
	public static SignalHandler<T> DoOperation<T>(List<Operation> operations, Func<T, EventArgs, Task> action, bool makeInsensitive = true) where T : Widget {
		return async void (t, eventArgs) => {
			if (!operations.All(CanDo))
				return;
			if (makeInsensitive)
				t.SetSensitive(false);
			ongoingOperations.AddRange(operations);
			try {
				await action(t, eventArgs);
			}
			catch (Exception e) {
				if (t is Button button)
					Logger.Error($"Error while pressing button \"{button.Label ?? "unnamed"}\":\n{e}");
				Logger.Error(e);
			}
			ongoingOperations.RemoveAll(operations.Contains);
			if (makeInsensitive)
				t.SetSensitive(true);
		};
	}
	// useful helpers
	public static SignalHandler<Button> OpenWindowButton(Func<Button, EventArgs, Task> action, bool makeInsensitive = true) {
		return DoOperation([Operation.OpenWindow], action);
	}
	public static SignalHandler<Button> OpenWindowButton(Action<Button, EventArgs> action) {
		return DoOperation([Operation.OpenWindow], action);
	}
	

	/**
	* Non-async version of the above
	*/
	public static SignalHandler<T> DoOperation<T>(List<Operation> operations, Action<T, EventArgs> action) where T : Widget {
		return void (t, eventArgs) => {
			foreach (Operation operation1 in operations) {
				foreach (Operation operation2 in ongoingOperations) {
					if (isConflicting(operation1, operation2))
						return;
				}
			}
			try {
				action(t, eventArgs);
			}
			catch (Exception e) {
				if (t is Button button)
					Logger.Error($"Error while pressing button \"{button.Label ?? "unnamed"}\":\n{e}");
				Logger.Error(e);
			}
		};
	}
	
	
	public static async Task TryWriteConfig() {
		Logger.Debug("Saving config");
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

	public static readonly string NoGameSelected = "No game selected";
	public static void SetGame(Game? newGame) {
		game = newGame;
		mainWindow.CurrentGameLabel.SetText(game?.DisplayName ?? NoGameSelected);
		SetProfile(null);
	}
	public static Profile? GetProfile() {
		return profile;
	}
	public static readonly string NoProfileSelected = "No profile selected";
	
	/**
	 * Sets the current profile of the UI. Returns true if, as a result of this,
	 * the game entry was updated to a new string ID. (If the new ID is empty (profile is null), this will be false)
	 */
	public static bool SetProfile(Profile? newProfile) {
		profile = newProfile;
		mainWindow.CurrentProfileLabel.SetText(profile?.Name ?? NoProfileSelected);
		if (newProfile is null)
			return false;
		
		Debug.Assert(game is not null);
		string oldId = game.Entry.ProfileFolderName;
		string newId = newProfile.ID ?? "";
		game.Entry.ProfileFolderName = newId;
		return oldId != newId;
	}
	public static string CurrentProfileFolderPath() {
		Debug.Assert(game is not null);
		Debug.Assert(profile is not null);
		return game.GetProfileFolderPath(profile);
	}

	private static Thread MainThread = null!;
	private static MainWindow mainWindow = null!;
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
			mainWindow = new();
			MainThread = Thread.CurrentThread;
			application.AddWindow(mainWindow);
			ApplyColorScheme(Config.ColorScheme);

			mainWindow.Show();
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

	public static void ThreadAssert() {
		Debug.Assert(Thread.CurrentThread == MainThread);
	}

}