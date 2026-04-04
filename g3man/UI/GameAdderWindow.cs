using System.Security.Cryptography;
using g3man.Models;
using g3man.Patching;
using g3man.UI.Main;
using g3man.Util;
using Gtk;
using UndertaleModLib;
using Thread = System.Threading.Thread;

namespace g3man.UI;

public class GameAdderWindow : G3manWindow {
	public static Logger logger = Logger.Make("GAMEADDER");
	
	private readonly Label label;
	private readonly string directory;
	private MainWindow mainWindow;
	public GameAdderWindow(string directory, MainWindow mainWindow) {
		SetSizeRequest(350, 150);
		SetResizable(false);
		this.directory = directory;
		this.mainWindow = mainWindow;
		
		label = Label.New("Adding game...");
		label.SetHalign(Align.Center);
		label.SetValign(Align.Center);
		label.SetJustify(Justification.Center);

		
		SetChild(label);
	}
	private record Success(Game Game);
	private record Error(string Reason, Exception? Exception);
	private Result<Success, Error> LoadAndSetupGame() {
		(string, string)? datafileInfo = ProgramPaths.GetDatafileFromDirectory(directory);
		if (datafileInfo is null)
			return new Result<Success, Error>(new Error("Could not find the game's GameMaker datafile", null));
		(string datafileName, string datafilePath) = datafileInfo.Value;
		string outputDatafileName = "g3man_" + datafileName;
		
		
		byte[] hash;
		UndertaleData data;
		try {
			using FileStream stream = new FileStream(datafilePath, FileMode.Open, FileAccess.Read);
			hash = MD5.HashData(stream);
			data = UndertaleIO.Read(stream);
		}
		catch (Exception e) {
			return new Result<Success, Error>(new Error("An error occurred while reading the game's datafile", e));
		}
		
		if (DatafilePatcher.IsDataPatched(data)) {
			// TODO: Write something to check if the clean datafile still exists so we can cleanly readd the game
			return new Result<Success, Error>(new Error("This game is already patched by g3man. Please make sure the game's datafile is not modified so g3man can copy it.", null));
		}

		string defaultProfileID = "default";

		GameEntry entry = new GameEntry(directory, defaultProfileID);
		
		Game game = new Game(entry,
			data.GeneralInfo.DisplayName.Content,
			data.GeneralInfo.FileName.Content, 
			datafileName, 
			0,
			ProgramPaths.GuessExecutablePath(directory),
			-1, outputDatafileName);

		Profile profile = new Profile("Default", defaultProfileID, false, "", []);
		try {
			profile.Write(game);
		}
		catch (Exception e) {
			return new Result<Success, Error>(new Error("Failed to create default profile folders", e));
		}
		

		
		
		try {
			game.Write();
		}
		catch (Exception e) {
			return new Result<Success, Error>(new Error("Failed to create game.json", e));
		}
		
		try {
			File.Copy(datafilePath, game.GetCleanDatafilePath(), true);
		}
		catch (Exception e) {
			return new Result<Success, Error>(new Error("Failed to create clean copy of datafile", e));
		}
		
		return new Result<Success, Error>(new Success(game));

	}
	
	public void Dialog(Window window) {
		SetTransientFor(window);
		SetModal(true);
		Present();
		Thread thread = new Thread(() => {
			
			Result<Success, Error> result;
			/*
			if (Program.Config.GameEntries.Any(entry => entry.Path == directory))
				result = new Result<Success, Error>(new Error("You already have a game with this directory added.", null));
			else 
			*/
			result = LoadAndSetupGame();

			Program.RunOnMainThreadEventually(() => {
				if (result.IsOk()) {
					Success s = result.GetValue();
					Program.AddGameEntry(s.Game.Entry);
					mainWindow.AddToGamesList(s.Game, false);	
					Close();
				}
				else
				{
					Error err = result.GetError();
					logger.Error(err.Reason);
					if (err.Exception is not null)
						logger.Error(err.Exception.ToString());
					label.SetText("Game couldn't be added:\n" + err.Reason);
				}
			});
		});
		thread.Start();
	}
}