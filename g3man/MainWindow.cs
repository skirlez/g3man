using System.Diagnostics;
using System.Runtime.InteropServices;
using g3man;
using Gtk;

public class MainWindow : Window
{
    private CheckButton isolateSaveCheck;
    private Entry gameDirectoryEntry;
    
    private ListBox gamesList;
    private ListBox modsList;
    private Stack modsListStack;
    
    private Label noModsLabel;
    private Label noGameLabel;


    private Label nothingAutoDetectedLabel;
    private Label noGamesAddedLabel;

    private Game? currentGame;
    private Label currentGameLabel;
    
    private Box[] allBoxes;
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
        Box modsPage = Box.New(Orientation.Vertical, 0);
        Box settingsPage = Box.New(Orientation.Vertical, 0);
        Box aboutPage = Box.New(Orientation.Vertical, 0);
        
        allBoxes = [gamesPage, modsPage, settingsPage, aboutPage];
        pageNames = ["games", "mods", "settings", "about"];
        pageTitles = ["Games", "Mods", "Settings", "About"];


        DisplayCategories(1);
        
        Box pageBox = Box.New(Orientation.Horizontal, 0);
        pageBox.Append(pageSidebar);
        pageBox.Append(pageStack);
        pageBox.SetHomogeneous(false);
        pageStack.SetVisibleChild(gamesPage);
        
        SetupGamesPage(gamesPage);
        SetupModsPage(modsPage);
        SetupSettingsPage(settingsPage);
        SetupAboutPage(aboutPage);
        
        Debug.Assert(modsList is not null
            && isolateSaveCheck is not null
            && gameDirectoryEntry is not null);
        
        
        currentGameLabel = Label.New("No game selected");
        currentGameLabel.SetMargin(10);
        currentGameLabel.SetHalign(Align.Center);
        
        
        Box currentGameBox = Box.New(Orientation.Horizontal, 0);
        currentGameBox.Append(currentGameLabel);
        currentGameBox.SetHalign(Align.Center);
        currentGameBox.SetHexpand(true);


        Box programBox = Box.New(Orientation.Vertical, 0);
        programBox.Append(pageBox);
        programBox.Append(Separator.New(Orientation.Horizontal));
        programBox.Append(currentGameBox);
        
        SetChild(programBox);
    }

    private void DisplayCategories(int amount) {
        foreach (Box box in allBoxes) {
            if (box.IsAncestor(pageStack))
                pageStack.Remove(box);
        }

        for (int i = 0; i < amount; i++) {
            pageStack.AddTitled(allBoxes[i], pageNames[i], pageTitles[i]);
        }
    }
    
    public void AddToGamesList(Game game) {
        Label gameNameLabel = Label.New(game.DisplayName);
        Button selectGameButton = Button.NewWithLabel("Select");
        selectGameButton.OnClicked += (_, _) =>
        {
            SelectGame(game);
        };

        Box box = Box.New(Orientation.Horizontal, 10);
        box.Append(gameNameLabel);
        box.Append(selectGameButton);
        box.SetValign(Align.Center);
        
        ListBoxRow row = ListBoxRow.New();
        
        row.SetChild(box);
        row.SetActivatable(false);
        row.SetMargin(10);
        
        gamesList.Append(row);
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
        gameDirectoryBox.SetHalign(Align.Start);
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
        isolateSaveCheck = CheckButton.New();
        isolateSaveCheck.SetHalign(Align.Start);
        isolateSaveCheck.Label = "Separate modded save";
        isolateSaveCheck.SetTooltipText("Separates your vanilla save from your modded save.");
        isolateSaveCheck.OnToggled += (sender, args) => {
            State.Get().SeparateModdedSave = sender.Active;
        };
        
        Button saveSettingsButton = Button.New();
        saveSettingsButton.Label = "Save Settings";
        saveSettingsButton.SetHalign(Align.End);
        saveSettingsButton.SetValign(Align.End);
        saveSettingsButton.SetVexpand(true);
        
        page.Append(isolateSaveCheck);
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
        /*
        List<Mod> mods = Mod.ParseMods(Path.Combine(State.Get().GameInstallDirectory, "mods"));
        modsList.RemoveAll();
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
        */
        
    }

    private void SelectGame(Game game) {
        currentGameLabel.SetText(game.DisplayName);
        DisplayCategories(allBoxes.Length);
        Program.DataLoader.LoadAsync(game);
        currentGame = game;
    }

    public Game? GetGame() {
        return currentGame;
    }

}