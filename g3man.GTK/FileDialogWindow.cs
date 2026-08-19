using Gio;
using Gtk;


namespace g3man.GTK;

public static class FileDialogWindow {
	public static async Task<Gio.File?> Dialog(Window window, string title, List<FileFilter> filters, string? initialFolder = null) {
		FileDialog dialog = FileDialog.New();
		dialog.Title = title;
		if (initialFolder is not null)
			dialog.SetInitialFolder(FileHelper.NewForPath(initialFolder));

		FileFilter allFilter = FileFilter.New();
		allFilter.SetName("All Files");
		allFilter.AddPattern("*");
			
		Gio.ListStore filtersStore = Gio.ListStore.New(FileFilter.GetGType());
		filtersStore.Append(allFilter);
		foreach (FileFilter filter in filters)
			filtersStore.Append(filter);
		dialog.SetFilters(filtersStore);
		dialog.SetDefaultFilter(allFilter);

		try {
			return await dialog.OpenAsync(window);
		}
		catch (GLib.GException) {
			return null;
		}
	}
}