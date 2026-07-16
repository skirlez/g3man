using System.Collections.Concurrent;
using g3man.GTK.MainUI;
using g3man.Core.Models;
using g3man.Core.Util;
using g3man.GTK.Util;
using Gtk;
using Pango;

namespace g3man.GTK;

public class GameAutoDetectWindow : G3manWindow {
	private MainWindow owner;
	public GameAutoDetectWindow(MainWindow owner, List<Game> existingGames) {
		SetSizeRequest(350, 300);
		this.owner = owner;
		
		Label nothingAutoDetectedLabel = Label.New("No games were auto-detected");
		nothingAutoDetectedLabel.SetMargin(20);
		
		ListBox autoDetectedListBox = ListBox.New();
		autoDetectedListBox.SetSelectionMode(SelectionMode.None);
		autoDetectedListBox.SetPlaceholder(nothingAutoDetectedLabel);
		
		ScrolledWindow autoDetectedWindow = ScrolledWindow.New();
		autoDetectedWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
		autoDetectedWindow.SetPropagateNaturalWidth(true);
		autoDetectedWindow.SetChild(autoDetectedListBox);

		List<string> existingPaths = UI.Config.GameEntries.Select(x => x.Path).ToList();
		List<string> paths = ProgramPaths.GuessPossibleGamePaths();
		ConcurrentBag<string> gamePaths = new ConcurrentBag<string>();
		Parallel.ForEach(paths, path => {
			if (existingPaths.Any(x => ProgramPaths.FolderPathsEqual(x, path)))
				return;
			Status status = ProgramPaths.GameMakerDirectoryStatus(path);
			if (status.ok) {
				gamePaths.Add(path);
			}
		});

		foreach (string path in gamePaths) {
			ListBoxRow row = ListBoxRow.New();
			Label l = Label.New(path);
			l.SetEllipsize(EllipsizeMode.Start);

			Button button = Button.NewWithLabel("Add");
			button.OnClicked += (_, _) => {
				GameAdderWindow adderWindow = new GameAdderWindow(path, owner);
				adderWindow.Dialog(this);
				autoDetectedListBox.Remove(row);
			};
			button.SetMargin(5);
			button.SetMarginEnd(20);

			Box spacer = Box.New(Orientation.Horizontal, 0);
			spacer.SetHexpand(true);

			Box box = Box.New(Orientation.Horizontal, 5);
			box.SetHexpand(true);

			box.Append(l);
			box.Append(spacer);
			box.Append(button);
			row.SetChild(box);
			autoDetectedListBox.Append(row);
		}

		SetChild(autoDetectedWindow);
	}
	public void Dialog() {
		SetTransientFor(owner);
		SetModal(true);
		Present();
	}
}