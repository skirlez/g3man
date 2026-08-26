using System.Diagnostics;
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
	private bool canClose = false;
	
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

	private async void Dialog(List<IMod>? mods, bool applyAndLaunch) {
		SetTransientFor(owner);
		SetModal(true);
		Present();
		Game game = UI.GetGame()!;
		Profile profile = UI.GetProfile()!;
		bool shouldLaunch;
		if (mods == null) {
			shouldLaunch = true;
		}
		else {
			bool success = await PatchRoutine(game, profile, mods);
			shouldLaunch = (success && applyAndLaunch);
		}
		if (shouldLaunch)
			await LaunchRoutine(game, profile);
		canClose = true;
		closeButton.SetSensitive(true);
	}
	
	public void ApplyDialog(List<IMod> mods, bool launch) {
		Dialog(mods, applyAndLaunch: launch);
	}
	public void LaunchDialog() {
		Dialog(mods: null, applyAndLaunch: true);
	}
	
	private async Task<bool> PatchRoutine(Game game, Profile profile, List<IMod> mods) {
		statusLabel.SetText("Hashing current datafile...");
		string hash = "";
		string lastHash = "";
		bool readHashesSuccessfully = false;
		await Task.Run(() => {
			lastHash = IO.GetLastOutputHash(game);
			if (lastHash == "")
				return;
			try {
				using FileStream stream = new(game.GetInputDatafilePath(), FileMode.Open, FileAccess.Read);
				hash = IO.HashToString(MD5.HashData(stream));

				readHashesSuccessfully = true;
			}
			catch {
				// don't care
			}
		});
		
		bool forceReloadDatafile = false;
		if (lastHash != hash && readHashesSuccessfully) {
			string[] buttonTexts = ["Update clean datafile copy", "Keep it as is", "Cancel"];
			AlertDialog alertDialog = AlertDialog.NewWithProperties([]);
			alertDialog.Message =
				$"g3man has detected that the game's datafile ({game.GetInputDatafileRelativePath()}) has been modified.\n"
				+ $"Do you wish to update your clean copy as well?\nIf so, select \"{buttonTexts[0]}\"\n(You probably want to choose this if you just updated the game).\n"
				+ $"Otherwise, select \"{buttonTexts[1]}\" to stay on this version.";
			alertDialog.SetButtons(buttonTexts);
			alertDialog.SetDefaultButton(1);
			alertDialog.SetCancelButton(2);
			int choice = await alertDialog.ChooseAsync(this);
			
			if (choice == 0) {
				statusLabel.SetText("Updating clean datafile...");
				try {
					if (File.Exists(game.GetCleanDatafilePath()))
						File.Move(game.GetCleanDatafilePath(), game.GetBackupDatafilePath(), true);
					File.Copy(game.GetInputDatafilePath(),
						game.GetCleanDatafilePath(), true);
					// update hash to what we just read
					IO.WriteGameLastOutputHash(game.Directory, hash);
				}
				catch (Exception e) {
					UI.Logger.Error($"Failed to update clean datafile: {e}");
					statusLabel.SetText("Failed to update clean datafile! Please report this as a bug.");
					return false;
				}

				forceReloadDatafile = true;
			}
			else if (choice == 1) {
				// update hash to what we just read
				IO.WriteGameLastOutputHash(game.Directory, hash);
			}
			else if (choice == 2) {
				canClose = true;
				UI.RunOnMainThreadEventually(Close);
				return false;
			}
		}
		
		string profilePath = game.GetProfileFolderPath(profile);
		string profileLivePath = game.GetProfileLiveFolderPath(profile);
		bool overwritingInput = (game.DatafilePath == game.GetOutputDatafileRelativePath(profile));
		bool success = await Task.Run(() => { 
			void setStatus(string status) {
				UI.RunOnMainThreadEventually(() => {
					statusLabel.SetMarkup(status);
				});
			}
			
			List<Xdelta> datafileXdeltaPatches = Xdelta.GetDatafileXdeltaPatches(mods, profilePath, game.DatafilePath);
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
			Logger logger = Logger.MakeWithoutInfo("PATCHER", UI.Logger.Pipe); 
			DatafilePatcher datafilePatcher = new(s => {
				setStatus(s);
				logger.Info(s);
			});
			
			string relativeProfilePath = $"g3man/profiles/{profile.ID}";
			int vanillaAudioGroupCount = data.AudioGroups?.Count ?? 0;
			DatafilePatcher.PatchProduct output;
			try {
				output = datafilePatcher.Patch(noXdeltas, profile, profilePath,
					relativeProfilePath, profile.ID,
					data, allowModScripting: UI.Config.AllowModScripting);
			}
			catch (DatafilePatcher.PatcherException e) {
				setStatus(e.ToStringReplacingOther("Check the logs for more information."));
				UI.Logger.Error(e);
				return false;
			}
			catch (Exception e) {
				setStatus("Unknown error occurred while patching! Check the logs and report this as a bug.");
				UI.Logger.Error($"Unhandled exception while patching:\n{e}");
				return false;
			}
			
			setStatus("Writing...");
			
			try {
				IO.Apply(data, vanillaAudioGroupCount, output.AudioGroupTransfers, game, profile, modsFolder: game.GetProfileFolderPath(profile));
			}
			catch (Exception e) {
				UI.Logger.Error(e);
				setStatus("Failed to write output files! Check the log for more information.");
				return false;
			}
			
			
			bool createOldSymlink = mods.Any(m => m.CreateOldProfileSymlink);
			if (createOldSymlink)
				IO.CreateLegacySymlink(game.Directory, game.GetProfileFolderPath(profile));

			return true;
		});
		if (!success)
			return false;
		string launchInstructions =
			overwritingInput ? "You can launch the game by any means to play!" :
				"You must launch the game through g3man\nto see the changes.";
		statusLabel.SetText($"Done!\n{launchInstructions}");
		return true;
	}


	private async Task LaunchRoutine(Game game, Profile profile) {
		try {
			statusLabel.SetText("Launching...");
			Status executableStatus = game.ExecutableStatus(UI.Config);
			if (!executableStatus.ok) {
				statusLabel.SetText(executableStatus.message);
				return;
			}
			if (!File.Exists(game.GetOutputDatafilePath(profile))) {
				statusLabel.SetText("This profile's datafile does not exist! Try applying the mods again.");
				return;
			}
			await Task.Run(() => game.Launch(UI.Config, profile));
		}
		catch (Exception e) {
			UI.Logger.Error($"Failed to launch game: {e}");
			statusLabel.SetText($"Failed to launch game: {e.Message}");
			return;
		}

		statusLabel.SetText($"Game launch should be successful.\ng3man does not have to stay open past this point.");
	}
	
}