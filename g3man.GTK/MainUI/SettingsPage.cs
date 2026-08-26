using g3man.Core;
using g3man.GTK;
using g3man.Core.Util;
using g3man.GTK.Util;
using Gtk;

namespace g3man.GTK.MainUI;

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

		themeDropDown.SetActive((int)UI.Config.ColorScheme);
		themeDropDown.OnChanged += (_, _) =>
		{
			ColorScheme selected = (ColorScheme)themeDropDown.GetActive();
			UI.ApplyColorScheme(selected);
			#if THEMABLE_TITLEBAR
				ApplyCurrentThemeToTitlebar();
			#endif
			UI.Config.ColorScheme = selected;
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
		
		
		initializerDropDown.SetActive((int)UI.Config.Initializer);
		initializerDropDown.OnChanged += (_, _) => {
			Initializer selected = (Initializer)initializerDropDown.GetActive();
			UI.Config.Initializer = selected;
			initializerRestartLabel.SetVisible(UI.InitializedUsing != selected);
			MarkDirty();
		};
		initializerDropDown.SetTooltipText("Set which library g3man should use to create its window.\nWith libadwaita, g3man will look like a GNOME app.");

		
		Box initializerBox = Box.New(Orientation.Horizontal, 10);
		initializerBox.Append(initializerLabel);
		initializerBox.Append(initializerDropDown);
		initializerBox.Append(initializerRestartLabel);
		initializerBox.SetHalign(Align.Start);
		
		Label allowModScriptingLabel =  Label.New("Allow mods to run C# scripts");
		ComboBoxText allowModScriptsDropDown = ComboBoxText.New();
		allowModScriptsDropDown.AppendText("Disallow");
		allowModScriptsDropDown.AppendText("Allow");
		allowModScriptsDropDown.SetActive(UI.Config.AllowModScripting ? 1 : 0);
		allowModScriptsDropDown.OnChanged += (_, _) => {
			UI.Config.AllowModScripting = allowModScriptsDropDown.GetActive() == 1;
			MarkDirty();
		};
		Button scriptInfoDialog = Button.NewWithLabel("!");
		scriptInfoDialog.OnClicked += UI.OpenWindowButton((_, _) => {
			PopupWindow popup = new(this, "Info", 
				"This option allows mods to run C# scripts."
					+ "\nSome mods need them, but remember that these scripts could"
					+ "\npotentially do anything to your computer!",
				"I will be careful");
			
			popup.Dialog();
		});
		scriptInfoDialog.SetSizeRequest(20, 20);
		
		Box allowModScriptsBox = Box.New(Orientation.Horizontal, 5);
		allowModScriptsBox.Append(allowModScriptingLabel);
		allowModScriptsBox.Append(allowModScriptsDropDown);
		allowModScriptsBox.Append(scriptInfoDialog);
		
		
		Label steamExecutableLabel = Label.New("Steam executable/command");
		steamExecutableLabel.SetHalign(Align.Start);
		Entry steamExecutableEntry = Entry.New();
		steamExecutableEntry.SetText(UI.Config.SteamExecutable);
		steamExecutableEntry.OnChanged += (editable, _) => {
			UI.Config.SteamExecutable = editable.GetText();
			MarkDirty();
		};
		steamExecutableEntry.SetTooltipText("The path to Steam's executable/command to launch Steam, for launching games with Steam.");
		
		Button steamBrowseButton = Button.NewWithLabel("Browse");
		steamBrowseButton.OnClicked += UI.OpenWindowButton(async (_, _) => {
			Gio.File? file = await FileDialogWindow.Dialog(this, "Choose an executable", []);
			string? path = file?.GetPath();
			if (path is null)
				return;
			steamExecutableEntry.SetText(path);
		});

		Box steamBox = Box.New(Orientation.Vertical, 10)
			.With(
				steamExecutableLabel,
				Box.New(Orientation.Horizontal, 5)
					.With(
						steamBrowseButton, steamExecutableEntry
					)
				);
		

		CheckButton checkForUpdatesCheck = CheckButton.NewWithLabel("Check for updates on startup");
		checkForUpdatesCheck.SetActive(UI.Config.CheckForUpdates);
		checkForUpdatesCheck.OnToggled += (sender, _) => {
			UI.Config.CheckForUpdates = sender.GetActive();
			MarkDirty();
			
		};
		checkForUpdatesCheck.SetTooltipText("Check the g3man GitHub to see if there's a new release when you open the program. If there is, you'll see a (!) on the \"About\" page.");

		saveSettingsButton.SetHalign(Align.End);
		saveSettingsButton.SetValign(Align.End);
		saveSettingsButton.SetVexpand(true);
		saveSettingsButton.OnClicked += UI.DoOperation<Button>([UI.Operation.SaveConfig], async (_, _) => {
			await UI.TryWriteConfig();
			saveSettingsButton.SetLabel(saveSettingsLabel);
		});
		
		page.Append(initializerBox);
		page.Append(themeBox);
		page.Append(allowModScriptsBox);
		page.Append(steamBox);
		page.Append(checkForUpdatesCheck);
		page.Append(saveSettingsButton);
		page.SetMargin(20);
		page.SetSpacing(10);
	}
}