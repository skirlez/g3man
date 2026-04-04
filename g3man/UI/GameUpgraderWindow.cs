using g3man.Models;
using g3man.UI.Main;
using g3man.Util;
using Gtk;

namespace g3man.UI;

public class GameUpgraderWindow : G3manWindow  {
	
	public GameUpgraderWindow(MainWindow mainWindow, Game game) {
		SetSizeRequest(500, 300);
		Box box = Box.New(Orientation.Vertical, 5);
		Label text = Label.New("Your g3man setup for this game must be converted to a new format.\nOlder g3man versions will not be able to parse this game correctly.");
		

		Button proceed = Button.NewWithLabel("Proceed");
		proceed.OnClicked += (_, _) => {
			try {
				string g3manFolder = Path.Combine(game.Directory, "g3man");
				string newProfilesDirectory = Path.Combine(g3manFolder, "profiles");
				if (Directory.Exists(newProfilesDirectory))
					Directory.Delete(newProfilesDirectory, true);
				Directory.CreateDirectory(newProfilesDirectory);
				List<string> directories = Directory.GetDirectories(g3manFolder).ToList();
				
				foreach (string profileDirectory in directories) {
					string profileFolderName = Path.GetFileName(profileDirectory);
					if (profileFolderName == "profiles") {
						continue;
					}
					
					if (!Path.Exists(Path.Combine(profileDirectory, "profile.json")))
						continue;
					IO.CopyDirectory(profileDirectory, Path.Combine(newProfilesDirectory, profileFolderName), true);
				}

				foreach (string profileDirectory in directories) {
					if (Path.GetDirectoryName(profileDirectory) == "profiles")
						continue;
					if (!Path.Exists(Path.Combine(profileDirectory, "profile.json")))
						continue;
					Directory.Delete(profileDirectory, true);
				}

				game.FormatVersion = 2;
				game.Write();
				Close();
			}
			catch (Exception e) {
				game.FormatVersion = 1;
				Program.Logger.Error($"Error while converting game {game.DisplayName}: {e}");
				PopupWindow window = new PopupWindow(this, "Error!",
					"An error occured while converting this game.\nPlease report this as a bug, and revert to an earlier g3man version for now.",
					"Close");
				window.Dialog();
			}
		};
		Button cancel = Button.NewWithLabel("Cancel");

		cancel.OnClicked += (_, _) => Close();
		Box decision = Box.New(Orientation.Horizontal, 5);
		decision.Append(proceed);
		decision.Append(cancel);
		decision.SetHalign(Align.Center);
		
		
		box.Append(text);
		box.Append(decision);
		box.SetValign(Align.Center);
		box.SetHalign(Align.Center);

		

		SetChild(box);
	}
	public void Dialog(Window window) {
		SetTransientFor(window);
		SetModal(true);
		Present();
	}
}