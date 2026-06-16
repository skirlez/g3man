using g3man.Models;
using g3man.UI.Main;
using g3man.Util;
using Gtk;

namespace g3man.UI;

public class ManageGameWindow : G3manWindow {
	private MainWindow mainWindow;
	
	public ManageGameWindow(Game game, MainWindow mainWindow, Func<Game, bool> saveCallback, Func<bool> removeCallback) {
		this.mainWindow = mainWindow;
		SetSizeRequest(400, 300);
		SetTitle("Manage Game");
		
		
		Label nameLabel = Label.New("Name");
		nameLabel.SetHalign(Align.Start);
		
		Entry nameEntry = Entry.New();
		nameEntry.SetText(game.DisplayName);
		nameEntry.SetTooltipText("This name is for display purposes only, so you can put whatever you want here.");
		Box nameBox = Box.New(Orientation.Vertical, 5);
		nameBox.Append(nameLabel);
		nameBox.Append(nameEntry);
		
		
		ComboBoxText launchMethod = ComboBoxText.New();
		launchMethod.AppendText("Launch game directly");
		launchMethod.AppendText("Launch through Steam");
		launchMethod.SetActive((int)game.ChosenExecutableType);
		launchMethod.SetHalign(Align.Start);
		launchMethod.SetValign(Align.End);
		launchMethod.SetTooltipText("How g3man should launch the game.\nLaunch directly - g3man will run the file provided.\nLaunch through Steam - g3man will tell Steam which App ID to run");
		
		Stack launchMethodStack = Stack.New();
		Box launchMethodBox = Box.New(Orientation.Horizontal, 10)
			.With(launchMethodStack, launchMethod);
		
		Label fileExecutableLabel = Label.New("Executable file");
		fileExecutableLabel.SetHalign(Align.Start);
		
		Entry fileExeEntry = Entry.New();
		fileExeEntry.SetText(game.ExecutablePath);
		fileExeEntry.SetHexpand(true);
		
		Button fileExeBrowse = Button.NewWithLabel("Browse");
		fileExeBrowse.OnClicked += (_, _) => {
			FileDialogWindow window = new FileDialogWindow("Choose an executable", [],file => {
				string? path = file.GetPath();
				if (path is null)
					return;
				string relativePath = Path.GetRelativePath(game.Directory, path);
				if (relativePath.StartsWith("..")) {
					// path should only be relative if it is inside the game's folder
					fileExeEntry.SetText(path);
				}
				else
					fileExeEntry.SetText(relativePath);
			}, game.Directory);
			window.Dialog(this);
		};
		
		Box fileExeEntryBox = Box.New(Orientation.Horizontal, 5);
		fileExeEntryBox.Append(fileExeBrowse);
		fileExeEntryBox.Append(fileExeEntry);
		
		Box fileExeBox = Box.New(Orientation.Vertical, 5);
		fileExeBox.Append(fileExecutableLabel);
		fileExeBox.Append(fileExeEntryBox);
		
		Label steamExecutableLabel = Label.New("Steam App ID");
		steamExecutableLabel.SetHalign(Align.Start);
		
		Entry steamAppIdEntry = Entry.New();
		int appId = game.ExecutableSteamAppId;
		steamAppIdEntry.SetText(appId == -1 ? "" : appId.ToString());
		
		Box steamExeBox = Box.New(Orientation.Vertical, 5);
		steamExeBox.Append(steamExecutableLabel);
		steamExeBox.Append(steamAppIdEntry);
		
		Widget[] methodBoxes = [fileExeBox, steamExeBox];
		foreach (Widget methodBox in methodBoxes) {
			launchMethodStack.AddChild(methodBox);
		}
		void UpdateExecutableType(int type) {
			launchMethodStack.SetVisibleChild(methodBoxes[type]);
		}
		UpdateExecutableType((int)game.ChosenExecutableType);
		launchMethod.OnChanged += (sender, _) => UpdateExecutableType(sender.GetActive());

		Label launchParadigmLabel = Label.New("Current patching paradigm:");
		launchParadigmLabel.SetHalign(Align.Start);
		Stack paradigmDisplayStack = Stack.New();
		paradigmDisplayStack.SetHalign(Align.Start);
		
		Label[] paradigms = [
			Label.New("\"Do not modify game; launch via g3man/arguments ONLY\""),
			Label.New("\"Modify game; can launch via any means\"")
		];
		foreach (Label p in paradigms) {
			paradigmDisplayStack.AddChild(p);
			p.SetHalign(Align.Start);
		}

		Game.LaunchParadigm currentParadigm = game.GetLaunchParadigm();
		void UpdateParadigmChoice(Game.LaunchParadigm launchParadigm) {
			paradigmDisplayStack.SetVisibleChild(paradigms[(int)launchParadigm]);
			currentParadigm = launchParadigm;
		}
		UpdateParadigmChoice(currentParadigm);
		
		Button changeLaunchParadigm = Button.NewWithLabel("Change patching paradigm");
		changeLaunchParadigm.SetValign(Align.Center);
		changeLaunchParadigm.SetHalign(Align.End);

		Box paradigmSpacer = Box.New(Orientation.Horizontal, 0);
		paradigmSpacer.SetHexpand(true);
		
		Box paradigmBox = 
			Box.New(Orientation.Horizontal, 10)
			.With(
				Box.New(Orientation.Vertical, 6)
					.With(
						launchParadigmLabel,
						paradigmDisplayStack
					),
				paradigmSpacer,
				changeLaunchParadigm
			);
		paradigmBox.SetMarginTop(5);
;
		paradigmBox.SetHexpand(true);
		
		
		
		changeLaunchParadigm.OnClicked += (_, _) => {
			LaunchParadigmWindow paradigmWindow = new LaunchParadigmWindow(showRegretLabel: false, (choice) => {
				if (choice is null)
					return;
				UpdateParadigmChoice(choice.Value);
			});
			paradigmWindow.Dialog(this);
		};
		
		
		
		Button openGameFolderButton = Button.NewWithLabel("Open game folder");
		openGameFolderButton.OnClicked += (_, _) => { IO.OpenFileExplorer(game.Directory); };
		openGameFolderButton.SetHalign(Align.Start);
		
		Button cleanToInput = Button.NewWithLabel($"(1) Copy {game.GetCleanDatafileRelativePath()} -> {game.GetInputDatafileRelativePath()}");
		cleanToInput.OnClicked += (_, _) => {
			DoFileOperation(() => {
				try {
					IO.Deapply(game);
					return (true, null);
				}
				catch (Exception e) {
					Program.Logger.Error($"Failed to restore clean datafile: {e}");
					return (false, null);
				}
			});
		};
		cleanToInput.SetHalign(Align.Start);
		
		Button inputToClean = Button.NewWithLabel($"(2) Copy {game.GetInputDatafileRelativePath()} -> {game.GetCleanDatafileRelativePath()}");
		inputToClean.OnClicked += (_, _) => {
			DoFileOperation(() => {
				try {
					File.Copy(game.GetCleanDatafilePath(), game.GetBackupDatafilePath(), true);
					File.Copy(game.GetInputDatafilePath(), game.GetCleanDatafilePath(), true);
					Program.DataLoader.ReloadAsync();
					return (true, null);
				}
				catch (Exception e) {
					Program.Logger.Error($"Failed to copy input datafile to clean datafile: {e}");
					return (false, null);
				}
			});
		};
		inputToClean.SetHalign(Align.Start);
		Button restoreCleanBackup = Button.NewWithLabel($"(3) Restore last backup of clean datafile");
		restoreCleanBackup.OnClicked += (_, _) => {
			DoFileOperation(() => {
				try {
					if (!File.Exists(game.GetBackupDatafilePath()))
						return (false, "No clean datafile backup found...");
					File.Copy(game.GetBackupDatafilePath(), game.GetCleanDatafilePath(), true);
					Program.DataLoader.ReloadAsync();

					return (true, null);
				}
				catch (Exception e) {
					Program.Logger.Error($"Failed to copy input datafile to clean datafile: {e}");
					return (false, null);
				}
			});
		};
		restoreCleanBackup.SetHalign(Align.Start);
		
		
		Button saveButton = Button.NewWithLabel("Save");
		saveButton.OnClicked += (_, _) => {
			int newAppId;
			try {
				newAppId = int.Parse(steamAppIdEntry.GetText());
			}
			catch {
				newAppId = -1;
			}


			string outputDatafileName;
			if (currentParadigm == Game.LaunchParadigm.Launch) {
				outputDatafileName = Game.GetDefaultOutputDatafilePath(game.DatafilePath);
			}
			else {
				outputDatafileName = game.DatafilePath;
			}
			Game newGame = new Game(game.Entry, nameEntry.GetText(), game.InternalName, game.DatafilePath,
					launchMethod.Active, fileExeEntry.GetText(), newAppId, outputDatafileName);
			if (saveCallback(newGame)) {
				Close();
			}
		};
		Button removeButton = Button.NewWithLabel("Remove entry");
		removeButton.OnClicked += (_, _) => {
			if (removeCallback()) {
				Close();
			}
		};
		
		Box fateBox = Box.New(Orientation.Horizontal, 10);
		fateBox.SetHalign(Align.Center);
		fateBox.SetValign(Align.End);
		fateBox.Append(saveButton);
		fateBox.Append(removeButton);

		Box spacer = Box.New(Orientation.Horizontal, 0);
		spacer.SetVexpand(true);
		
		Box box = Box.New(Orientation.Vertical, 10);

		Label optionsLabel = Label.New("");
		optionsLabel.SetMarkup("<u>Options</u>");
		optionsLabel.SetHalign(Align.Start);
		Label paradigmLabel = Label.New("");
		paradigmLabel.SetMarkup("<u>Patching Paradigm</u>");
		paradigmLabel.SetHalign(Align.Start);
		Label fileLabel = Label.New("");
		fileLabel.SetMarkup("<u>File Management</u>");
		fileLabel.SetHalign(Align.Start);
		
		box.SetMargin(10);
		box.Append(optionsLabel);
		box.Append(nameBox);
		box.Append(launchMethodBox);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(paradigmLabel);
		box.Append(paradigmBox);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(fileLabel);
		box.Append(openGameFolderButton);
		box.Append(cleanToInput);
		box.Append(inputToClean);
		box.Append(restoreCleanBackup);
		box.Append(spacer);
		box.Append(fateBox);
		
		
		SetChild(box);
	}
	
	public void Dialog() {
		SetTransientFor(mainWindow);
		SetModal(true);
		Present();
	}


	private void DoFileOperation(Func<(bool, string?)> action) {
		Thread thread = new Thread(() => {
			(bool success, string? message) = action();
			Program.RunOnMainThreadEventually(() => {
				PopupWindow window;
				if (success) {
					window = new PopupWindow(this,
						"Success", message ?? "Operation completed successfully", "Thanks");
				}
				else {
					window = new PopupWindow(this,
						"Error!", message ?? "An error occurred trying to do this operation", "Damn");
				}
				window.Dialog();
				SetSensitive(true);
			});
		});
		//TODO: make sure this prevents you from closing the window
		SetSensitive(false);
		thread.Start();
		
	}
}

