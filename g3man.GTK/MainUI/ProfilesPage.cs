using g3man.GTK;
using g3man.Core.Models;
using g3man.Core.Util;
using g3man.GTK.Util;
using Gtk;
using Pango;

namespace g3man.GTK.MainUI;

public partial class MainWindow {
	private List<Profile> profiles;
	private void SetupProfilesPage(Box box) {
		profilesListBox = ListBox.New();
		profilesListBox.SetSelectionMode(SelectionMode.None);
		selectProfileButtons = [];
		
		Button openProfilesFolder = Button.NewWithLabel("Open profiles folder");
		openProfilesFolder.OnClicked += (_, _) => {
			TryUtil.TryOpeningFileExplorer(this, Path.Combine(UI.GetGame()!.Directory, "g3man", "profiles"));
		};
		
		Button addNewProfile = Button.NewWithLabel("Add new profile");
		addNewProfile.OnClicked += (_, _) => {
			ManageProfileWindow window = new(null, (newProfile, _) => {
				AddToProfilesList(newProfile, false);
			});
			window.Dialog(this);
		};
		
		Button refreshProfiles = Button.NewWithLabel("Refresh");
		refreshProfiles.OnClicked += (_, _) => {
			ParseProfilesAndUpdateMenu();
		};
		
		Button importFromZipButton = Button.NewWithLabel("Import from ZIP");
		importFromZipButton.OnClicked += (_, _) => {
			FileFilter zipFilter = FileFilter.New();
			zipFilter.SetName("ZIP archives");
			zipFilter.AddMimeType("application/zip");
			FileDialogWindow window = new FileDialogWindow("Select a profile ZIP file", [zipFilter], (file) => {
				UnzipperWindow window = new UnzipperWindow(UnzipperWindow.ZipType.Profile);
				window.Dialog(this, file, ParseProfilesAndUpdateMenu);
				
			});
			window.Dialog(this);
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
		profiles = Profile.ParseAll(Path.Combine(UI.GetGame()!.Directory, "g3man", "profiles"), (e, path) => {
			UI.Logger.Error($"Profile at {path} failed to parse:\n{e.Message}");
		});
		profiles = profiles.OrderBy(profile => profile.Name).ToList();
		if (profiles.Count == 0) {
			EnableExtraCategories(ExtraCategories.Profiles);
			return;
		}
		Profile? profile = profiles.FirstOrDefault(p => p!.ID == UI.GetGame()!.Entry.ProfileFolderName, null);
		if (profile is null) {
			PopulateProfilesList();
			// let user choose profile if for some reason we couldn't use the normal one
			EnableExtraCategories(ExtraCategories.Profiles);
			return;
		}
		UI.SetProfile(profile);
		currentProfileLabel.SetText(profile.Name);
		
		PopulateProfilesList(profile);
		ParseModsAndUpdateMenu();
		EnableExtraCategories(ExtraCategories.ProfilesAndMods);
	}

	
	private void PopulateProfilesList(Profile? selectedId = null) {
		profilesListBox.RemoveAll();
		foreach (Profile profile in profiles) {
			AddToProfilesList(profile, profile == selectedId);
		}
	}

	private void AddToProfilesList(Profile profile, bool selected) {
		int newIndex = 0;
		while (profilesListBox.GetRowAtIndex(newIndex) is not null)
			newIndex++;
		profilesListBox.Append(createProfileWidgets(profile, selected, newIndex));
	}

	private void UpdateProfilesList(Profile? profile, int index, bool selected) {
		ListBoxRow old = profilesListBox.GetRowAtIndex(index)!;
		profilesListBox.Remove(old);
		if (profile is not null) {
			profilesListBox.Insert(createProfileWidgets(profile, selected, index), index);
		}
	}
	
	private ListBoxRow createProfileWidgets(Profile profile, bool selected, int index) {
		Label profileName = Label.New(profile.Name);
		profileName.SetEllipsize(EllipsizeMode.End);
		Box spacer = Box.New(Orientation.Horizontal, 0);
		spacer.SetHexpand(true);
			
		Button manageProfileButton = Button.NewWithLabel("Manage");
		manageProfileButton.OnClicked += (_, _) => {
			ManageProfileWindow window = new ManageProfileWindow(profile, (newProfile, createdNew) => {
				if (createdNew) {
					AddToProfilesList(newProfile, false);
					return;
				}
				bool prevSelected = UI.GetProfile() == profile;
				UpdateProfilesList(newProfile, index, prevSelected);
				if (prevSelected) {
					SelectProfile(newProfile);
				}
			}, () => {
				bool prevSelected = UI.GetProfile() == profile;
				UpdateProfilesList(null, index, prevSelected);
				if (prevSelected) {
					EnableExtraCategories(ExtraCategories.Profiles);
					currentProfileLabel.SetText("No profile selected");
				}
			});
			
			window.Dialog(this);
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
	
	
	private void SelectProfile(Profile profile, Button? buttonPressed = null) {
		UI.SetProfile(profile);
		if (currentExtraCategories == ExtraCategories.Profiles) 
			EnableExtraCategories(ExtraCategories.ProfilesAndMods);
		if (buttonPressed is not null) {
			foreach (Button button in selectProfileButtons) {
				button.SetSensitive(true);
			}

			buttonPressed.SetSensitive(false);
		}

		currentProfileLabel.SetText(profile.Name);
		ParseModsAndUpdateMenu();
		
		UI.GetGame()!.Entry.ProfileFolderName = profile.ID;
		UI.TryWriteConfig();
	}
	
}