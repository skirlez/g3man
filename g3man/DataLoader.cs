using System.Diagnostics;
using g3man.Models;
using g3man.Util;
using UndertaleModLib;
using UndertaleModLib.Decompiler;

namespace g3man;


/** Responsible for the loading and preloading of the current game's data.win */
public class DataLoader {
	private volatile UndertaleData? data;
	private readonly MemoryStream dataMemory = new MemoryStream();
	private string lastHash = "";
	public readonly LoaderLock Lock = new LoaderLock(LoaderAction.Proceed, null, false);
	private readonly Logger logger;
	
	public DataLoader() {
		logger = new Logger("DATALOADER");
		Thread thread = new Thread(() => {
			string path;
			LoaderAction action;
			UndertaleData readData = null!;
			while (true) {
				lock (Lock) {
					if (Lock.Action == LoaderAction.Restart) {
						if (readData is not null)
							logger.Debug("Told to restart. Discarding: " + readData.GeneralInfo.DisplayName.Content);
						else
							logger.Debug("Told to restart. Discarding nothing.");
						Lock.Action = LoaderAction.Proceed;
					}
					else {
						if (readData is not null) {
							logger.Debug("Loaded data of " + readData.GeneralInfo.DisplayName.Content);
							data = readData;
						}

						logger.Debug("Waiting (idle)");
						Lock.IsLoading = false;
						Monitor.PulseAll(Lock);
						Monitor.Wait(Lock);
						Lock.IsLoading = true;
						logger.Debug("Loading data");
					}
					
					Debug.Assert(Lock.Path is not null);
					path = Lock.Path;
					action = Lock.Action;
				}
				
				if (action == LoaderAction.Proceed) {
					try {
						dataMemory.SetLength(0);
						{
							using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
							stream.CopyTo(dataMemory);
						}
						readData = UndertaleIO.Read(dataMemory);
					}
					catch (Exception e) {
						logger.Debug("Failed to load datafile: " + e.Message);
					}
				}
				else if (action == LoaderAction.Clone) {
					dataMemory.Position = 0;
					readData = UndertaleIO.Read(dataMemory);
				}
			}
		});
		thread.IsBackground = true;
		thread.Start();
	}


	public bool CanSnatch() {
		return !Lock.IsLoading && Lock.Action != LoaderAction.Restart;
	}
	public UndertaleData Snatch() {
		Debug.Assert(Monitor.IsEntered(Lock));
		Debug.Assert(CanSnatch());
		Debug.Assert(data is not null);
		Lock.Action = LoaderAction.Clone;
		UndertaleData bye = data!;
		Monitor.PulseAll(Lock);
		return bye;
	}
	
	
	
	public void Assume(UndertaleData newData) {
		logger.Debug("Assuming data " + newData.GeneralInfo.DisplayName.Content);
		lock (Lock) {
			Debug.Assert(!Lock.IsLoading);
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
		Restart,
		Proceed,
		Clone,
	}
}