using System.Security.Cryptography;
using g3man.Models;
using g3man.Util;
using Gtk;
using UndertaleModLib;
using Thread = System.Threading.Thread;

namespace g3man.UI;

public class GameAdderWindow : Window {
	public static Logger logger = new Logger("GAMEADDER");
	
	private readonly Label label;
	private readonly string directory;
	private MainWindow owner;
	public GameAdderWindow(string directory, MainWindow owner) {
		SetSizeRequest(350, 150);
		SetResizable(false);
		this.directory = directory;
		this.owner = owner;
		
		label = Label.New("Adding game...");
		label.SetHalign(Align.Center);
		label.SetValign(Align.Center);
		label.SetJustify(Justification.Center);

		
		SetChild(label);
	}
	private record Success(Game Game, UndertaleData Data);
	private record Error(string Reason, Exception? Exception);
	private Result<Success, Error> LoadAndSetupGame() {
		string? datafilePath = ProgramPaths.GetDatafileFromDirectory(directory);
		if (datafilePath is null)
			return new Result<Success, Error>(new Error("Could not find the game's GameMaker datafile", null));
		byte[] hash;
		UndertaleData data;
		try {
			using (FileStream stream = new FileStream(datafilePath, FileMode.Open, FileAccess.Read)) {
				hash = MD5.Create().ComputeHash(stream);
				data = UndertaleIO.Read(stream);
			}
		}
		catch (Exception e) {
			return new Result<Success, Error>(new Error("An error occurred while reading the game's datafile", e));
		}
		
		if (Patcher.IsDataPatched(data)) {
			// TODO: Write something to check if the clean datafile still exists so we can cleanly readd the game
			return new Result<Success, Error>(new Error("This game is already patched by g3man. Please make sure the game's datafile is not modified so g3man can copy it.", null));
		}


		Profile profile = new Profile("Default", "default", false, []);
		try {
			profile.Write(directory);
		}
		catch (Exception e) {
			return new Result<Success, Error>(new Error("Failed to create default profile folders", e));
		}
		
		Game game = new Game(data.GeneralInfo.DisplayName.Content,
			data.GeneralInfo.FileName.Content, 
			directory, 
			BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(), 
			profile.FolderName);
		
		
		try {
			game.Write();
		}
		catch (Exception e) {
			return new Result<Success, Error>(new Error("Failed to create game.json", e));
		}
		
		try {
			File.Copy(datafilePath, Path.Combine(directory, Patcher.CleanDataName), true);
		}
		catch (Exception e) {
			return new Result<Success, Error>(new Error("Failed to create clean copy of datafile", e));
		}
		
		return new Result<Success, Error>(new Success(game, data));

	}
	
	public void Dialog() {
		SetTransientFor(owner);
		SetModal(true);
		Present();
		Thread thread = new Thread(() => {
			
			Result<Success, Error> result;
			if (Program.Config.GameDirectories.Any(existingDirectory => existingDirectory == directory))
				result = new Result<Success, Error>(new Error("You already have a game with this directory added.", null));
			else 
				result = LoadAndSetupGame();

			Program.RunOnMainThreadEventually(() => {
				if (result.IsOk()) {
					Success s = result.GetValue();
					Program.AddGame(s.Game, s.Data);
					owner.AddToGamesList(s.Game, false);	
					
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