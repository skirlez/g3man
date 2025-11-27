using System.Diagnostics;
using System.Security.Cryptography;
using g3man.Models;
using g3man.Util;
using UndertaleModLib;
using UndertaleModLib.Decompiler;

namespace g3man;


/** Responsible for the loading and preloading of the current game's clean data.win
 *
 * Also responsible for the hashing of the current game's "dirty" data.win, to see
 * if the user possibly updated their game
 */
public class DataLoader {
	private volatile UndertaleData? data;
	private volatile string? hash;
	
	private readonly MemoryStream dataMemory = new MemoryStream();
	private string lastHash = "";
	public readonly LoaderLock Lock = new LoaderLock();
	private readonly Logger logger;
	
	public DataLoader() {
		logger = new Logger("DATALOADER");
		Thread thread = new Thread(() => {
			string cleanPath;
			string dirtyPath;
			LoaderAction action;
			UndertaleData readData = null!;
			string readHash = null!;
			
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
							hash = readHash;
						}

						logger.Debug("Waiting (idle)");
						Lock.IsLoading = false;
						Monitor.PulseAll(Lock);
						Monitor.Wait(Lock);
						Lock.IsLoading = true;
						Lock.Errored = false;
						logger.Debug("Loading data");
					}
					
					Debug.Assert(Lock.CleanPath is not null);
					Debug.Assert(Lock.DirtyPath is not null);
					cleanPath = Lock.CleanPath;
					dirtyPath = Lock.DirtyPath;
					action = Lock.Action;
				}

				
				if (action == LoaderAction.Proceed) {
					try {
						dataMemory.SetLength(0);
						{
							using FileStream stream = new FileStream(cleanPath, FileMode.Open, FileAccess.Read);
							stream.CopyTo(dataMemory);
						}
						readData = UndertaleIO.Read(dataMemory);
					}
					catch (Exception e) {
						logger.Debug("Failed to load datafile: " + e);
						Lock.Errored = true;
					}

					try {
						byte[] hashBytes;
						{
							using FileStream stream = new FileStream(dirtyPath, FileMode.Open, FileAccess.Read);
							hashBytes = MD5.Create().ComputeHash(stream);
						}

						readHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
					}
					catch (Exception _) {
						// if file doesn't exist or cannot be read, we don't care that much,
						// this is only being done to make sure users aren't overwriting game updates.
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

	/**
	 * Returns the hash of the data.win of the datafile belonging to the loaded game.
	 * Not to be confused with the clean data.win, this is the hash of the plain data.win/game.unx
	 * that happened to be there at the time of loading. It should be used to check if perhaps the user
	 * has updated the game.
	 *
	 * This method should ONLY be called after obtaining the LoaderLock lock, and while CanSnatch is true.
	 * In case that datafile is not present, this will return an empty string.
	 */
	public string GetDirtyHash() {
		Debug.Assert(Monitor.IsEntered(Lock));
		Debug.Assert(CanSnatch());
		Debug.Assert(hash is not null);
		return hash;
	}
	
	
	public void LoadAsync(Game newGame) {
		logger.DebugNewline();
		logger.Debug("New request for " + newGame.DisplayName);
		if (newGame.Hash == lastHash) {
			logger.Debug("Same data as what's already loaded or being loaded");
			return;
		}

		lock (Lock) {
			Lock.CleanPath = newGame.GetCleanDatafilePath();
			Lock.DirtyPath = newGame.GetOutputDatafilePath();
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
		public string? CleanPath = null;
		public string? DirtyPath = null;
		public bool IsLoading = false;
		public bool Errored = false;
	}

	public enum LoaderAction {
		Restart,
		Proceed,
		Clone,
	}
}