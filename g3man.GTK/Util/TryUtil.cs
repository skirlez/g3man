using g3man.Core.Util;
using Gtk;

namespace g3man.GTK.Util;

public static class TryUtil {
	public static async Task TryOpeningFileExplorer(Window window, string path) {
		await PopupWindow.PopupIfError(window, async Task () => {
			await Task.Run(() => IO.OpenFileExplorer(path));
		});
	}
}