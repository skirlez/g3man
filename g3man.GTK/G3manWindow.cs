using System.Diagnostics;
using System.Runtime.InteropServices;
using g3man.Core;
using Gdk;
using Gtk;
#if THEMABLE_TITLEBAR
    #if WINDOWS
        using System.Runtime.InteropServices;
        using System.Diagnostics;
        using GdkWin32;
        using Win32;
        using Gdk;
    #endif
#endif
namespace g3man.GTK;
public class G3manWindow : Window {
    protected G3manWindow() {
        OnCloseRequest += (_, _) => !UI.CanDo(UI.Operation.CloseWindow);
        
        #if THEMABLE_TITLEBAR
            OnRealize += (_, _) => ApplyCurrentThemeToTitlebar();
        #endif
    }
    
    #if THEMABLE_TITLEBAR
        [DllImport("dwmapi.dll")]
        private static extern uint DwmSetWindowAttribute(
            IntPtr hwnd,
            uint dwAttribute,
            ref uint pvAttribute,
            uint cbAttribute 
        );
        
        private const uint DWMWA_CAPTION_COLOR = 35;
        private const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFF;

        // We can't predict the user theme ahead of time - GtkDark just lines up with Breeze -
        // but I think if you're using libadwaita colors are OK to assume, so AdwaitaDark lines up with Adwaita.
        // It's a bit silly to support both for windows...
        private enum WindowTitlebarColor : uint {
            Default = DWMWA_COLOR_DEFAULT,
            GtkDark = 0x002D2D2D,
            AdwaitaDark = 0x00201D1D,
        }
        private static WindowTitlebarColor getAppropriateColor() {
            if (UI.InitializedUsing == Initializer.Gtk4) {
                Settings? settings = Settings.GetDefault();
                if (settings is not null && settings.GtkInterfaceColorScheme == InterfaceColorScheme.Dark)
                    return WindowTitlebarColor.GtkDark;
                return WindowTitlebarColor.Default;
            }
            return Adw.StyleManager.GetDefault().GetColorScheme() switch {
                Adw.ColorScheme.Default 
                    or Adw.ColorScheme.ForceLight 
                    or Adw.ColorScheme.PreferLight => WindowTitlebarColor.Default,
                Adw.ColorScheme.ForceDark 
                    or Adw.ColorScheme.PreferDark => WindowTitlebarColor.AdwaitaDark,
                _ => throw new UnreachableException()
            };
        }
        private static void ApplyCurrentThemeToTitlebar(HWND hwnd) {
            uint cast = (uint)getAppropriateColor();
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref cast, sizeof(uint));
        }
		    
        protected void ApplyCurrentThemeToTitlebar() {
            Debug.Assert(OperatingSystem.IsWindows());
            Surface? surface = GetSurface();
            if (surface is null)
                return;
            if (!Win32Surface.IsWin32(surface))
                return;
            ApplyCurrentThemeToTitlebar(Win32Surface.GetImplHwnd(surface));
        }
    #endif
}