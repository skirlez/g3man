
using g3man.Models;
using g3man.Patching;
using Gtk;
using UndertaleModLib;

namespace g3man.UI;

public class PatcherWindow : Window {
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

		OnCloseRequest += (sender, args) => !closeButton.IsSensitive();
		SetChild(box);
	}

	public void Dialog(List<Mod> mods) {
		SetTransientFor(owner);
		SetModal(true);
		
		new Thread(() => {
			Start:
			UndertaleData data;
			string hash;
			lock (Program.DataLoader.Lock) {
				while (!Program.DataLoader.CanSnatch()) {
					if (Program.DataLoader.HasErrored()) {
						setStatus("Failed to load game's data.win. Check that g3man/clean_data.win exists in the game folder.\nIf it does, report this as a bug!");
						return;
					}
					setStatus("Waiting for game data to load...");
					Monitor.Wait(Program.DataLoader.Lock);
				}
				hash = Program.DataLoader.GetDirtyHash();
				data = Program.DataLoader.Snatch();
			}


			string lastHash = IO.GetLastOutputHash(Program.GetGame()!);
			
			if (lastHash != hash && hash != "" && lastHash != "") {
				string[] buttonTexts = ["Update Clean Datafile", "Overwrite"];
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
					]);

				lock (lockObject) {
					popupWindow.Dialog();
					// wait for user to make choice
					while (choice == 0)
						Monitor.Wait(lockObject);
				}

				if (choice == 1) {
					// update clean datafile
					setStatus("Updating clean datafile...");
					try {
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
					goto Start;
				}
			}

			if (mods.Count == 0) {
				Program.RunOnMainThreadEventually(() => {
					statusLabel.SetText("Restoring clean datafile");
					Present();
				});
				try {
					IO.RemoveLastOutputHash(Program.GetGame()!);
					IO.Deapply(Program.GetGame()!);
					
					Program.RunOnMainThreadEventually(() => 
						statusLabel.SetText("Restored clean datafile!"));
				}
				catch (Exception e) {
					Console.Error.WriteLine(e);
					Program.RunOnMainThreadEventually(() => 
						statusLabel.SetLabel("Failed to restore clean datafile. Please report this as an error!"));
				}
			}
			else 
				DoThing(mods, data);
			Program.RunOnMainThreadEventually(() => {
				closeButton.SetSensitive(true);
			});
		}).Start();

	}
	private void setStatus(string status) {
		statusLabel.SetText(status);
		if (!IsVisible())
			Present();
	}
	private void DoThing(List<Mod> mods, UndertaleData data) {
		Patcher patcher = new Patcher();
		string profileDirectory = Path.Combine(Program.GetGame()!.Directory, "g3man", Program.GetProfile()!.FolderName);
		UndertaleData? output = patcher.Patch(mods, Program.GetProfile()!, 
			profileDirectory, data, (status) => {
				Program.RunOnMainThreadEventually(() => setStatus(status));
			});
		if (output is null)
			return;
		Program.RunOnMainThreadEventually(() => setStatus("Writing..."));
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