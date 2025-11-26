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
	public readonly LoaderLock Lock = new LoaderLock();
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
						Lock.Errored = false;
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
						logger.Debug("Failed to load datafile: " + e);
						Lock.Errored = true;
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
		return !Lock.IsLoading && Lock.Action != LoaderAction.Restart && !Lock.Errored;
	}
	public bool HasErrored() {
		return Lock.Errored;
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


	public class LoaderLock() {
		public LoaderAction Action = LoaderAction.Proceed;
		public string? Path = null;
		public bool IsLoading = false;
		public bool Errored = false;
	}

	public enum LoaderAction {
		Restart,
		Proceed,
		Clone,
	}
}