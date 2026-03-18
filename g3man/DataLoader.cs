using System.Diagnostics;
using System.Security.Cryptography;
using g3man.Models;
using g3man.Util;
using UndertaleModLib;
using UndertaleModLib.Decompiler;
using Xdelta = g3man.Util.Xdelta;

namespace g3man;


/** Responsible for the loading and preloading of the current game's clean data.win
 */
public class DataLoader {
	private volatile UndertaleData? data;
	private volatile string? hash;
	
	private MemoryStream dataMemory = new MemoryStream();
	private Game? lastGame;
	private List<Xdelta>? lastXdeltaPaths;
	public readonly LoaderLock Lock = new LoaderLock();
	private readonly Logger logger;
	
	public DataLoader() {
		logger = Logger.Make("DATALOADER");
		Thread thread = new Thread(() => {
			string path;
			LoaderAction action;
			UndertaleData readData = null!;
			string readHash = null!;
			List<Xdelta> xdeltas;
			bool doCloning = false;
			
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
					}
					
					doCloning = Program.Config.UseMoreMemory;
					Debug.Assert(Lock.Path is not null);
					path = Lock.Path;
					action = Lock.Action;
					xdeltas = Lock.Xdeltas;
					if (!doCloning) {
						if (dataMemory.Length != 0) {
							dataMemory.Dispose();
							dataMemory = new MemoryStream();
						}
					}

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
					MemoryStream memoryStream = new MemoryStream();
					try {
						if (xdeltas.Count == 0) {
							using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
							dataMemory.SetLength(0);
							stream.CopyTo(memoryStream);
						}
						else {
							
							logger.Debug($"Found {xdeltas.Count} xdelta patches");
							logger.Debug($"Applying: {xdeltas.First().Filename}");
							int status = xdeltas.First().Decode(path, memoryStream);
							for (int i = 1; i < xdeltas.Count; i++) {
								if (status != 0) {
									break;
								}
								logger.Debug($"Applying: {xdeltas[i].Filename}");
								byte[] bytes = memoryStream.ToArray();
								memoryStream.SetLength(0);
								status = xdeltas[i].DecodeFromMemory(bytes, memoryStream);
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
						readData = UndertaleIO.Read(memoryStream);
					}
					catch (Exception e) {
						readData = null!;
						logger.Error("Failed to load datafile: " + e);
						Lock.Errored = true;
						continue;
					}

					if (doCloning)
						dataMemory = memoryStream;
				}
				else if (action == LoaderAction.Clone) {
					// should not fail; this already loaded once
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
		Lock.Errored = false;
		Lock.IsLoading = true;
		UndertaleData bye = data!;
		Monitor.PulseAll(Lock);
		return bye;
	}
	
	// I'm starting to think this feature is stupid and i should remove it
	public void ReevaluateMemoryStrategy() {
		if (lastGame is null)
			return;
		Debug.Assert(lastXdeltaPaths is not null);
		lock (Lock) {
			if (!Program.Config.UseMoreMemory && !Lock.IsLoading && dataMemory.Length != 0) {
				logger.Debug("Discarding dataMemory due to UseMoreMemory being disabled");
				dataMemory.Dispose();
				dataMemory = new MemoryStream();
				return;
			}
			if (Program.Config.UseMoreMemory && dataMemory.Length == 0) {
				logger.Debug("We're now allowed to use more memory but we don't have the dataMemory clone. So we're going to load the same game again to obtain it.");
				Program.DataLoader.LoadAsync(lastGame, lastXdeltaPaths, allowSameGame: true);
			}
		}
	}
	
	public bool IsAlreadyGiven(Game game, List<Xdelta> xdeltaPaths) {
		return lastGame is not null
			&& game.Hash == lastGame.Hash
			&& xdeltaPaths.Select(x => x.Filepath).Order().SequenceEqual(lastXdeltaPaths!.Select(x => x.Filepath).Order());
	}
	public void LoadAsync(Game newGame, List<Xdelta> xdeltaPaths, bool allowSameGame = false) {
		if (IsAlreadyGiven(newGame, xdeltaPaths) && !allowSameGame) {
			logger.Debug("Same data as what's already loaded or being loaded");
			return;
		}
		logger.DebugNewline();
		logger.Debug("New request for " + newGame.DisplayName);
		lock (Lock) {
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
			Lock.IsLoading = true;
			Lock.Errored = false;
			Monitor.PulseAll(Lock);
		}
	}


	public class LoaderLock() {
		public LoaderAction Action = LoaderAction.Proceed;
		public string? Path = null;
		public List<Xdelta> Xdeltas = [];
		public bool IsLoading = false;
		public bool Errored = false;
		public string ProfileFolder = "";
	}

	public enum LoaderAction {
		Restart,
		Proceed,
		Clone,
	}
}