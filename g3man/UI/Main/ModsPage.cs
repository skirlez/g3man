using System.Diagnostics;
using g3man.Models;
using g3man.Util;
using Gtk;

namespace g3man.UI.Main;

public partial class MainWindow {
	private Label modNameLabel;
	private Label modDescriptionLabel;

	private void ResetModInfo() {
		modNameLabel.SetText("Mod info");
		modDescriptionLabel.SetText("Click on a mod to view information about it!");
	}
	private void SetupModsPage(Box page) {
		noModsLabel = Label.New("No mods found.");
		noModsLabel.SetMargin(30);
		
		modNameLabel = Label.New("");
		modNameLabel.SetMarginTop(10);
		modDescriptionLabel = Label.New("");
		modDescriptionLabel.SetWrap(true);
		modDescriptionLabel.SetWrapMode(Pango.WrapMode.WordChar);
		
		

		ScrolledWindow modInfoWindow = ScrolledWindow.New();
		modInfoWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
		modInfoWindow.SetMargin(10);
		modInfoWindow.SetChild(modDescriptionLabel);

		modsListBox = ListBox.New();
		modsListBox.SetHexpand(true);
		modsListBox.SetPlaceholder(noModsLabel);
		modsListBox.OnRowSelected += (sender, args) => {
			if (args.Row is null) {
				return;
			}

			int index = args.Row.GetIndex();
			IMod mod = modsList[index];

			if (mod.MaybeVersion is null)
				modNameLabel.SetText($"{mod.DisplayName}");
			else
				modNameLabel.SetText($"{mod.DisplayName} ({mod.MaybeVersion})");

			string credits;
			if (mod.Credits.Length == 0)
				credits = "";
			else {
				credits = $"By {mod.Credits[0].Name}";
				for (int i = 1; i < mod.Credits.Length; i++)
					credits += $", {mod.Credits[i].Name}";
			}

			modDescriptionLabel.SetText(mod.Description + "\n" + credits);
		};

		modsListWindow = ScrolledWindow.New();
		modsListWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
		modsListWindow.SetChild(modsListBox);
		modsListWindow.SetPropagateNaturalHeight(true);

		Box manageModsBox = Box.New(Orientation.Horizontal, 5);
		manageModsBox.SetHalign(Align.Center);
		manageModsBox.SetValign(Align.Center);


		Button openModsFolderButton = Button.NewWithLabel("Open mods folder");
		openModsFolderButton.OnClicked += (_, _) => { IO.OpenFileExplorer(Program.CurrentProfileFolderPath()); };

		Button refreshButton = Button.NewWithLabel("Refresh");
		refreshButton.OnClicked += (_, _) => {
			Program.GetProfile()!.UpdateModsStatus(modsList, enabledMods);
			try {
				Program.GetProfile()!.Write(Program.GetGame()!);
			}
			catch (Exception e) {
				Program.Logger.Error(e);
			}

			ParseModsAndUpdateMenu();
		};

		Button moveModsUp = Button.New();
		moveModsUp.Label = "↑";
		Button moveModsDown = Button.New();
		moveModsDown.Label = "↓";

		moveModsUp.OnClicked += reorderMods;
		moveModsDown.OnClicked += reorderMods;

		void reorderMods(Button sender, EventArgs _) {
			int direction = (sender == moveModsUp ? -1 : 1);
			ListBoxRow? selected = modsListBox.GetSelectedRow();
			if (selected is null)
				return;
			ListBoxRow? next = modsListBox.GetRowAtIndex(selected.GetIndex() + direction);
			if (next is null)
				return;
			int index = selected.GetIndex();

			IMod mod = modsList[index];
			if (direction == 1 && mod is XdeltaMod && modsList[index + direction] is not XdeltaMod ||
				direction == -1 && mod is not XdeltaMod && modsList[index + direction] is XdeltaMod) {
				PopupWindow popup = new PopupWindow(this, "Stop!",
					"An xdelta mod cannot have lower priority than non-xdelta mods.", "Ohhh");
				popup.Dialog();
				return;
			}

			modsList.RemoveAt(index);
			modsList.Insert(index + direction, mod);

			modsListBox.UnselectAll();
			modsListBox.Remove(selected);
			modsListBox.Insert(selected, index + direction);
			modsListBox.SelectRow(selected);
		}

		Button importFromZipButton = Button.NewWithLabel("Import");
		importFromZipButton.OnClicked += (_, _) => {
			FileFilter zipFilter = FileFilter.New();
			zipFilter.SetName("ZIP archives");
			zipFilter.AddMimeType("application/zip");

			FileFilter xdeltaFilter = FileFilter.New();
			xdeltaFilter.SetName("Xdelta patches");
			xdeltaFilter.AddPattern("*.xdelta");
			FileDialogWindow window = new FileDialogWindow("Select a mod's file", [zipFilter, xdeltaFilter], (file) => {
				string? path = file.GetPath();
				if (path is null)
					return;
				if (Path.GetExtension(path) == ".xdelta") {
					// TODO: this is done on the main thread
					File.Copy(path, Path.Combine(Program.CurrentProfileFolderPath(), Path.GetFileName(path)), true);
					ParseModsAndUpdateMenu();
				}
				else {
					UnzipperWindow window = new UnzipperWindow(UnzipperWindow.ZipType.Mod);
					window.Dialog(this, file, ParseModsAndUpdateMenu);
				}
			});
			window.Dialog(this);
		};

		Button deleteModButton = Button.NewWithLabel("Delete selected");
		deleteModButton.OnClicked += (_, _) => {
			ListBoxRow? selected = modsListBox.GetSelectedRow();
			if (selected is null)
				return;
			int index = selected.GetIndex();
			IMod mod = modsList[index];
			string profileFolder = Program.CurrentProfileFolderPath();
			try {
				mod.Delete(profileFolder);
			}
			catch (Exception e) {
				Program.Logger.Error(e);
				PopupWindow popup = new PopupWindow(this, "Error!",
					"Failed to delete this mod. Please report this as a bug!", "Damn");
				popup.Dialog();
				return;
			}

			ListBoxRow? next = modsListBox.GetRowAtIndex(index + 1);
			if (next is not null)
				modsListBox.SelectRow(next);
			else
				modsListBox.UnselectAll();
			modsListBox.Remove(selected);
			modsList.RemoveAt(index);
		};

		manageModsBox.Append(openModsFolderButton);
		manageModsBox.Append(refreshButton);
		manageModsBox.Append(moveModsUp);
		manageModsBox.Append(moveModsDown);
		manageModsBox.Append(importFromZipButton);
		manageModsBox.Append(deleteModButton);
		manageModsBox.SetMargin(10);
		

		page.Append(modsListWindow);
		page.Append(manageModsBox);
		page.Append(Separator.New(Orientation.Horizontal));
		page.Append(modNameLabel);
		page.Append(modInfoWindow);
	}


	private void ParseModsAndUpdateMenu() {
		Game? game = Program.GetGame();
		Profile? profile = Program.GetProfile();

		Debug.Assert(game is not null);
		Debug.Assert(profile is not null);

		modsList = new List<IMod>();
		modsList.AddRange(Mod.ParseAll(game.GetProfileFolderPath(profile)));
		List<XdeltaMod> xdeltaMods = XdeltaMod.ParseAll(game.GetProfileFolderPath(profile));
		modsList.AddRange(xdeltaMods);

		modsListBox.RemoveAll();
		modsListBox.SetPlaceholder(noModsLabel);

		List<string> modOrder = profile.ModOrder.ToList();
		List<string> missingXdeltas = xdeltaMods.Select(m => m.ModId).Where(id => !modOrder.Contains(id)).ToList();
		modOrder.InsertRange(0, missingXdeltas);
		modsList.Sort((mod1, mod2) => int.Sign(modOrder.IndexOf(mod1.ModId) - modOrder.IndexOf(mod2.ModId)));

		enabledMods = new Dictionary<IMod, bool>();
		List<string> disabledIds = profile.ModsDisabled.ToList();

		foreach (IMod mod in modsList) {
			ListBoxRow row = ListBoxRow.New();
			CheckButton modEnabled = CheckButton.New();

			if (!disabledIds.Contains(mod.ModId)) {
				modEnabled.SetActive(true);
				enabledMods.Add(mod, true);
			}
			else {
				enabledMods.Add(mod, false);
			}

			modEnabled.OnToggled += (sender, _) => {
				enabledMods.Remove(mod);
				enabledMods.Add(mod, sender.Active);
			};

			Label modName = Label.New(mod.DisplayName);
			Box modBox = Box.New(Orientation.Horizontal, 5);
			modBox.Append(modEnabled);
			modBox.Append(modName);
			modBox.SetMargin(10);
			row.SetChild(modBox);
			modsListBox.Append(row);
		}

		ResetModInfo();
	}
}