using g3man.Models;
using g3man.Util;
using Gdk;
using Gtk;

namespace g3man.UI;

public class ManageProfileWindow : Window {
	private MainWindow owner;
	private Profile profile;
	
	public ManageProfileWindow(MainWindow owner, Profile profile, int? index) {
		SetSizeRequest(400, 300);
		SetTitle("Manage Profile");
		this.owner = owner;
		this.profile = profile;
		Box box = Box.New(Orientation.Vertical, 10);
		box.SetMargin(10);
		
			
		Label nameLabel = Label.New("Name");
		nameLabel.SetHalign(Align.Start);
		Entry nameEntry = Entry.New();
		nameEntry.SetText(profile.Name);
		
		
		Box nameBox = Box.New(Orientation.Vertical, 5);
		nameBox.Append(nameLabel);
		nameBox.Append(nameEntry);
		
		CheckButton moddedSaveCheck = CheckButton.New();
		moddedSaveCheck.SetLabel("Separate modded save");
		moddedSaveCheck.SetActive(profile.SeparateModdedSave);

		Label saveNameLabel = Label.New("Modded save name");
		saveNameLabel.SetHalign(Align.Start);
		Entry saveNameEntry = Entry.New();
		saveNameEntry.SetText(profile.ModdedSaveName);

		Box saveNameBox = Box.New(Orientation.Vertical, 5);
		saveNameBox.Append(saveNameLabel);
		saveNameBox.Append(saveNameEntry);
		moddedSaveCheck.OnToggled += (sender, _) => {
			moddedSaveToggled(sender.GetActive());
		};
		moddedSaveToggled(profile.SeparateModdedSave);
		void moddedSaveToggled(bool value) {
			saveNameBox.SetSensitive(value);
		}
		
		/*
		Label descriptionLabel = Label.New("Description");
		descriptionLabel.SetHalign(Align.Start);
		Entry descriptionEntry = Entry.New();
		descriptionEntry.SetText(profile.Description);
		
		Box descriptionBox = Box.New(Orientation.Vertical, 5);
		descriptionBox.Append(descriptionLabel);
		descriptionBox.Append(descriptionEntry);
		*/
		
		Button editMetadataButton = Button.NewWithLabel("Edit metadata");
		editMetadataButton.SetHalign(Align.Start);
		
		bool isSelected = Program.GetProfile()! == profile;
		
		Button doneButton = Button.New();
		doneButton.SetLabel(index is null ? "Create" : "Save");
		
		
		Box fateBox = Box.New(Orientation.Horizontal, 5);
		fateBox.SetHalign(Align.Center);
		fateBox.SetValign(Align.End);
		fateBox.Append(doneButton);
		fateBox.SetVexpand(true);
		
		if (index is not null) {
			Button deleteButton = Button.NewWithLabel("Delete");
			deleteButton.OnClicked += (_, _) => {
				bool success = profile.Delete(Program.GetGame()!.Directory);
				if (!success) {
					PopupWindow popup = new PopupWindow(this,  "Error!" ,"An error occured trying to delete this profile", "Damn");
					popup.Dialog();
					return;
				}
				this.owner.UpdateProfilesList(null, index.Value, isSelected);
				Close();
			};
			fateBox.Append(deleteButton);
		}
		
		doneButton.OnClicked += (_, _) => {
			if (index is null) {
				string folderName = ToProfileFolderName(nameEntry.GetText());
				if (folderName == "") {
					PopupWindow popup = new PopupWindow(this,  "Cannot save!" ,$"You must give your creation a name.", "Okay I'll Name It");
					popup.Dialog();
					return;
				}
				string?[] folders;
				try {
					folders = Directory.GetDirectories(Path.Combine(Program.GetGame()!.Directory, "g3man")).Select(Path.GetFileName).ToArray();
				}
				catch (Exception e) {
					Console.Error.WriteLine(e);
					PopupWindow popup = new PopupWindow(this,  "Error!" ,"An error occured trying to save this profile", "Damn");
					popup.Dialog();
					return;
				}
				if (folders.Contains(folderName)) {
					PopupWindow popup = new PopupWindow(this,  "Conflict!" ,$"A profile with the name \"{folderName}\" already exists, so you'll need to change the name.", "Okay I'll Rename It");
					popup.Dialog();
					return;
				}
				profile.FolderName = folderName;
			}
			profile.Name = nameEntry.GetText();

			profile.SeparateModdedSave = moddedSaveCheck.GetActive();
			profile.ModdedSaveName = saveNameEntry.GetText();
			bool success = profile.Write(Program.GetGame()!.Directory);
			if (!success) {
				// TODO: this still updates the profile, which is kinda weird.
				PopupWindow popup = new PopupWindow(this,  "Error!" ,"An error occured trying to save this profile", "Damn");
				popup.Dialog();
				return;
			}
			if (index is null)
				this.owner.AddToProfilesList(profile, false);
			else
				this.owner.UpdateProfilesList(profile, index.Value, isSelected);
			
			Close();
		};
		
		box.Append(nameBox);
		box.Append(moddedSaveCheck);
		box.Append(saveNameBox);
		box.Append(editMetadataButton);
		//box.Append(Separator.New(Orientation.Horizontal));
		//box.Append(Label.New("Distribution Metadata"));
		//box.Append(descriptionBox);
		box.Append(fateBox);
		
		
		
		SetChild(box);
	}

	private string ToProfileFolderName(string profileDisplayName) {
		return profileDisplayName.ToLowerInvariant().Replace(' ', '_');
	}
	
	public void Dialog() {
		SetTransientFor(owner);
		SetModal(true);
		Present();
	}
}