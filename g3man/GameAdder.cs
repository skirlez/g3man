
using System.Diagnostics;
using System.Security.Cryptography;
using g3man.Models;
using g3man.Util;
using Gtk;
using UndertaleModLib;
using Thread = System.Threading.Thread;

namespace g3man;

public class GameAdder : Window {
	private readonly Label label;
	private readonly string directory;
	private MainWindow owner;
	public GameAdder(string directory, MainWindow owner) {
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
	private record Success(string GameName, string InternalName, UndertaleData Data, string Hash, string ProfileId);
	private record Error(string Reason);
	private Result<Success, Error> LoadAndSetupGame(string datafilePath) {
		byte[] hash;
		UndertaleData data;
		try {
			using (FileStream stream = new FileStream(datafilePath, FileMode.Open, FileAccess.Read)) {
				hash = MD5.Create().ComputeHash(stream);
				data = UndertaleIO.Read(stream);
			}
		}
		catch (Exception e) {
			Console.WriteLine("Couldn't open game's datafile: " + e);
			return new Result<Success, Error>(new Error("An error occurred while reading the game's datafile"));
		}
		
		if (Patcher.IsDataPatched(data)) {
			// TODO: Write something to check if the clean datafile still exists so we can cleanly readd the game
			return new Result<Success, Error>(new Error("This game is already patched by g3man. Please make sure the game's datafile is not modified so g3man can copy it."));
		}

		Profile profile = new Profile("Default", "default", false, []);
		try {
			profile.Write(directory);
	
		}
		catch (Exception e) {
			Console.WriteLine("Failed to create default profile folders: " + e);
			return new Result<Success, Error>(new Error("Couldn't create default profile folders"));
		}
		
		try {
			File.Copy(datafilePath, Path.Combine(directory, Patcher.CleanDataName), true);
		}
		catch (Exception e) {
			Console.WriteLine("Failed to create clean copy of datafile: " + e);
			return new Result<Success, Error>(new Error("Failed to create clean copy of datafile"));
		}
		
		return new Result<Success, Error>(new Success(
			data.GeneralInfo.DisplayName.Content, 
			data.GeneralInfo.FileName.Content,
			data, BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant(),
			profile.FolderName));

	}
	
	public void Dialog() {
		SetTransientFor(owner);
		SetModal(true);
		Present();
		Thread thread = new Thread(() => {

			string? datafilePath = null;
			Result<Success, Error> result;
			if (Program.Config.Games.Any(game => game.Directory == directory))
				result = new Result<Success, Error>(new Error("You already have a game with this directory added."));
			else {
				datafilePath = ProgramPaths.GetDatafileFromDirectory(directory);
				if (datafilePath is null)
					result = new Result<Success, Error>(new Error("Could not find the game's GameMaker datafile"));
				else
					result = LoadAndSetupGame(datafilePath);
			}

			Program.RunOnMainThreadEventually(() => {
				if (result.IsOk()) {
					Debug.Assert(datafilePath is not null);
					Success s = result.GetValue();
					
					Game game = new Game(s.GameName, s.InternalName, directory, s.Hash, s.ProfileId);
					Program.AddGame(game, s.Data);
					owner.AddToGamesList(game, false);	
					
					Close();
				}
				else {
					label.SetText("Game couldn't be added:\n" + result.GetError().Reason);
				}
			});
		});
		thread.Start();
	}
}