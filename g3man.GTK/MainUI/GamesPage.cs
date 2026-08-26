using g3man.Core.Models;
using g3man.Core.Util;
using g3man.GTK.Util;
using GdkPixbuf;
using Gtk;
using Xdelta = g3man.Core.Util.Xdelta;

namespace g3man.GTK.MainUI;

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
		gamesList = new List<Game>();
		
		
		List<Game> games = Game.ParseAll(UI.Config.GameEntries, (e, entry) => {
			UI.Logger.Error($"Error reading game at {entry.Path}:\n{e.Message}");
		});
		games.Sort((game1, game2) => string.Compare(game1.DisplayName, game2.DisplayName, StringComparison.Ordinal));
		PopulateGamesList(games);
		
		ScrolledWindow gamesListWindow = ScrolledWindow.New();
		gamesListWindow.SetPolicy(PolicyType.Automatic, PolicyType.Automatic);
		gamesListWindow.SetPropagateNaturalHeight(true);
		gamesListWindow.SetChild(gamesListBox);
		
		Label autoDetectedLabel = Label.New("Auto-detect");
		autoDetectedLabel.SetHalign(Align.Start);
		autoDetectedLabel.SetMarginStart(10);
		
		Button autoDetectButton = Button.NewWithLabel("Auto-detect games");
		autoDetectButton.OnClicked += UI.DoOperation<Button>([UI.Operation.TouchingGames, UI.Operation.OpenWindow], (_, _) => {
			GameAutoDetectWindow window = new(this, gamesList);
			window.Dialog();
		});
		autoDetectButton.SetHalign(Align.Center);
		autoDetectButton.SetMargin(10);
		
		Label manualLabel = Label.New("Manually add game");
		manualLabel.SetHalign(Align.Start);
		manualLabel.SetMarginStart(10);
		
		gameDirectoryEntry = Entry.New();
		gameDirectoryEntry.SetHalign(Align.Start);

		gameDirectoryEntry.SetMaxWidthChars(75);
		Button browseButton = Button.NewWithLabel("Browse");
		browseButton.OnClicked += UI.OpenWindowButton(async (_, _) => {
			FileDialog dialog = FileDialog.New();
			dialog.Title = "Select a GameMaker game's folder";
			Gio.File? file = await dialog.SelectFolderAsync(this);
			gameDirectoryEntry.SetText(file?.GetPath() ?? "");
		});
		
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
				Status status = ProgramPaths.GameMakerDirectoryStatus(text);
				statusLabel.SetText(status.message);
			}
		}
		
		Button addGameButton = Button.NewWithLabel("Add game");
		addGameButton.SetMarginBottom(10);
		addGameButton.SetHalign(Align.Center);
		addGameButton.OnClicked += UI.DoOperation<Button>([UI.Operation.TouchingGames, UI.Operation.OpenWindow], (_, _) => {
			GameAdderWindow adderWindow = new(gameDirectoryEntry.GetText(), this);
			adderWindow.Dialog(this);
		});
		
		gameDirectoryEntry.GetBuffer().OnDeletedText += (buffer, args) => {
			string text = buffer.GetText();
			OnTextChanged(text.Remove((int)args.Position, (int)args.NChars));
		};
		gameDirectoryEntry.GetBuffer().OnInsertedText += (buffer, _) => {
			OnTextChanged(buffer.GetText());
		};
		
		box.Append(gamesLabel);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(gamesListWindow);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(autoDetectedLabel);
		box.Append(autoDetectButton);
		box.Append(Separator.New(Orientation.Horizontal));
		box.Append(manualLabel);
		
		box.Append(gameDirectoryBox);
		box.Append(addGameButton);
	}
	
	private void PopulateGamesList(List<Game> games, Game? selectedGame = null) {
		selectGameButtons.Clear();
		
		gamesListBox.RemoveAll();
		gamesListBox.SetPlaceholder(noGamesAddedLabel);

		gamesList.Clear();
		foreach (Game game in games) {
			AddToGamesList(game, selectedGame == game);
		}
	}

	private ListBoxRow MakeGameRow(Game game, bool selected) {
		Label gameNameLabel = Label.New(game.DisplayName);
		
		Box spacer = Box.New(Orientation.Horizontal, 0);
		spacer.SetHexpand(true);
		
		Button selectGameButton = Button.NewWithLabel("Select");
		selectGameButton.OnClicked += UI.DoOperation<Button>([UI.Operation.TouchingGames, UI.Operation.OpenWindow], async (button, _) => {
			EnableExtraCategories(ExtraCategories.None);
			await SelectGame(game, button);
		}, makeInsensitive: false);
		
		selectGameButton.SetSensitive(!selected);
		selectGameButtons.Add(selectGameButton);

		Button manageGameButton = Button.NewWithLabel("Manage");
		manageGameButton.OnClicked += UI.DoOperation<Button>([UI.Operation.TouchingGames, UI.Operation.OpenWindow], (_,_) => {
			if (game.FormatVersion == 1) {
				GameUpgraderWindow upgraderWindow = new GameUpgraderWindow(this, game);
				upgraderWindow.Dialog(this);
				return;
			}
			ManageGameWindow window = new ManageGameWindow(game, this, 
				saveCallback: (Game newGame) => {
					newGame.Write();
					int index = gamesList.IndexOf(game);
					ListBoxRow oldRow = gamesListBox.GetRowAtIndex(index)!;
					ListBoxRow newRow = MakeGameRow(newGame, (UI.GetGame() == game));
					gamesListBox.Remove(oldRow);
					gamesList.RemoveAt(index);
					gamesListBox.Insert(newRow, index);
					gamesList.Insert(index, newGame);
					UI.SetGame(newGame);
					return true;
				}, 
				removeCallback: () => {
					int index = gamesList.IndexOf(game);
					if (game == UI.GetGame())
						EnableExtraCategories(ExtraCategories.None);
					UI.RemoveGameEntry(game.Entry);
					ListBoxRow row = gamesListBox.GetRowAtIndex(index)!;
					gamesListBox.Remove(row);
					gamesList.RemoveAt(index);
					UI.SetGame(null);
					return true;
				});
			window.Dialog();
		});

		
		Box box = Box.New(Orientation.Horizontal, 10);
		box.Append(gameNameLabel);
		box.Append(spacer);
		box.Append(manageGameButton);
		box.Append(selectGameButton);
	
		box.SetValign(Align.Center);
		
		
		ListBoxRow row = ListBoxRow.New();
		
		row.SetChild(box);
		row.SetActivatable(false);
		row.SetMargin(10);
		return row;
	}
	
	public void AddToGamesList(Game game, bool selected) {
		ListBoxRow row = MakeGameRow(game, selected);
		gamesListBox.Append(row);
		gamesList.Add(game);
	}
	/*
	// TODO: BROKEN; also move to a different thread
	public void TryLoadExecutableImage(Game game, Image image) {
		try {
			PeFile file = new PeFile(Path.Combine(game.Directory, game.ExecutablePath));
			GroupIconDirectoryEntry iconGroup = file.Resources!.GroupIconDirectories![0].DirectoryEntries.First();
			int id = iconGroup.NId;
			Icon icon = iconGroup.AssociatedIcons(file)!.First();
			if (icon.Id != id)
				return;
			byte[] bytes = icon.AsRawSpan().ToArray();

			// obtained through trial and error
			int offset = 38;
			
			Span<byte> span = new Span<byte>(bytes, offset, (int)icon.Size - offset);
			
	
			
			int zeroIs256(int num) {
				return num == 0 ? 256 : num;
			}
			
			Pixbuf pixbuf = Pixbuf.NewFromBytes(
				GLib.Bytes.New(span),
				Colorspace.Rgb, false, 8, zeroIs256(iconGroup.BWidth),
				zeroIs256(iconGroup.BHeight), zeroIs256(iconGroup.BWidth) * 3);

			// it is backwards otherwise
			pixbuf = pixbuf.Flip(false)!;
			
			image.SetFromPixbuf(pixbuf);
			image.SetPixelSize(100);


		}
		catch (Exception e) {
			UI.Logger.Debug(e.ToString());
		}
	}
	*/
	private async Task SelectGame(Game game, Button buttonPressed) {
		if (game.FormatVersion == 1) {
			GameUpgraderWindow window = new(this, game);
			window.Dialog(this);
			return;
		}
		foreach (Button button in selectGameButtons) {
			button.SetSensitive(true);
		}
		buttonPressed.SetSensitive(false);
		UI.SetGame(game);
		EnableExtraCategories(ExtraCategories.None);
		//TryLoadExecutableImage(game, currentGameIcon);
		await ParseProfilesAndUpdateMenu();
		if (UI.GetProfile() is not null) {
			// TODO: this should be done when you select a profile too
			List<Xdelta> xdeltas = Xdelta.GetDatafileXdeltaPatches(
				modsList.Where(m => enabledMods.ContainsKey(m)), 
				UI.CurrentProfileFolderPath(), 
				game.DatafilePath);
			UI.DataLoader.LoadAsync(game, xdeltas);
		}
	}
}