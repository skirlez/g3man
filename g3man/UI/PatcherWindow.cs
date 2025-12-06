
using System.Security.Cryptography;
using g3man.Models;
using g3man.Patching;
using g3man.Util;
using Gtk;
using UndertaleModLib;

namespace g3man.UI;

public class PatcherWindow : Window {
	private volatile bool canClose = false;
	
	private Label statusLabel;
	private Button closeButton;
	private MainWindow owner;
	
	
	public PatcherWindow(MainWindow owner) {
		this.owner = owner;
		
		SetSizeRequest(400, 300);
		SetResizable(false);
		statusLabel = Label.New("");
		statusLabel.SetJustify(Justification.Center);
		statusLabel.SetValign(Align.Center);
		statusLabel.SetHalign(Align.Center);
		statusLabel.SetMargin(20);
		statusLabel.SetVexpand(true);
		
		closeButton = Button.NewWithLabel("Close");
		closeButton.SetSensitive(false);
		closeButton.SetValign(Align.End);
		closeButton.SetHalign(Align.Center);
		closeButton.OnClicked += (_, _) => {
			Close();
		};
		closeButton.SetMarginBottom(10);
		
		Box box = Box.New(Orientation.Vertical, 10);
		box.Append(statusLabel);
		box.Append(closeButton);

		OnCloseRequest += (sender, args) => !canClose;
		SetChild(box);
	}

	public void Dialog(List<Mod> mods) {
		SetTransientFor(owner);
		SetModal(true);
		
		new Thread(() => {
			DoThing(mods);
			Program.RunOnMainThreadEventually(() => {
				canClose = true;
				closeButton.SetSensitive(true);
			});
		}).Start();

	}
	private void setStatus(string status) {
		Program.RunOnMainThreadEventually(() => {
			statusLabel.SetMarkup(status);
			if (!IsVisible())
				Present();
		});
	}
	private void DoThing(List<Mod> mods) {
		setStatus("Hashing current datafile...");
		string hash;
		try {
			using FileStream stream = new FileStream(Program.GetGame()!.GetOutputDatafilePath(), FileMode.Open, FileAccess.Read);
			hash = IO.HashToString(MD5.HashData(stream));
		}
		catch (Exception _) {
			hash = "";
		}
		
		string lastHash = IO.GetLastOutputHash(Program.GetGame()!);
		
		if (lastHash != hash && hash != "" && lastHash != "") {
			string[] buttonTexts = ["Update Clean Datafile", "Overwrite", "Cancel"];
			object lockObject = new object();
			int choice = 0;
			PopupWindow popupWindow = new PopupWindow(this, "Question",
				"g3man has detected that the game's datafile has been modified.\n"
				+ $"Did you update the game? If so, select \"{buttonTexts[0]}\".\n"
				+ $"Otherwise, select \"{buttonTexts[1]}\".",
				buttonTexts,
				[
					(PopupWindow window) => {
						window.Close();
						lock (lockObject) {
							choice = 1;
							Monitor.Pulse(lockObject);
						}
					},
					(PopupWindow window) => {
						window.Close();
						lock (lockObject) {
							choice = 2;
							Monitor.Pulse(lockObject);
						}
					},
					(PopupWindow window) => {
						window.Close();
						lock (lockObject) {
							choice = 3;
							Monitor.Pulse(lockObject);
						}
					},
				]);

			lock (lockObject) {
				Program.RunOnMainThreadEventually(() => popupWindow.Dialog());
				// wait for user to make choice
				while (choice == 0)
					Monitor.Wait(lockObject);
			}

			if (choice == 1) {
				// update clean datafile
				setStatus("Updating clean datafile...");
				try {
					File.Move(Program.GetGame()!.GetCleanDatafilePath(), Program.GetGame()!.GetBackupDatafilePath(), true);
					
					File.Copy(Program.GetGame()!.GetOutputDatafilePath(),
						Program.GetGame()!.GetCleanDatafilePath(), true);
					Program.GetGame()!.Hash = hash;
					Program.GetGame()!.Write();
					IO.RemoveLastOutputHash(Program.GetGame()!);
				}
				catch (Exception e) {
					Console.Error.WriteLine(e);
					setStatus("Failed to update clean datafile! Please report this as a bug.");
					return;
				}


				Program.DataLoader.LoadAsync(Program.GetGame()!);
			}
			else if (choice == 2) {
				// do nothing
			}
			else if (choice == 3) {
				canClose = true;
				Program.RunOnMainThreadEventually(Close);
				return;
			}
		}
		
		if (mods.Count == 0) {
			setStatus("Restoring clean datafile");
			try
			{
				IO.RemoveLastOutputHash(Program.GetGame()!);
				IO.Deapply(Program.GetGame()!);

				setStatus("Restored clean datafile!");
			}
			catch (FileNotFoundException _) {
				setStatus("The game's clean datafile couldn't be found.\n"
				          + "See the <a href=\"https://github.com/skirlez/g3man/wiki/Error:-Failed-to-load-game's-clean-datafile\">wiki page</a> for this error.");
			}
			catch (Exception e) {
				Console.Error.WriteLine(e);
				setStatus("Failed to restore clean datafile. Please report this as an error!");
			}

			return;
		}
		
		UndertaleData data;
		lock (Program.DataLoader.Lock) {
			while (!Program.DataLoader.CanSnatch()) {
				if (Program.DataLoader.HasErrored()) {
					setStatus("Failed to load the game's clean datafile.\nThis can happen for a number of reasons.\n"
					          + "See the <a href=\"https://github.com/skirlez/g3man/wiki/Error:-Failed-to-load-game's-clean-datafile\">wiki page</a> for this error.");
					return;
				}
				setStatus("Waiting for game data to load...");
				Monitor.Wait(Program.DataLoader.Lock);
			}
			data = Program.DataLoader.Snatch();
		}

		
		
		Patcher patcher = new Patcher();
		string profileDirectory = Path.Combine(Program.GetGame()!.Directory, "g3man", Program.GetProfile()!.FolderName);
		UndertaleData? output = patcher.Patch(mods, Program.GetProfile()!, profileDirectory, data, Logger.MakeWithoutInfo("PATCHER"), setStatus);
		if (output is null)
			return;
		setStatus("Writing...");
		try {
			IO.Apply(output, Program.GetGame()!.Directory, profileDirectory, Program.GetGame()!.DatafileName);
		}
		catch (Exception e) {
			Console.Error.WriteLine(e);
			setStatus("Failed to write output datafile! Check the log.");
			return;
		}
		setStatus("Done!");
	}
}