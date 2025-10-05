using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace forgery_manager;

public partial class ModsPage : UserControl {
    public ModsPage() {
        InitializeComponent();
        ModsList.ItemsSource = new ModEntry[]
        {
            new ModEntry(new Mod("Mod 1")), new ModEntry(new Mod("Mod 2"))
        };
        
    }
}