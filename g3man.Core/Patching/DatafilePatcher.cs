using System.Diagnostics;
using System.Reflection;
using System.Text;
using g3man.Core.Models;
using g3man.Core.Util;
using gmlp;
using gmlpv2;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using PatchCommon;
using Underanalyzer.Decompiler;
using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;
using UndertaleModLib.Util;

namespace g3man.Core.Patching;

public class DatafilePatcher {
	public const string CleanDataName = "clean_data.win";
	public const string CleanDataBackupName = "BACKUP_clean_data.win";

	enum OverlapBehavior {
		ImplicitlyFakeExplicitlyOverride,
		ExplicitlyFakeImplicitlyOverride,
		AllExplicit,
	}
	private OverlapBehavior overlapBehavior = OverlapBehavior.ImplicitlyFakeExplicitlyOverride;

	/**
	* Keeps track of overriden assets so we don't override the same asset more than once.
	*/
	private HashSet<UndertaleObject> overridenAssets = new HashSet<UndertaleObject>();
	
	private const string OVERRIDE_PREFIX = "g3man_override_";
	private const string FAKE_PREFIX = "g3man_fake_";
	private const string IGNORE_PREFIX = "g3man_ignore_";
	
	private static readonly ScriptOptions scriptOptions = ScriptingUtil.CreateDefaultScriptOptions();
	
	/**
	 * If the resource should be excluded according to the patcher's settings, return the object it
	 * is supposed to mimic.
	 * Otherwise returns null.
	 */
	private T? GetMimicedResource<T>(Dictionary<string, T> nameMap, T resource) where T : UndertaleNamedResource {
		string name = resource.Name.Content;
		if (overlapBehavior == OverlapBehavior.ImplicitlyFakeExplicitlyOverride) {
			if (!nameMap.ContainsKey(name))
				return default(T);
			return nameMap[name];
		}

		Debug.Assert(overlapBehavior == OverlapBehavior.ExplicitlyFakeImplicitlyOverride 
					|| overlapBehavior == OverlapBehavior.AllExplicit);
		if (!name.StartsWith(FAKE_PREFIX))
			return default(T);

		string substr = name.Substring(FAKE_PREFIX.Length);
		if (!nameMap.ContainsKey(substr))
			return default(T);
		return nameMap[substr];
	}
	
	/**
	 * If the resource should override some other resource according to the patcher's settings, return the object it should replace.
	 * Otherwise returns null.
	 */
	private T? GetResourceToOverride<T>(IList<T?> list, T resource) where T : UndertaleNamedResource {
		string name = resource.Name.Content;
		if (overlapBehavior == OverlapBehavior.ImplicitlyFakeExplicitlyOverride 
				|| overlapBehavior == OverlapBehavior.AllExplicit) {
			if (!name.StartsWith(OVERRIDE_PREFIX))
				return default(T);
			return list.ByName(name.Substring(OVERRIDE_PREFIX.Length));
		}
		Debug.Assert(overlapBehavior == OverlapBehavior.ExplicitlyFakeImplicitlyOverride);
		T? old = list.ByName(name);
		return old;
	}
	
	// every null check here is warranted and added because i found it in the wild at some point
	private void MergeLists<T>(IList<T?>? to, IList<T?>? from, bool canMimic = true, Func<T, Dictionary<string, T>, bool>? process = null) where T : UndertaleNamedResource {
		if (to is null || from is null)
			return;
		Dictionary<string, T>? nameMap = null;
		if (canMimic || process != null)
			nameMap = to.Where(t => t is not null).ToDictionary(t => t!.Name.Content)!;
		foreach (T? resource in from) {
			if (resource is null) 
				continue;
			if (resource.Name.Content.StartsWith(IGNORE_PREFIX))
				continue;
			if (canMimic && GetMimicedResource(nameMap!, resource) is not null)
				continue;
			if (process is not null) {
				if (!process(resource, nameMap!))
					continue;
			}
			to.Add(resource);
		}
		HandleOverrides(to, from);
	}

	
	// TODO:
	// Calls GetResourceToOverride for each overrider (uses ByName, very bad performance with many overriders)
	private void HandleOverrides<T>(IList<T?> to, IList<T?> from) where T : UndertaleNamedResource {
		List<T> overriders = from.Where(resource => resource is not null).Where(resource => resource!.Name.Content.StartsWith(OVERRIDE_PREFIX)).ToList()!;
		foreach (T overrider in overriders) {
			T? old = GetResourceToOverride(to, overrider);
			if (old is null || overridenAssets.Contains(old)) {
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
				
				// don't wanna swap the object names
				if (field.Name == "<Name>k__BackingField")
					continue;
				
				object? temp = field.GetValue(overrider);
				field.SetValue(overrider, field.GetValue(old));
				field.SetValue(old, temp);
			}

			overridenAssets.Add(old);
		}
	}

	private record struct MergeProduct(HashSet<string> groupsToUpdate, HashSet<string> groupsToCopy) {
		public HashSet<string> GroupsToUpdate = groupsToUpdate;
		public HashSet<string> GroupsToCopy = groupsToCopy;
	}


	// namespace the names of functions in accordance to NamespacingOptions
	private void autoNamespace(UndertaleData data, string modId, NamespacingOptions namespacingOptions) {
	    const string SCRIPT_PREFIX = "gml_Script_";
		bool shouldNamespaceScript(UndertaleScript script) {
			string name = script.Name.Content;
			if (name.Contains('@') || name.StartsWith("gml_GlobalScript_"))
				return false;
			if (!name.StartsWith(SCRIPT_PREFIX))
				return false;
			string functionName = name.Remove(0, SCRIPT_PREFIX.Length);
			return !namespacingOptions.Scheme.IsExcluded(functionName);
		}
		foreach (UndertaleScript script in data.Scripts) {
			if (shouldNamespaceScript(script)) {
				string name = script.Name.Content.Remove(0, SCRIPT_PREFIX.Length);
				UndertaleVariable? variable = data.Variables.ByName(name);
				variable?.Name.Content = $"@{modId}@{name}";
				script.Name.Content = $"{SCRIPT_PREFIX}@{modId}@{name}";
			}
		}
	}
	
	/**
	 * Merges (as in, copies all data) from `modData` into `data`.
	 * 
	 * This is pretty old code. I don't remember how much of it is necessary or could be improved.
	 */
	// TODO: There's several instances now where maps between an asset's name and the asset are made more than once. They should be made once.
	private MergeProduct merge(UndertaleData data, UndertaleData modData, string modFolderPath) {
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
		
		MergeLists(data.Sprites, modData.Sprites, canMimic: true, (sprite, _) => {
			foreach (UndertaleSprite.TextureEntry textureEntry in sprite.Textures) {
				int newIndex = dict[textureEntry.Texture.TexturePage];
				textureEntry.Texture.TexturePage = data.EmbeddedTextures[newIndex];
				lastTexturePageItem++;
				textureEntry.Texture.Name = new UndertaleString("PageItem " + lastTexturePageItem);
				data.TexturePageItems.Add(textureEntry.Texture);
			}
			return true;
		});

		
		Dictionary<UndertaleAudioGroup, int> audioGroupLengths = data.Sounds.GroupBy(x => x.AudioGroup)
			.ToDictionary(group => group.Key, group => group.Count());

		Dictionary<string, UndertaleAudioGroup> audioGroupNameMap = data.AudioGroups.Where(t => t is not null).ToDictionary(t => t!.Name.Content)!;

		MergeLists(data.AudioGroups, modData.AudioGroups, canMimic: true);
		MergeLists(data.EmbeddedAudio, modData.EmbeddedAudio, canMimic: false);
		

		// audiogroups to copy from the mod folder to the game folder
		HashSet<string> groupsToCopy = new();
		// audiogroups that must be updated to add modded audio over
		HashSet<string> groupsToUpdate = new();
		
		MergeLists(data.Sounds, modData.Sounds, canMimic: true, (sound, _) => {
			if (sound.AudioGroup == modData.AudioGroups[0]) {
				// streamed or embedded audio has go in the default audiogroup
				sound.AudioGroup = data.AudioGroups[0];
				if (sound.Flags.HasFlag(UndertaleSound.AudioEntryFlags.IsEmbedded)) {
					// we don't need to do anything, embedded sound was already added
				}
				else
					sound.File.Content = $"{modFolderPath}/{sound.File.Content}";
			}
			else {
				UndertaleAudioGroup? groupFromGame = GetMimicedResource(audioGroupNameMap, sound.AudioGroup);
				if (groupFromGame is not null) {
					// this sound is supposed to belong to some base game group
					sound.AudioGroup = groupFromGame;
					// we will need to update the appropriate audiogroup file after this
					sound.AudioID += audioGroupLengths[groupFromGame];
					
					groupsToUpdate.Add(sound.AudioGroup.Name.Content);
				}
				else {
					// this sound belongs to an audiogroup of this mod
					// we will need to copy this audiogroup to the game folder
					groupsToCopy.Add(sound.AudioGroup.Name.Content);
				}
			}
			return true;
		});
		
		MergeLists(data.Code, modData.Code, canMimic: false);
		
		foreach (UndertaleFunction function in modData.Functions) {
			if (isBuiltinFunction(modData, function))
				continue;
			data.Functions.Add(function);
			function.NameStringID += stringListLength;
		}

		foreach (UndertaleVariable variable in modData.Variables) {
			data.Variables.Add(variable);

			if (variable.VarID == variable.NameStringID && variable.VarID != 0)
				variable.VarID += stringListLength;
			
			variable.NameStringID += stringListLength;
			
		}
		
		data.MaxLocalVarCount = Math.Max(data.MaxLocalVarCount, modData.MaxLocalVarCount);

		if (modData.CodeLocals is not null) {
			foreach (UndertaleCodeLocals locals in modData.CodeLocals)
				data.CodeLocals.Add(locals);
		}

		MergeLists(data.Scripts, modData.Scripts, canMimic: false);
		
		Dictionary<string, UndertaleGameObject> gameObjectNameMap = data.GameObjects.Where(t => t is not null).ToDictionary(t => t!.Name.Content)!;

		MergeLists(data.GameObjects,  modData.GameObjects, canMimic: true, (gameObject, nameMap) => {
			UndertaleGameObject parent = gameObject.ParentId;
			if (parent is not null) {
				UndertaleGameObject? parentFromGame = GetMimicedResource(nameMap, parent);
				if (parentFromGame is not null)
					gameObject.ParentId = parentFromGame;
			}
			return true;
		});
		
		foreach (UndertaleGameObject obj in modData.GameObjects) {
			UndertaleGameObject? overriden = GetResourceToOverride(data.GameObjects, obj);
			UndertaleGameObject gameObject = overriden ?? obj;

			// This is probably always true.
			if (gameObject.Events.Count >= 5) {
				// If there are any collision events, we have to either correct the index used
				// or if it's a fake object switch to that one
				UndertalePointerList<UndertaleGameObject.Event>? collisionEvents = gameObject.Events[4];
				foreach (UndertaleGameObject.Event? collisionEvent in collisionEvents) {
					int objectIndex = (int)collisionEvent.EventSubtype;
					UndertaleGameObject collisionObject = modData.GameObjects[objectIndex];

					UndertaleGameObject? collisionObjectFromGame = GetMimicedResource(gameObjectNameMap, collisionObject);
					objectIndex = data.GameObjects.IndexOf(collisionObjectFromGame ?? collisionObject);

					collisionEvent.EventSubtype = (uint)objectIndex;
				}
			}
		}

		if (!(modData.Rooms.Count == 1 && modData.Rooms[0].Name.Content == "g3man_must_exist")) {
			{
				Dictionary<string, UndertaleRoom> nameMap = data.Rooms.ToDictionary(t => t.Name.Content);
				foreach (UndertaleResourceById<UndertaleRoom, UndertaleChunkROOM> room in
						modData.GeneralInfo.RoomOrder) {
					if (GetMimicedResource(nameMap, room.Resource) is not null)
						continue;
					data.GeneralInfo.RoomOrder.Add(room);
				}
			}
			MergeLists(data.Rooms, modData.Rooms, canMimic: true, (room, _) => {
				foreach (UndertaleRoom.Layer layer in room.Layers) {
					if (layer.LayerType != UndertaleRoom.LayerType.Instances)
						continue;
					foreach (UndertaleRoom.GameObject gameObject in layer.InstancesData.Instances)
						gameObject.InstanceID += addInstanceId;
				}

				return true;
			});
		}

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

		return new MergeProduct(groupsToUpdate, groupsToCopy);
	}

	private const string CHECK_LOG = "Check the log for more details.";

	public struct PatchProduct(UndertaleData data, List<AudioGroupTransfer> transfers) {
		public UndertaleData Data = data;
		public List<AudioGroupTransfer> AudioGroupTransfers = transfers;
	}
	
	public PatchProduct? Patch(List<Mod> mods, Profile profile, 
			string profileLocation, string relativeProfilePath, string relativeProfileLivePath,
			UndertaleData data, Logger logger, Action<string> statusCallback, bool allowModScripting) 
	{
		void setStatusAndInfo(string message) {
			logger.Info(message);
			statusCallback(message);
		}
		void setStatusAndError(string message, string? error = null) {
			if (error is null) {
				logger.Error(message);
				statusCallback(message);
				return;
			}
			logger.Error($"{message}\n{error}");
			statusCallback($"{message} {CHECK_LOG}");
		}
		
		bool runModScript(Mod mod, Func<Mod, string> getScriptPath, ScriptGlobals globals) {
			string path = getScriptPath(mod);
			if (path == "")
				return true;
			setStatusAndInfo($"Running script: {path}");
			string relativePath = Path.Combine(mod.ModId, path);
			string fullStringPath = Path.Combine(profileLocation, relativePath);
			string code;
				
			try {
				code = File.ReadAllText(fullStringPath);
			}
			catch (Exception e) {
				setStatusAndError($"Failed to read script belonging to {mod.Identify()}!", e.ToString());
				return false;
			}
			
			// makes errors point to the path of the script
			code = $"#line 1 \"{relativePath}\"\n" + code;
			try {
				CSharpScript.EvaluateAsync(code, scriptOptions, globals);
			}
			catch (CompilationErrorException e) {
				setStatusAndError($"Script belonging to {mod.Identify()} threw an exception.", e.GetBaseException().Message);
				return false;
			}
			return true;
		}
		

		List<string> issues = CheckModApplicationIssues(mods, allowModScripting);
		if (issues.Count > 0) {
			StringBuilder sb = new("Encountered issues that are preventing mod application!");
			for (int i = 0; i < issues.Count; i++) {
				var issue = issues[i];
				sb.Append($"\n{i + 1}. {issue}");
			}

			setStatusAndInfo(sb.ToString());
			return null;
		}


		
		// keep track of which audiogroups from mods need to be copied into the game folder
		// when files actually need to be written
		// TODO: For GameMaker 2024.14 we don't actually need to do this apparently, since audiogroups have a path variable
		List<AudioGroupTransfer> audioGroupTransfers = new();
		
		Assets vanillaAssets = GetAllIndices(data);

		Assets prevAssets = vanillaAssets;

		Dictionary<string, Assets> modIndices = new();
		
		foreach (Mod mod in mods) {
			if (mod.DatafilePath == "") {
				Assets emptyAssets = EmptyIndices(prevAssets);
				modIndices[mod.ModId] = emptyAssets;
				prevAssets = emptyAssets;
				continue;
			}
			setStatusAndInfo($"Merging: {mod.DisplayName}");
			string fullDatafilePath = Path.Combine(profileLocation, mod.ModId, mod.DatafilePath);
			UndertaleData modData;
			try {
				using FileStream stream = new(fullDatafilePath, FileMode.Open, FileAccess.Read);
				modData = UndertaleIO.Read(stream);
			}
			catch (Exception e) {
				setStatusAndError($"Failed to load the datafile of {mod.Identify()}.", e.ToString());
				return null;
			}

			
			if (!runModScript(mod, m => m.PreMergeScriptPath, new ScriptGlobals(data, modData)))
				return null;
				
			autoNamespace(modData, mod.ModId, mod.NamespacingOptions);
			//ReferenceFixer.FixReferences(modData);
			MergeProduct product;
			try {
				product = merge(data, modData, Path.Combine(relativeProfilePath, mod.ModId));
			}
			catch (Exception e) {
				setStatusAndError($"Merging {mod.Identify()} failed!", e.ToString());
				return null;
			}
			
			// TODO: don't get audio group indices like this please
			// and make this one loop somehow
			foreach (string audioGroup in product.GroupsToCopy) {
				int originalIndex = modData.AudioGroups.IndexOf(modData.AudioGroups.ByName(audioGroup));
				int newIndex = data.AudioGroups.IndexOf(data.AudioGroups.ByName(audioGroup));
				audioGroupTransfers.Add(new AudioGroupTransfer(mod, originalIndex, newIndex, false));
			}
			foreach (string audioGroup in product.GroupsToUpdate) {
				int originalIndex = modData.AudioGroups.IndexOf(modData.AudioGroups.ByName(audioGroup));
				int newIndex = data.AudioGroups.IndexOf(data.AudioGroups.ByName(audioGroup));
				audioGroupTransfers.Add(new AudioGroupTransfer(mod, originalIndex, newIndex, true));
			}
			if (!runModScript(mod, m => m.PostMergeScriptPath, new ScriptGlobals(data, modData)))
				return null;
			
			Assets assets = GetAllIndices(data, prevAssets);
			modIndices[mod.ModId] = assets;
			prevAssets = assets;
		}


		foreach (Mod mod in mods) {
			if (!runModScript(mod, m => m.PrePatchScriptPath, new ScriptGlobals(data)))
				return null;
		}
		

		GlobalDecompileContext mainContext = new(data);
		CompileGroup mainGroup = new(data, mainContext);
		GameMakerCodeSource source = new(mainGroup);

		string[] requestedAPIs = GameAPI.GetRequestedAPIs(mods.SelectMany(mod => mod.Imports));
		if (requestedAPIs.Length > 0) {
			GameAPI.Inject(data, requestedAPIs,
				profile,
				relativeProfilePath,
				relativeProfileLivePath,
				vanillaAssets,
				modIndices,
				mainGroup);
			CompileResult gameAPIResult = mainGroup.Compile();
			if (!gameAPIResult.Successful) {
				setStatusAndError("Failed to insert g3man Game API(s)!", 
					gameAPIResult.PrintAllErrors(false));
				return null;
			}
		}
		
		
		List<PatchStep<Mod>> firstPatchSteps = [];
		List<PatchStep<Mod>> lastPatchSteps = [];
		
		foreach (Mod mod in mods) {
			if (mod.Patches.Length != 0)
				setStatusAndInfo($"Reading patches from: {mod.DisplayName}");
			
			PatchIntentionAggregate<UnitOperations> gmlpIntentionAggregate = new();
			PatchIntentionAggregate<FileRecord> gmlpv2IntentionAggregate = new();
			
			foreach (PatchLocation patchLocation in mod.Patches) {
				string modFolder = Path.Combine(profileLocation, mod.ModId);
				string fullPath = Path.Combine(modFolder, patchLocation.Path);
				
				if (Directory.Exists(fullPath)) {
					foreach (string file in Directory.GetFiles(fullPath, $"*.{patchLocation.Extension}", SearchOption.AllDirectories)) {
						if (!processPatch(file, Path.GetRelativePath(modFolder, file)))
							return null;
					}
				}
				else if (File.Exists(fullPath)) {
					if (!processPatch(fullPath, patchLocation.Path))
						return null;
				}
				else {
					setStatusAndError($"Mod {mod.Identify()} specified an invalid patch or patch directory: \"{patchLocation.Path}\"");
					return null;
				}
				
				bool processPatch(string patchPath, string relativePath) {
					string patchText;
					try {
						patchText = File.ReadAllText(patchPath).ReplaceLineEndings("\n");
						if (patchLocation.Type == PatchFormatType.GMLP) {
							gmlp.Language.FindIntentions(patchText, relativePath, gmlpIntentionAggregate);
							return true;
						}

						if (patchLocation.Type == PatchFormatType.GMLPv2) {
							gmlpv2.Language.FindIntentions(patchText, Path.GetDirectoryName(patchPath), relativePath, gmlpv2IntentionAggregate);
							return true;
						}
					}
					catch (Exception e) {
						setStatusAndError(
							$"An error occurred while trying to read a patch file at \"{relativePath}\" from {mod.Identify()}!",
							e.ToString());
						return false;
					}
					throw new UnreachableException();
				}
				
			}

			List<string> intentionErrors = gmlpv2IntentionAggregate.GetAllErrors().Concat(gmlpIntentionAggregate.GetAllErrors()).ToList();
			if (intentionErrors.Count != 0) {
				string total = "";
				foreach (string error in intentionErrors) {
					total += error + "\n";
				}
				setStatusAndError("Patch intention errors occurred!", total);
				return null;
			}

			
			gmlpv2IntentionAggregate.AddStepsIfNecessary(firstPatchSteps, lastPatchSteps, source, mod, gmlpv2.Language.Apply);
			gmlpIntentionAggregate.AddStepsIfNecessary(firstPatchSteps, lastPatchSteps, source, mod, gmlp.Language.Apply);
		}


		lastPatchSteps.Reverse();
		List<PatchStep<Mod>> patchSteps = firstPatchSteps.Concat(lastPatchSteps).ToList();
		int steps = patchSteps.Count;

		int currentStep = 1;
		
		foreach (PatchStep<Mod> step in patchSteps) {
			NamespacedGlobalFunctions namespacedGlobalFunctions = new(data.GlobalFunctions, getModIdsRelevantToMod(step.Owner));
			GlobalDecompileContext context = new(data, namespacedGlobalFunctions);
			CompileGroup group = new(data, context);
			
			setStatusAndInfo($"Applying patches... (step {currentStep}/{steps})");
			PatchResults patchResults = step.Apply();
		
			if (patchResults.HasErrors()) {
				setStatusAndError("Some patches failed to execute!", string.Join('\n', patchResults.GetAllErrors()));
				return null;
			}
			foreach (KeyValuePair<string, string> pair in patchResults.GetAllResults()) {
				group.QueueCodeReplace(data.Code.ByName(pair.Key), pair.Value);
				source.RemoveFromCache(pair.Key);
			}

			CompileResult compileResult = group.Compile();
			if (!compileResult.Successful) {
				List<PatchInfo> allResponsible = compileResult.Errors.Select(e => e.Code.Name.Content).SelectMany(step.WhoTouches).ToList();
				if (allResponsible.All(info => !info.Critical)) {
					continue;
				}
				
				string detailedError = generateCompileError(compileResult, step, patchResults);
				string message = $"Compilation error while applying patches from {step.Owner.Identify()}!";
				setStatusAndError(message, $"Below will be a file-by-file analysis of every compilation error.\n\n{detailedError}");
				return null;
			}
			currentStep++;
		}
	
	
		
		if (profile.SeparateModdedSave)
			data.GeneralInfo.Name.Content = profile.ModdedSaveName;
		
		foreach (Mod mod in mods) {
			if (!runModScript(mod, m => m.PostPatchScriptPath, new ScriptGlobals(data)))
				return null;
		}
		
		
		
		return new PatchProduct(data, audioGroupTransfers);
	}
	
	public static bool IsDataPatched(UndertaleData data) {
		return data.Scripts.ByName(GameAPI.ScriptName) is not null;
	}
	
	private List<string> CheckModApplicationIssues(List<Mod> mods, bool allowModScripting) {
		List<string> issues = new();
		List<IGrouping<string, Mod>> idGroups = mods.GroupBy(mod => mod.ModId).ToList();

		if (idGroups.Any(idGroup => idGroup.Count() > 1)) {
			string baseIssue = "You have several mods with the same ID, which is not allowed:";
			foreach (IGrouping<string, Mod> idGroup in idGroups) {
				if (idGroup.Count() > 1)
					baseIssue += $"\n\"{idGroup.Key}\", found {idGroup.Count()} times";
			}
			issues.Add(baseIssue);
			return issues;
		}
		
		
		
		Dictionary<string, Mod> idMap = mods.ToDictionary(mod => mod.ModId);
		foreach (Mod mod in mods) {
			if (mod.TargetPatcherVersion > ProgramConstants.VERSION.Major) {
				issues.Add($"Mod {mod.Identify()} is made for a version of g3man that is too high: {mod.TargetPatcherVersion} (you are on {ProgramConstants.VERSION})");
			}
			
			CheckDepends(mods, mod, idMap, issues);
			CheckBreaks(mods, mod, idMap, issues);
			CheckImports(mods, mod, idMap, issues);
		}

		if (!allowModScripting) {
			foreach (Mod mod in mods) {
				CheckModScripts(issues, mod);
			}
		}
		return issues;
	}

	private void CheckModScripts(List<string> issues, Mod mod) {
		if (mod.HasAnyScripts()) {
			lock (issues) {
				issues.Add($"Mod {mod.Identify()} wants to run scripts, but mod scripting is disabled! Go to settings to enable it.");
			}
		}
	}

	private void CheckDepends(List<Mod> mods, Mod mod, Dictionary<string, Mod> idMap, List<string> issues) {
		foreach (RelatedMod related in mod.Depends) {
			Mod? dependency = idMap!.GetValueOrDefault(related.ModId, null);
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
					if (dependencyIndex > index) {
						lock (issues) {
							issues.Add(
								$"Mod \"{mod.DisplayName}\" depends on the mod \"{dependency.DisplayName}\", but the dependency must be loaded AFTER it in the order");
						}
					}

					break;
				case OrderRequirement.BeforeUs:
					if (dependencyIndex < index)
						break;
					lock (issues) {
						issues.Add(
							$"Mod \"{mod.DisplayName}\" depends on the mod \"{dependency.DisplayName}\", but the dependency must be loaded BEFORE it in the order");
					}

					break;
			}
		}
	}
	private void CheckBreaks(List<Mod> mods, Mod mod, Dictionary<string, Mod> idMap, List<string> issues) {
		foreach (RelatedMod related in mod.Breaks) {
			Mod? dependency = idMap!.GetValueOrDefault(related.ModId, null);
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
	

	private void CheckImports(List<Mod> mods, Mod mod, Dictionary<string, Mod> idMap, List<string> issues) {
		foreach (Import import in mod.Imports) {
			if (GameAPI.IsImportAskingForUs(import))
				continue;
			
			List<Mod> WhoExports(string name) {
				List<Mod> exporters = [];
				foreach (Mod mod2 in mods) {
					if (mod2.Exports.Contains(name))
						exporters.Add(mod2);
				}
				return exporters;
			}
			
			List<Mod> exporters = WhoExports(import.Name);
			if (exporters.Count == 0) {
				if (import.Contingency is GiveUpContingency) {
					lock (issues) {
						issues.Add($"Mod {mod.Identify()} depends on the import \"{import.Name}\" but it is not provided by anyone.");
					}

					return;
				}
				RecommendContingency contingency = (RecommendContingency)import.Contingency;
				lock (issues) {
 					issues.Add($"Mod {mod.Identify()} depends on the import \"{import.Name}\" but it is not provided by anyone.\nMod's suggestion: Download {contingency.Name} at <a href=\"{contingency.Link}\">{contingency.Link}</a>");
				}
				return;
			}
			if (exporters.Count > 1) {
				string modsResponsibleString = exporters[0].Identify();
				for (int i = 1; i < exporters.Count; i++) {
					modsResponsibleString += $", {exporters[i].Identify()}";
				}
				lock (issues) {
					issues.Add($"Mod {mod.Identify()} depends on the import \"{import.Name}\" but it is provided more than once,\nby the following mods: {modsResponsibleString}");
				}
				return;
			}


		}
	}
	
	
	private string generateCompileError(CompileResult compileResult, PatchStep<Mod> step, PatchResults result) {
		StringBuilder sb = new();
		int number = 1;
			
		// group errors by file
		IEnumerable<IGrouping<UndertaleCode, CompileError>> errors = compileResult.Errors.GroupBy(error => error.Code);
		foreach (IGrouping<UndertaleCode, CompileError> errorGroup in errors) {
			string fileName = errorGroup.Key.Name.Content;
			sb.AppendLine($"{number}. Code filename: {fileName}");

	
			sb.AppendLine($"The following patches from the mod {step.Owner.Identify()} touch this code file:");
			foreach (PatchInfo info in step.WhoTouches(fileName)) {
				sb.AppendLine($"- {info.Name}");
			}

			if (step.WhoTouches(fileName).All(i => !i.Critical)) {
				sb.AppendLine(
					"None of these patch files are marked as critical, so normally this error wouldn't prevent patching.");
			}
			
			sb.AppendLine("========== ERRORS START ==========");
			string[] lines = result.GetResult(fileName).Split('\n');
			int errorIndex = 1;
			HashSet<int> relevantLines = new HashSet<int>();
			const int RADIUS = 5;
			// compiler weirdly likes to repeat some errors so we just check if we already said something before saying it
			HashSet<string> alreadySaid = new HashSet<string>();
			
			foreach (CompileError error in errorGroup) {
				string detailedMessage = error.GenerateDetailedMessage();
				if (!alreadySaid.Contains(detailedMessage)) {
					sb.AppendLine($"{errorIndex}. {detailedMessage}");
					alreadySaid.Add(detailedMessage);
					

					int tryExtractLineNumber() {
						const string LINE_TEXT = " line ";
						int lineTextLocation = detailedMessage.IndexOf(LINE_TEXT, StringComparison.Ordinal);
						if (lineTextLocation == -1)
							return -1;
						
						int spaceAfter = detailedMessage.IndexOf(' ', lineTextLocation + LINE_TEXT.Length);
						if (spaceAfter == -1)
							return -1;
						string numberText = detailedMessage.Substring(lineTextLocation + LINE_TEXT.Length,
							spaceAfter - (lineTextLocation + LINE_TEXT.Length) - 1);
						bool success = int.TryParse(numberText, out int num);
						if (!success) 
							return -1;
						return num;
					}
					int num = tryExtractLineNumber();
					if (num != -1) {
						for (int i = -RADIUS; i < RADIUS; i++) {
							int l = num + i;
							if (l < lines.Length && l >= 0)
								relevantLines.Add(l);
						}
					}

					errorIndex++;
				}
			}
			
			sb.AppendLine("========== ERRORS END ==========");
			if (relevantLines.Count > 0) {
				sb.AppendLine("========== RELEVANT LINES ==========");
				List<int> relevantLinesList = relevantLines.Order().ToList();
				int lastPrintedLine = relevantLinesList[0];
				
				foreach (int i in relevantLinesList) {
					if (i - lastPrintedLine > 1) {
						sb.AppendLine($"--------------------------------");
					}
					sb.AppendLine($"{i}. {lines[i - 1]}");
					lastPrintedLine = i;
				}
				sb.AppendLine("========== RELEVANT LINES END ==========");
			}
			


			number += 1;
		}

		return sb.ToString();
	}

	private static List<string> getModIdsRelevantToMod(Mod mod) {
		List<string> ids = [mod.ModId];
		foreach (RelatedMod relatedMod in mod.Depends) {
			ids.Add(relatedMod.ModId);
		}
		foreach (RelatedMod relatedMod in mod.Suggests) {
			ids.Add(relatedMod.ModId);
		}

		return ids;
	}

	private static bool isBuiltinFunction(UndertaleData data, UndertaleFunction function) {
		// TODO: this causes issues
		//return data.BuiltinList.LookupBuiltinFunction(function.Name.Content) is not null;
		return false;
	}

	private static Assets GetAllIndices(UndertaleData data, Assets? previous = null) {
		Assets assets = new();
		assets.Set = new();
		object?[] lists = [data.Sprites, data.Backgrounds, data.GameObjects, data.Rooms, data.Sounds, data.AudioGroups,
							  data.AnimationCurves, data.Fonts, data.ParticleSystems, data.Paths, data.Paths, data.Shaders, data.Sequences, data.Timelines];

		assets.Offsets = new int[lists.Length];
		for (int i = 0; i < lists.Length; i++) {
			object? uncastedList = lists[i];
			if (uncastedList is null)
				continue;
			IEnumerable<UndertaleNamedResource?> enumerable = (IEnumerable<UndertaleNamedResource?>)uncastedList;
			List<UndertaleNamedResource?> list = new(enumerable);
			int offset = previous?.Offsets[i] ?? 0;
			for (int j = offset; j < list.Count; j++) {
				UndertaleNamedResource? asset = list[j];
				if (asset is null)
					continue;
				assets.Set.Add(asset.Name.Content);
			}

			assets.Offsets[i] = list.Count;
		}
		return assets;
	}
	private static Assets EmptyIndices(Assets previous) {
		Assets assets = new();
		assets.Set = new();
		assets.Offsets = previous.Offsets;
		return assets;
	}

	public struct Assets {
		public HashSet<string> Set;
		public int[] Offsets;
	}
}



public class ScriptGlobals(UndertaleData data, UndertaleData? modData = null) {
	public UndertaleData Data = data;
	public UndertaleData? ModData = modData;
}