using System.Security.Cryptography;
using g3man.Core.Models;
using g3man.Core.Patching;
using g3man.Core.Util;
using g3man.GTK.Util;
using Gtk;
using UndertaleModLib;
using Xdelta = g3man.Core.Util.Xdelta;

namespace g3man.GTK;

public class PatcherWindow : G3manWindow {
	private volatile bool canClose = false;
	
	private Label statusLabel;
	private Button closeButton;
	private MainUI.MainWindow owner;
	
	
	public PatcherWindow(MainUI.MainWindow owner) {
		this.owner = owner;
		
		SetSizeRequest(400, 300);

		statusLabel = Label.New("");
		statusLabel.SetJustify(Justification.Center);
		statusLabel.SetValign(Align.Center);
		statusLabel.SetHalign(Align.Center);
		statusLabel.SetMargin(20);
		statusLabel.SetVexpand(true);
		statusLabel.SetWrap(true);
		
		
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

		OnCloseRequest += (_, _) => !canClose;
		SetChild(box);
	}

	public void Dialog(List<IMod> mods, Action onSuccess) {
		SetTransientFor(owner);
		SetModal(true);
		
		new Thread(() => {
			bool success = DoThing(UI.GetGame()!, UI.GetProfile()!, mods);
			UI.RunOnMainThreadEventually(() => {
				canClose = true;
				closeButton.SetSensitive(true);
				if (success)
					onSuccess();
			});
		}).Start();

	}
	private void setStatus(string status) {
		UI.RunOnMainThreadEventually(() => {
			statusLabel.SetMarkup(status);
			if (!IsVisible())
				Present();
		});
	}

	record ChoiceLock() {
		public int Choice = 0;
	}
	private bool DoThing(Game game, Profile profile, List<IMod> mods) {
		string profilePath = game.GetProfileFolderPath(profile);
		string profileLivePath = game.GetProfileLiveFolderPath(profile);
		
		List<Xdelta> datafileXdeltaPatches = Xdelta.GetDatafileXdeltaPatches(mods, profilePath, game.DatafilePath);
		
		setStatus("Hashing current datafile...");
		string hash;
		try {
			using FileStream stream = new FileStream(game.GetInputDatafilePath(), FileMode.Open, FileAccess.Read);
			hash = IO.HashToString(MD5.HashData(stream));
		}
		catch {
			hash = "";
		}
		
		string lastHash = IO.GetLastOutputHash(game);
		bool forceReloadDatafile = false;
		
		if (lastHash != hash && hash != "" && lastHash != "") {
			string[] buttonTexts = ["Update clean datafile copy", "Keep it as is", "Cancel"];
			ChoiceLock lockObject = new ChoiceLock();
			
			PopupWindow popupWindow = new PopupWindow(this, "Question",
				$"g3man has detected that the game's datafile ({game.GetInputDatafileRelativePath()}) has been modified.\n"
				+ $"Do you wish to update your clean copy as well? If so, select \"{buttonTexts[0]}\"\n(You probably want to choose this if you just updated the game).\n"
				+ $"Otherwise, select \"{buttonTexts[1]}\".",
				buttonTexts,
				
				actions: [
					(PopupWindow window) => {
						lock (lockObject) {
							lockObject.Choice = 1;
							Monitor.PulseAll(lockObject);
						}
						window.Close();
					},
					(PopupWindow window) => {
						lock (lockObject) {
							lockObject.Choice = 2;
							Monitor.PulseAll(lockObject);
						}
						window.Close();
					}, 
					PopupWindow.CloseWindowAction,
				], 
				beforeClose: _ => {
					lock (lockObject) {
						if (lockObject.Choice == 0) {
							lockObject.Choice = 3;
							Monitor.PulseAll(lockObject);
						}
					}
				});

			lock (lockObject) {
				UI.RunOnMainThreadEventually(() => popupWindow.Dialog());
				// wait for user to make choice
				while (lockObject.Choice == 0)
					Monitor.Wait(lockObject);
			}

			if (lockObject.Choice == 1) {
				setStatus("Updating clean datafile...");
				try {
					File.Move(game.GetCleanDatafilePath(), game.GetBackupDatafilePath(), true);
					File.Copy(game.GetInputDatafilePath(),
						game.GetCleanDatafilePath(), true);
					// update hash to what we just read
					IO.WriteGameLastOutputHash(game.Directory, hash);
				}
				catch (Exception e) {
					UI.Logger.Error($"Failed to update clean datafile: {e}");
					setStatus("Failed to update clean datafile! Please report this as a bug.");
					return false;
				}

				forceReloadDatafile = true;
			}
			else if (lockObject.Choice == 2) {
				// update hash to what we just read
				IO.WriteGameLastOutputHash(game.Directory, hash);
			}
			else if (lockObject.Choice == 3) {
				canClose = true;
				UI.RunOnMainThreadEventually(Close);
				return false;
			}
		}
		
		/*
		if (mods.Count == 0) {
			setStatus("Restoring clean datafile");
			try
			{
				IO.RemoveLastOutputHash(game);
				IO.Deapply(game);

				setStatus("Restored clean datafile!");
			}
			catch (FileNotFoundException) {
				setStatus("The game's clean datafile couldn't be found.\n"
						  + "See the <a href=\"https://github.com/skirlez/g3man/wiki/Error:-Failed-to-load-game's-clean-datafile\">wiki page</a> for this error.");
			}
			catch (Exception e) {
				UI.Logger.Error($"Failed to restore clean datafile: {e}");
				setStatus("Failed to restore clean datafile. Please report this as an error!");
			}

			return;
		}
		*/
		
		UI.DataLoader.LoadAsync(game, datafileXdeltaPatches, forceReloadDatafile);
		
		setStatus("Applying .xdelta patches");
		
		IO.CreateLiveFolder(profilePath, profileLivePath);

		(IMod Mod, XdeltaSourcePair FailedPatch)? xdeltaError = IO.CreateXdeltaFoldersAndApply(game.Directory, profilePath, profileLivePath, mods);
		if (xdeltaError.HasValue) {
			setStatus($"Mod {xdeltaError.Value.Mod.Identify()} had a failed Xdelta patch called \"{xdeltaError.Value.FailedPatch.Filename}\"\nfor \"{xdeltaError.Value.FailedPatch.RelativeSourcePath}\"");
			return false;
		} 
		
		setStatus("Waiting for game data to load...");
		UndertaleData data;
		lock (UI.DataLoader.Lock) {
			while (!UI.DataLoader.CanSnatch()) {
				if (UI.DataLoader.HasErrored()) {
					setStatus("Failed to load the game's clean datafile.\nThis can happen for a number of reasons.\n"
							  + "See the <a href=\"https://github.com/skirlez/g3man/wiki/Error:-Failed-to-load-game's-clean-datafile\">wiki page</a> for this error.");
					return false;
				}
				Monitor.Wait(UI.DataLoader.Lock);
			}
			data = UI.DataLoader.Snatch();
		}


		List<Mod> noXdeltas = mods.Where(m => m is Mod).Cast<Mod>().ToList();
		DatafilePatcher datafilePatcher = new DatafilePatcher();


		string relativeProfilePath = $"g3man/profiles/{profile.ID}";
		int vanillaAudioGroupCount = data.AudioGroups.Count;
		DatafilePatcher.PatchProduct? output;
		try {
			output = datafilePatcher.Patch(noXdeltas, profile, profilePath,
				relativeProfilePath, profile.ID,
				data, Logger.MakeWithoutInfo("PATCHER", UI.Logger.Pipe), setStatus, allowModScripting: UI.Config.AllowModScripting);
		}
		catch (Exception e) {
			setStatus("Unknown error occurred while patching! Report this as a bug.");
			UI.Logger.Error($"Unhandled exception while patching:\n{e}");
			return false;
		}

		if (!output.HasValue)
			return false;
		
		
		bool overwritingInput = (game.DatafilePath == game.GetOutputDatafileRelativePath(profile));
		
		setStatus("Writing...");
		
		try {
			if (game.GetPatchParadigm() == Game.PatchParadigm.Launch) {
				IO.CreateStage(data, game.Directory, game.DatafilePath,game.GetProfileFolderPath(profile), profile.ID, vanillaAudioGroupCount,
					output.Value.AudioGroupTransfers);
			}
			else {
				IO.Apply(output.Value.Data, game.Directory, game.GetProfileFolderPath(profile),
					game.GetOutputDatafileRelativePath(profile),
					writeHash: overwritingInput, vanillaAudioGroupCount, output.Value.AudioGroupTransfers);
			}
		}
		catch (Exception e) {
			UI.Logger.Error(e);
			setStatus("Failed to write output datafile! Check the log.");
			return false;
		}
		
		bool createOldSymlink = mods.Any(m => m.CreateOldProfileSymlink);
		if (createOldSymlink)
			IO.CreateLegacySymlink(game.Directory, game.GetProfileFolderPath(profile));
		
		string launchInstructions =
			overwritingInput ? "You can launch the game by any means to play!" :
						"You must launch the game through g3man\nto see the changes.";
		
		setStatus($"Done!\n{launchInstructions}");
		return true;
	}
}