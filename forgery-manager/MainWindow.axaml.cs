using Avalonia.Controls;
using Avalonia.Interactivity;

namespace forgery_manager;

public partial class MainWindow : Window
{
    private ModsPage modsPage;
    private AboutPage aboutPage;
    private SettingsPage settingsPage;
    public MainWindow() {
        modsPage = new ModsPage();
        aboutPage = new AboutPage();
        settingsPage = new SettingsPage();
        InitializeComponent();
    }

    private void ModsButtonClicked(object sender, RoutedEventArgs e) {
        Page.Content = modsPage;
    }
    private void SettingsButtonClicked(object sender, RoutedEventArgs e) {
        Page.Content = settingsPage;
    }
    
    private void AboutButtonClicked(object sender, RoutedEventArgs e) {
        Page.Content = aboutPage;
    }
}