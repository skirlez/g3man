using System.Diagnostics;
using g3man.Models;
using g3man.Util;
using UndertaleModLib;
using UndertaleModLib.Decompiler;

namespace g3man;


/** Responsible for the loading and preloading of the current game's data.win */
public class DataLoader {
	private volatile UndertaleData? data;
	private volatile GlobalDecompileContext? context;
	private string lastHash = "";
	public readonly LoaderLock Lock = new LoaderLock(LoaderAction.Proceed, null, false);
	
	private readonly Logger logger;
	
	public DataLoader() {
		logger = new Logger("DATALOADER");
		Thread thread = new Thread(() => {
			lock (Lock)
				Monitor.Wait(Lock);
			while (true) {
				string path;
				lock (Lock) {
					Lock.IsLoading = true;
					logger.Debug("Loading data");
					Debug.Assert(Lock.Path is not null);
					path = Lock.Path;
				}


				UndertaleData? readData = null;
				try {
					using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
					readData = UndertaleIO.Read(stream);
				}
				catch (Exception e) {
					logger.Debug("Failed to load datafile: " + e.Message);
				}
				context = new GlobalDecompileContext(data);
				
				lock (Lock) {
					if (Lock.Action == LoaderAction.Restart) {
						if (readData != null)
							logger.Debug("Told to restart. Discarding: " +
							             readData.GeneralInfo.DisplayName.Content);
						else
							logger.Debug("Told to restart. Discarding game that failed to load.");

						Lock.Action = LoaderAction.Proceed;
						continue;
					}
					if (readData != null) {
						if (Lock.Action != LoaderAction.Discard) {
							logger.Debug("Loaded data of " + readData.GeneralInfo.DisplayName.Content);
							data = readData;
						}
						else
							logger.Debug("Told to discard. Discarding: " + readData.GeneralInfo.DisplayName.Content);
					}

					logger.Debug("Waiting (idle)");
					Lock.IsLoading = false;
					Monitor.Wait(Lock);
				}
			}
		});
		thread.IsBackground = true;
		thread.Start();
	}
	
	public UndertaleData? GetData() {
		return data;
	}

	public GlobalDecompileContext? GetDecompileContext() {
		return context;
	}
	
	public void Assume(UndertaleData newData) {
		logger.Debug("Assuming data " + newData.GeneralInfo.DisplayName.Content);
		lock (Lock) {
			if (Lock.IsLoading) {
				logger.Debug("We're loading so discard the result");
				Lock.Action = LoaderAction.Discard;
			}
			data = newData;
		}
	}
	
	public void LoadAsync(Game newGame) {
		logger.DebugNewline();
		logger.Debug("New request for " + newGame.DisplayName);
		if (newGame.Hash == lastHash) {
			logger.Debug("Same data as what's already loaded or being loaded");
			return;
		}

		lock (Lock) {
			Lock.Path = newGame.GetCleanDatafilePath();
			lastHash = newGame.Hash;
			
			if (Lock.IsLoading) {
				logger.Debug("Telling loader to load new game after it is done with this one");
				Lock.Action = LoaderAction.Restart;
			}
			else
				Lock.Action = LoaderAction.Proceed;

			logger.Debug("Waking up thread to load the data");
			Monitor.PulseAll(Lock);
		}
	}


	public class LoaderLock(LoaderAction action, string? path, bool isLoading) {
		public LoaderAction Action = action;
		public string? Path = path;
		public bool IsLoading = isLoading;
	}

	public enum LoaderAction {
		Discard,
		Restart,
		Proceed,
	}
}