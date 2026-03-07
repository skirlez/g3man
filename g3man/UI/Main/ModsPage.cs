using System.Diagnostics;
using g3man.Models;
using g3man.Util;
using Gdk;
using Gtk;

namespace g3man.UI.Main;

public partial class MainWindow {
	private void SetupModsPage(Box page) {
		noModsLabel = Label.New("No mods found.");
		noModsLabel.SetMargin(30);
		
		Label modNameLabel = Label.New("");
		modNameLabel.SetMarginTop(10);
		Label modDescriptionLabel = Label.New("");
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
			Mod mod = modsList[index];
			
			modNameLabel.SetText($"{mod.DisplayName} ({mod.Version})");
			
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
		
		
		Button openModsFolderButton = Button.New();
		openModsFolderButton.Label = "Open mods folder";
		openModsFolderButton.OnClicked += (_, _) => {
			IO.OpenFileExplorer(Path.Combine(Program.GetGame()!.Directory, "g3man", Program.GetProfile()!.ID));
		};
		
		Button refreshButton = Button.NewWithLabel("Refresh");
		refreshButton.OnClicked += (_, _) => {
			Program.GetProfile()!.UpdateModsStatus(modsList, enabledMods);
			Program.GetProfile()!.Write(Program.GetGame()!.Directory);
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
			modsListBox.UnselectAll();
			modsListBox.Remove(selected);
			modsListBox.Insert(selected, index + direction);
			modsListBox.SelectRow(selected);

			// we assume the list is identical to the listbox (so this operation will be valid)
			Mod mod = modsList[index];
			modsList.RemoveAt(index);
			modsList.Insert(index + direction, mod);
		}
		
		Button importFromZipButton = Button.NewWithLabel("Import from ZIP");
		importFromZipButton.OnClicked += (_, _) => {
			FileFilter zipFilter = FileFilter.New();
			zipFilter.SetName("ZIP archives");
			zipFilter.AddMimeType("application/zip");
			DoFileDialog("Select a mod ZIP file", [zipFilter], (file) => {
				TryExtractingZip(file, ZipType.Mod);
				ParseModsAndUpdateMenu();
			});
		};
		
		Button deleteModButton = Button.NewWithLabel("Delete selected");
		deleteModButton.OnClicked += (_, _) => {
			ListBoxRow? selected = modsListBox.GetSelectedRow();
			if (selected is null)
				return;
			int index = selected.GetIndex();
			Mod mod = modsList[index];
			string modPath = Path.Combine(Program.GetGame()!.Directory, "g3man", Program.GetProfile()!.ID, mod.FolderName);
			try {
				Directory.Delete(modPath, true);
			}
			catch (Exception e) {
				Program.Logger.Error(e);
				PopupWindow popup = new PopupWindow(this, "Error!", "Failed to delete this mod's folder. Please report this as a bug!", "Damn");
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
		
		
		
		Button applyButton = Button.NewWithLabel("Apply!");
		applyButton.SetHalign(Align.Center);
		applyButton.SetValign(Align.End);
		applyButton.SetVexpand(true);
		applyButton.SetMarginBottom(20);
		applyButton.OnClicked += (_, _) => {
			Program.GetProfile()!.UpdateModsStatus(modsList, enabledMods);
			Program.GetProfile()!.Write(Program.GetGame()!.Directory);
			PatcherWindow window = new PatcherWindow(this);
			List<Mod> enabledModsList = modsList.Where(mod => enabledMods.GetValueOrDefault(mod, false)).ToList();
			window.Dialog(enabledModsList);
		};


		
		page.Append(modsListWindow);
		page.Append(manageModsBox);
		page.Append(applyButton);
		page.Append(Separator.New(Orientation.Horizontal));
		page.Append(modNameLabel);
		page.Append(modInfoWindow);

	}
	
	
	private void ParseModsAndUpdateMenu() {
		Game? game = Program.GetGame();
		Profile? profile = Program.GetProfile();
		
		Debug.Assert(game is not null);
		Debug.Assert(profile is not null);
		
		modsList = Mod.ParseAll(Path.Combine(game.Directory, "g3man", profile.ID));
		
		modsListBox.RemoveAll();
		modsListBox.SetPlaceholder(noModsLabel);
		
		List<string> modOrder = profile.ModOrder.ToList();
		modsList.Sort((mod1, mod2) => int.Sign(modOrder.IndexOf(mod1.ModId) - modOrder.IndexOf(mod2.ModId)));
	
		enabledMods = new Dictionary<Mod, bool>();
		List<string> disabledIds = profile.ModsDisabled.ToList();

		foreach (Mod mod in modsList) {
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
	}
}