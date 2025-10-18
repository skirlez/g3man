using System.Collections.Concurrent;
using System.Text.Json;
using g3man.Util;

namespace g3man.Models;

public class Mod {
	public static Logger logger = new Logger("MOD-PARSER");
	
	public string ModId;
	public string DisplayName;
	public string Description;
	public string IconPath;
	public string Version;
	public string TargetGameVersion;
	public string TargetPatcherVersion;
	public PatchLocation[] Patches;
	public string DatafilePath;
	public string PostMergeScriptPath;
	public string[] Credits;
	
	public RelatedMod[] Depends;
	public RelatedMod[] Breaks;

	public string FolderName;
	
	/*
	public Mod(string modId, string displayName, string description, string[] credits, string version, string targetGameVersion, 
	string targetPatcherVersion, string patchesPath, string datafilePath,  RelatedMod[] depends, RelatedMod[] breaks) {
		ModId = modId;
		DisplayName = displayName;
		Description = description;
		Credits = credits;
		Version = version;
		TargetGameVersion = targetGameVersion;
		TargetPatcherVersion = targetPatcherVersion;
		PatchesPath = patchesPath;
		DatafilePath = datafilePath;
		
		Depends = depends;
		Breaks = breaks;
	}
	*/

	
	private Mod(JsonElement root, string folderName) {
		ModId = JsonUtil.GetStringOrThrow(root, "mod_id");
		DisplayName = JsonUtil.GetStringOrThrow(root, "display_name");
		Description = JsonUtil.GetStringOrThrow(root, "description");
		IconPath = JsonUtil.GetStringOrThrow(root, "icon_path");
		Credits = JsonUtil.GetStringArrayOrThrow(root, "credits");
		Version = JsonUtil.GetStringOrThrow(root, "version");
		TargetGameVersion = JsonUtil.GetStringOrThrow(root, "target_game_version");
		TargetPatcherVersion = JsonUtil.GetStringOrThrow(root, "target_patcher_version");
		Patches = JsonUtil.GetObjectArrayOrThrow(root, "patches")
			.Select(x => new PatchLocation(x)).ToArray();
		DatafilePath = JsonUtil.GetStringOrThrow(root, "datafile_path");
		PostMergeScriptPath = JsonUtil.GetStringOrThrow(root, "post_merge_script_path");
		
		Depends = JsonUtil.GetObjectArrayOrThrow(root, "depends")
			.Select(x => new RelatedMod(x)).ToArray();
		Breaks = JsonUtil.GetObjectArrayOrThrow(root, "breaks")
			.Select(x => new RelatedMod(x)).ToArray();
		
		FolderName = folderName;
	}
	
	
	public static List<Mod> Parse(string directory) {
		ConcurrentBag<Mod> mods = new ConcurrentBag<Mod>();
		string[] modFolders;
		try {
			modFolders = Directory.GetDirectories(directory);
		}
		catch (Exception e) {
			// ignored
			return [];
		}
		Parallel.ForEach(modFolders, modFolder => {
			string fullPath = Path.Combine(modFolder, "mod.json");
			JsonDocument jsonDoc;
			try {
				string text = File.ReadAllText(fullPath);
				jsonDoc = JsonDocument.Parse(text);
			}
			catch (Exception e) {
				logger.Error("Couldn't find or load mod.json at " + fullPath + ":\n" + e.Message);
				return;
			}
			
			try {
				Mod mod = new Mod(jsonDoc.RootElement, Path.GetFileName(modFolder));
				mods.Add(mod);
			}
			catch (InvalidDataException e) {
				logger.Error("Invalid mod.json at " + fullPath + ":\n" + e.Message);
			}
		});


		return mods.ToList();
	}
}


public class PatchLocation {
	public string Path;
	public PatchFormatType Type;
	
	public PatchLocation(JsonElement root) {
		Path = JsonUtil.GetStringOrThrow(root, "path");
		string typeString = JsonUtil.GetStringOrThrow(root, "type");
		Type = typeString switch {
			"gmlp" => PatchFormatType.GMLP,
			_ => throw new InvalidPatchTypeException("Invalid patch format type: " + typeString
				+ "\nRight now the only type is \"gmlp\".)")
		};
	}
}
public class InvalidPatchTypeException(string message) : Exception(message);


public enum PatchFormatType {
	GMLP
}

public class RelatedMod {
	public string ModId;
	public string Version;
	public OrderRequirement OrderRequirement;

	public RelatedMod(JsonElement root) {
		ModId = JsonUtil.GetStringOrThrow(root, "mod_id");
		Version = JsonUtil.GetStringOrThrow(root, "version");
		string orderRequirement = JsonUtil.GetStringOrThrow(root, "order_requirement");
		OrderRequirement = orderRequirement switch {
			"before_us" => OrderRequirement.BeforeUs,
			"after_us" => OrderRequirement.AfterUs,
			"irrelevant" => OrderRequirement.Irrelevant,
			_ => throw new InvalidOrderRequirementException("Invalid order requirement: " + orderRequirement
					+ "\nOrder requirements can be \"before_us\", \"after_us\", or \"irrelevant\".")
		};
	}
}

public enum OrderRequirement {
	BeforeUs,
	AfterUs,
	Irrelevant
}
public class InvalidOrderRequirementException(string message) : Exception(message);