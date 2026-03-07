using System.Diagnostics;
using System.Reflection;
using System.Text;
using g3man.Models;
using g3man.Util;
using gmlp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using UndertaleModLib;
using UndertaleModLib.Compiler;
using UndertaleModLib.Decompiler;
using UndertaleModLib.Models;
using UndertaleModLib.Util;
namespace g3man.Patching;

using PatchBlame = Dictionary<string, List<string>>;

public class Patcher {
	public const string CleanDataName = "clean_data.win";
	public const string CleanDataBackupName = "BACKUP_clean_data.win";

	enum OverlapBehavior {
		ImplicitlyExcludeExplicitlyOverride,
		ExplicitlyExcludeImplicitlyOverride,
		AllExplicit,
	}
	private OverlapBehavior overlapBehavior = OverlapBehavior.ImplicitlyExcludeExplicitlyOverride;

	/**
	* Keeps track of overriden assets so we don't override the same asset more than once.
	*/
	private HashSet<UndertaleObject> overridenAssets;
	
	private const string OVERRIDE_PREFIX = "g3man_override_";
	private const string EXCLUDE_PREFIX = "g3man_fake_";
	private const string IGNORE_PREFIX = "g3man_ignore_";

	/**
	 * If mangling is enabled for some asset type, g3man prepends this string to its name.
	 * It isn't actually an issue for two assets to have the same names. The point of mangling is
	 * just to allow you to name your assets as generically as you want without worrying that it could
	 * conflict with other names.
	 */
	private const string G3MAN_MANGLE_PREFIX = "g3man_mangled_";

	// mostly the same as undertalemodcli
	private static readonly ScriptOptions scriptOptions = ScriptOptions.Default
		.AddImports(
			"UndertaleModLib", "UndertaleModLib.Models", "UndertaleModLib.Decompiler",
			"UndertaleModLib.Scripting", "UndertaleModLib.Compiler",
			"UndertaleModLib.Util", "System", "System.IO", "System.Threading.Tasks",
			"System.Collections.Generic",  "System.Text.RegularExpressions")
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
	private T? GetMimicedResource<T>(Dictionary<string, T> nameMap, T resource) where T : UndertaleNamedResource {
		string name = resource.Name.Content;
		if (overlapBehavior == OverlapBehavior.ImplicitlyExcludeExplicitlyOverride) {
			if (!nameMap.ContainsKey(name))
				return default(T);
			return nameMap[name];
		}

		Debug.Assert(overlapBehavior == OverlapBehavior.ExplicitlyExcludeImplicitlyOverride 
		             || overlapBehavior == OverlapBehavior.AllExplicit);
		if (!name.StartsWith(EXCLUDE_PREFIX))
			return default(T);

		string substr = name.Substring(EXCLUDE_PREFIX.Length);
		if (!nameMap.ContainsKey(substr))
			return default(T);
		return nameMap[substr];
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
	
	// every null check here is warranted and added because i found it in the wild at some point
	private void MergeLists<T>(IList<T?>? to, IList<T?>? from, bool mangle, bool canMimic = true, Func<T, Dictionary<string, T>, bool>? process = null) where T : UndertaleNamedResource {
		if (to is null || from is null)
			return;
		Dictionary<string, T> nameMap = to.Where(t => t is not null).ToDictionary(t => t!.Name.Content)!;
		foreach (T? resource in from) {
			if (resource is null) 
				continue;
			if (resource.Name.Content.StartsWith(IGNORE_PREFIX))
				continue;
			if (canMimic && GetMimicedResource(nameMap, resource) is not null)
				continue;
			if (process is not null) {
				if (!process(resource, nameMap))
					continue;
			}
			if (mangle)
				resource.Name.Content = G3MAN_MANGLE_PREFIX + resource.Name.Content;
			to.Add(resource);
		}
		HandleOverrides(to, from);
	}

	
	// TODO:
	// Calls GetResourceToOverride for each overrider (uses ByName, very bad performance with many overriders)
	private void HandleOverrides<T>(IList<T> to, IList<T?> from) where T : UndertaleNamedResource {
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
	/**
	 * Merges (as in, copies all data) from `modData` into `data`.
	 * 
	 * This is pretty old code. I don't remember how much of it is necessary or could be improved.
	 */
	private void merge(UndertaleData data, UndertaleData modData, string[] mangle, string modFolderName) {
		int stringListLength = data.Strings.Count;
		uint addInstanceId = data.GeneralInfo.LastObj - 100000;
		data.GeneralInfo.LastObj += modData.GeneralInfo.LastObj - 100000;

		bool mangleAll = mangle.Contains("all");
		
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
		
		MergeLists(data.Sprites, modData.Sprites, mangleAll || mangle.Contains("sprites"), canMimic: true, (sprite, _) => {
			foreach (UndertaleSprite.TextureEntry textureEntry in sprite.Textures) {
				int newIndex = dict[textureEntry.Texture.TexturePage];
				textureEntry.Texture.TexturePage = data.EmbeddedTextures[newIndex];
				lastTexturePageItem++;
				textureEntry.Texture.Name = new UndertaleString("PageItem " + lastTexturePageItem);
				data.TexturePageItems.Add(textureEntry.Texture);
			}
			return true;
		});
	
		MergeLists(data.Sounds, modData.Sounds, mangleAll || mangle.Contains("sounds"), canMimic: true, (sound, _) => {
			// This stuff is unfinished, I don't trust these flags. I'll write the intention with each of these...
			if (sound.Flags.HasFlag(UndertaleSound.AudioEntryFlags.IsCompressed) || sound.Flags.HasFlag(UndertaleSound.AudioEntryFlags.IsEmbedded)) {
				// assign all embedded audio to audiogroup_default (assigning them to different ones would require
				// us to manage audiogroup files, which seems like a pretty annoying thing to do)
				sound.AudioGroup = data.AudioGroups[0];
				data.EmbeddedAudio.Add(sound.AudioFile);
			}
			else {
				// streamed audio has to go in the default audiogroup
				sound.AudioGroup = data.AudioGroups[0];
				sound.File.Content = $"g3man_applied_profile/{modFolderName}/{sound.File.Content}";
			}

			return true;
		});
		
		MergeLists(data.Code, modData.Code, mangleAll || mangle.Contains("code"));
		
		
		MergeLists(data.Functions, modData.Functions, mangleAll || mangle.Contains("functions"), canMimic: false, (function, _) => {
			function.NameStringID += stringListLength;
			return true;
		});
		/*
		foreach (UndertaleFunction function in modData.Functions) {
			data.Functions.Add(function);
			function.NameStringID += stringListLength;
		}
		*/

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

		if (data.CodeLocals is not null) {
			foreach (UndertaleCodeLocals locals in modData.CodeLocals)
				data.CodeLocals.Add(locals);
		}

		MergeLists(data.Scripts, modData.Scripts, mangleAll || mangle.Contains("scripts"));
		
		// TODO: I think there's several instances now where these maps are made more than once. They should be made once.
		Dictionary<string, UndertaleGameObject> gameObjectNameMap = data.GameObjects.Where(t => t is not null).ToDictionary(t => t!.Name.Content)!;

		MergeLists(data.GameObjects,  modData.GameObjects, mangleAll || mangle.Contains("objects"), canMimic: true, (gameObject, nameMap) => {
			UndertaleGameObject parent = gameObject.ParentId;
			if (parent is not null) {
				UndertaleGameObject? parentFromGame = GetMimicedResource(nameMap, parent);
				if (parentFromGame is not null)
					gameObject.ParentId = parentFromGame;
			}
			return true;
		});
		
		foreach (UndertaleGameObject gameObject in modData.GameObjects) {
			// This is probably always true.
			if (gameObject.Events.Count >= 5) {
				
				// If there are any collision events, we have to either correct the index used
				// or if it's a fake object switch to that one
				UndertalePointerList<UndertaleGameObject.Event>? collisionEvents = gameObject.Events[4];
				foreach (UndertaleGameObject.Event? collisionEvent in collisionEvents) {
					int objectIndex = (int)collisionEvent.EventSubtype;
					UndertaleGameObject collisionObject = modData.GameObjects[objectIndex];

					UndertaleGameObject? collisionObjectFromGame = GetMimicedResource(gameObjectNameMap, collisionObject);
					if (collisionObjectFromGame is null) {
						objectIndex = data.GameObjects.IndexOf(collisionObject);
					}
					else {
						objectIndex = data.GameObjects.IndexOf(collisionObjectFromGame);
					}

					collisionEvent.EventSubtype = (uint)objectIndex;
				}
			}
		}

		{
			Dictionary<string, UndertaleRoom> nameMap = data.Rooms.ToDictionary(t => t.Name.Content);
			foreach (UndertaleResourceById<UndertaleRoom, UndertaleChunkROOM> room in modData.GeneralInfo.RoomOrder) {
				if (GetMimicedResource(nameMap, room.Resource) is not null)
					continue;
				data.GeneralInfo.RoomOrder.Add(room);
			}
		}

		MergeLists(data.Rooms, modData.Rooms, mangleAll || mangle.Contains("rooms"), canMimic: true,(room, _) => {
			foreach (UndertaleRoom.Layer layer in room.Layers) {
				if (layer.LayerType != UndertaleRoom.LayerType.Instances) 
					continue;
				foreach (UndertaleRoom.GameObject gameObject in layer.InstancesData.Instances)
					gameObject.InstanceID += addInstanceId;
			}
			return true;
		});




		MergeLists(data.AnimationCurves, modData.AnimationCurves, mangleAll || mangle.Contains("animation_curves"));

		
		
		// TODO: test these
		MergeLists(data.ParticleSystems, modData.ParticleSystems, mangleAll || mangle.Contains("particle_systems"));
		MergeLists(data.ParticleSystemEmitters, modData.ParticleSystemEmitters, mangleAll || mangle.Contains("particle_system_emitters"));
		MergeLists(data.Sequences, modData.Sequences, mangleAll || mangle.Contains("sequences"));
		MergeLists(data.Timelines, modData.Timelines, mangleAll || mangle.Contains("timelines"));
		MergeLists(data.Paths, modData.Paths, mangleAll || mangle.Contains("paths"));
		MergeLists(data.Shaders, modData.Shaders, mangleAll || mangle.Contains("shaders"));
		
		foreach (UndertaleGlobalInit script in modData.GlobalInitScripts)
			data.GlobalInitScripts.Add(script);

		foreach (UndertaleString str in modData.Strings)
			data.Strings.Add(str);
		
		data.GeneralInfo.FunctionClassifications |= modData.GeneralInfo.FunctionClassifications;
	}

	private const string CHECK_LOGS = "Check the log for more details.";

	private string identifyMod(Mod mod) {
		return $"{mod.DisplayName} (ID \"{mod.ModId}\")";
	}
	public UndertaleData? Patch(List<Mod> mods, Profile profile, string profileLocation, UndertaleData data, Logger logger, Action<string> statusCallback) {
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
			statusCallback($"{message} {CHECK_LOGS}");
		}
		
		bool runModScript(Mod mod, Func<Mod, string> getScriptPath, ScriptGlobals globals) {
			string path = getScriptPath(mod);
			if (path == "")
				return true;
			setStatusAndInfo($"Running script: {path}");
			string relativePath = Path.Combine(mod.FolderName, path);
			string fullStringPath = Path.Combine(profileLocation, relativePath);
			string code;
				
			try {
				code = File.ReadAllText(fullStringPath);
			}
			catch (Exception e) {
				setStatusAndError($"Failed to read script belonging to {identifyMod(mod)}!", e.ToString());
				return false;
			}
			
			// makes errors point to the path of the script
			code = $"#line 1 \"{relativePath}\"\n" + code;
			try {
				CSharpScript.EvaluateAsync(code, scriptOptions, globals);
			}
			catch (CompilationErrorException e) {
				setStatusAndError($"Script belonging to {identifyMod(mod)} threw an exception.", e.GetBaseException().Message);
				return false;
			}
			return true;
		}
		

		List<string> issues = CheckModApplicationIssues(mods);
		if (issues.Count > 0) {
			StringBuilder sb = new StringBuilder("Encountered issues that are preventing mod application!");
			for (int i = 0; i < issues.Count; i++) {
				var issue = issues[i];
				sb.Append($"\n{i + 1}. {issue}");
			}

			setStatusAndInfo(sb.ToString());
			return null;
		}

		overridenAssets = new HashSet<UndertaleObject>();
		foreach (Mod mod in mods) {
			UndertaleData? modData = null;
			if (mod.DatafilePath != "") {
				setStatusAndInfo($"Merging: {mod.DisplayName}");
				string fullDatafilePath = Path.Combine(profileLocation, mod.FolderName, mod.DatafilePath);
				try {
					using FileStream stream = new FileStream(fullDatafilePath, FileMode.Open, FileAccess.Read);
					modData = UndertaleIO.Read(stream);
				}
				catch (Exception e) {
					setStatusAndError($"Failed to load the datafile of {identifyMod(mod)}.", e.ToString());
					return null;
				}
				if (!runModScript(mod, m => m.PreMergeScriptPath, new ScriptGlobals(data, modData)))
					return null;
				merge(data, modData, mod.Mangle, mod.FolderName);
				if (!runModScript(mod, m => m.PostMergeScriptPath, new ScriptGlobals(data, modData)))
					return null;
			}
			
		}


		foreach (Mod mod in mods) {
			if (!runModScript(mod, m => m.PrePatchScriptPath, new ScriptGlobals(data)))
				return null;
		}

		GlobalDecompileContext context = new GlobalDecompileContext(data);
		PatchesRecord record = new PatchesRecord();
		CompileGroup group = new CompileGroup(data, context);
		GameMakerCodeSource source = new GameMakerCodeSource(group);
		
		List<PatchOwner> order = mods.Select(mod => new PatchOwner(mod.ModId)).ToList();
		
		// Map of mods to patch blames
		// patch blame maps map gamemaker code targets
		Dictionary<Mod, PatchBlame> patchBlames = new Dictionary<Mod, PatchBlame>();
		
		foreach (Mod mod in mods) {
			int index = mods.IndexOf(mod);
			if (mod.Patches.Length != 0)
				setStatusAndInfo($"Reading patches from: {mod.DisplayName}");
			patchBlames[mod] = new PatchBlame();
			foreach (PatchLocation patchLocation in mod.Patches) {
				// the only one right now
				Debug.Assert(patchLocation.Type == PatchFormatType.GMLP);
				
				string modFolder = Path.Combine(profileLocation, mod.FolderName);
				string fullPath = Path.Combine(modFolder, patchLocation.Path);
				
				if (Directory.Exists(fullPath)) {
					foreach (string file in Directory.GetFiles(fullPath, "*.gmlp", SearchOption.AllDirectories)) {
						if (!processPatch(file, Path.GetRelativePath(modFolder, file)))
							return null;
					}
				}
				else if (File.Exists(fullPath)) {
					if (!processPatch(fullPath, patchLocation.Path))
						return null;
				}
				else {
					setStatusAndError($"Mod {identifyMod(mod)} specified an invalid patch or patch directory: \"{patchLocation.Path}\"");
					return null;
				}
				


				bool processPatch(string patchPath, string relativePath) {
					List<string> targets;
					string patchText;
					try {
						patchText = File.ReadAllText(patchPath);
					}
					catch (Exception e) {
						setStatusAndError($"An error occured while trying to read a patch file at {relativePath} from {identifyMod(mod)}!", e.ToString());
						return false;
					}
					try {
						targets = Language.ExecuteEntirePatch(patchText, source, record, order[index]);
						foreach (string target in targets) {
							if (!patchBlames[mod].ContainsKey(target))
								patchBlames[mod][target] = [relativePath];
							else
								patchBlames[mod][target].Add(relativePath);
						}
						return true;
					}
					catch (PatchExecutionException e) {
						setStatusAndError($"An error occured while executing a patch from {identifyMod(mod)} at {relativePath}!", e.Message);
					}
					catch (InvalidPatchException e) {
						setStatusAndError($"Invalid patch provided by {identifyMod(mod)} at {relativePath}!", e.Message);
					}
					return false;
				}
			}
		}

		setStatusAndInfo("Applying patches...");

		try {
			Language.ApplyPatches(record, source, order);
		}
		catch (PatchApplicationException e) {
			statusCallback($"Patching failed! {CHECK_LOGS}\n{e.Blame()}");
			logger.Error($"Patching failed! {e.FullMessage()}");
			return null;
		}

		setStatusAndInfo("Compiling...");
		
		CompileResult result = group.Compile();
		if (!result.Successful) {
			(string error, List<Mod> modsResponsible) = generateCompileError(result, patchBlames, source);
			string modsResponsibleString = identifyMod(modsResponsible[0]);
			for (int i = 1; i < modsResponsible.Count; i++) {
				modsResponsibleString += $", {identifyMod(modsResponsible[i])}";
			}
			statusCallback($"Compilation failed! {CHECK_LOGS}\nOne or more of the following mods are at fault:\n{modsResponsibleString}");
			logger.Error($"Compilation failed! Below will be a file-by-file analysis,"
				+ " showing which files failed to compile, and which mods change that file."
				+ $"\n{error}");
			return null;
		}
		

		if (profile.SeparateModdedSave)
			data.GeneralInfo.Name.Content = profile.ModdedSaveName;
		
		foreach (Mod mod in mods) {
			if (!runModScript(mod, m => m.PostPatchScriptPath, new ScriptGlobals(data)))
				return null;
		}
		return data;
	}

	private (string, List<Mod>) generateCompileError(CompileResult result, Dictionary<Mod, PatchBlame> patchBlames, GameMakerCodeSource source) {
		List<Mod> modsResponsible = [];
		StringBuilder sb = new StringBuilder();
		int number = 1;
			
		// group errors by file
		IEnumerable<IGrouping<UndertaleCode, CompileError>> errors = result.Errors.GroupBy(error => error.Code);
		foreach (IGrouping<UndertaleCode, CompileError> errorGroup in errors) {
			string fileName = errorGroup.Key.Name.Content;
			sb.AppendLine($"{number}. Code filename: {fileName}");

			foreach (KeyValuePair<Mod, PatchBlame> kvp in patchBlames) {
				if (!kvp.Value.ContainsKey(fileName))
					continue;
				if (!modsResponsible.Contains(kvp.Key))
					modsResponsible.Add(kvp.Key);
				sb.AppendLine($"Mod {identifyMod(kvp.Key)} contains the following patch files that change this code file:");
				foreach (string path in kvp.Value[fileName]) {
					sb.AppendLine(path);
				}
			}
				
			sb.AppendLine("========== ERRORS START ==========");
			int errorIndex = 1;
			// compiler weirdly likes to repeat some errors so we just check if we already said something before saying it
			HashSet<string> alreadySaid = new HashSet<string>();
			foreach (CompileError error in errorGroup) {
				string detailedMessage = error.GenerateDetailedMessage();
				if (!alreadySaid.Contains(detailedMessage)) {
					sb.AppendLine($"{errorIndex}. {detailedMessage}");
					alreadySaid.Add(detailedMessage);
					errorIndex++;
				}
			}
			sb.AppendLine("========== ERRORS END ==========");
				
			string code = source.GetReplacedCodeVerbatim(fileName)!;
			sb.AppendLine($"========== BAD FILE START ==========\n{code}\n========== BAD FILE END ==========");
				

			number += 1;
		}

		return (sb.ToString(), modsResponsible);
	}

	public static bool IsDataPatched(UndertaleData data) {
		return false;
	}
	
	private List<string> CheckModApplicationIssues(List<Mod> mods) {
		List<string> issues = new List<string>();
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
			CheckDepends(mods, mod, idMap, issues);
			CheckBreaks(mods, mod, idMap, issues);
			CheckImports(mods, mod, idMap, issues);
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
				RecommendContingency contingency = (RecommendContingency)import.Contingency;
				lock (issues) {
 					issues.Add($"Mod {identifyMod(mod)} depends on the import \"{import.Name}\" but it is not provided by anyone.\nMod's suggestion: Download {contingency.Name} at {contingency.Link}");
				}
				return;
			}
			if (exporters.Count > 1) {
				string modsResponsibleString = identifyMod(exporters[0]);
				for (int i = 1; i < exporters.Count; i++) {
					modsResponsibleString += $", {identifyMod(exporters[i])}";
				}
				lock (issues) {
					issues.Add($"Mod {identifyMod(mod)} depends on the import \"{import.Name}\" but it is provided more than once,\nby the following mods: {modsResponsibleString}");
				}
				return;
			}


		}
	}
}

public class ScriptGlobals(UndertaleData data, UndertaleData? modData = null) {
	public UndertaleData Data = data;
	public UndertaleData? ModData = modData;
}