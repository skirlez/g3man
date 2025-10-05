using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace forgery_manager;

public partial class ModEntry : UserControl
{
    public ModEntry(Mod mod)
    {
        InitializeComponent();
        ModName.Text = mod.name;
    }
}