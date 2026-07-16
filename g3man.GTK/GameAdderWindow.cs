using System.Diagnostics;
using System.Security.Cryptography;
using g3man.Core;
using g3man.GTK.MainUI;
using g3man.Core.Models;
using g3man.Patching;
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
		
		SetDefaultSize(600, 400);
		this.mainWindow = mainWindow;
		this.directory = directory;
		label = Label.New(null);
		label.SetHalign(Align.Center);
		label.SetValign(Align.Center);
		label.SetJustify(Justification.Center);
		
		datafileInfo = ProgramPaths.GetDatafileFromDirectory(directory);
		if (datafileInfo is null) {
			label.SetText($"This folder does not have a datafile.\n(One of: {IO.CommaSeparatedDatafilePaths()})");
			SetChild(label);
			return;
		}
		
		Widget widget = LaunchParadigmWindow.CreateLaunchParadigmWidgets(showRegretLabel: true, (Game.LaunchParadigm? choice) => {
			if (choice is null) {
				Close();
				return;
			}
			label.SetText("Adding game...");
			SetChild(label);
			Thread thread = new Thread(() => ThreadRoutine(choice.Value));
			thread.Start();
		});
		SetChild(widget);
	}
	private record Success(Game Game);
	private record Error(string Reason, Exception? Exception);


	private Result<Success, Error> LoadAndSetupGame(Game.LaunchParadigm paradigm) {
		Debug.Assert(datafileInfo is not null);
		(string datafileRelativePath, string datafilePath) = datafileInfo.Value;
		string outputDatafileName = Game.GetDefaultOutputDatafilePath(datafileRelativePath);
		
		
		UndertaleData data;
		byte[] hash;
		try {
			using FileStream stream = new FileStream(datafilePath, FileMode.Open, FileAccess.Read);
			hash = MD5.HashData(stream);
			stream.Seek(0, SeekOrigin.Begin);
			data = UndertaleIO.Read(stream, ((warning, important) => { if (important ) UI.Logger.Info(warning); }));
		}
		catch (Exception e) {
			return new Result<Success, Error>(new Error("An error occurred while reading the game's datafile", e));
		}



		string defaultProfileID = "default";

		GameEntry entry = new GameEntry(directory, defaultProfileID);

		Game game = new Game(entry,
			data.GeneralInfo.DisplayName.Content,
			data.GeneralInfo.FileName.Content,
			datafileRelativePath,
			0,
			ProgramPaths.GuessExecutablePath(directory),
			-1, outputDatafileName);

		bool cleanDataExists = Path.Exists(game.GetCleanDatafilePath());
		if (!cleanDataExists && DatafilePatcher.IsDataPatched(data))
			return new Result<Success, Error>(new Error($"This game is already patched by g3man.\nPlease make sure the game's \"{datafileRelativePath}\" file is not modified so g3man can copy it.", null));



		if (!cleanDataExists) {
			Profile profile = new Profile("Default", defaultProfileID, false, "", false, []);
			try {
				profile.Write(game);
			}
			catch (Exception e) {
				return new Result<Success, Error>(new Error("Failed to create default profile folders", e));
			}
		}

		try {
			game.Write();
		}
		catch (Exception e) {
			return new Result<Success, Error>(new Error("Failed to create game.json", e));
		}

		
		try {
			if (!cleanDataExists)
				File.Copy(datafilePath, game.GetCleanDatafilePath(), true);
			
			IO.WriteGameLastOutputHash(game.Directory, hash);
		}
		catch (Exception e) {
			return new Result<Success, Error>(new Error("Failed to create clean copy of datafile", e));
		}
		


		return new Result<Success, Error>(new Success(game));

	}

	private void ThreadRoutine(Game.LaunchParadigm paradigm) {
		Result<Success, Error> result = LoadAndSetupGame(paradigm);
		UI.RunOnMainThreadEventually(() => {
			if (result.IsOk()) {
				Success s = result.GetValue();
				UI.AddGameEntry(s.Game.Entry);
				mainWindow.AddToGamesList(s.Game, false);	
				Close();
			}
			else
			{
				Error err = result.GetError();
				UI.Logger.Error(err.Reason);
				if (err.Exception is not null)
					UI.Logger.Error(err.Exception.ToString());
				label.SetText("Game couldn't be added:\n" + err.Reason);
			}
		});
	}
	
	public void Dialog(Window window) {
		SetTransientFor(window);
		SetModal(true);
		Present();
	}
}