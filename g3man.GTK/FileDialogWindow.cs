using Gio;
using Gtk;

namespace g3man.GTK;

public class FileDialogWindow {
	private FileDialog dialog;
	private Action<Gio.File> callback;
	public FileDialogWindow(string title, List<FileFilter> filters, Action<Gio.File> callback, string? initialFolder = null) {
		dialog = FileDialog.New();
		dialog.Title = title;
		if (initialFolder is not null)
			dialog.SetInitialFolder(FileHelper.NewForPath(initialFolder));
		this.callback = callback;
		
		FileFilter allFilter = FileFilter.New();
		allFilter.SetName("All Files");
		allFilter.AddPattern("*");
			
		Gio.ListStore filtersStore = Gio.ListStore.New(FileFilter.GetGType());
		filtersStore.Append(allFilter);
		foreach (FileFilter filter in filters)
			filtersStore.Append(filter);
		dialog.SetFilters(filtersStore);
		dialog.SetDefaultFilter(allFilter);
		
	}

	public void Dialog(Window window) {
		Task<Gio.File?> task = dialog.OpenAsync(window);
		task.GetAwaiter().OnCompleted(() => {
			if (!task.IsCompletedSuccessfully)
				return;
			Gio.File file = task.Result!;
			callback(file);
		});
	}
}