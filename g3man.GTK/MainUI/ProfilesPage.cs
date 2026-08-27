using System.Diagnostics;
using g3man.GTK;
using g3man.Core.Models;
using g3man.Core.Util;
using g3man.GTK.Util;
using Gtk;
using Pango;

namespace g3man.GTK.MainUI;

public partial class MainWindow {
	
	private UIList<Profile> profilesList;
	private Label noProfilesLabel;
	
	private void SetupProfilesPage(Box box) {
		ListBox profilesListBox = ListBox.New();
		profilesListBox.SetSelectionMode(SelectionMode.None);
		noProfilesLabel = Label.New("No profiles found.");
		noProfilesLabel.SetMargin(30);
		profilesList = new UIList<Profile>([], profilesListBox, MakeProfileRow, noProfilesLabel);
		
		Button openProfilesFolder = Button.NewWithLabel("Open profiles folder");
		openProfilesFolder.OnClicked += UI.OpenWindowButton(async (_, _) => {
			await TryUtil.TryOpeningFileExplorer(this, Path.Combine(UI.GetGame()!.Directory, "g3man", "profiles"));
		});
		
		Button addNewProfile = Button.NewWithLabel("Add new profile");
		addNewProfile.OnClicked += UI.DoOperation<Button>([UI.Operation.TouchingProfiles, UI.Operation.OpenWindow], (_, _) => {
			ManageProfileWindow window = new(null, (newProfile) => {
				profilesList.Add(newProfile);
				return Task.CompletedTask;
			});
			window.Dialog(this);
		});
		
		Button refreshProfiles = Button.NewWithLabel("Refresh");
		refreshProfiles.OnClicked += UI.DoOperation<Button>([UI.Operation.TouchingProfiles], async (_, _) => {
			await ParseProfilesAndUpdateMenu();
		});
		
		Button importFromZipButton = Button.NewWithLabel("Import from ZIP");

		importFromZipButton.OnClicked += UI.DoOperation<Button>([UI.Operation.TouchingProfiles, UI.Operation.OpenWindow], async (_, _) => {
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
		List<Profile> profiles = await Task.Run(() => {
			return Profile.ParseAll(Path.Combine(UI.GetGame()!.Directory, "g3man", "profiles"), 
				(e, path) => UI.Logger.Error($"Profile at {path} failed to parse:\n{e.Message}"))
				.OrderBy(profile => profile.Name).ToList();
		});
		
		profilesList.Clear();
		foreach (Profile profile in profiles)
			profilesList.Add(profile);
		Profile? selected = profiles.FirstOrDefault(p => p!.ID == UI.GetGame()!.Entry.ProfileFolderName, null);
		await SelectProfile(selected);
	}
	
	
	private (ListBoxRow, Button) MakeProfileRow(Profile profile) {
		Label profileName = Label.New(profile.Name);
		profileName.SetEllipsize(EllipsizeMode.End);
		Box spacer = Box.New(Orientation.Horizontal, 0);
		spacer.SetHexpand(true);
			
		Button manageProfileButton = Button.NewWithLabel("Manage");
		manageProfileButton.OnClicked += UI.DoOperation<Button>([UI.Operation.TouchingProfiles, UI.Operation.OpenWindow], (_, _) => {
			ManageProfileWindow window = new(profile,
				saveAsNewCallback: newProfile => {
					profilesList.Add(newProfile);
					return Task.CompletedTask;
				},
				changeCallback: async newProfile => {
					int index = profilesList.IndexOf(profile);
					profilesList.SetOrRemoveAt(newProfile, index);
					bool prevSelected = UI.GetProfile() == profile;
					if (prevSelected)
						await SelectProfile(newProfile);
				});
			window.Dialog(this);
		});
		Button selectButton = Button.NewWithLabel("Select");
		selectButton.OnClicked += UI.DoOperation<Button>([UI.Operation.TouchingProfiles], async (_, _) => {
			await SelectProfile(profile);
		}, makeInsensitive: false);
			
		Box box = Box.New(Orientation.Horizontal, 10);
		box.Append(profileName);
		box.Append(spacer);
		box.Append(manageProfileButton);
		box.Append(selectButton);
			
		ListBoxRow row = ListBoxRow.New();
		
		row.SetChild(box);
		row.SetActivatable(false);
		row.SetMargin(10);
		return (row, selectButton);
	}
	


	private async Task SelectProfile(Profile? profile) {
		bool shouldSave = UI.SetProfile(profile);
		profilesList.UpdateButtonStates(profile);
		if (profile is null) {
			EnableExtraCategories(ExtraCategories.Profiles);
			return;
		}
		await ParseModsAndUpdateMenu();
		EnableExtraCategories(ExtraCategories.ProfilesAndMods);
		if (shouldSave)
			await UI.TryWriteConfig();
	}
	
}