
using System.Diagnostics;
using System.Security.Cryptography;
using Gtk;
using UndertaleModLib;
using Thread = System.Threading.Thread;

namespace g3man;

public class GameAdder : Window {
	private readonly Label label;
	private readonly string directory;
	private MainWindow owner;
	public GameAdder(string directory, MainWindow owner) {
		SetSizeRequest(320, 150);
		SetResizable(false);
		this.directory = directory;
		this.owner = owner;
		
		label = Label.New("Adding game...");
		label.SetHalign(Align.Center);
		label.SetValign(Align.Center);
		label.SetJustify(Justification.Center);

		
		SetChild(label);
	}
	private record Success(string GameName, string InternalName, UndertaleData Data, byte[] hash);
	private record Error(string Reason);
	private Result<Success, Error> CanAddGame(string datafilePath) {
		try {
			FileStream stream = new FileStream(datafilePath, FileMode.Open, FileAccess.Read);
			byte[] hash = MD5.Create().ComputeHash(stream);
			UndertaleData data = UndertaleIO.Read(stream);

			if (Patcher.IsDataPatched(data)) {
				// TODO: Write something to check if the clean datafile still exists
				return new Result<Success, Error>(new Error("This game is already patched by g3man. Please make sure the game's datafile is not modified so g3man can copy it."));
			}
			
			return new Result<Success, Error>(new Success(
				data.GeneralInfo.DisplayName.Content, 
				data.GeneralInfo.FileName.Content,
				data, hash));
		}
		catch (Exception e) {
			Console.WriteLine("Couldn't open game's datafile: " + e);
			return new Result<Success, Error>(new Error("An error occurred while reading the game's datafile"));;
		}
	}
	
	public void Dialog() {
		SetTransientFor(owner);
		SetModal(true);
		Present();
		Thread thread = new Thread(() => {
			string? datafilePath = ProgramPaths.GetDatafileFromDirectory(directory);
			
			Result<Success, Error> result;
			if (datafilePath == null)
				result = new Result<Success, Error>(new Error("Could not find the game's GameMaker datafile"));
			else
				result = CanAddGame(datafilePath);
			GLib.MainContext.Default().InvokeFull((int)GLib.ThreadPriority.Low, () => {
				if (result.IsOk()) {
					Debug.Assert(datafilePath is not null);
					
					Success s = result.GetValue();
					Game game = new Game(s.GameName, s.InternalName, directory, s.hash);
					File.Copy(datafilePath, Path.Combine(directory, Patcher.CleanDataName), true);
					owner.AddToGamesList(game);
					Program.DataLoader.Assume(game, s.Data);
					Close();

				}
				else {
					label.SetText("Game couldn't be added:\n" + result.GetError().Reason);
				}

				return false;
			});




		});
		thread.Start();
	}
}