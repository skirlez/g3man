using System.Diagnostics;
using UndertaleModLib;

namespace g3man;


/** Responsible for the loading and preloading of the current game's data.win */
public class DataLoader {
	private volatile UndertaleData? data;
	private Game? game;
	private readonly LoaderLock loaderLock = new LoaderLock(LoaderAction.Proceed, null, false);
	
	private readonly Logger logger;
	
	public DataLoader()
	{
		logger = new Logger("DATALOADER");
		new Thread(() => {
			lock (loaderLock)
				Monitor.Wait(loaderLock);
			while (true) {
				string path;
				lock (loaderLock)
				{
					loaderLock.IsLoading = true;
					logger.Debug("Loading data");
					Debug.Assert(loaderLock.Path is not null);
					path = loaderLock.Path;
				}


				UndertaleData? readData = null;

				using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read)) {
					try {
						readData = UndertaleIO.Read(stream);
					}
					catch (Exception e) {
						logger.Debug("Failed to load datafile: " + e.Message);
					}
				}

				lock (loaderLock) {
					if (readData != null) {
						if (loaderLock.Action != LoaderAction.Discard) {
							if (loaderLock.Action == LoaderAction.Restart) {
								logger.Debug("Told to restart for another game. Discarding: " +
												  readData.GeneralInfo.DisplayName.Content);
								loaderLock.Action = LoaderAction.Proceed;
								continue;
							}
						
							logger.Debug("Loaded data of " + readData.GeneralInfo.DisplayName);
							data = readData;
						}
						else
							logger.Debug("Told to discard. Discarding: " + readData.GeneralInfo.DisplayName.Content);
					}

					logger.Debug("Waiting (idle)");
					loaderLock.IsLoading = false;
					Monitor.Wait(loaderLock);
				}
			}
		}).Start();
	}
	
	public UndertaleData? GetData() {
		return data;
	}

	public void Assume(Game newGame, UndertaleData newData) {
		logger.Debug("Assuming data " + newGame.DisplayName);
		lock (loaderLock) {
			if (loaderLock.IsLoading) {
				logger.Debug("We're loading so discard the result");
				loaderLock.Action = LoaderAction.Discard;
			}
			data = newData;
			game = newGame;
		}
		

		
	}
	
	public void LoadAsync(Game newGame) {
		logger.DebugNewline("");
		logger.Debug("New request for " + newGame.DisplayName);
		// Does not matter when this happens
		if (game != null && game.HasSameData(newGame)) {
			logger.Debug("Same data as what's already loaded or being loaded");
			game = newGame;
			return;
		}

		lock (loaderLock) {
			game = newGame;
			loaderLock.Path = game.GetCleanDatafilePath();
			
			if (loaderLock.IsLoading) {
				logger.Debug("Telling loader to load new game after it is done with this one");
				loaderLock.Action = LoaderAction.Restart;
			}
			else {
				loaderLock.Action = LoaderAction.Proceed;
			}

			logger.Debug("Waking up thread to load the data");
			Monitor.Pulse(loaderLock);
		}
	}


	private class LoaderLock(LoaderAction action, string? path, bool isLoading) {
		public LoaderAction Action = action;
		public string? Path = path;
		public bool IsLoading = isLoading;
	}

	private enum LoaderAction
	{
		Discard,
		Restart,
		Proceed,
	}
}