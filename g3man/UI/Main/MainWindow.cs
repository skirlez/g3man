using System.Diagnostics;
using System.IO.Compression;
using g3man.Models;
using Gtk;
using Pango;

namespace g3man.UI.Main;

#pragma warning disable CS8618

public partial class MainWindow : G3manWindow {
	private ListBox gamesListBox;
	private Entry gameDirectoryEntry;
	private List<Button> selectGameButtons;
	
	private ListBox profilesListBox;
	private List<Button> selectProfileButtons;
	
	private ListBox modsListBox;
	private ScrolledWindow modsListWindow;
	private List<IMod> modsList;
	private List<Game> gamesList;
	private Dictionary<IMod, bool> enabledMods;
	
	private Label noModsLabel;
	
	private Label noGamesAddedLabel;
	
	private Label currentGameLabel;
	private Label currentProfileLabel;
	
	private ToggleButton[] pageButtons;
	
	private const string aboutTitle = "About";
	private const string aboutTitleWithUpdate = "About (!)";
	
	// this is done so that when g3man switches these two out, it doesn't cause the window to move
	private Stack aboutButtonLabelStack;
	private Label aboutButtonLabelWithUpdate;
	
	private ExtraCategories currentExtraCategories;


	public MainWindow() {
		Title = "g3man";
		SetDefaultSize(700, 600);
		Stack pageStack = Stack.New();
		pageStack.SetHexpand(true);

		Box pageSidebar = Box.New(Orientation.Vertical, 8);
		
		Box gamesPage = Box.New(Orientation.Vertical, 0);
		Box profilesPage = Box.New(Orientation.Vertical, 0);
		Box modsPage = Box.New(Orientation.Vertical, 0);
		Box settingsPage = Box.New(Orientation.Vertical, 0);
		Box logsPage = Box.New(Orientation.Vertical, 0);
		Box aboutPage = Box.New(Orientation.Vertical, 0);
		
		Box[] allPages = [gamesPage, profilesPage, modsPage, settingsPage, logsPage, aboutPage];
		string[] pageTitles = ["Games", "Profiles", "Mods", "Settings", "Logs", aboutTitle];
		pageButtons = new ToggleButton[pageTitles.Length];
		
		
		Box pageBox = Box.New(Orientation.Horizontal, 0);
		pageBox.Append(pageSidebar);
		pageBox.Append(Separator.New(Orientation.Vertical));
		pageBox.Append(pageStack);
		pageBox.SetHomogeneous(false);
		

		pageStack.SetTransitionType(StackTransitionType.SlideUpDown);
		
		pageSidebar.SetMargin(5);
		

		for (int i = 0; i < allPages.Length; i++) {
			Box page = allPages[i];
			pageStack.AddChild(page);
			
			ToggleButton pageButton = ToggleButton.New();
			Label pageButtonLabel = Label.New(pageTitles[i]);
			if (page != aboutPage) {
				pageButton.SetChild(pageButtonLabel);
			}
			else {
				aboutButtonLabelStack = Stack.New();
				aboutButtonLabelWithUpdate = Label.New(aboutTitleWithUpdate);
				aboutButtonLabelStack.AddChild(pageButtonLabel);
				aboutButtonLabelStack.AddChild(aboutButtonLabelWithUpdate);
				pageButton.SetChild(aboutButtonLabelStack);
			}
			
			pageButton.SetHasFrame(false);
			if (i != 0)
				pageButton.SetGroup(pageButtons[i - 1]);
			
			pageButton.OnClicked += (sender, _) => {
				pageStack.SetVisibleChild(page);
			};
			
			pageSidebar.Append(pageButton);
			pageButtons[i] = pageButton;
		}
		
		if (Program.InitializedUsing == Program.Initializer.Libadwaita) {
			CssProvider pageButtonProvider = new CssProvider();
			pageButtonProvider.LoadFromString(
			@"button {
				font-weight	: normal;
			}"
			);
			foreach (ToggleButton button in pageButtons) {
				// priority number obtained via trial and error as the smallest one needed for this to have an effect (lol)
				button.GetStyleContext().AddProvider(pageButtonProvider, 200);
			}
		}
		
		pageStack.SetVisibleChild(allPages[0]);
		pageButtons[0].SetActive(true);
		
		EnableExtraCategories(ExtraCategories.None);
		
		SetupGamesPage(gamesPage);
		SetupProfilesPage(profilesPage);
		SetupModsPage(modsPage);
		SetupSettingsPage(settingsPage);
		SetupLogsPage(logsPage);
		SetupAboutPage(aboutPage);
		
		currentGameLabel = Label.New("No game selected");
		currentGameLabel.SetEllipsize(EllipsizeMode.Start);
		Label slash = Label.New("/");
		currentProfileLabel = Label.New("No profile selected");
		currentProfileLabel.SetEllipsize(EllipsizeMode.End);
		
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

	enum ExtraCategories {
		None,
		Profiles,
		ProfilesAndMods
	}
	/**
	* Turns on extra categories (Profiles and Mods) depending on the parameter.
	*/
	private void EnableExtraCategories(ExtraCategories extra) {
		currentExtraCategories = extra;

		Button modsButton = pageButtons[2];
		Button profilesButton = pageButtons[1];
		if (extra < ExtraCategories.Profiles) {
			profilesButton.SetSensitive(false);
			modsButton.SetSensitive(false);
			return;
		}
		Debug.Assert(Program.GetGame() is not null);
		profilesButton.SetSensitive(true);
		if (extra < ExtraCategories.ProfilesAndMods) {
			modsButton.SetSensitive(false);
			return;
		}

		Debug.Assert(Program.GetProfile() is not null);
		modsButton.SetSensitive(true);
	}
	

	
}