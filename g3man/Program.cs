using System.Diagnostics;
using System.Text.Json;
using g3man.Models;
using Gtk;
using UndertaleModLib;

namespace g3man;

public static class Program {
	public static DataLoader DataLoader = null!;
	public static Config Config = null!;
	
	public static Initializer InitializedUsing;

	private static Game? game;
	private static Profile? profile;
	public static Profile? GetProfile() {
		return profile;
	}
	
	public static void AddGame(Game newGame, UndertaleData newData) {
		if (game == null) {
			DataLoader.Assume(newData);
		}
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
	
	public static int Main(string[] args) {
		
		DataLoader = new DataLoader();
		JsonElement? configJson = Config.Read();
		if (configJson is null)
			Config = new Config();
		else
			Config = new Config(configJson.Value);

		Application application;
		
		if (Config.Initializer == Initializer.Gtk)
			application = Application.New("com.skirlez.g3man", Gio.ApplicationFlags.FlagsNone);
		else
			application = Adw.Application.New("com.skirlez.g3man", Gio.ApplicationFlags.FlagsNone);
		InitializedUsing = Config.Initializer;
		
		application.OnActivate += (sender, _) => {
			MainWindow window = new MainWindow();
			application.AddWindow(window);
			window.Show();
		};
		return application.RunWithSynchronizationContext([]);
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
		Gtk,
		Adwaita
	}
	public enum Theme {
		SystemDefault,
		Light,
		Dark
	}
}

