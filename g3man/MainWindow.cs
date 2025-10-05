using System.Diagnostics;
using Gtk;

public class MainWindow : Window
{
    private Box modsPage;
    private Box settingsPage;
    private Box aboutPage;

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
        
        
        SetupSettingsPage(settingsPage);
        SetupAboutPage(aboutPage);
        
        SetChild(pageBox);
    }


    private void SetupSettingsPage(Box page) {
        Label gameDirectoryLabel = Label.New("Game install directory");
        gameDirectoryLabel.SetHalign(Align.Start);
        
        Entry gameDirectoryEntry = Entry.New();
        gameDirectoryEntry.SetHalign(Align.Start);
        gameDirectoryEntry.SetMaxWidthChars(75);
        
        Label statusLabel = Label.New("");
        statusLabel.SetHalign(Align.Start);

        void OnTextChanged() {
            statusLabel.SetText("balls");
        }

        Debug.Assert(gameDirectoryEntry.Buffer is not null);  // on everybody's soul
        
        gameDirectoryEntry.Buffer.OnDeletedText += (buffer, args) => {
            OnTextChanged();
        };
        gameDirectoryEntry.Buffer.OnInsertedText += (buffer, args) => {
            OnTextChanged();
        };
        
        
        page.Append(gameDirectoryLabel);
        page.Append(gameDirectoryEntry);
        page.Append(statusLabel);
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