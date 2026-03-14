using System.Diagnostics;
using System.Runtime.InteropServices;
using Gdk;
using Gtk;

#if WINDOWS
    using GdkWin32;
    using Win32;
#endif

namespace g3man.UI;

public class G3manWindow : Window {
    protected G3manWindow() {
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

        
        private enum WindowTitlebarColor : uint {
            Default = DWMWA_COLOR_DEFAULT,
            GtkDark = 0x002D2D2D,
            AdwaitaDark = 0x00201D1D,
        }
        private static WindowTitlebarColor getAppropriateColor() {
            if (Program.InitializedUsing == Program.Initializer.Gtk4) {
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