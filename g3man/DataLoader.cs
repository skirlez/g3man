using System.Diagnostics;
using g3man.Models;
using g3man.Util;
using UndertaleModLib;

namespace g3man;


/** Responsible for the loading and preloading of the current game's data.win */
public class DataLoader {
	private volatile UndertaleData? data;
	private string lastHash = "";
	private readonly LoaderLock loaderLock = new LoaderLock(LoaderAction.Proceed, null, false);
	
	private readonly Logger logger;
	
	public DataLoader() {
		logger = new Logger("DATALOADER");
		Thread thread = new Thread(() => {
			lock (loaderLock)
				Monitor.Wait(loaderLock);
			while (true) {
				string path;
				lock (loaderLock) {
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
					if (loaderLock.Action == LoaderAction.Restart) {
						if (readData != null)
							logger.Debug("Told to restart. Discarding: " +
							             readData.GeneralInfo.DisplayName.Content);
						else
							logger.Debug("Told to restart. Discarding game that failed to load.");

						loaderLock.Action = LoaderAction.Proceed;
						continue;
					}
					if (readData != null) {
						if (loaderLock.Action != LoaderAction.Discard) {
							logger.Debug("Loaded data of " + readData.GeneralInfo.DisplayName.Content);
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
		});
		thread.IsBackground = true;
		thread.Start();
	}
	
	public UndertaleData? GetData() {
		return data;
	}

	public void Assume(UndertaleData newData) {
		logger.Debug("Assuming data " + newData.GeneralInfo.DisplayName.Content);
		lock (loaderLock) {
			if (loaderLock.IsLoading) {
				logger.Debug("We're loading so discard the result");
				loaderLock.Action = LoaderAction.Discard;
			}
			data = newData;
		}
	}
	
	public void LoadAsync(Game newGame) {
		logger.DebugNewline("");
		logger.Debug("New request for " + newGame.DisplayName);
		if (newGame.Hash == lastHash) {
			logger.Debug("Same data as what's already loaded or being loaded");
			return;
		}

		lock (loaderLock) {
			loaderLock.Path = newGame.GetCleanDatafilePath();
			lastHash = newGame.Hash;
			
			if (loaderLock.IsLoading) {
				logger.Debug("Telling loader to load new game after it is done with this one");
				loaderLock.Action = LoaderAction.Restart;
			}
			else
				loaderLock.Action = LoaderAction.Proceed;

			logger.Debug("Waking up thread to load the data");
			Monitor.Pulse(loaderLock);
		}
	}


	private class LoaderLock(LoaderAction action, string? path, bool isLoading) {
		public LoaderAction Action = action;
		public string? Path = path;
		public bool IsLoading = isLoading;
	}

	private enum LoaderAction {
		Discard,
		Restart,
		Proceed,
	}
}