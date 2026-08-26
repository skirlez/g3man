using g3man.GTK.MainUI;
using g3man.Core.Models;
using g3man.Core.Util;
using g3man.GTK.Util;
using Gtk;

namespace g3man.GTK;

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
		fileExeBrowse.OnClicked += UI.OpenWindowButton(async (_, _) => {
			Gio.File? file = await FileDialogWindow.Dialog(this, "Choose an executable", [], game.Directory);
			string? path = file?.GetPath();
			if (path is null)
				return;
			string relativePath = Path.GetRelativePath(game.Directory, path);
			if (relativePath.StartsWith("..")) {
				// path should only be relative if it is inside the game's folder
				fileExeEntry.SetText(path);
			}
			else
				fileExeEntry.SetText(relativePath);
		});
		
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
		
		
		CheckButton writeDirectlyCheck = CheckButton.NewWithLabel("Overwrite game files");
		writeDirectlyCheck.SetActive(game.OverwriteGameFiles);
		writeDirectlyCheck.SetTooltipText(
			"If this is set, g3man will overwrite the game's file directly."
			+ " This means you can launch the game without g3man/launch arguments, and still have mods applied.");

		Box paradigmSpacer = Box.New(Orientation.Horizontal, 0);
		paradigmSpacer.SetHexpand(true);

		
		Button openGameFolderButton = Button.NewWithLabel("Open game folder");
		openGameFolderButton.OnClicked += UI.OpenWindowButton(async (_, _) => {
			await TryUtil.TryOpeningFileExplorer(this, game.Directory);
		});
		openGameFolderButton.SetHalign(Align.Start);
		
		Button cleanToInput = Button.NewWithLabel($"(1) Copy {game.GetCleanDatafileRelativePath()} -> {game.GetInputDatafileRelativePath()}");
		cleanToInput.OnClicked += UI.OpenWindowButton(async (_, _) => {
			await DoFileOperation("Failed to restore clean datafile", () => {
				Thread.Sleep(3000);
				IO.Deapply(game);
				return (true, null);
			});
		});
		cleanToInput.SetHalign(Align.Start);
		
		Button inputToClean = Button.NewWithLabel($"(2) Copy {game.GetInputDatafileRelativePath()} -> {game.GetCleanDatafileRelativePath()}");
		inputToClean.OnClicked += UI.OpenWindowButton(async (_, _) => {
			await DoFileOperation("Failed to copy input datafile to clean datafile", () => {
				File.Copy(game.GetCleanDatafilePath(), game.GetBackupDatafilePath(), true);
				File.Copy(game.GetInputDatafilePath(), game.GetCleanDatafilePath(), true);
				UI.DataLoader.ReloadAsync();
				return (true, null);
			});
		});
		inputToClean.SetHalign(Align.Start);
		Button restoreCleanBackup = Button.NewWithLabel($"(3) Restore last backup of clean datafile");
		restoreCleanBackup.OnClicked += UI.OpenWindowButton(async (_, _) => {
			await DoFileOperation("Failed to restore last backup of clean datafile", () => {
				if (!File.Exists(game.GetBackupDatafilePath()))
					return (false, "No clean datafile backup found...");
				File.Copy(game.GetBackupDatafilePath(), game.GetCleanDatafilePath(), true);
				UI.DataLoader.ReloadAsync();
				return (true, null);
			});
		});
		restoreCleanBackup.SetHalign(Align.Start);
		
		
		Button saveButton = Button.NewWithLabel("Save");
		saveButton.OnClicked += UI.CloseWindowButton((_, _) => {
			int newAppId;
			try {
				newAppId = int.Parse(steamAppIdEntry.GetText());
			}
			catch {
				newAppId = -1;
			}
			Game newGame = new(game.Entry, nameEntry.GetText(), game.InternalName, game.DatafilePath, launchMethod.Active, fileExeEntry.GetText(), newAppId, writeDirectlyCheck.Active);
			if (saveCallback(newGame)) {
				Close();
			}
		});
		Button removeButton = Button.NewWithLabel("Remove entry");
		removeButton.OnClicked += UI.CloseWindowButton((_, _) => {
			if (removeCallback()) {
				Close();
			}
		});
		
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
		Label fileLabel = Label.New("");
		fileLabel.SetMarkup("<u>File Management</u>");
		fileLabel.SetHalign(Align.Start);
		
		box.SetMargin(10);
		box.Append(optionsLabel);
		box.Append(nameBox);
		box.Append(launchMethodBox);
		box.Append(writeDirectlyCheck);
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


	private async Task DoFileOperation(string errorMessage, Func<(bool, string?)> action) {
		bool success = false;
		string? message = null;
		try {
			await Task.Run(() => (success, message) = action());
		}
		catch (Exception e) {
			UI.Logger.Error($"{errorMessage}: {e}");
		}
		PopupWindow window;
		if (success) {
			window = new PopupWindow(this,
				"Success", message ?? "Operation completed successfully", "Thanks");
		}
		else {
			window = new PopupWindow(this,
				"Error!", message ?? $"{errorMessage}.", "Damn");
		}
		window.Dialog();
	}
}

