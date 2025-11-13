using System.Diagnostics;
using System.Reflection;
using System.Text;
using g3man.Models;
using g3man.Util;
using gmlp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using UndertaleModLib;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;
using UndertaleModLib.Util;

namespace g3man.Patching;

public class Patcher {
	private static readonly Logger logger = new Logger("PATCHER");
	public const string CleanDataName = "g3man_data.win";
	public const string TempDataName = "g3man_temp_data.win";

	enum OverlapBehavior {
		ImplicitlyExcludeExplicitlyOverride,
		ExplicitlyExcludeImplicitlyOverride,
		AllExplicit,
	}
	private OverlapBehavior overlapBehavior = OverlapBehavior.ImplicitlyExcludeExplicitlyOverride;
	private const string OVERRIDE_PREFIX = "g3man_override_";
	private const string EXCLUDE_PREFIX = "g3man_fake_";

	// mostly the same as undertalemodcli
	private ScriptOptions scriptOptions = ScriptOptions.Default
		.AddImports(
			"UndertaleModLib", "UndertaleModLib.Models", "UndertaleModLib.Decompiler",
			"UndertaleModLib.Scripting", "UndertaleModLib.Compiler",
			"UndertaleModLib.Util", "System", "System.IO", "System.Collections.Generic",
			"System.Text.RegularExpressions")
		.AddReferences(typeof(UndertaleObject).GetTypeInfo().Assembly,
			typeof(System.Text.RegularExpressions.Regex).GetTypeInfo().Assembly,
			typeof(TextureWorker).GetTypeInfo().Assembly,
			typeof(ImageMagick.MagickImage).GetTypeInfo().Assembly,
			typeof(Underanalyzer.Decompiler.DecompileContext).Assembly)
		.WithEmitDebugInformation(true);
	
	/**
	 * If the resource should be excluded according to the patcher's settings, return the object it
	 * is supposed to mimic.
	 * Otherwise returns null.
	 */
	private T? GetMimicedResource<T>(IList<T> list, T resource) where T : UndertaleNamedResource {
		string name = resource.Name.Content;
		if (overlapBehavior == OverlapBehavior.ImplicitlyExcludeExplicitlyOverride) {
			return list.ByName(name);
		}

		Debug.Assert(overlapBehavior == OverlapBehavior.ExplicitlyExcludeImplicitlyOverride 
		             || overlapBehavior == OverlapBehavior.AllExplicit);
		if (name.StartsWith(EXCLUDE_PREFIX))
			return default(T);
		return list.ByName(name.Substring(EXCLUDE_PREFIX.Length));
	}
	
	/**
	 * If the resource should override some other resource according to the patcher's settings, return the object it should replace.
	 * Otherwise returns null.
	 */
	private T? GetResourceToOverride<T>(IList<T> list, T resource) where T : UndertaleNamedResource {
		string name = resource.Name.Content;
		if (overlapBehavior == OverlapBehavior.ImplicitlyExcludeExplicitlyOverride ||
		    overlapBehavior == OverlapBehavior.AllExplicit) {
			if (!name.StartsWith(OVERRIDE_PREFIX))
				return default(T);
			return list.ByName(name.Substring(OVERRIDE_PREFIX.Length));
		}
		Debug.Assert(overlapBehavior == OverlapBehavior.ExplicitlyExcludeImplicitlyOverride);
		T? old = list.ByName(name);
		return old;
	}
	
	private void MergeLists<T>(IList<T> to, IList<T> from, Func<T, bool>? process = null) where T : UndertaleNamedResource {
		foreach (T resource in from) {
			if (GetMimicedResource(to, resource) is not null)
				continue;
			if (process is not null) {
				if (!process(resource))
					continue;
			}
			to.Add(resource);
			
		}
		HandleOverrides(to, from);
	}

	private void HandleOverrides<T>(IList<T> to, IList<T> from) where T : UndertaleNamedResource {
		List<T> overriders = from.Where(resource => resource.Name.Content.StartsWith(OVERRIDE_PREFIX)).ToList();
		foreach (T overrider in overriders) {
			T? old = GetResourceToOverride(to, overrider);
			if (old is null) {
				continue;
			}
			// This is a bit dumb but it's probably the cleanest way to go about this.
			// UndertaleModTool doesn't keep track of indices to the Data's resource lists, but just keeps references.
			// For example, UndertaleGameObject stores the reference to the UndertaleSprite it uses. If we were to replace the sprite at that index,
			// It would not do anything for the object's sprite. So, we set each field of the instance.
			
			// Specifically we swap their fields because I'm worried about the saving possibly not working because of unwritten pointers,
			// if that turns out to not be an issue the swap can be removed.
			FieldInfo[] fields = old.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
			foreach (FieldInfo field in fields) {
				object? temp = field.GetValue(overrider);
				field.SetValue(overrider, field.GetValue(temp));
				field.SetValue(old, temp);
			}
		}
	}
	/**
	 * Merges (as in, copies all data) from `modData` into `data`.
	 * 
	 * This is pretty old code. I don't remember how much of it is necessary or could be improved.
	 */
	private void merge(UndertaleData data, UndertaleData modData) {
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
		
		MergeLists(data.Sprites, modData.Sprites, sprite => {
			foreach (UndertaleSprite.TextureEntry textureEntry in sprite.Textures) {
				int newIndex = dict[textureEntry.Texture.TexturePage];
				textureEntry.Texture.TexturePage = data.EmbeddedTextures[newIndex];
				lastTexturePageItem++;
				textureEntry.Texture.Name = new UndertaleString("PageItem " + lastTexturePageItem);
				data.TexturePageItems.Add(textureEntry.Texture);
			}
			return true;
		});
		
		HandleOverrides(data.Sprites, modData.Sprites);

		// TODO: Is This OK
		MergeLists(data.Sounds, modData.Sounds, sound => {
			sound.AudioGroup = data.AudioGroups[0];
			data.EmbeddedAudio.Add(sound.AudioFile);
			return true;
		});
		
		MergeLists(data.Code, modData.Code);
		
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
		
		MergeLists(data.Scripts, modData.Scripts);
		MergeLists(data.GameObjects,  modData.GameObjects, gameObject => {
			UndertaleGameObject parent = gameObject.ParentId;
			if (parent is null)
				return true;
			UndertaleGameObject? parentFromGame = GetMimicedResource(data.GameObjects, parent);
			if (parentFromGame is not null)
				gameObject.ParentId = parentFromGame;
			return true;
		});

		foreach (UndertaleResourceById<UndertaleRoom, UndertaleChunkROOM> room in modData.GeneralInfo.RoomOrder) {
			if (GetMimicedResource(data.Rooms, room.Resource) is not null)
				continue;
			data.GeneralInfo.RoomOrder.Add(room);
		}
		
		MergeLists(data.Rooms, modData.Rooms, room => {
			foreach (UndertaleRoom.Layer layer in room.Layers) {
				if (layer.LayerType != UndertaleRoom.LayerType.Instances) 
					continue;
				foreach (UndertaleRoom.GameObject gameObject in layer.InstancesData.Instances)
					gameObject.InstanceID += addInstanceId;
			}
			return true;
		});




		MergeLists(data.AnimationCurves, modData.AnimationCurves);

		
		
		// TODO: test these
		MergeLists(data.ParticleSystems, modData.ParticleSystems);
		MergeLists(data.ParticleSystemEmitters, modData.ParticleSystemEmitters);
		MergeLists(data.Sequences, modData.Sequences);
		MergeLists(data.Timelines, modData.Timelines);
		MergeLists(data.Paths, modData.Paths);
		MergeLists(data.Shaders, modData.Shaders);
		
		foreach (UndertaleGlobalInit script in modData.GlobalInitScripts)
			data.GlobalInitScripts.Add(script);

		foreach (UndertaleString str in modData.Strings)
			data.Strings.Add(str);
		
		data.GeneralInfo.FunctionClassifications |= modData.GeneralInfo.FunctionClassifications;
	}


	public void Patch(List<Mod> mods, Profile profile, Game game, Action<string, bool> statusCallback) {
		string modsFolder = Path.Combine(game.Directory, "g3man", profile.FolderName, "mods");
		void setStatus(string message, bool leave = false) {
			logger.Info(message);
			statusCallback(message, leave);
		}
		
		bool runModScript(Mod mod, Func<Mod, string> getScriptPath, ScriptGlobals globals) {
			string path = getScriptPath(mod);
			if (path == "")
				return true;
			setStatus($"Running script: {path}");
			string fullStringPath = Path.Combine(modsFolder, mod.FolderName, mod.PostMergeScriptPath);
			string code;
				
			try {
				code = File.ReadAllText(fullStringPath);
			}
			catch (Exception e) {
				setStatus($"Failed to read script belonging to {mod.DisplayName}. Check the log.", true);
				logger.Error(e.ToString());
				return false;
			}
			
			// makes errors point to the path of the script
			code = $"#line 1 \"{fullStringPath}\"\n" + code;
			try {
				CSharpScript.EvaluateAsync(code, scriptOptions, globals);
			}
			catch (Exception e) {
				setStatus($"Script belonging to {mod.DisplayName} threw an exception. Check the log.", true);
				logger.Error(e.ToString());
				return false;
			}

			return true;
		}
		

		List<string> issues = CheckPatchPreventionIssues(mods);
		if (issues.Count > 0) {
			StringBuilder sb = new StringBuilder("Encountered issues that are preventing patching!");
			for (int i = 0; i < issues.Count; i++) {
				var issue = issues[i];
				sb.Append($"\n{i + 1}. {issue}");
			}

			statusCallback(sb.ToString(), true);
			return;
		}
		UndertaleData data;
		statusCallback($"Waiting for game data to load...", false);

		lock (Program.DataLoader.Lock) {
			while (!Program.DataLoader.CanSnatch()) {
				if (Program.DataLoader.HasErrored()) {
					statusCallback($"Failed to load game's data.win. I don't know what to do in this situation yet. TODO", true);
					return;
				}
				Monitor.Wait(Program.DataLoader.Lock);
			}
			data = Program.DataLoader.Snatch();
		}

		
		foreach (Mod mod in mods) {
			UndertaleData? modData = null;
			
			if (mod.DatafilePath != "") {
				setStatus($"Merging: {mod.DisplayName}");
				string fullDatafilePath = Path.Combine(modsFolder, mod.FolderName, mod.DatafilePath);
				try {
					using FileStream stream = new FileStream(fullDatafilePath, FileMode.Open, FileAccess.Read);
					modData = UndertaleIO.Read(stream);
					if (!runModScript(mod, m => m.PreMergeScriptPath, new ScriptGlobals(data, modData)))
						return;
					merge(data, modData);
					if (!runModScript(mod, m => m.PostMergeScriptPath, new ScriptGlobals(data, modData)))
						return;
				}
				catch (Exception e) {
					logger.Error($"Failed to load datafile of mod {mod.DisplayName}:\n" + e.Message);
					setStatus($"Failed to load the datafile of {mod.DisplayName}. Check the log.", true);
					return;
				}
			}
			
		}


		foreach (Mod mod in mods) {
			if (!runModScript(mod, m => m.PrePatchScriptPath, new ScriptGlobals(data)))
				return;
		}

		GlobalDecompileContext context = new GlobalDecompileContext(data);
		PatchesRecord record = new PatchesRecord();
		GameMakerCodeSource source = new GameMakerCodeSource(data, context);
		
		List<PatchOwner> order = mods.Select(mod => new PatchOwner(mod.ModId)).ToList();
		
		foreach (Mod mod in mods) {
			int index = mods.IndexOf(mod);
			setStatus($"Reading patches from: {mod.DisplayName}");
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
						setStatus($"Failed to read patch file at {relativePath}: {e.Message}", true);
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
			setStatus(e.HumanError(), true);
			if (e.GetBadCode() is not null)
				logger.Error("This code failed to compile:\n" + e.GetBadCode()!);
			return;
		}

		if (profile.SeparateModdedSave)
			data.GeneralInfo.Name.Content = profile.ModdedSaveName;
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
		
		foreach (Mod mod in mods) {
			if (!runModScript(mod, m => m.PostPatchScriptPath, new ScriptGlobals(data)))
				return;
		}
		
		
		setStatus("Done!", true);
		
	}
	
	public static bool IsDataPatched(UndertaleData data) {
		return false;
	}


	private List<string> CheckPatchPreventionIssues(List<Mod> mods) {
		List<string> issues = new List<string>();
		Dictionary<string, Mod> IdMap = mods.ToDictionary(mod => mod.ModId);
		foreach (Mod mod in mods) {
			CheckDepends(mods, mod, IdMap, issues);
			CheckBreaks(mods, mod, IdMap, issues);
		}

		if (!Program.Config.AllowModScripting) {
			foreach (Mod mod in mods) {
				CheckModScripts(issues, mod);
			}
		}
		return issues;
	}

	private void CheckModScripts(List<string> issues, Mod mod) {
		if (mod.HasAnyScripts()) {
			lock (issues) {
				issues.Add($"Mod \"{mod.DisplayName}\" wants to run scripts, but mod scripting is disabled! Go to settings to enable it.");
			}
		}
	}

	private void CheckDepends(List<Mod> mods, Mod mod, Dictionary<string, Mod> IdMap, List<string> issues) {
		foreach (RelatedMod related in mod.Depends) {
			Mod? dependency = IdMap!.GetValueOrDefault(related.ModId, null);
			if (dependency is null) {
				lock (issues) {
					issues.Add($"Mod {mod.DisplayName} depends on mod with ID {related.ModId} (version {related.VersionRequirements}), but it is not present");
				}
				return;
			}
			if (!related.VersionRequirements.IsCompatibleWith(dependency.Version)) {
				lock (issues) {
					issues.Add(
						$"Mod \"{mod.DisplayName}\" depends on the mod \"{dependency.DisplayName}\", but the version present isn't compatible "
						+ $"(required: {related.VersionRequirements}, present: {dependency.Version})");
				}
			}

			int index = mods.IndexOf(mod);
			int dependencyIndex = mods.IndexOf(dependency);
			switch (related.OrderRequirement) {
				case OrderRequirement.AfterUs:
					if (dependencyIndex < index) {
						lock (issues) {
							issues.Add(
								$"Mod \"{mod.DisplayName}\" depends on the mod \"{dependency.DisplayName}\", but the dependency must be loaded AFTER it in the order");
						}
					}

					break;
				case OrderRequirement.BeforeUs:
					if (dependencyIndex > index)
						break;
					lock (issues) {
						issues.Add(
							$"Mod \"{mod.DisplayName}\" depends on the mod \"{dependency.DisplayName}\", but the dependency must be loaded BEFORE it in the order");
					}

					break;
			}
		}
	}
	private void CheckBreaks(List<Mod> mods, Mod mod, Dictionary<string, Mod> IdMap, List<string> issues) {
		foreach (RelatedMod related in mod.Breaks) {
			Mod? dependency = IdMap!.GetValueOrDefault(related.ModId, null);
			if (dependency is null)
				return;
			if (!related.VersionRequirements.IsCompatibleWith(dependency.Version)) {
				return;
			}
			
			string versionHelp = $"Find a version of \"{dependency.DisplayName}\" that does not meet the version requirement: {related.VersionRequirements}";
			string allHelp = $"Reorder the mods/{versionHelp}";
			
			string? issue = null;
			int index = mods.IndexOf(mod);
			int dependencyIndex = mods.IndexOf(dependency);
			switch (related.OrderRequirement) {
				case OrderRequirement.AfterUs:
					if (dependencyIndex > index) {
						issue = $"Mod \"{mod.DisplayName}\" is marked as broken if the mod \"{dependency.DisplayName}\" is loaded AFTER it in the order";
					}

					break;
				case OrderRequirement.BeforeUs:
					if (dependencyIndex < index)
						break;
					issue =	$"Mod \"{mod.DisplayName}\" is marked as broken if the mod \"{dependency.DisplayName}\" is loaded BEFORE it in the order";
					break;
				case OrderRequirement.Irrelevant:
					issue = $"Mod \"{mod.DisplayName}\" is marked as broken if the mod \"{dependency.DisplayName}\" exists.";
					break;
			}

			if (issue is not null) {
				lock (issues) {
					if (related.OrderRequirement == OrderRequirement.Irrelevant)
						issues.Add($"{issue}\n{versionHelp}");
					else
						issues.Add($"{issue}\n{allHelp}");
				}	
			}
		}
	}
}

public class ScriptGlobals(UndertaleData data, UndertaleData? modData = null) {
	public UndertaleData Data = data;
	public UndertaleData? ModData = modData;
}