using System.Diagnostics;
using System.IO.Compression;
using g3man.Models;
using g3man.Util;
using Gdk;
using Gtk;
using Pango;
using Window = Gtk.Window;

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
	
	enum ZipType {
		Mod,
		Profile
	}
	private void TryExtractingZip(Gio.File file, ZipType type) {
		try {
			using ZipArchive archive = ZipFile.OpenRead(file.GetPath()!);
			
			ZipArchiveEntry[] profileJsonEntries = archive.Entries.Where(entry => entry.FullName.EndsWith("/profile.json") || entry.FullName == "profile.json").ToArray();
			ZipArchiveEntry[] modJsonEntries = archive.Entries.Where(entry => entry.FullName.EndsWith("/mod.json") || entry.FullName == "mod.json").ToArray();
			
			
			ZipArchiveEntry[] filterSubentries(ZipArchiveEntry[] entries) {
				return entries.Where(entry => entries.Count(entry2 => entry2 != entry 
				    && entry.FullName.StartsWith(Path.GetDirectoryName(entry2.FullName) ?? "")) == 0).ToArray();
					
			}
			// filter out mod/profile.jsons who are contained inside folders of other ones
			modJsonEntries = filterSubentries(modJsonEntries);
			profileJsonEntries = filterSubentries(profileJsonEntries);

			ZipArchiveEntry[] jsonEntries;
			string basePath;
			
			if (type == ZipType.Mod) {
				jsonEntries = modJsonEntries;
				basePath = Program.CurrentProfileFolderPath();
				if (profileJsonEntries.Length != 0) {
					PopupWindow popup = new PopupWindow(this, "Wait!",
						"This is a profile zip. You should install it as a profile in the profiles tab.", "Alright");
					popup.Dialog();
					return;
				}
				if (modJsonEntries.Length == 0) {
					PopupWindow popup = new PopupWindow(this, "Error!",
						"No mod folders found in this zip.", "Damn");
					popup.Dialog();
					return;
				}
			}
			else {
				jsonEntries = profileJsonEntries;
				basePath = Path.Combine(Program.GetGame()!.Directory, "g3man", "profiles");
				if (profileJsonEntries.Length == 0) {
					string message;
					string buttonText;
					if (modJsonEntries.Length == 0) {
						message = "This zip contains no profiles and no mods. Did you select the right file?";
						buttonText = "Close";
					}
					else {
						string has = (modJsonEntries.Length == 1) ? "a mod" : "a collection of mods";
						message = $"This zip contains no profiles, but it does have {has}. Try installing it in the mods tab.";
						buttonText = "Alright";
					}
					PopupWindow popup = new PopupWindow(this, "Wait!", message, buttonText);
					popup.Dialog();
					return;
				}
			}
			
			foreach (ZipArchiveEntry jsonEntry in jsonEntries) {
				string precedingPath = Path.GetDirectoryName(jsonEntry.FullName) ?? "";
				string folderName = 
					precedingPath != "" ? Path.GetFileName(precedingPath)
					: Path.GetFileNameWithoutExtension(file.GetBasename()!);
				string folder = Path.Combine(basePath, folderName);
				Directory.CreateDirectory(folder);

				Dictionary<bool, ZipArchiveEntry[]> groups = archive.Entries
					.Where(entry => entry.FullName.StartsWith(precedingPath) && entry.FullName != precedingPath)
					.GroupBy(entry => entry.FullName.EndsWith("/"))
					.ToDictionary(group => group.Key, group => group.ToArray());

				// these are just ignored. they don't show up on all platforms, and we know
				// which folders files need from their path anyway
				//ZipArchiveEntry[] foldermates = groups.GetValueOrDefault(true, []);
				
				ZipArchiveEntry[] filemates = groups.GetValueOrDefault(false, []);
				
				int precedingPathLength = precedingPath == "" ? 0 : precedingPath.Length + 1; // one more for trailing slash
				foreach (ZipArchiveEntry filemate in filemates) {
					string relativePath = filemate.FullName.Remove(0, precedingPathLength);
					string? relativeDirectory = Path.GetDirectoryName(relativePath);
					if (relativeDirectory is not null)
						Directory.CreateDirectory(Path.Combine(folder, relativeDirectory));
					filemate.ExtractToFile(Path.Combine(folder, relativePath), true);
				}
			}
		}
		catch (Exception e) {
			Program.Logger.Error(e);
			PopupWindow popup = new PopupWindow(this, "Error!",
				"Failed to import from ZIP. This file might not be a ZIP file.", "Damn");
			popup.Dialog();
			return;
		}
	}
}