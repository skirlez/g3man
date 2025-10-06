using System.Diagnostics;
using g3man;
using Gtk;

public class MainWindow : Window
{
    private Box modsPage;
    private Box settingsPage;
    private Box aboutPage;

    private CheckButton isolateSaveCheck;
    private Entry gameDirectoryEntry;

    private Stack pageStack;

    private ListBox modsList;
    
    public MainWindow() {
        Title = "g3man";
        SetDefaultSize(300, 300);
        pageStack = Stack.New();
        pageStack.SetHexpand(true);
        
        StackSidebar pageSidebar = StackSidebar.New();
        pageSidebar.SetStack(pageStack);
        pageSidebar.SetValign(Align.Fill);
        pageSidebar.SetVexpand(true);
        
        modsPage = Box.New(Orientation.Vertical, 0);
        settingsPage = Box.New(Orientation.Vertical, 0);
        aboutPage = Box.New(Orientation.Vertical, 0);
        
        Box[] boxes = { modsPage, settingsPage, aboutPage };
        string[] pageNames = ["mods", "settings", "about"];
        string[] pageTitles = ["Mods", "Settings", "About"];
        
        for (int i = 0; i < pageNames.Length; i++) {
            pageStack.AddTitled(boxes[i], pageNames[i], pageTitles[i]);
        }
        
        Box pageBox = Box.New(Orientation.Horizontal, 0);
        pageBox.Append(pageSidebar);
        pageBox.Append(pageStack);
        pageBox.SetHomogeneous(false);
        pageStack.SetVisibleChild(modsPage);
        
        SetupModsPage(modsPage);
        SetupSettingsPage(settingsPage);
        SetupAboutPage(aboutPage);
        
        Debug.Assert(modsList is not null
            && isolateSaveCheck is not null
            && gameDirectoryEntry is not null);
        
        SetChild(pageBox);
    }


    private void SetupModsPage(Box page) {
        ListBoxRow row = ListBoxRow.New();
        Label test = Label.New("test");
        row.SetChild(test);
        modsList = ListBox.New();
        modsList.SetHexpand(true);
        modsList.Append(page);
        modsList.Append(row);
        
        
        Box manageModsBox = Box.New(Orientation.Horizontal, 5);
        manageModsBox.SetHalign(Align.Center);
        manageModsBox.SetValign(Align.Center);
        
        Button openModsFolderButton = Button.New();
        openModsFolderButton.Label = "Open mods folder";
        
        Button refreshButton = Button.New();
        refreshButton.Label = "Refresh";
        
        Button moveModsUp = Button.New();
        moveModsUp.Label = "↑";
        Button moveModsDown = Button.New();
        moveModsDown.Label = "↓";
        
        manageModsBox.Append(openModsFolderButton);
        manageModsBox.Append(refreshButton);
        manageModsBox.Append(moveModsUp);
        manageModsBox.Append(moveModsDown);

        
        page.Append(modsList);
        page.Append(manageModsBox);
    }

    private void SetupSettingsPage(Box page) {
        Label gameDirectoryLabel = Label.New("Game install directory");
        gameDirectoryLabel.SetHalign(Align.Start);
        
        gameDirectoryEntry = Entry.New();
        gameDirectoryEntry.SetHalign(Align.Start);
        gameDirectoryEntry.SetMaxWidthChars(75);
        
        
        Label statusLabel = Label.New("");
        statusLabel.SetHalign(Align.Start);

        Box gameDirectoryBox = Box.New(Orientation.Vertical, 0);
        gameDirectoryBox.SetHalign(Align.Start);
        gameDirectoryBox.Append(gameDirectoryLabel);
        gameDirectoryBox.Append(gameDirectoryEntry);
        gameDirectoryBox.Append(statusLabel);
        gameDirectoryBox.SetMarginBottom(20);
        void OnTextChanged() {
            statusLabel.SetText("balls");
        }

        Debug.Assert(gameDirectoryEntry.Buffer is not null);  // on everybody's soul
        
        gameDirectoryEntry.Buffer.OnDeletedText += (_, _) => {
            OnTextChanged();
        };
        gameDirectoryEntry.Buffer.OnInsertedText += (_, _) => {
            OnTextChanged();
        };
        
        isolateSaveCheck = CheckButton.New();
        isolateSaveCheck.SetHalign(Align.Start);
        isolateSaveCheck.Label = "Separate modded save";
        isolateSaveCheck.SetTooltipText("Separates your vanilla save from your modded save. This is highly recommended.");

        
        Button saveSettingsButton = Button.New();
        saveSettingsButton.Label = "Save Settings";
        saveSettingsButton.SetHalign(Align.End);
        saveSettingsButton.SetValign(Align.End);
        saveSettingsButton.SetVexpand(true);
        
        page.Append(gameDirectoryBox);
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
}