using System.Diagnostics;
using g3man.GTK;
using g3man.Core.Models;
using g3man.Core.Util;
using g3man.GTK.Util;
using Gtk;
using Pango;

namespace g3man.GTK.MainUI;

public partial class MainWindow {
	private List<Profile> profiles;
	
	private Label noProfilesLabel;
	private const string NO_PROFILE_SELECTED = "No profile selected";
	
	private void SetupProfilesPage(Box box) {
		profilesListBox = ListBox.New();
		profilesListBox.SetSelectionMode(SelectionMode.None);
		noProfilesLabel = Label.New("No profiles found.");
		noProfilesLabel.SetMargin(30);
		
		selectProfileButtons = [];
		
		Button openProfilesFolder = Button.NewWithLabel("Open profiles folder");
		openProfilesFolder.OnClicked += UI.LockedOrCancel<Button>(async (_, _) => {
			await TryUtil.TryOpeningFileExplorer(this, Path.Combine(UI.GetGame()!.Directory, "g3man", "profiles"));
		});
		
		Button addNewProfile = Button.NewWithLabel("Add new profile");
		addNewProfile.OnClicked += (_, _) => {
			ManageProfileWindow window = new(null, (newProfile, _) => {
				AddToProfilesList(newProfile, false);
			});
			window.Dialog(this);
		};
		
		Button refreshProfiles = Button.NewWithLabel("Refresh");
		refreshProfiles.OnClicked += UI.LockedOrQueue<Button>(async (_, _) => {
			profilesListBox.SetSensitive(false);
			await ParseProfilesAndUpdateMenu();
			profilesListBox.SetSensitive(true);
		});
		
		Button importFromZipButton = Button.NewWithLabel("Import from ZIP");

		importFromZipButton.OnClicked += UI.LockedOrCancel<Button>(async (_, _) => {
			FileFilter zipFilter = FileFilter.New();
			zipFilter.SetName("ZIP archives");
			zipFilter.AddMimeType("application/zip");
			Gio.File? file = await FileDialogWindow.Dialog(this, "Select a profile ZIP file", [zipFilter]);
			if (file is null)
				return;
			UnzipperWindow window = new(UnzipperWindow.ZipType.Profile);
			window.Dialog(this, file, async void () => {
				await ParseProfilesAndUpdateMenu();
			});
		});
		
		
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
	
	
	private async Task ParseProfilesAndUpdateMenu() {
		profiles = await Task.Run(() => Profile.ParseAll(Path.Combine(UI.GetGame()!.Directory, "g3man", "profiles"), (e, path) => {
			UI.Logger.Error($"Profile at {path} failed to parse:\n{e.Message}");
		}));
		profiles = profiles.OrderBy(profile => profile.Name).ToList();
		
		// let user choose profile if we have no profiles or we couldn't use the normal one
		Profile? profile = profiles.FirstOrDefault(p => p!.ID == UI.GetGame()!.Entry.ProfileFolderName, null);
		UI.SetProfile(profile);
		if (profile is null) {
			currentProfileLabel.SetText(NO_PROFILE_SELECTED);
			PopulateProfilesList();
			EnableExtraCategories(ExtraCategories.Profiles);
			return;
		}
		currentProfileLabel.SetText(profile.Name);
		PopulateProfilesList(profile);
		await ParseModsAndUpdateMenu();
		EnableExtraCategories(ExtraCategories.ProfilesAndMods);
	}

	
	private void PopulateProfilesList(Profile? selectedId = null) {
		profilesListBox.RemoveAll();
		profilesListBox.SetPlaceholder(noProfilesLabel);
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
		manageProfileButton.OnClicked += UI.LockedOrQueue<Button>((_, _) => {
			if (TransientFor is not null)
				return;
			ManageProfileWindow window = new(profile, async void (newProfile, createdNew) => {
				if (createdNew) {
					AddToProfilesList(newProfile, false);
					return;
				}
				bool prevSelected = UI.GetProfile() == profile;
				UpdateProfilesList(newProfile, index, prevSelected);
				if (prevSelected) {
					await SelectProfile(newProfile);
				}
			}, () => {
				bool prevSelected = UI.GetProfile() == profile;
				UpdateProfilesList(null, index, prevSelected);
				if (prevSelected) {
					currentProfileLabel.SetText(NO_PROFILE_SELECTED);
					EnableExtraCategories(ExtraCategories.Profiles);
				}
			});
			window.Dialog(this);
		});
		Button selectButton = Button.NewWithLabel("Select");
		if (selected)
			selectButton.SetSensitive(false);
		selectProfileButtons.Add(selectButton);
		selectButton.OnClicked += UI.LockedOrCancel<Button>(async (button, _) => {
			await SelectProfile(profile, button);
		});
			
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
	
	
	private async Task SelectProfile(Profile profile, Button? buttonPressed = null) {
		UI.SetProfile(profile);
		foreach (Button selectProfileButton in selectProfileButtons)
			selectProfileButton.SetSensitive(true);
		buttonPressed?.SetSensitive(false);
		currentProfileLabel.SetText(profile.Name);
		await ParseModsAndUpdateMenu();
		if (currentExtraCategories == ExtraCategories.Profiles) 
			EnableExtraCategories(ExtraCategories.ProfilesAndMods);
		await UI.TryWriteConfig();
	}
	
}