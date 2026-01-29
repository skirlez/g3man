using Gtk;

namespace g3man.UI.Main;

public partial class MainWindow {
	private void SetupSettingsPage(Box page) {
		Button saveSettingsButton = Button.New();
		
		const string saveSettingsLabel = "Save Settings";
		const string saveSettingsDirtyLabel = "Save Settings*";
		saveSettingsButton.SetLabel(saveSettingsLabel);
		void MarkDirty() {
			saveSettingsButton.SetLabel(saveSettingsDirtyLabel);
		}
		ComboBoxText themeDropDown = ComboBoxText.New();

		themeDropDown.AppendText("System Default");
		themeDropDown.AppendText("Light");
		themeDropDown.AppendText("Dark");

		themeDropDown.SetActive((int)Program.Config.ColorScheme);
		themeDropDown.OnChanged += (_, _) =>
		{
			Program.ColorScheme selected = (Program.ColorScheme)themeDropDown.GetActive();
			ApplyColorScheme(selected);
			Program.Config.ColorScheme = selected;
			MarkDirty();
		};

		Label themeLabel = Label.New("Color Scheme");


		Box themeBox = Box.New(Orientation.Horizontal, 10);
		themeBox.Append(themeLabel);
		themeBox.Append(themeDropDown);
		themeBox.SetHalign(Align.Start);


		Label initializerLabel = Label.New("Initializer");
		ComboBoxText initializerDropDown =  ComboBoxText.New();
		Label initializerRestartLabel = Label.New("Save settings and restart app for changes to apply");
		initializerRestartLabel.SetVisible(false);
		
		initializerDropDown.AppendText("GTK4");
		initializerDropDown.AppendText("libadwaita");
		
		
		initializerDropDown.SetActive((int)Program.Config.Initializer);
		initializerDropDown.OnChanged += (_, _) => {
			Program.Initializer selected = (Program.Initializer)initializerDropDown.GetActive();
			Program.Config.Initializer = selected;
			initializerRestartLabel.SetVisible(Program.InitializedUsing != selected);
			MarkDirty();
		};
		Button initializerInfo = Button.NewWithLabel("?");
		initializerInfo.OnClicked += (sender, args) => {
			PopupWindow popup = new PopupWindow(this, "Info", 
				"This option decides which library g3man should use to create its window."
				+ "\nWith GTK4, you'll be able to change themes."
				+ "\nWith libadwaita, g3man will look like a GNOME app.",
				"I will be careful");
			
			popup.Dialog();
		};
		initializerInfo.SetSizeRequest(20, 20);
		
		
		
		Box initializerBox = Box.New(Orientation.Horizontal, 10);
		initializerBox.Append(initializerLabel);
		initializerBox.Append(initializerDropDown);
		initializerBox.Append(initializerInfo);
		initializerBox.Append(initializerRestartLabel);
		initializerBox.SetHalign(Align.Start);
		
		Label allowModScriptingLabel =  Label.New("Allow mods to run C# scripts");
		ComboBoxText allowModScriptsDropDown = ComboBoxText.New();
		allowModScriptsDropDown.AppendText("Disallow");
		allowModScriptsDropDown.AppendText("Allow");
		allowModScriptsDropDown.SetActive(Program.Config.AllowModScripting ? 1 : 0);
		allowModScriptsDropDown.OnChanged += (_, _) => {
			Program.Config.AllowModScripting = allowModScriptsDropDown.GetActive() == 1;
			MarkDirty();
		};
		Button scriptInfoDialog = Button.NewWithLabel("?");
		scriptInfoDialog.OnClicked += (sender, args) => {
			PopupWindow popup = new PopupWindow(this, "Info", 
				"This option allows mods to run C# scripts."
					+ "\nSome mods need them, but remember that these scripts could"
					+ "\npotentially do anything to your computer!",
				"I will be careful");
			
			popup.Dialog();
		};
		scriptInfoDialog.SetSizeRequest(20, 20);
		
		Box allowModScriptsBox = Box.New(Orientation.Horizontal, 5);
		allowModScriptsBox.Append(allowModScriptingLabel);
		allowModScriptsBox.Append(allowModScriptsDropDown);
		allowModScriptsBox.Append(scriptInfoDialog);
		
		

		CheckButton checkForUpdatesCheck = CheckButton.NewWithLabel("Check for Updates on Startup");
		checkForUpdatesCheck.SetActive(Program.Config.CheckForUpdates);
		checkForUpdatesCheck.OnToggled += (sender, _) => {
			Program.Config.CheckForUpdates = sender.GetActive();
			MarkDirty();
		};
		Box checkForUpdatesBox = Box.New(Orientation.Horizontal, 5);
		checkForUpdatesBox.Append(checkForUpdatesCheck);
		

		saveSettingsButton.SetHalign(Align.End);
		saveSettingsButton.SetValign(Align.End);
		saveSettingsButton.SetVexpand(true);
		saveSettingsButton.OnClicked += (sender, args) => {
			Program.Config.Write();
			saveSettingsButton.SetLabel(saveSettingsLabel);
		};
		
		page.Append(initializerBox);
		page.Append(themeBox);
		
		page.Append(allowModScriptsBox);
		page.Append(checkForUpdatesBox);
		page.Append(saveSettingsButton);
		page.SetMargin(20);
		page.SetSpacing(10);
	}
}