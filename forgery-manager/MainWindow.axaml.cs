using Avalonia.Controls;
using Avalonia.Interactivity;

namespace forgery_manager;

public partial class MainWindow : Window {
    public MainWindow() {
        InitializeComponent();
    }

    private void ModsButtonClicked(object sender, RoutedEventArgs e)
    {
        Page.Content = new TextBlock() { Text = "Mods" };
    }
}