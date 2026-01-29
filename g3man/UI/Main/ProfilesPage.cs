using g3man.Models;
using Gtk;

namespace g3man.UI.Main;

public partial class MainWindow {
	private void SetupProfilesPage(Box box) {
		profilesListBox = ListBox.New();
		profilesListBox.SetSelectionMode(SelectionMode.None);
		selectProfileButtons = [];
		
		Button openProfilesFolder = Button.NewWithLabel("Open profiles folder");
		openProfilesFolder.OnClicked += (_, _) => {
			IO.OpenFileExplorer(Path.Combine(Program.GetGame()!.Directory, "g3man"));
		};
		
		Button addNewProfile = Button.NewWithLabel("Add new profile");
		addNewProfile.OnClicked += (sender, args) => {
			Profile profile = new Profile("", "", false, "", []);
			ManageProfileWindow window = new ManageProfileWindow(this, profile, null);
			window.Dialog();
		};
		
		Button refreshProfiles = Button.NewWithLabel("Refresh");
		refreshProfiles.OnClicked += (sender, args) => {
			ParseProfilesAndUpdateMenu();
		};
		
		Button importFromZipButton = Button.NewWithLabel("Import from ZIP");
		importFromZipButton.OnClicked += (_, _) => {
			FileFilter zipFilter = FileFilter.New();
			zipFilter.SetName("ZIP archives");
			zipFilter.AddMimeType("application/zip");
			DoFileDialog("Select a profile ZIP file", [zipFilter], (file) => {
				TryExtractingZip(file, ZipType.Profile);
				ParseProfilesAndUpdateMenu();
			});
		};
		
		
		Box profileManagementBox = Box.New(Orientation.Horizontal, 10);
		profileManagementBox.Append(openProfilesFolder);
		profileManagementBox.Append(refreshProfiles);
		profileManagementBox.Append(addNewProfile);
		profileManagementBox.Append(importFromZipButton);
		profileManagementBox.SetMargin(10);
		profileManagementBox.SetHalign(Align.Center);
		
		box.Append(profilesListBox);
		box.Append(profileManagementBox);
	}
	
	
	private void ParseProfilesAndUpdateMenu() {
		List<Profile> profiles = Profile.ParseAll(Path.Combine(Program.GetGame()!.Directory, "g3man"));
		if (profiles.Count == 0) {
			EnableExtraCategories(ExtraCategories.Profiles);
			return;
		}
		Profile? profile = profiles.FirstOrDefault(p => p!.FolderName == Program.GetGame()!.ProfileFolderName, null);
		if (profile is null) {
			PopulateProfilesList(profiles);
			// let user choose profile if for some reason we couldn't use the normal one
			EnableExtraCategories(ExtraCategories.Profiles);
			return;
		}
		Program.SetProfile(profile);
		currentProfileLabel.SetText(profile.Name);
		
		PopulateProfilesList(profiles, profile);
		ParseModsAndUpdateMenu();
		EnableExtraCategories(ExtraCategories.ProfilesAndMods);
	}

	
	private void PopulateProfilesList(List<Profile> profiles, Profile? selectedId = null) {
		profiles = profiles.OrderBy(profile => profile.Name).ToList();
		profilesListBox.RemoveAll();
		foreach (Profile profile in profiles) {
			AddToProfilesList(profile, profile == selectedId);
		}
	}

	public void AddToProfilesList(Profile profile, bool selected) {
		// TODO: bad
		int newIndex = 0;
		while (profilesListBox.GetRowAtIndex(newIndex) is not null)
			newIndex++;
		profilesListBox.Append(createProfileWidgets(profile, selected, newIndex));
	}

	public void UpdateProfilesList(Profile? profile, int index, bool selected) {
		ListBoxRow old = profilesListBox.GetRowAtIndex(index)!;
		profilesListBox.Remove(old);
		if (profile is not null) {
			profilesListBox.Insert(createProfileWidgets(profile, selected, index), index);
			currentProfileLabel.SetText(profile.Name);
		}
		else if (selected) {
			// if deleted currently selected profile, hide mods tab
			EnableExtraCategories(ExtraCategories.Profiles);
			currentProfileLabel.SetText("No profile selected");
		}
	}
	
	private ListBoxRow createProfileWidgets(Profile profile, bool selected, int index) {
		Label profileName = Label.New(profile.Name);
		Box spacer = Box.New(Orientation.Horizontal, 0);
		spacer.SetHexpand(true);
			
		Button manageProfileButton = Button.NewWithLabel("Manage");
		manageProfileButton.OnClicked += (_, _) => {
			ManageProfileWindow window = new ManageProfileWindow(this, profile, index);
			window.Dialog();
		};
		Button selectButton = Button.NewWithLabel("Select");
		if (selected)
			selectButton.SetSensitive(false);
		selectProfileButtons.Add(selectButton);
		selectButton.OnClicked += (sender, args) => {
			SelectProfile(profile, sender);
		};
			
		Box box = Box.New(Orientation.Horizontal, 10);
		box.Append(profileName);
		box.Append(spacer);
		box.Append(manageProfileButton);
		box.Append(selectButton);
			
		ListBoxRow row = ListBoxRow.New();
		
		row.SetChild(box);
		row.SetActivatable(false);
		row.SetMargin(10);
		return row;
	}
	
	
	private void SelectProfile(Profile profile, Button buttonPressed) {
		Program.SetProfile(profile);
		if (currentExtraCategories == ExtraCategories.Profiles) 
			EnableExtraCategories(ExtraCategories.ProfilesAndMods);
		foreach (Button button in selectProfileButtons) {
			button.SetSensitive(true);
		}
		buttonPressed.SetSensitive(false);
		currentProfileLabel.SetText(profile.Name);
		ParseModsAndUpdateMenu();
		
		Program.GetGame()!.ProfileFolderName = profile.FolderName;
		try {
			Program.GetGame()!.Write();
		}
		catch (Exception e) {
			Program.Logger.Error("Failed to update game.json after selecting profile: " + e);
		}
	}
	
}