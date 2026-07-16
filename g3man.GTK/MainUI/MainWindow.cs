using System.Diagnostics;
using System.IO.Compression;
using g3man.Core;
using g3man.GTK;
using g3man.Core.Models;
using g3man.Core.Util;
using g3man.GTK.Util;
using Gtk;
using Pango;

namespace g3man.GTK.MainUI;

#pragma warning disable CS8618

public partial class MainWindow : G3manWindow {
	private ListBox gamesListBox;
	private Entry gameDirectoryEntry;
	private List<Button> selectGameButtons;
	
	private ListBox profilesListBox;
	private List<Button> selectProfileButtons;
	
	private ListBox modsListBox;
	private ScrolledWindow modsListWindow;
	private List<IMod> modsList = new();
	private Dictionary<IMod, bool> enabledMods = new();
	private List<Game> gamesList;
	
	
	private Label noModsLabel;
	
	private Label noGamesAddedLabel;
	
	private Image currentGameIcon;
	private Label currentGameLabel;
	private Label currentProfileLabel;
	

	private Box actionBox;
	
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
		
		if (UI.InitializedUsing == Initializer.Libadwaita) {
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
		
		void ApplyModsDialog(bool launch) {
			PatcherWindow window = new PatcherWindow(this);
			List<IMod> enabledModsList = modsList.Where(mod => enabledMods.GetValueOrDefault(mod, false)).ToList();
			window.Dialog(enabledModsList, () => {
				if (launch) {
					window.Close();
					LaunchDialog();
				}
			});
		}
		

		void LaunchDialog() {
			Game game = UI.GetGame()!;
			Status executableStatus = game.ExecutableStatus(UI.Config);
			if (!executableStatus.ok) {
				PopupWindow popup = new PopupWindow(this, "Error!",
					$"{executableStatus.message}",
					"OK");
				popup.Dialog();
				return;
			}

			try {
				game.Launch(UI.Config, UI.GetProfile()!);
			}
			catch (Exception e) {
				UI.Logger.Error($"Failed to launch game: {e}");
				PopupWindow popup = new PopupWindow(this, "Error!",
					$"Failed to launch game: {e.Message}",
					"Damn");
				popup.Dialog();
				return;
			}
			
			PopupWindow successPopup = new PopupWindow(this, "Game launched!",
				$"Game launch should be successful.\ng3man does not have to stay open past this point.",
				"OK");
			successPopup.Dialog();
		}
		
		Button applyButton = Button.NewWithLabel("Apply");
		applyButton.OnClicked += (_, _) => {
			UI.GetProfile()!.UpdateModsStatus(modsList, enabledMods);
			UI.GetProfile()!.Write(UI.GetGame()!);
			ApplyModsDialog(launch: false);
		};
		
		Button launchButton = Button.NewWithLabel("Launch");
		launchButton.OnClicked += (_, _) => { LaunchDialog(); };

		Button applyAndLaunchButton = Button.NewWithLabel("Apply and Launch!");
		applyAndLaunchButton.OnClicked += (_, _) => {
			UI.GetProfile()!.UpdateModsStatus(modsList, enabledMods);
			UI.GetProfile()!.Write(UI.GetGame()!);
			ApplyModsDialog(launch: true);
		};
		actionBox = Box.New(Orientation.Horizontal, 10);
		actionBox.Append(applyAndLaunchButton);
		actionBox.Append(applyButton);
		actionBox.Append(launchButton);
		actionBox.SetMargin(10);
		
		Box currentSetupBox = Box.New(Orientation.Horizontal, 5);
		currentSetupBox.Append(currentGameLabel);
		currentSetupBox.Append(slash);
		currentSetupBox.Append(currentProfileLabel);
		currentSetupBox.SetMargin(10);
		currentSetupBox.SetHexpand(true);

		//currentGameIcon = Image.New();
		//currentGameIcon.UseFallback = true;
		Box bottomBox = Box.New(Orientation.Horizontal, 5)
			.With(currentSetupBox, actionBox);
		Box programBox = Box.New(Orientation.Vertical, 0);
		programBox.Append(pageBox);
		programBox.Append(Separator.New(Orientation.Horizontal));
		programBox.Append(bottomBox);

		
		SetChild(programBox);
		
		EnableExtraCategories(ExtraCategories.None);
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
			actionBox.SetSensitive(false);
			return;
		}
		Debug.Assert(UI.GetGame() is not null);
		profilesButton.SetSensitive(true);
		if (extra < ExtraCategories.ProfilesAndMods) {
			modsButton.SetSensitive(false);
			actionBox.SetSensitive(false);
			return;
		}

		Debug.Assert(UI.GetProfile() is not null);
		modsButton.SetSensitive(true);
		actionBox.SetSensitive(true);
	}
	

	
}