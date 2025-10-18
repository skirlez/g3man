using System.Diagnostics;
using g3man.Models;
using g3man.Util;
using UndertaleModLib;
using UndertaleModLib.Models;
using g3man.GMLP;
using UndertaleModLib.Decompiler;


namespace g3man;

public class Patcher {
	private static readonly Logger logger = new Logger("PATCHER");
	public const string CleanDataName = "clean_data.g3man";


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
			if (data.GameObjects.ByName(gameObject.Name.Content) != null)
				continue;
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
				if (layer.LayerType == UndertaleRoom.LayerType.Instances) {
					foreach (UndertaleRoom.GameObject gameObject in layer.InstancesData.Instances)
						gameObject.InstanceID += addInstanceId;
				}
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
	
	public void Patch(List<Mod> mods, Profile profile, Game game, Action<string> statusCallback) {
		DataLoader.LoaderLock loaderLock = Program.DataLoader.Lock;
		
		UndertaleData data;
		GlobalDecompileContext context;
		
		lock (loaderLock) {
			while (loaderLock.IsLoading) {
				Monitor.Wait(loaderLock);
			}
			data = Program.DataLoader.GetData()!;
			context = Program.DataLoader.GetDecompileContext()!;
		}
		
		
		
		
		string modsFolder = Path.Combine(game.Directory, profile.FolderName, "mods");
		foreach (Mod mod in mods) {
			if (mod.DatafilePath == "")
				continue;
			statusCallback($"Merging: {mod.DisplayName}");
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


		PatchesRecord record = new PatchesRecord();
		foreach (Mod mod in mods) {
			statusCallback($"Patching: {mod.DisplayName}");
			foreach (PatchLocation patchLocation in mod.Patches) {
				// the only one right now
				Debug.Assert(patchLocation.Type == PatchFormatType.GMLP);

				string modFolder = Path.Combine(modsFolder, mod.FolderName);
				string fullPath = Path.Combine(modFolder, patchLocation.Path);
				
				if (Directory.Exists(fullPath)) {
					foreach (string file in Directory.GetFiles(fullPath, "*.gmlp", SearchOption.AllDirectories)) {
						processPatch(file, Path.GetRelativePath(modFolder, file));
					}
				}
				else if (File.Exists(fullPath))
					processPatch(fullPath, patchLocation.Path);
				else {
					logger.Error($"In mod {mod.DisplayName}: Invalid patch or patch directory {patchLocation.Path}" );
				}
				
				void processPatch(string patchPath, string relativePath) {
					try {
						string patchText = File.ReadAllText(patchPath);
						PatchOwner owner = new ModPatchOwner(mod, relativePath);
						GMLP.GMLP.ExecuteEntirePatch(patchText, data, context, record, owner);
					}
					catch (Exception e) {
						logger.Error("Failed to read patch file: " + e);
					}
				}
			}
		}	

		PatchApplier applier = new GMLPatchApplier(data, context);
		GMLP.GMLP.ApplyPatches(record, applier, mods);
	}
	
	public static bool IsDataPatched(UndertaleData data) {
		return false;
	}
	
}