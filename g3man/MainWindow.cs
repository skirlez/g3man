using System.Diagnostics;
using System.Runtime.InteropServices;
using g3man;
using g3man.Models;
using g3man.Util;
using Gdk;
using Gtk;

public class MainWindow : Window {

	private int categoriesShowing;
	private ListBox gamesList;
	private Entry gameDirectoryEntry;
	private List<Button> selectGameButtons;
	
	private ListBox profilesList;
	private List<Button> selectProfileButtons;
	
	private ListBox modsList;
	private Stack modsListStack;
	
	private Label noModsLabel;
	private Label noGameLabel;
	
	private Label nothingAutoDetectedLabel;
	private Label noGamesAddedLabel;
	
	private Label currentGameLabel;
	private Label currentProfileLabel;
	
	private Box[] allPages;
	private string[] pageNames;
	private string[] pageTitles;

	private Stack pageStack;
	
	public MainWindow() {
		Title = "g3man";
		SetDefaultSize(300, 300);
		pageStack = Stack.New();
		pageStack.SetHexpand(true);
		
		StackSidebar pageSidebar = StackSidebar.New();
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


		DisplayCategories(1);
		
		Box pageBox = Box.New(Orientation.Horizontal, 0);
		pageBox.Append(pageSidebar);
		pageBox.Append(pageStack);
		pageBox.SetHomogeneous(false);
		pageStack.SetVisibleChild(gamesPage);
		
		
		SetupGamesPage(gamesPage);
		SetupProfilesPage(profilesPage);
		SetupModsPage(modsPage);
		SetupSettingsPage(settingsPage);
		SetupAboutPage(aboutPage);
		
		Debug.Assert(modsList is not null 
			&& profilesList is not null
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


	}

	private void DisplayCategories(int amount) {
		if (amount < categoriesShowing) {
			for (int i = categoriesShowing - 1; i >= amount; i--) {
				pageStack.Remove(allPages[i]);
			}
		}
		else {
			for (int i = categoriesShowing; i < amount; i++) {
				pageStack.AddTitled(allPages[i], pageNames[i], pageTitles[i]);
			}
		}

		categoriesShowing = amount;
	}
	

	
	private void SetupGamesPage(Box box) {
		Label gamesLabel = Label.New("Games");
		gamesLabel.SetHalign(Align.Start);
		gamesLabel.SetMarginStart(10);
		gamesLabel.SetMarginTop(10);

		noGamesAddedLabel = Label.New("There are no games added");
		noGamesAddedLabel.SetMargin(10);

		gamesList = ListBox.New();
		gamesList.SetSelectionMode(SelectionMode.None);
		gamesList.SetPlaceholder(noGamesAddedLabel);
		selectGameButtons = [];

		PopulateGamesList(Program.Config.Games);
		
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
		addGameButton.SetHalign(Align.Center);
		addGameButton.OnClicked += (sender, args) => {
			GameAdder adder = new GameAdder(gameDirectoryEntry.GetText(), this);
			adder.Dialog();
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
		box.Append(gamesList);
		box.Append(autoDetectedLabel);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(autodetectedStack);

		box.Append(manualLabel);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(gameDirectoryBox);
		box.Append(addGameButton);
	}

	private void SetupProfilesPage(Box box) {
		profilesList = ListBox.New();
		profilesList.SetSelectionMode(SelectionMode.None);
		selectProfileButtons = [];
		
		box.Append(profilesList);
	}
	
	private void SetupModsPage(Box page) {
		Box manageModsBox = Box.New(Orientation.Horizontal, 5);
		manageModsBox.SetHalign(Align.Center);
		manageModsBox.SetValign(Align.Center);
		
		Button openModsFolderButton = Button.New();
		openModsFolderButton.Label = "Open mods folder";
		
		Button refreshButton = Button.NewWithLabel("Refresh");
		refreshButton.OnClicked += (_, _) => PopulateModsList();
		
		Button moveModsUp = Button.New();
		moveModsUp.Label = "↑";
		Button moveModsDown = Button.New();
		moveModsDown.Label = "↓";
		
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

		noModsLabel = Label.New("No mods found.");
		noModsLabel.SetMargin(30);
		noGameLabel = Label.New("Select a game to begin adding mods!");
		noGameLabel.SetMargin(30);

		modsList = ListBox.New();
		modsList.SetHexpand(true);
		modsList.SetPlaceholder(noModsLabel);
		
		page.Append(modsList);
		page.Append(manageModsBox);
	}

	private void SetupSettingsPage(Box page) {
		Button saveSettingsButton = Button.New();
		saveSettingsButton.Label = "Save Settings";
		saveSettingsButton.SetHalign(Align.End);
		saveSettingsButton.SetValign(Align.End);
		saveSettingsButton.SetVexpand(true);
		
		//page.Append(isolateSaveCheck);
		page.Append(saveSettingsButton);
		page.SetMargin(20);
	}
	

	private static void SetupAboutPage(Box page) {
		Label title = Label.New("");
		title.SetMarkup("<span size=\"large\">g3man</span>");
		title.SetSizeRequest(100, 20);
		Label subtitle = Label.New("");
		subtitle.SetMarkup("<b>G</b>ame<b>M</b>aker <b>M</b>od <b>Man</b>ager");
		page.Append(title);
		page.Append(subtitle);
		page.SetHalign(Align.Center);
		page.SetValign(Align.Center);
	}


	private void PopulateModsList() {
		Game? game = Program.GetGame();
		Profile? profile = Program.GetProfile();
		
		Debug.Assert(game is not null);
		Debug.Assert(profile is not null);
		
		List<Mod> mods = Mod.ParseMods(Path.Combine(game.Directory, "g3man", profile.FolderName, "mods"));
		modsList.RemoveAll();
		modsList.SetPlaceholder(noModsLabel);
		foreach (Mod mod in mods) {
			ListBoxRow row = ListBoxRow.New();
			CheckButton modEnabled = CheckButton.New();
			Label modName = Label.New(mod.DisplayName);
			Box modBox = Box.New(Orientation.Horizontal, 5);
			modBox.Append(modEnabled);
			modBox.Append(modName);
			modBox.SetMargin(10);
			row.SetChild(modBox);
			modsList.Append(row);
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
		
		gamesList.Append(row);
	}
	private void PopulateGamesList(List<Game> games, Game? selectedGame = null) {
		selectGameButtons.Clear();
		
		gamesList.RemoveAll();
		gamesList.SetPlaceholder(noGamesAddedLabel);
		
		foreach (Game game in games) {
			AddToGamesList(game, selectedGame == game);
		}
	}
	
	private void PopulateProfilesList(List<Profile> profiles, string? selectedId = null) {
		profilesList.RemoveAll();
		foreach (Profile profile in profiles) {
			Label profileName = Label.New(profile.Name);
			Box spacer = Box.New(Orientation.Horizontal, 0);
			spacer.SetHexpand(true);
			
			Button manageProfileButton = Button.NewWithLabel("Manage");
			Button selectButton = Button.NewWithLabel("Select");
			if (profile.FolderName == selectedId)
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
			
			profilesList.Append(row);
		}
	}

	private void SelectGame(Game game, Button buttonPressed) {
		foreach (Button button in selectGameButtons) {
			button.SetSensitive(true);
		}
		buttonPressed.SetSensitive(false);
		
		Program.SetGame(game);
		currentGameLabel.SetText(game.DisplayName);
		List<Profile> profiles = Profile.ParseProfiles(Path.Combine(game.Directory, "g3man"));
		if (profiles.Count == 0) {
			DisplayCategories(2);
			return;
		}
		
		Profile? profile = profiles.FirstOrDefault(p => {
			Debug.Assert(p is not null);
			return p.FolderName == game.ProfileFolderName;
		}, null);
		if (profile == null) {
			PopulateProfilesList(profiles, null);
			// let user choose profile if for some reason we couldn't use the normal one
			DisplayCategories(2);
			return;
		}
		Program.SetProfile(profile);
		currentProfileLabel.SetText(profile.Name);
		
		PopulateProfilesList(profiles, game.ProfileFolderName);
		PopulateModsList();
		DisplayCategories(allPages.Length);
	}

	private void SelectProfile(Profile profile, Button buttonPressed) {
		if (categoriesShowing == 2)
			DisplayCategories(allPages.Length);
		foreach (Button button in selectProfileButtons) {
			button.SetSensitive(true);
		}
		buttonPressed.SetSensitive(false);
		Program.SetProfile(profile);
		currentProfileLabel.SetText(profile.Name);
		PopulateModsList();
	}
}