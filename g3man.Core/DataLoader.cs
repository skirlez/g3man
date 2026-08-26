using System.Diagnostics;
using System.Security.Cryptography;
using g3man.Core.Models;
using g3man.Core.Util;
using UndertaleModLib;
using Xdelta = g3man.Core.Util.Xdelta;

namespace g3man.Core;


/** Responsible for the loading and preloading of the current game's clean data.win
 */
public class DataLoader {
	private Game? lastGame;
	private List<Xdelta>? lastXdeltaPaths;
	public readonly LoaderLock Lock = new();
	private readonly Logger logger;
	
	public DataLoader(Logger.LoggerPipe pipe) {
		logger = Logger.Make("DATALOADER", pipe);
		Thread thread = new(() => {
			UndertaleData? result = null;
			string? hash = null;
			MemoryStream dataMemory = new MemoryStream();
			while (true) {
				string path;
				LoaderAction action;
				List<Xdelta> xdeltas;
				lock (Lock) {
					if (Lock.Action == LoaderAction.Restart) {
						if (result is not null)
							logger.Debug("Told to restart. Discarding: " + result.GeneralInfo?.DisplayName.Content);
						else
							logger.Debug("Told to restart. Discarding nothing.");
						Lock.Action = LoaderAction.Proceed;
					}
					else {
						if (result is not null) {
							logger.Info("Loaded data of " + result.GeneralInfo?.DisplayName.Content);
							logger.Info($"Data has MD5 hash: {hash}");
							Lock.Result = result;
						}
						logger.Debug("Waiting (idle)");
						Lock.IsLoading = false;
						Monitor.PulseAll(Lock);
						Monitor.Wait(Lock);
						Debug.Assert(Lock.IsLoading);
					}
					
					Debug.Assert(Lock.Path is not null);
					path = Lock.Path;
					action = Lock.Action;
					xdeltas = Lock.Xdeltas;

					if (action == LoaderAction.Clone) {
						if (dataMemory.Length == 0) {
							action = LoaderAction.Proceed;
							logger.Debug("Told to clone, but we don't have a clone, so we're loading from disk");
						}
						else {
							logger.Debug("Cloning data in memory");
						}
					}
					else {
						logger.Debug("Loading data");
					}
				}

				
				if (action == LoaderAction.Proceed) {
					dataMemory.Dispose();
					dataMemory = new MemoryStream();
					try {
						if (xdeltas.Count == 0) {
							using FileStream stream = new(path, FileMode.Open, FileAccess.Read);
							stream.CopyTo(dataMemory);
						}
						else {
							logger.Debug($"Found {xdeltas.Count} xdelta patches");
							logger.Debug($"Applying: {xdeltas.First().Filename}");
							int status = xdeltas.First().Decode(path, dataMemory);
							for (int i = 1; i < xdeltas.Count; i++) {
								if (status != 0) {
									break;
								}
								logger.Debug($"Applying: {xdeltas[i].Filename}");
								byte[] bytes = dataMemory.ToArray();
								dataMemory.SetLength(0);
								status = xdeltas[i].DecodeFromMemory(bytes, dataMemory);
							}
							if (status != 0) {
								logger.Error("Xdelta application failed");
								Lock.Errored = true;
								continue;
							}
							logger.Debug("Applied all Xdelta patches");
						}
					}
					catch (Exception e) {
						logger.Error("Failed to read datafile: " + e);
						Lock.Errored = true;
						continue;
					}

					try {
						dataMemory.Seek(0,SeekOrigin.Begin);
						hash = IO.HashToString(MD5.HashData(dataMemory));
						dataMemory.Seek(0,SeekOrigin.Begin);
						result = UndertaleIO.Read(dataMemory);
					}
					catch (Exception e) {
						result = null;
						hash = null;
						logger.Error("Failed to load datafile: " + e);
						Lock.Errored = true;
						continue;
					}
				}
				else if (action == LoaderAction.Clone) {
					// should not fail; this already loaded once
					dataMemory.Position = 0;
					result = UndertaleIO.Read(dataMemory);
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
		Debug.Assert(Lock.Result is not null);
		Lock.Action = LoaderAction.Clone;
		Lock.Errored = false;
		UndertaleData bye = Lock.Result;
		Lock.PulseToLoad();
		return bye;
	}
	
	
	public bool IsAlreadyGiven(Game game, List<Xdelta> xdeltaPaths) {
		Debug.Assert(Monitor.IsEntered(Lock));
		return lastGame is not null
			//&& game.Hash == lastGame.Hash
			&& game == lastGame
			&& Xdelta.SequenceEquals(xdeltaPaths, lastXdeltaPaths!);
	}
	public void LoadAsync(Game newGame, List<Xdelta> xdeltaPaths, bool forceReloadDatafile = false) {
		logger.DebugNewline();
		logger.Debug("New request for " + newGame.DisplayName);
		lock (Lock) {
			if (IsAlreadyGiven(newGame, xdeltaPaths) && !forceReloadDatafile && !Lock.Errored) {
				logger.Debug("Same data as what's already loaded or being loaded");
				return;
			}
			
			Lock.Path = newGame.GetCleanDatafilePath();
			Lock.Xdeltas = xdeltaPaths;
			lastGame = newGame;
			lastXdeltaPaths = xdeltaPaths;
			
			if (Lock.IsLoading) {
				logger.Debug("Telling loader to load new game after it is done with this one");
				Lock.Action = LoaderAction.Restart;
			}
			else
				Lock.Action = LoaderAction.Proceed;

			logger.Debug("Waking up thread to load the data");
			Lock.Errored = false;
			Lock.PulseToLoad();
		}
	}
	public void ReloadAsync() {
		if (lastGame is null) {
			return;
		}
		lock (Lock) {
			LoadAsync(lastGame, lastXdeltaPaths ?? [], true);
		}
	}
	
	public class LoaderLock() {
		public LoaderAction Action = LoaderAction.Proceed;
		public string? Path = null;
		public List<Xdelta> Xdeltas = [];
		public bool IsLoading = false;
		public bool Errored = false;
		public UndertaleData? Result;
		public void PulseToLoad() {
			IsLoading = true;
			Monitor.PulseAll(this);
		}
	}


	
	public enum LoaderAction {
		Restart,
		Proceed,
		Clone,
	}


}