using g3man.GTK.MainUI;
using g3man.Core.Models;
using g3man.Core.Util;
using g3man.GTK.Util;
using Gtk;

namespace g3man.GTK;

public class ManageProfileWindow : G3manWindow {
	public ManageProfileWindow(Profile? profile, Func<Profile, Task> saveAsNewCallback, Func<Profile?, Task>? changeCallback = null) {
		SetSizeRequest(400, 300);
		SetTitle(profile is null ? "Create Profile" : "Manage Profile");
		
		Label nameLabel = Label.New("Name");
		nameLabel.SetHalign(Align.Start);
		Entry nameEntry = Entry.New();
		nameEntry.SetText(profile?.Name ?? "");
		
		Box nameBox = Box.New(Orientation.Vertical, 5);
		nameBox.Append(nameLabel);
		nameBox.Append(nameEntry);
		
		Label IDLabel = Label.New("ID");
		IDLabel.SetHalign(Align.Start);

		Entry IDEntry = Entry.New();
		IDEntry.SetText(profile?.ID ?? "");
		IDEntry.SetSensitive(profile is not null && profile.ID != ToProfileFolderName(profile.Name));
		
		nameEntry.OnChanged += (sender, _) => {
			bool enabled = IDEntry.GetSensitive();
			if (!enabled)
				IDEntry.SetText(ToProfileFolderName(sender.GetText()));
		};
		
		Button IDLock = Button.New();
		IDLock.SetTooltipText("If locked, the profile's ID is set automatically.");
		IDLock.SetIconName("changes-prevent");
		IDLock.OnClicked += (_, _) => {
			bool enabled = IDEntry.GetSensitive();
			IDEntry.SetSensitive(!enabled);
			IDLock.SetIconName(enabled ? "changes-prevent" : "changes-allow");
			if (enabled)
				IDEntry.SetText(ToProfileFolderName(nameEntry.GetText()));
		};
		
		Box labelBox = Box.New(Orientation.Horizontal, 10);
		labelBox.Append(IDLabel);
		labelBox.Append(IDLock);
		Box IDBox = Box.New(Orientation.Vertical, 5);
		IDBox.Append(labelBox);
		IDBox.Append(IDEntry);
		
		CheckButton moddedSaveCheck = CheckButton.New();
		moddedSaveCheck.SetLabel("Separate modded save");
		moddedSaveCheck.SetActive(profile?.SeparateModdedSave ?? false);
		
		Label saveNameLabel = Label.New("Modded save name");
		saveNameLabel.SetHalign(Align.Start);
		Entry saveNameEntry = Entry.New();
		saveNameEntry.SetText(profile?.ModdedSaveName ?? "");

		Box saveNameBox = Box.New(Orientation.Vertical, 5);
		saveNameBox.Append(saveNameLabel);
		saveNameBox.Append(saveNameEntry);
		saveNameBox.SetTooltipText("Set the name of the folder that this profile will save to. (inside %LOCALAPPDATA% on Windows, ~/.config on Linux)");
		moddedSaveCheck.OnToggled += (sender, _) => {
			moddedSaveToggled(sender.GetActive());
		};
		moddedSaveCheck.SetTooltipText(
			"This option, if enabled, will allow you to change what save folder the game uses."
			+ " Meaning, when this profile is applied, the game will save into a different folder, and not know about your vanilla save.");
		
		moddedSaveToggled(profile?.SeparateModdedSave ?? false);
		void moddedSaveToggled(bool value) {
			saveNameBox.SetSensitive(value);
		}
		
		
		
		Label launchArgumentsLabel = Label.New("Launch arguments");
		launchArgumentsLabel.SetHalign(Align.Start);
		
		Entry launchArgumentsEntry = Entry.New();
		launchArgumentsEntry.SetEditable(false);
		launchArgumentsEntry.SetHexpand(true);
		launchArgumentsEntry.SetText(GetLaunchArguments());
		Button clipboardButton = Button.New();
		clipboardButton.SetIconName("edit-copy");
		
		IDEntry.OnChanged += (_, _) => {
			launchArgumentsEntry.SetText(GetLaunchArguments());
		};
		string GetLaunchArguments() {
			Game game = UI.GetGame()!;
			if (game.OverwriteGameFiles)
				return "This game has \"Overwrite game files\" enabled, so no launch arguments are necessary";
			return string.Join(" ", game.GetBaseLaunchArguments(IDEntry.GetText()));
		}
		clipboardButton.OnClicked += (_, _) => {
			GetClipboard().SetText(GetLaunchArguments());
		};
		if (UI.GetGame()!.OverwriteGameFiles)
			clipboardButton.SetSensitive(false);
		
		Box outputOverrideBox = Box.New(Orientation.Vertical, 5)
			.With(launchArgumentsLabel,
				Box.New(Orientation.Horizontal, 5).With(
					clipboardButton, launchArgumentsEntry));
		
		/*
		Label descriptionLabel = Label.New("Description");
		descriptionLabel.SetHalign(Align.Start);
		Entry descriptionEntry = Entry.New();
		descriptionEntry.SetText(profile.Description);
		
		Box descriptionBox = Box.New(Orientation.Vertical, 5);
		descriptionBox.Append(descriptionLabel);
		descriptionBox.Append(descriptionEntry);
		*/
		
		//Button editMetadataButton = Button.NewWithLabel("Not Implemented Yet");
		//editMetadataButton.SetHalign(Align.Start);
		
		Button doneButton = Button.New();
		doneButton.SetLabel(profile is null ? "Create" : "Save");
		
		Box fateBox = Box.New(Orientation.Horizontal, 5);
		fateBox.SetHalign(Align.Center);
		fateBox.SetValign(Align.End);
		fateBox.Append(doneButton);
		fateBox.SetVexpand(true);
		
		async Task SaveProfile(bool asNew) {
			// TODO: can't have it be nicer by making it so we stop the insert-text signal so we limit the characters you can type.
			// until it works with Entry (seems to just not at the moment) OR
			// when gir.core supports overriding virtual functions (we could subclass EntryBuffer)
			IDEntry.SetText(ToProfileFolderName(IDEntry.GetText()));
			
			SetSensitive(false);
			void makePopupAndSetSensitive(string title, string message, string buttonText) {
				PopupWindow popup = new(title, message, buttonText);
				popup.Dialog(this);
				SetSensitive(true);
			}
			
			Profile newProfile = new(nameEntry.GetText(), IDEntry.GetText(), 
							moddedSaveCheck.GetActive(), saveNameEntry.GetText(), []);
			if (newProfile.Name == "") {
				makePopupAndSetSensitive("Cannot save!", "You must give your creation a name.", "Okay I'll Name It");
				return;
			}
			if (newProfile.ID == "") {
				makePopupAndSetSensitive("Cannot save!", "You must give your profile an ID.", "Okay I'll ID It");
				return;
			}
			if (newProfile.SeparateModdedSave && newProfile.ModdedSaveName == "") {
				makePopupAndSetSensitive("Issue!",
				"If \"Separate modded save\" is enabled, \"Modded save name\"\n"
						+ $"cannot be blank (as it is the game's new save folder name).",
						"Okay");
				return;
			}
			
			
			bool oldProfileExistsAndIDChanged = profile is not null && newProfile.ID != profile.ID;
			string profilesFolder = Path.Combine(UI.GetGame()!.Directory, "g3man", "profiles");
			try {
				if (oldProfileExistsAndIDChanged || asNew) {
					if (newProfile.ID == "") {
						makePopupAndSetSensitive("Cannot save!", "ID cannot be blank.","Okay I'll ID It");
						return;
					}
					
					string?[] folders = Directory.GetDirectories(profilesFolder).Select(Path.GetFileName).ToArray();
					if (folders.Contains(newProfile.ID)) {
						makePopupAndSetSensitive("Conflict!",
							$"A profile with the ID \"{newProfile.ID}\" already exists, so you'll need to change it.",
							"Okay");
						return;
					}

					await Task.Run(() => {
						Directory.CreateDirectory(Path.Combine(profilesFolder, newProfile.ID));
						IO.CopyDirectory(Path.Combine(profilesFolder, profile!.ID),
							Path.Combine(profilesFolder, newProfile.ID), recursive: true);
					});

				}
				
				await Task.Run(() => newProfile.Write(UI.GetGame()!));
			}
			catch (Exception e) {
				UI.Logger.Error(e);
				try {
					Directory.Delete(Path.Combine(profilesFolder, newProfile.ID), true);
				}
				catch {
					// ignored
				}
				makePopupAndSetSensitive("Error!", "An error occured trying to save this profile.","Damn");
				return;
			}

			if (oldProfileExistsAndIDChanged && !asNew) {
				try {
					await Task.Run(() => profile!.Delete(UI.GetGame()!));
				}
				catch (Exception e) {
					UI.Logger.Error(e);
					makePopupAndSetSensitive("Error!",
						"The profile was saved correctly, however, due to an error, the profile has been duplicated.\n" +
						"When you refresh, the older version of this profile will reappear.",
						"Damn");
					return;
				}
				
			}

			if (asNew)
				await saveAsNewCallback(newProfile);
			else 
				await changeCallback!(newProfile);
			Close();
		}
		
		doneButton.OnClicked += async (_, _) => {
			await SaveProfile(asNew: profile is null);
		};
		
		if (profile is not null) {
			Button deleteButton = Button.NewWithLabel("Delete");
			deleteButton.OnClicked += async (_, _) => {
				SetSensitive(false);
				try {
					profile.Delete(UI.GetGame()!);
				}
				catch (Exception e) {
					UI.Logger.Error(e);
					PopupWindow popup = new("Error!" ,"An error occured trying to delete this profile", "Damn");
					popup.Dialog(this);
					SetSensitive(true);
					return;
				}
				await changeCallback!(null);
				Close();
			};
			Button saveAsNew = Button.NewWithLabel("Save as New");
			saveAsNew.OnClicked += async (_, _) => {
				await SaveProfile(asNew: true);
			};
			fateBox.Append(saveAsNew);
			fateBox.Append(deleteButton);
		}
		

		
		Box box = Box.New(Orientation.Vertical, 12);
		box.SetMargin(10);
		box.Append(nameBox);
		box.Append(IDBox);
		box.Append(moddedSaveCheck);
		box.Append(saveNameBox);
		box.Append(outputOverrideBox);
		//box.Append(editMetadataButton);
		//box.Append(Separator.New(Orientation.Horizontal));
		//box.Append(Label.New("Distribution Metadata"));
		//box.Append(descriptionBox);
		box.Append(fateBox);
		
		
		
		SetChild(box);
	}

	private string ToProfileFolderName(string profileDisplayName) {
		char[] disallowed = Path.GetInvalidFileNameChars().Concat(['.', '/', '\\']).ToArray();
		string build = profileDisplayName.ToLowerInvariant().Replace(' ', '-');
		foreach (char c in disallowed) {
			build = build.Replace(c, '_');	
		}
		return build;
	}
	
	public void Dialog(MainWindow window) {
		SetTransientFor(window);
		SetModal(true);
		Present();
	}
	
}
