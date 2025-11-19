using System.Diagnostics;
using g3man.Models;
using g3man.Util;
using Gtk;
using Window = Gtk.Window;

namespace g3man.UI;

public class MainWindow : Window {
	
	private ListBox gamesListBox;
	private Entry gameDirectoryEntry;
	private List<Button> selectGameButtons;
	
	private ListBox profilesListBox;
	private List<Button> selectProfileButtons;
	
	private ListBox modsListBox;
	private List<Mod> modsList;
	private Dictionary<Mod, bool> enabledMods;
	
	private Label noModsLabel;
	
	private Label nothingAutoDetectedLabel;
	private Label noGamesAddedLabel;
	
	private Label currentGameLabel;
	private Label currentProfileLabel;
	
	private Box[] allPages;
	private string[] pageNames;
	private string[] pageTitles;

	private Stack pageStack;
	private ExtraCategories currentExtraCategories;
	private StackSidebar pageSidebar;
	
	public MainWindow() {
		Title = "g3man";
		SetDefaultSize(300, 300);
		pageStack = Stack.New();
		pageStack.SetHexpand(true);
		
		pageSidebar = StackSidebar.New();
		pageSidebar.SetStack(pageStack);
		pageSidebar.SetValign(Align.Fill);
		pageSidebar.SetVexpand(true);
		
		Box gamesPage = Box.New(Orientation.Vertical, 0);
		Box profilesPage = Box.New(Orientation.Vertical, 0);
		Box modsPage = Box.New(Orientation.Vertical, 0);
		Box settingsPage = Box.New(Orientation.Vertical, 0);
		Box aboutPage = Box.New(Orientation.Vertical, 0);
		
		allPages = [gamesPage, profilesPage, modsPage, settingsPage, aboutPage];
		pageNames = ["games", "profiles","mods", "settings", "about"];
		pageTitles = ["Games", "Profiles", "Mods", "Settings", "About"];
		
		Box pageBox = Box.New(Orientation.Horizontal, 0);
		pageBox.Append(pageSidebar);
		pageBox.Append(pageStack);
		pageBox.SetHomogeneous(false);
		
		pageStack.SetTransitionType(StackTransitionType.SlideUpDown);
		for (int i = 0; i < allPages.Length; i++) {
			pageStack.AddTitled(allPages[i], pageNames[i], pageTitles[i]);
		}
		pageStack.SetVisibleChild(gamesPage);
		EnableExtraCategories(ExtraCategories.None);
		
		SetupGamesPage(gamesPage);
		SetupProfilesPage(profilesPage);
		SetupModsPage(modsPage);
		SetupSettingsPage(settingsPage);
		SetupAboutPage(aboutPage);
		
		Debug.Assert(modsListBox is not null 
			&& profilesListBox is not null
			&& gameDirectoryEntry is not null);
		
		
		currentGameLabel = Label.New("No game selected");
		Label slash = Label.New("/");
		currentProfileLabel = Label.New("No profile selected");
		
		
		Box currentSetupBox = Box.New(Orientation.Horizontal, 5);
		currentSetupBox.Append(currentGameLabel);
		currentSetupBox.Append(slash);
		currentSetupBox.Append(currentProfileLabel);
		
		currentSetupBox.SetHalign(Align.Center);
		currentSetupBox.SetHexpand(true);
		currentSetupBox.SetMargin(10);


		Box programBox = Box.New(Orientation.Vertical, 0);
		programBox.Append(pageBox);
		programBox.Append(Separator.New(Orientation.Horizontal));
		programBox.Append(currentSetupBox);
		
		SetChild(programBox);
		
#if THEMESELECTOR
		ApplyTheme(Program.Config.Theme);
#endif
	}

	enum ExtraCategories {
		None,
		Profiles,
		ProfilesAndMods
	}
	/**
	* Turns on extra categories (Profiles and Mods) depending on the parameter.
	*
	* TODO: This is a really dumb way to be doing things.
	* There must be a better way that achieves the same visual result.
	*/
	private void EnableExtraCategories(ExtraCategories extra) {
		currentExtraCategories = extra;
		ListBox pageList = (ListBox)pageSidebar.GetFirstChild()!.GetFirstChild()!.GetFirstChild()!;
		ListBoxRow gamesRow = (ListBoxRow)pageList.GetFirstChild()!;
		ListBoxRow profilesRow = (ListBoxRow)gamesRow.GetNextSibling()!;
		ListBoxRow modsRow = (ListBoxRow)profilesRow.GetNextSibling()!;
		if (extra < ExtraCategories.Profiles) {
			profilesRow.SetSensitive(false);
			profilesRow.SetSelectable(false);
			modsRow.SetSensitive(false);
			modsRow.SetSelectable(false);
			return;
		}
		Debug.Assert(Program.GetGame() is not null);
		profilesRow.SetSensitive(true);
		profilesRow.SetSelectable(true);
		if (extra < ExtraCategories.ProfilesAndMods) {
			modsRow.SetSensitive(false);
			modsRow.SetSelectable(false);
			return;
		}

		Debug.Assert(Program.GetProfile() is not null);
		modsRow.SetSensitive(true);
		modsRow.SetSelectable(true);
	}
	

	
	private void SetupGamesPage(Box box) {
		Label gamesLabel = Label.New("Games");
		gamesLabel.SetHalign(Align.Start);
		gamesLabel.SetMarginStart(10);
		gamesLabel.SetMarginTop(10);

		noGamesAddedLabel = Label.New("There are no games added");
		noGamesAddedLabel.SetMargin(10);

		gamesListBox = ListBox.New();
		gamesListBox.SetSelectionMode(SelectionMode.None);
		gamesListBox.SetPlaceholder(noGamesAddedLabel);
		selectGameButtons = [];


		List<Game> games = Game.Parse(Program.Config.GameDirectories);
		Program.Config.UpdateGameDirectories(games);
		PopulateGamesList(games);
		
		Label autoDetectedLabel = Label.New("Auto-detected");
		autoDetectedLabel.SetHalign(Align.Start);
		autoDetectedLabel.SetMarginStart(10);
		
		nothingAutoDetectedLabel = Label.New("Couldn't auto-detect any GameMaker games on your computer");
		nothingAutoDetectedLabel.SetMargin(20);
		
		Stack autodetectedStack = Stack.New();
		autodetectedStack.AddChild(nothingAutoDetectedLabel);
		
		Label manualLabel = Label.New("Manually add game");
		manualLabel.SetHalign(Align.Start);
		manualLabel.SetMarginStart(10);
		
		gameDirectoryEntry = Entry.New();
		gameDirectoryEntry.SetHalign(Align.Start);

		gameDirectoryEntry.SetMaxWidthChars(75);
		Button browseButton = Button.NewWithLabel("Browse");
		
		Box gameDirectoryEntryBox = Box.New(Orientation.Horizontal, 10);
		gameDirectoryEntryBox.Append(browseButton);
		gameDirectoryEntryBox.Append(gameDirectoryEntry);
		
		Label statusLabel = Label.New("");
		statusLabel.SetHalign(Align.Start);
		
		Box gameDirectoryBox = Box.New(Orientation.Vertical, 0);
		gameDirectoryBox.SetHalign(Align.Center);
		gameDirectoryBox.Append(gameDirectoryEntryBox);
		gameDirectoryBox.Append(statusLabel);
		gameDirectoryBox.SetMargin(20);
		gameDirectoryBox.SetMarginBottom(5);
		void OnTextChanged(string text) {
			if (text == "")
				statusLabel.SetText("");
			else {
				PathStatus status = ProgramPaths.GameMakerDirectoryStatus(text);
				statusLabel.SetText(status.message);
			}
		}
		
		Button addGameButton = Button.NewWithLabel("Add game");
		addGameButton.SetMarginBottom(10);
		addGameButton.SetHalign(Align.Center);
		addGameButton.OnClicked += (sender, args) => {
			GameAdderWindow adderWindow = new GameAdderWindow(gameDirectoryEntry.GetText(), this);
			adderWindow.Dialog();
		};
		
		gameDirectoryEntry.GetBuffer().OnDeletedText += (buffer, args) => {
			string text = buffer.GetText();
			OnTextChanged(text.Remove((int)args.Position, (int)args.NChars));
		};
		gameDirectoryEntry.GetBuffer().OnInsertedText += (buffer, _) => {
			OnTextChanged(buffer.GetText());
		};
		
		box.Append(gamesLabel);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(gamesListBox);
		box.Append(autoDetectedLabel);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(autodetectedStack);

		box.Append(manualLabel);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(gameDirectoryBox);
		box.Append(addGameButton);
	}

	private void SetupProfilesPage(Box box) {
		profilesListBox = ListBox.New();
		profilesListBox.SetSelectionMode(SelectionMode.None);
		selectProfileButtons = [];
		
		Button addNewProfile = Button.NewWithLabel("Add new profile");
		addNewProfile.SetHalign(Align.Center);
		addNewProfile.SetMargin(10);
		addNewProfile.OnClicked += (sender, args) => {
			Profile profile = new Profile("", "", false, "", []);
			ManageProfileWindow window = new ManageProfileWindow(this, profile, null);
			// TODO
			window.Dialog();
		};
		
		box.Append(profilesListBox);
		box.Append(addNewProfile);
	}
	
	private void SetupModsPage(Box page) {
		noModsLabel = Label.New("No mods found.");
		noModsLabel.SetMargin(30);
		
		modsListBox = ListBox.New();
		modsListBox.SetHexpand(true);
		modsListBox.SetPlaceholder(noModsLabel);
		
		Box manageModsBox = Box.New(Orientation.Horizontal, 5);
		manageModsBox.SetHalign(Align.Center);
		manageModsBox.SetValign(Align.Center);
		
		Button openModsFolderButton = Button.New();
		openModsFolderButton.Label = "Open mods folder";
		
		Button refreshButton = Button.NewWithLabel("Refresh");
		refreshButton.OnClicked += (_, _) => {
			Program.GetProfile()!.UpdateModsStatus(modsList, enabledMods);
			Program.GetProfile()!.Write(Program.GetGame()!.Directory);
			PopulateModsList();
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
		
		Button installFromZipButton = Button.New();
		installFromZipButton.Label = "Install from ZIP";
		
		Button removeModButton = Button.New();
		removeModButton.Label = "Remove selected";
		
		manageModsBox.Append(openModsFolderButton);
		manageModsBox.Append(refreshButton);
		manageModsBox.Append(moveModsUp);
		manageModsBox.Append(moveModsDown);
		manageModsBox.Append(installFromZipButton);
		manageModsBox.Append(removeModButton);
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
		
		page.Append(modsListBox);
		page.Append(manageModsBox);
		page.Append(applyButton);
		page.SetVexpand(true);
	}

	private void SetupSettingsPage(Box page) {
		
#if THEMESELECTOR
			// TODO CHANGE THIS TO COMBOBOXTEXT
			DropDown themeDropDown = DropDown.NewFromStrings(["System Default", "Light", "Dark"]);
			themeDropDown.SetSelected((uint)Program.Config.Theme);
			themeDropDown.OnNotify += (_, args) => {
				// doesn't seem to be a signal for this
				if (args.Pspec.GetName() != "selected-item")
					return;
				Program.Theme selected = (Program.Theme)themeDropDown.GetSelected();
				ApplyTheme(selected);
				Program.Config.Theme = selected;
			};
			
			Label themeLabel = Label.New("Theme");
			
			
			Box themeBox = Box.New(Orientation.Horizontal, 10);
			themeBox.Append(themeLabel);
			themeBox.Append(themeDropDown);
			themeBox.SetHalign(Align.Start);
			themeBox.SetMargin(10);
#endif

		Label initializerLabel = Label.New("Initializer");
		ComboBoxText initializerDropDown =  ComboBoxText.New();
		Label initializerRestartLabel = Label.New("Restart app for changes to apply");
		initializerRestartLabel.SetVisible(false);
		
		initializerDropDown.AppendText("Adwaita");
		initializerDropDown.AppendText("GTK");
		
		initializerDropDown.SetActive((int)Program.Config.Initializer);
		initializerDropDown.OnChanged += (_, _) => {
			Program.Initializer selected = (Program.Initializer)initializerDropDown.GetActive();
			Program.Config.Initializer = selected;
			initializerRestartLabel.SetVisible(Program.InitializedUsing != selected);
		};
		
		
		Box initializerBox = Box.New(Orientation.Horizontal, 10);
		initializerBox.Append(initializerLabel);
		initializerBox.Append(initializerDropDown);
		initializerBox.Append(initializerRestartLabel);
		initializerBox.SetHalign(Align.Start);
		
		Label allowModScriptingLabel =  Label.New("Allow mods to run C# scripts");
		ComboBoxText allowModScriptsDropDown = ComboBoxText.New();
		allowModScriptsDropDown.AppendText("Disallow");
		allowModScriptsDropDown.AppendText("Allow");
		allowModScriptsDropDown.SetActive(Program.Config.AllowModScripting ? 1 : 0);
		allowModScriptsDropDown.OnChanged += (_, _) => {
			Program.Config.AllowModScripting = allowModScriptsDropDown.GetActive() == 1;
		};
		Button infoButton = Button.NewWithLabel("?");
		infoButton.OnClicked += (sender, args) => {
			PopupWindow popup = new PopupWindow(this, "Info", 
				"This option allows mods to run C# scripts."
					+ "\nSome mods need them, but remember that these scripts could"
					+ "\npotentially do anything to your computer!",
				"I will be careful");
			
			popup.Dialog();
		};
		infoButton.SetSizeRequest(20, 20);
		
		Box allowModScriptsBox = Box.New(Orientation.Horizontal, 5);
		allowModScriptsBox.Append(allowModScriptingLabel);
		allowModScriptsBox.Append(allowModScriptsDropDown);
		allowModScriptsBox.Append(infoButton);
		
		
		Button saveSettingsButton = Button.New();
		saveSettingsButton.Label = "Save Settings";
		saveSettingsButton.SetHalign(Align.End);
		saveSettingsButton.SetValign(Align.End);
		saveSettingsButton.SetVexpand(true);
		saveSettingsButton.OnClicked += (sender, args) => {
			Program.Config.Write();
		};
		
#if THEMESELECTOR
			page.Append(themeBox);
#endif
		page.Append(initializerBox);
		page.Append(allowModScriptsBox);
		page.Append(saveSettingsButton);
		page.SetMargin(20);
		page.SetSpacing(10);
	}
	

	private static void SetupAboutPage(Box page) {
		Label title = Label.New("");
		title.SetMarkup("<span size=\"large\">g3man</span>");
		title.SetSizeRequest(100, 20);
		Label subtitle = Label.New("");
		subtitle.SetMarkup("<b>G</b>ame<b>M</b>aker <b>M</b>od <b>Man</b>ager");

		Label license = Label.New("Licensed under the terms of the AGPLv3,\nThis is Free Software (with Free as in Freedom)");
		license.SetMarginTop(10);
		license.SetJustify(Justification.Center);

		
		Label source = Label.New("");
		source.SetMarginTop(10);
		source.SetMarkup("<a href=\"https://github.com/skirlez/g3man\">GitHub Repository</a>");

		page.Append(title);
		page.Append(subtitle);
		page.Append(license);
		page.Append(source);
		page.SetHalign(Align.Center);
		page.SetValign(Align.Center);
	}


	private void PopulateModsList() {
		Game? game = Program.GetGame();
		Profile? profile = Program.GetProfile();
		
		Debug.Assert(game is not null);
		Debug.Assert(profile is not null);
		
		modsList = Mod.Parse(Path.Combine(game.Directory, "g3man", profile.FolderName, "mods"));
		
		
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

	public void AddToGamesList(Game game, bool selected) {
		Label gameNameLabel = Label.New(game.DisplayName);
		
		Box spacer = Box.New(Orientation.Horizontal, 0);
		spacer.SetHexpand(true);
		
		Button selectGameButton = Button.NewWithLabel("Select");
		
		selectGameButton.OnClicked += (button, _) => {
			SelectGame(game, button);
		};
		selectGameButton.SetSensitive(!selected);
		selectGameButtons.Add(selectGameButton);


		
		Box box = Box.New(Orientation.Horizontal, 0);
		box.Append(gameNameLabel);
		box.Append(spacer);
		box.Append(selectGameButton);
	
	
		box.SetValign(Align.Center);
		
		
		ListBoxRow row = ListBoxRow.New();
		
		row.SetChild(box);
		row.SetActivatable(false);
		row.SetMargin(10);
		
		gamesListBox.Append(row);
	}
	private void PopulateGamesList(List<Game> games, Game? selectedGame = null) {
		selectGameButtons.Clear();
		
		gamesListBox.RemoveAll();
		gamesListBox.SetPlaceholder(noGamesAddedLabel);
		
		foreach (Game game in games) {
			AddToGamesList(game, selectedGame == game);
		}
	}
	
	private void PopulateProfilesList(List<Profile> profiles, Profile? selectedId = null) {
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

	private void SelectGame(Game game, Button buttonPressed) {
		foreach (Button button in selectGameButtons) {
			button.SetSensitive(true);
		}
		buttonPressed.SetSensitive(false);
		
		Program.SetGame(game);
		currentGameLabel.SetText(game.DisplayName);
		List<Profile> profiles = Profile.ParseAll(Path.Combine(game.Directory, "g3man"));
		if (profiles.Count == 0)
		{
			EnableExtraCategories(ExtraCategories.Profiles);
			return;
		}
		
		Profile? profile = profiles.FirstOrDefault(p => {
			Debug.Assert(p is not null);
			return p.FolderName == game.ProfileFolderName;
		}, null);
		if (profile == null) {
			PopulateProfilesList(profiles, null);
			// let user choose profile if for some reason we couldn't use the normal one
			EnableExtraCategories(ExtraCategories.Profiles);
			return;
		}
		Program.SetProfile(profile);
		currentProfileLabel.SetText(profile.Name);
		
		PopulateProfilesList(profiles, profiles.FirstOrDefault(p => (p!.FolderName == game.ProfileFolderName), null));
		PopulateModsList();
		EnableExtraCategories(ExtraCategories.ProfilesAndMods);
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
		PopulateModsList();
	}

#if THEMESELECTOR
		private void ApplyTheme(Program.Theme theme) {
			if (Program.InitializedUsing == Program.Initializer.Gtk) {
				Settings? settings = Settings.GetDefault();
				if (settings is null)
					return;
				settings.GtkApplicationPreferDarkTheme = (theme == Program.Theme.Dark);
			}
			else {
				Adw.StyleManager.GetDefault().SetColorScheme(theme switch {
					Program.Theme.SystemDefault => Adw.ColorScheme.Default,
					Program.Theme.Light => Adw.ColorScheme.ForceLight,
					Program.Theme.Dark => Adw.ColorScheme.ForceDark,
					_ => throw new UnreachableException()
				});
			}
		}
#endif
}