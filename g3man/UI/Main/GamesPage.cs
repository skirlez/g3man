using g3man.Models;
using g3man.Util;
using Gtk;
using Xdelta = g3man.Util.Xdelta;

namespace g3man.UI.Main;

public partial class MainWindow {
	private void SetupGamesPage(Box box) {
		Label gamesLabel = Label.New("Games");
		gamesLabel.SetHalign(Align.Start);
		gamesLabel.SetMarginStart(10);
		gamesLabel.SetMarginTop(10);

		noGamesAddedLabel = Label.New("There are no games added");
		noGamesAddedLabel.SetMargin(10);

		gamesListBox = ListBox.New();
		gamesListBox.SetSelectionMode(SelectionMode.None);
		gamesListBox.SetPlaceholder(noGamesAddedLabel);
		selectGameButtons = [];


		List<Game> games = Game.Parse(Program.Config.GameDirectories);
		games.Sort((game1, game2) => string.Compare(game1.DisplayName, game2.DisplayName, StringComparison.Ordinal));
		Program.Config.UpdateGameDirectories(games);
		PopulateGamesList(games);
		
		Label autoDetectedLabel = Label.New("Auto-detected");
		autoDetectedLabel.SetHalign(Align.Start);
		autoDetectedLabel.SetMarginStart(10);
		
		nothingAutoDetectedLabel = Label.New("This feature is not yet implemented");
		nothingAutoDetectedLabel.SetMargin(20);
		
		Stack autodetectedStack = Stack.New();
		autodetectedStack.AddChild(nothingAutoDetectedLabel);
		
		Label manualLabel = Label.New("Manually add game");
		manualLabel.SetHalign(Align.Start);
		manualLabel.SetMarginStart(10);
		
		gameDirectoryEntry = Entry.New();
		gameDirectoryEntry.SetHalign(Align.Start);

		gameDirectoryEntry.SetMaxWidthChars(75);
		Button browseButton = Button.NewWithLabel("Browse");
		browseButton.OnClicked += (_, _) => {
			FileDialog dialog = new FileDialog();
			dialog.Title = "Select a GameMaker game's folder";
			Task<Gio.File?> task = dialog.SelectFolderAsync(this);
			task.GetAwaiter().OnCompleted(() => {
				if (!task.IsCompletedSuccessfully)
					return;
				Gio.File file = task.Result!;
				gameDirectoryEntry.SetText(file.GetPath() ?? "");
			});
		};
		
		Box gameDirectoryEntryBox = Box.New(Orientation.Horizontal, 10);
		gameDirectoryEntryBox.Append(browseButton);
		gameDirectoryEntryBox.Append(gameDirectoryEntry);
		
		Label statusLabel = Label.New("");
		statusLabel.SetHalign(Align.Start);
		
		Box gameDirectoryBox = Box.New(Orientation.Vertical, 0);
		gameDirectoryBox.SetHalign(Align.Center);
		gameDirectoryBox.Append(gameDirectoryEntryBox);
		gameDirectoryBox.Append(statusLabel);
		gameDirectoryBox.SetMargin(20);
		gameDirectoryBox.SetMarginBottom(5);
		void OnTextChanged(string text) {
			if (text == "")
				statusLabel.SetText("");
			else {
				PathStatus status = ProgramPaths.GameMakerDirectoryStatus(text);
				statusLabel.SetText(status.message);
			}
		}
		
		Button addGameButton = Button.NewWithLabel("Add game");
		addGameButton.SetMarginBottom(10);
		addGameButton.SetHalign(Align.Center);
		addGameButton.OnClicked += (sender, args) => {
			GameAdderWindow adderWindow = new GameAdderWindow(gameDirectoryEntry.GetText(), this);
			adderWindow.Dialog();
		};
		
		gameDirectoryEntry.GetBuffer().OnDeletedText += (buffer, args) => {
			string text = buffer.GetText();
			OnTextChanged(text.Remove((int)args.Position, (int)args.NChars));
		};
		gameDirectoryEntry.GetBuffer().OnInsertedText += (buffer, _) => {
			OnTextChanged(buffer.GetText());
		};
		
		box.Append(gamesLabel);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(gamesListBox);
		box.Append(autoDetectedLabel);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(autodetectedStack);

		box.Append(manualLabel);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(gameDirectoryBox);
		box.Append(addGameButton);
	}
	
	private void PopulateGamesList(List<Game> games, Game? selectedGame = null) {
		selectGameButtons.Clear();
		
		gamesListBox.RemoveAll();
		gamesListBox.SetPlaceholder(noGamesAddedLabel);
		
		foreach (Game game in games) {
			AddToGamesList(game, selectedGame == game);
		}
	}
	
	public void AddToGamesList(Game game, bool selected) {
		Label gameNameLabel = Label.New(game.DisplayName);
		
		Box spacer = Box.New(Orientation.Horizontal, 0);
		spacer.SetHexpand(true);
		
		Button selectGameButton = Button.NewWithLabel("Select");
		
		selectGameButton.OnClicked += (button, _) => {
			SelectGame(game, button);
		};
		selectGameButton.SetSensitive(!selected);
		selectGameButtons.Add(selectGameButton);


		
		Box box = Box.New(Orientation.Horizontal, 0);
		box.Append(gameNameLabel);
		box.Append(spacer);
		box.Append(selectGameButton);
	
	
		box.SetValign(Align.Center);
		
		
		ListBoxRow row = ListBoxRow.New();
		
		row.SetChild(box);
		row.SetActivatable(false);
		row.SetMargin(10);
		
		gamesListBox.Append(row);
	}
	
	private void SelectGame(Game game, Button buttonPressed) {
		foreach (Button button in selectGameButtons) {
			button.SetSensitive(true);
		}
		buttonPressed.SetSensitive(false);
		
		Program.SetGame(game);
		currentGameLabel.SetText(game.DisplayName);
		ParseProfilesAndUpdateMenu();
		if (Program.GetProfile() is not null) {
			List<Xdelta> xdeltas = Xdelta.GetDatafileXdeltaPatches(modsList.Where(m => enabledMods.ContainsKey(m)), Program.CurrentProfileFolderPath());
			Program.DataLoader.LoadAsync(game, xdeltas);
		}
	}
	
}