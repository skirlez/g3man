using System.Diagnostics;
using g3man.Models;
using g3man.Util;
using gmlp;
using UndertaleModLib;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;

namespace g3man.Patching;

public class Patcher {
	private static readonly Logger logger = new Logger("PATCHER");
	public const string CleanDataName = "clean_data.g3man";
	public const string TempDataName = "temp_data.g3man";

	/**
	 * Merges (as in, copies all data) from `modData` into `data`.
	 * 
	 * This is pretty old code. I don't remember how much of it is necessary or could be improved.
	 */
	private static void Merge(UndertaleData data, UndertaleData modData) {
		int stringListLength = data.Strings.Count;
		uint addInstanceId = data.GeneralInfo.LastObj - 100000;
		data.GeneralInfo.LastObj += modData.GeneralInfo.LastObj - 100000;

		int lastTexturePage = data.EmbeddedTextures.Count - 1;
		int lastTexturePageItem = data.TexturePageItems.Count - 1;

		Dictionary<UndertaleEmbeddedTexture, int> dict = new Dictionary<UndertaleEmbeddedTexture, int>();
		foreach (UndertaleEmbeddedTexture embeddedTexture in modData.EmbeddedTextures) {
			if (embeddedTexture.TextureInfo.Name.Content == "__YY__0fallbacktexture.png_YYG_AUTO_GEN_TEX_GROUP_NAME_")
				continue;
			UndertaleEmbeddedTexture newTexture = new UndertaleEmbeddedTexture();
			lastTexturePage++;
			newTexture.Name = new UndertaleString("Texture " + lastTexturePage);
			newTexture.TextureData.Image = embeddedTexture.TextureData.Image;
			data.EmbeddedTextures.Add(newTexture);
			dict.Add(embeddedTexture, lastTexturePage);
		}

		foreach (UndertaleSprite sprite in modData.Sprites) {
			data.Sprites.Add(sprite);
			foreach (UndertaleSprite.TextureEntry textureEntry in sprite.Textures) {
				int newIndex = dict[textureEntry.Texture.TexturePage];
				textureEntry.Texture.TexturePage = data.EmbeddedTextures[newIndex];
				lastTexturePageItem++;
				textureEntry.Texture.Name = new UndertaleString("PageItem " + lastTexturePageItem);
				data.TexturePageItems.Add(textureEntry.Texture);
			}
		}

		// TODO: Is This OK
		foreach (UndertaleSound sound in modData.Sounds) {
			sound.AudioGroup = data.AudioGroups[0];
			data.Sounds.Add(sound);
			data.EmbeddedAudio.Add(sound.AudioFile);
		}

		foreach (UndertaleCode code in modData.Code)
			data.Code.Add(code);

		foreach (UndertaleFunction function in modData.Functions) {
			data.Functions.Add(function);
			function.NameStringID += stringListLength;
		}

		foreach (UndertaleVariable variable in modData.Variables) {
			data.Variables.Add(variable);

			if (variable.VarID == variable.NameStringID && variable.VarID != 0)
				variable.VarID += stringListLength;
			
			variable.NameStringID += stringListLength;
			
		}
		// These assignments may not be necessary
		data.InstanceVarCount += modData.InstanceVarCount;
		data.InstanceVarCountAgain += modData.InstanceVarCountAgain;
		
		data.MaxLocalVarCount = Math.Max(data.MaxLocalVarCount, modData.MaxLocalVarCount);

		foreach (UndertaleCodeLocals locals in modData.CodeLocals) 
			data.CodeLocals.Add(locals);
		foreach (UndertaleScript script in modData.Scripts) 
			data.Scripts.Add(script);
		
		foreach (UndertaleGameObject gameObject in modData.GameObjects) {
			/*
			if (data.GameObjects.ByName(gameObject.Name.Content) != null)
				continue;
			*/
			UndertaleGameObject parent = gameObject.ParentId;
			if (parent is not null) {
				string name = parent.Name.Content;
				const string parentPrefix = "g3man_fake_";
				if (name.StartsWith(parentPrefix) && name.Length > parentPrefix.Length) {
					string nameWithoutPrefix = name.Substring(parentPrefix.Length);
					UndertaleGameObject parentFromGame = data.GameObjects.ByName(nameWithoutPrefix);
					if (parentFromGame is not null) {
						gameObject.ParentId = parentFromGame;
					}
				}
			}
			data.GameObjects.Add(gameObject);
		}

		foreach (UndertaleRoom room in modData.Rooms) {
			data.Rooms.Add(room);
			foreach (UndertaleRoom.Layer layer in room.Layers) {
				if (layer.LayerType != UndertaleRoom.LayerType.Instances) 
					continue;
				foreach (UndertaleRoom.GameObject gameObject in layer.InstancesData.Instances)
					gameObject.InstanceID += addInstanceId;
			}
		}

		foreach (UndertaleAnimationCurve curve in modData.AnimationCurves)
			data.AnimationCurves.Add(curve);
		
		foreach (UndertaleSequence sequence in modData.Sequences)
			data.Sequences.Add(sequence);
		
		foreach (UndertaleResourceById<UndertaleRoom, UndertaleChunkROOM> room in modData.GeneralInfo.RoomOrder)
			data.GeneralInfo.RoomOrder.Add(room);
		
		foreach (UndertaleGlobalInit script in modData.GlobalInitScripts)
			data.GlobalInitScripts.Add(script);

		foreach (UndertaleString str in modData.Strings)
			data.Strings.Add(str);
		
		data.GeneralInfo.FunctionClassifications |= modData.GeneralInfo.FunctionClassifications;
	}


	public void Patch(List<Mod> mods, Profile profile, Game game, Action<string, bool> statusCallback) {
		void setStatus(string message, bool leave = false) {
			logger.Info(message);
			statusCallback(message, leave);
		}

		Dictionary<string, Mod> IdMap = mods.ToDictionary(mod => mod.ModId);
		List<string> issues = new List<string>();
		
		// Check for dependency and issues
		Parallel.ForEach(mods, mod => {
			foreach (RelatedMod related in mod.Depends) {
				Mod? dependency = IdMap!.GetValueOrDefault(related.ModId, null);
				if (dependency is null) {
					lock (issues) {
						issues.Add($"Mod {mod.DisplayName} depends on mod with ID {related.ModId} (version {related.Version}), but it is not present");
					}
					return;
				}
				if (!related.Version.IsCompatibleWith(dependency.Version)) {
					issues.Add($"Mod {mod.DisplayName} depends on the mod {dependency.DisplayName}, but the version present is too old "
						+ $"(required: {related.Version}, present: {dependency.Version})");
				}

				int index = mods.IndexOf(mod);
				int dependencyIndex = mods.IndexOf(dependency);
				switch (related.OrderRequirement) {
					case OrderRequirement.AfterUs:
						if (dependencyIndex < index)
							issues.Add($"Mod {mod.DisplayName} depends on the mod {dependency.DisplayName}, but the dependency must be loaded AFTER it in the order");
						break;
					case OrderRequirement.BeforeUs:
						if (dependencyIndex > index)
							break;
						issues.Add($"Mod {mod.DisplayName} depends on the mod {dependency.DisplayName}, but the dependency must be loaded BEFORE it in the order");
						break;
					default:
						break;
				}
			}
		});
		
		UndertaleData data;
		statusCallback($"Waiting for game data to load...", false);

		lock (Program.DataLoader.Lock) {
			while (!Program.DataLoader.CanSnatch()) {
				Monitor.Wait(Program.DataLoader.Lock);
			}
			data = Program.DataLoader.Snatch();
		}

		string modsFolder = Path.Combine(game.Directory, "g3man", profile.FolderName, "mods");
		foreach (Mod mod in mods) {
			if (mod.DatafilePath == "")
				continue;
			setStatus($"Merging: {mod.DisplayName}");
			string fullDatafilePath = Path.Combine(modsFolder, mod.FolderName, mod.DatafilePath);
			try {
				using FileStream stream = new FileStream(fullDatafilePath, FileMode.Open, FileAccess.Read);
				UndertaleData modData = UndertaleIO.Read(stream);
				Merge(data, modData);
			}
			catch (Exception e) {
				logger.Error($"Failed to load datafile of mod {mod.DisplayName}:\n" + e.Message);
			}
		}

		GlobalDecompileContext context = new GlobalDecompileContext(data);
		PatchesRecord record = new PatchesRecord();
		GameMakerCodeSource source = new GameMakerCodeSource(data, context);


		List<PatchOwner> order = mods.Select(mod => new PatchOwner(mod.ModId)).ToList();
		
		
		// TODO: this can be parallelized in a lot of different ways.
		foreach (Mod mod in mods) {
			int index = mods.IndexOf(mod);
			setStatus($"Patching: {mod.DisplayName}");
			foreach (PatchLocation patchLocation in mod.Patches) {
				// the only one right now
				Debug.Assert(patchLocation.Type == PatchFormatType.GMLP);

				string modFolder = Path.Combine(modsFolder, mod.FolderName);
				string fullPath = Path.Combine(modFolder, patchLocation.Path);
				
				if (Directory.Exists(fullPath)) {
					foreach (string file in Directory.GetFiles(fullPath, "*.gmlp", SearchOption.AllDirectories)) {
						if (!processPatch(file, Path.GetRelativePath(modFolder, file)))
							return;
					}
				}
				else if (File.Exists(fullPath)) {
					if (!processPatch(fullPath, patchLocation.Path))
						return;
				}
				else {
					setStatus($"Mod {mod.DisplayName}: Invalid patch or patch directory {patchLocation.Path}", true);
					return;
				}
				


				bool processPatch(string patchPath, string relativePath) {
					try {
						string patchText = File.ReadAllText(patchPath);
						Language.ExecuteEntirePatch(patchText, source, record, order[index]);
						return true;
					}
					catch (InvalidPatchException e) {
						setStatus($"Failed to read patch file at {relativePath}: {e.Message}");
					}
					catch (Exception e) {
						setStatus("Error occured during patching! Check the log.", true);
						logger.Error("Failed to read patch file: " + e);
					}

					return false;
				}
			}
		}

		setStatus("Applying patches");
		
		try {
			Language.ApplyPatches(record, source, order);
		}
		catch (PatchApplicationException e) {
			setStatus(e.HumanError());
			if (e.GetBadCode() is not null)
				logger.Error("This code failed to compile:\n" + e.GetBadCode()!);
			return;
		}
		
		setStatus("Saving file");
		try {
			string tempFilePath = Path.Combine(game.Directory, TempDataName);
			using FileStream stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
			UndertaleIO.Write(stream, data);
			// TODO: change data.win
			File.Move(tempFilePath, Path.Combine(game.Directory, "data.win"), true);
			File.Delete(tempFilePath);
		}
		catch (Exception e) {
			setStatus("Error occured while saving! Check the log.", true);
			logger.Error("Failed to save datafile: " + e);
			return;
		}
		
		
		setStatus("Done!", true);
	}
	
	public static bool IsDataPatched(UndertaleData data) {
		return false;
	}
	
}