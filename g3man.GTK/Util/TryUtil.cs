using g3man.Core.Util;
using Gtk;

namespace g3man.GTK.Util;

public static class TryUtil {
	public static void TryOpeningFileExplorer(Window window, string path) {
		PopupWindow.PopupIfError(window, () => {
			IO.OpenFileExplorer(path);
		});
	}
}