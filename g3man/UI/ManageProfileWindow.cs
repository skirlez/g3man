using g3man.Models;
using Gtk;

namespace g3man.UI;

public class ManageProfileWindow : Window {
	private MainWindow owner;
	private Profile profile;
	
	public ManageProfileWindow(MainWindow owner, Profile profile, int? index) {
		SetSizeRequest(400, 300);
		SetResizable(false);
		this.owner = owner;
		this.profile = profile;
		Box box = Box.New(Orientation.Vertical, 10);
		box.SetMargin(10);
			
		Label nameLabel = Label.New("Name");
		nameLabel.SetHalign(Align.Start);
		Entry nameEntry = Entry.New();
		nameEntry.SetMaxWidthChars(30);
		nameEntry.SetText(profile.Name);

		Box nameBox = Box.New(Orientation.Vertical, 5);
		nameBox.SetHalign(Align.Start);
		box.Append(nameLabel);
		box.Append(nameEntry);
		
		CheckButton moddedSaveCheck = CheckButton.New();
		moddedSaveCheck.SetLabel("Separate modded save");
		moddedSaveCheck.SetActive(profile.SeparateModdedSave);

		Label saveNameLabel = Label.New("Modded save name");
		saveNameLabel.SetHalign(Align.Start);
		Entry saveNameEntry = Entry.New();
		saveNameEntry.SetMaxWidthChars(30);
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

		bool isSelected = Program.GetProfile()! == profile;
		
		Button doneButton = Button.New();
		doneButton.SetLabel(index is null ? "Create" : "Save");
		
		Box fateBox = Box.New(Orientation.Horizontal, 5);
		fateBox.SetHalign(Align.Center);
		fateBox.Append(doneButton);
		
		if (index is not null) {
			Button deleteButton = Button.NewWithLabel("Delete");
			deleteButton.OnClicked += (_, _) => {
				bool success = profile.Delete(Program.GetGame()!.Directory);
				if (!success) {
					// TODO
					return;
				}
				this.owner.UpdateProfilesList(null, index.Value, isSelected);
				Close();
			};
			fateBox.Append(deleteButton);
		}
		
		doneButton.OnClicked += (_, _) => {
			profile.Name = nameEntry.GetText();
			if (index is null)
				profile.FolderName = nameEntry.GetText().ToLowerInvariant();
			profile.SeparateModdedSave = moddedSaveCheck.GetActive();
			profile.ModdedSaveName = saveNameEntry.GetText();
			bool success = profile.Write(Program.GetGame()!.Directory);
			if (!success) {
				// TODO
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
		box.Append(fateBox);
		
		
		
		SetChild(box);
	}

	public void Dialog() {
		SetTransientFor(owner);
		SetModal(true);
		Present();
	}
}