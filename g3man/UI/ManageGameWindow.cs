using g3man.Models;
using g3man.UI.Main;
using Gtk;

namespace g3man.UI;

public class ManageGameWindow : G3manWindow {
	private MainWindow mainWindow;
	
	public ManageGameWindow(Game game, MainWindow mainWindow) {
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
		launchMethod.AppendText("Launch Directly");
		launchMethod.AppendText("Launch Through Steam");
		launchMethod.SetActive(game.ExecutableType);
		launchMethod.SetHalign(Align.Start);
		launchMethod.SetTooltipText("How g3man should launch the game.\nLaunch directly - g3man will run the file in the field below.\nLaunch through Steam - g3man will tell steam which game to run");
		
		Stack launchMethodStack = Stack.New();
		
		Label fileExecutableLabel = Label.New("Executable File");
		fileExecutableLabel.SetHalign(Align.Start);
		
		Entry fileExeEntry = Entry.New();
		fileExeEntry.SetText(game.ExecutablePath);
		fileExeEntry.SetHexpand(true);
		
		Button fileExeBrowse = Button.NewWithLabel("Browse");
		fileExeBrowse.OnClicked += (_, _) => {
			FileFilter allFilter = FileFilter.New();
			allFilter.SetName("Executable");
			allFilter.AddPattern("*");

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
		void OnUpdateExecutableType(int type) {
			launchMethodStack.SetVisibleChild(methodBoxes[type]);
		}
		OnUpdateExecutableType(game.ExecutableType);
		launchMethod.OnChanged += (sender, _) => OnUpdateExecutableType(sender.GetActive());
		
		Label datafileLabel = Label.New("Datafile Path");
		datafileLabel.SetHalign(Align.Start);
		
		Entry datafileEntry = Entry.New();
		datafileEntry.SetText(game.DatafileName);
		
		Label outputDatafileLabel =  Label.New("Output Datafile Path");
		outputDatafileLabel.SetHalign(Align.Start);
		
		Entry outputDatafileEntry = Entry.New();
		outputDatafileEntry.SetText(game.OutputDatafileName);
		
		Box box = Box.New(Orientation.Vertical, 10);
		box.SetMargin(10);
		box.Append(nameBox);
		box.Append(launchMethod);
		box.Append(launchMethodStack);
		box.Append(datafileLabel);
		box.Append(datafileEntry);
		box.Append(outputDatafileLabel);
		box.Append(outputDatafileEntry);
		
		SetChild(box);
	}
	
	public void Dialog() {
		SetTransientFor(mainWindow);
		SetModal(true);
		Present();
	}
	
}
