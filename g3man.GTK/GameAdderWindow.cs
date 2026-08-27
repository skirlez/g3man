using System.Diagnostics;
using System.Security.Cryptography;
using g3man.Core;
using g3man.GTK.MainUI;
using g3man.Core.Models;
using g3man.Core.Patching;
using g3man.Core.Util;
using Gtk;
using UndertaleModLib;
using Thread = System.Threading.Thread;

namespace g3man.GTK;

public class GameAdderWindow : G3manWindow {

	private Label label;
	private readonly string directory;
	private MainWindow mainWindow;
	private (string, string)? datafileInfo;
	public GameAdderWindow(string directory, MainWindow mainWindow) {
		
		SetDefaultSize(500, 300);
		this.mainWindow = mainWindow;
		this.directory = directory;
		label = Label.New(null);
		label.SetHalign(Align.Center);
		label.SetValign(Align.Center);
		label.SetJustify(Justification.Center);
		label.SetWrap(true);
	
		SetChild(label);
		datafileInfo = ProgramPaths.GetDatafileFromDirectory(directory);
		if (datafileInfo is null) {
			label.SetText($"This folder does not have a datafile.\n(One of: {IO.CommaSeparatedDatafilePaths()})");
			return;
		}
		label.SetText("Adding game...");
		Thread thread = new(ThreadRoutine);
		thread.Start();
	}
	
	

	private Game LoadAndSetupGame() {
		Debug.Assert(datafileInfo is not null);
		(string datafileRelativePath, string datafilePath) = datafileInfo.Value;
		
		using FileStream stream = new(datafilePath, FileMode.Open, FileAccess.Read);
		byte[] hash = MD5.HashData(stream);
		stream.Seek(0, SeekOrigin.Begin);
		UndertaleData data = UndertaleIO.Read(stream, (warning, important) => {
			if (important) 
				UI.Logger.Info(warning);
		});
		
		string defaultProfileID = "default";

		GameEntry entry = new(directory, defaultProfileID);

		Game game = new(entry,
			data.GeneralInfo.DisplayName.Content,
			data.GeneralInfo.FileName.Content,
			datafileRelativePath,
			0,
			ProgramPaths.GuessExecutablePath(directory),
			-1, overwriteGameFiles: false);

		bool cleanDataExists = Path.Exists(game.GetCleanDatafilePath());
		if (!cleanDataExists && DatafilePatcher.IsDataPatched(data))
			throw new Exception($"This game is already patched by g3man.\nPlease make sure the game's \"{datafileRelativePath}\" file is not modified so g3man can copy it.");
		if (!cleanDataExists) {
			Profile profile = new("Default", defaultProfileID, false, "", []);
			profile.Write(game);
		}
		game.Write();
		if (!cleanDataExists)
			File.Copy(datafilePath, game.GetCleanDatafilePath(), true);
		IO.WriteGameLastOutputHash(game.Directory, hash);
		return game;
	}

	private void ThreadRoutine() {
		Game game;
		try {
			game = LoadAndSetupGame();
		}
		catch (Exception e) {
			UI.RunOnMainThreadEventually(() => {
				string error = $"Game couldn't be added:\n{e}";
				UI.Logger.Error(error);
				label.SetText(error);
			});
			return;
		}
		UI.RunOnMainThreadEventually(() => {
			UI.AddGameEntry(game.Entry);
			mainWindow.AddToGamesList(game);	
			Close();
		});
	}
	
	public void Dialog(Window window) {
		SetTransientFor(window);
		SetModal(true);
		Present();
	}
}