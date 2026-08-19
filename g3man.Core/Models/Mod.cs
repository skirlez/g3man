using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using g3man.Core.Util;
using PatchCommon;

namespace g3man.Core.Models;

public class Mod : IMod {
	public string ModId { get; }
	public string DisplayName { get; }
	public string Description { get; }
	public string IconPath;
	
	public string Homepage;
	public string Source;
	public Credit[] Credits { get; }

	public SemVer? MaybeVersion => Version;
	public SemVer Version;
	
	public int TargetPatcherVersion;
	public bool CreateOldProfileSymlink => TargetPatcherVersion < 7;
	
	public PatchLocation[] Patches;
	
	public string DatafilePath;
	public NamespacingOptions NamespacingOptions;
	
	// right now, this only supports one patch per file. TODO: support consecutive patches, and TODO: support hashes alongside filenames.
	// the extremely contrived scenario where I imagine that's helpful is a case where a mod wants to use g3man
	// to apply xdelta patches to a game distributed across several platforms
	// (steam/itch.io, or windows/linux, where the datafile could be different for each configuration)
	// So they'd target one of these, and create small "conversion" patches that target the other datafiles
	// so that the main patch can be applied ontop. This is super low priority and would be very annoying though.
	private Dictionary<string, List<string>> XdeltaPatches;
	
	public string PreMergeScriptPath;
	public string PostMergeScriptPath;
	
	public string PrePatchScriptPath;
	public string PostPatchScriptPath;
	
	public RelatedMod[] Depends;
	public RelatedMod[] Suggests;
	public RelatedMod[] Breaks;
	
	public Import[] Imports;
	public string[] Exports;


	
	private Mod(JsonElement root) {
		ModId = JsonUtil.GetStringOrThrow(root, "mod_id");
		DisplayName = JsonUtil.GetStringOrThrow(root, "display_name");
		Description = JsonUtil.GetStringOrThrow(root, "description", "");
		IconPath = JsonUtil.GetStringOrThrow(root, "icon_path", "");
		
		Credits = JsonUtil.GetObjectArrayOrThrow(root, "credits", [])
			.Select(x => new Credit(x)).ToArray();
		
		Depends = JsonUtil.GetObjectArrayOrThrow(root, "depends", [])
			.Select(x => new RelatedMod(x)).ToArray();
		
		Homepage = JsonUtil.GetStringOrThrow(root, "homepage", "");
		Source = JsonUtil.GetStringOrThrow(root, "source", "");
		
		Version = new SemVer(JsonUtil.GetStringOrThrow(root, "version"));
		string target_patcher_version = JsonUtil.GetOrDefaultClass(root, "target_patcher_version", "");
		if (target_patcher_version != "") {
			// TODO
			//logger.Info(
			//	$"Warning for mod \"{DisplayName}\" (ID \"{ModId}\"): The field \"target_patcher_version\" is deprecated."
			//	+ "\nPlease instead specify \"target_g3man_version\" as an integer instead.");

			TargetPatcherVersion = int.Parse(target_patcher_version);
		}
		else {
			TargetPatcherVersion = JsonUtil.GetNumberOrThrow(root, "target_g3man_version");
		}
		

		Patches = JsonUtil.GetObjectArrayOrThrow(root, "patches", [])
			.Select(x => new PatchLocation(x)).ToArray();
		DatafilePath = JsonUtil.GetOrDefaultClass(root, "datafile_path", "");

		if (root.TryGetProperty("xdelta_patches", out JsonElement xdeltaPatches))
			XdeltaPatches = DeserializeXdeltaPatches(xdeltaPatches);
		else {
			XdeltaPatches = new Dictionary<string, List<string>>();
		}


		PreMergeScriptPath = JsonUtil.GetStringOrThrow(root, "pre_merge_script_path", "");
		PostMergeScriptPath = JsonUtil.GetStringOrThrow(root, "post_merge_script_path", "");
		
		PrePatchScriptPath = JsonUtil.GetStringOrThrow(root, "pre_patch_script_path", "");
		PostPatchScriptPath = JsonUtil.GetStringOrThrow(root, "post_patch_script_path", "");
		
		Depends = JsonUtil.GetObjectArrayOrThrow(root, "depends", [])
			.Select(x => new RelatedMod(x)).ToArray();
		Suggests = JsonUtil.GetObjectArrayOrThrow(root, "suggests", [])
			.Select(x => new RelatedMod(x)).ToArray();
		Breaks = JsonUtil.GetObjectArrayOrThrow(root, "breaks", [])
			.Select(x => new RelatedMod(x)).ToArray();

		
		Imports = JsonUtil.GetObjectArrayOrThrow(root, "imports", [])
			.Select(x => new Import(x)).ToArray();
		Exports = JsonUtil.GetStringArrayOrThrow(root, "exports", []);
		if (TargetPatcherVersion < 10) {
			NamespacingOptions = NamespacingOptions.None;
		}
		else {
			if (root.TryGetProperty("namespacing", out JsonElement namespacing))
				NamespacingOptions = new NamespacingOptions(namespacing);
			else
				NamespacingOptions = NamespacingOptions.All;
		}
	}

	public bool HasAnyScripts() {
		return PreMergeScriptPath != "" || PostMergeScriptPath != "" || PrePatchScriptPath != "" || PostPatchScriptPath != "";
	}

	public string FullPath(Game game, Profile profile) {
		return Path.Combine(game.GetProfileFolderPath(profile), ModId);
	}
	public static List<Mod> ParseAll(string directory, Action<Exception, string> errorHandler) {
		ConcurrentBag<Mod> mods = new ConcurrentBag<Mod>();
		string[] modFolders;
		try {
			modFolders = Directory.GetDirectories(directory);
		}
		catch {
			return [];
		}
		
		Parallel.ForEach(modFolders, modFolder => {
			string fullPath = Path.Combine(modFolder, "mod.json");
			try {
				Mod mod = ParseFolder(modFolder);
				mods.Add(mod);
			}
			catch (Exception e) {
				errorHandler(e, fullPath);
			}
		});


		return mods.ToList();
	}
	
	public static Mod ParseFolder(string modFolder) {
		string fullPath = Path.Combine(modFolder, "mod.json");
		string folderName = Path.GetFileName(modFolder);
		Mod mod;
		{
			using FileStream s = new FileStream(fullPath, FileMode.Open, FileAccess.Read);
			mod = Parse(s);
		}
		if (folderName != mod.ModId)
			throw new InvalidDataException($"Profile's ID does not match with its folder name. ID is \"{mod.ModId}\", but found it in folder \"{folderName}\"");
		return mod;
	}
	public static Mod Parse(Stream stream) {
		JsonDocument jsonDoc = JsonDocument.Parse(stream);
		Mod mod = new Mod(jsonDoc.RootElement);
		return mod;
	}

	public List<XdeltaSourcePair> GetXdeltaTargetPairs(string gameFolder, string profileFolder) {
		return XdeltaPatches.SelectMany(
			kvp => kvp.Value.Select(
				path => new XdeltaSourcePair(gameFolder, kvp.Key, Path.Combine(profileFolder, ModId), path)
			)
		).ToList();
	}

	public List<Xdelta> GetDatafileXdeltaPatches(string profileFolder, string datafileName) {
		if (!XdeltaPatches.ContainsKey(datafileName))
			return [];
		return XdeltaPatches[datafileName].Select(path => new Xdelta(profileFolder, path)).ToList();
	}

	public void Delete(string profileFolder) {
		Debug.Assert(ModId != "");
		string modFolder = Path.Combine(profileFolder, ModId);
		Directory.Delete(modFolder, true);
	}
	
	private Dictionary<string, List<string>> DeserializeXdeltaPatches(JsonElement element) {
		Dictionary<string, List<string>> dict = new Dictionary<string, List<string>>();
		foreach (JsonProperty property in element.EnumerateObject()) {
			if (property.Value.ValueKind != JsonValueKind.String) {
				throw new InvalidDataException(
					$"In field xdelta_patches: Expected a map of strings to strings but found a {property.Value.ValueKind}");
			}
			dict[property.Name] = [property.Value.GetString()!];
		}
		return dict;
	}
	
	public string Identify() {
		return $"\"{DisplayName}\" (ID \"{ModId}\")";
	} 
}
public class InvalidModException(string message) : Gexception(message);

public class PatchLocation {
	public string Path;
	public PatchFormatType Type;

	public string Extension {
		get {
			return Type switch {
				PatchFormatType.GMLP => "gmlp",
				PatchFormatType.GMLPv2 => "lua",
				_ => throw new UnreachableException()
			};
		}
	}


	public PatchLocation(JsonElement root) {
		Path = JsonUtil.GetStringOrThrow(root, "path");
		string typeString = JsonUtil.GetStringOrThrow(root, "type");
		Type = typeString switch {
			"gmlp" => PatchFormatType.GMLP,
			"gmlpv2" => PatchFormatType.GMLPv2,
			_ => throw new InvalidPatchTypeException("Invalid patch format type: " + typeString
				+ "\nSupported types: \"gmlp, gmlpv2\".)")
		};
	}
}
public class InvalidPatchTypeException(string message) : InvalidModException(message);




public class RelatedMod {
	public string ModId;
	public SemVerRequirements VersionRequirements;
	public OrderRequirement OrderRequirement;

	public RelatedMod(JsonElement root) {
		ModId = JsonUtil.GetStringOrThrow(root, "mod_id");
		VersionRequirements = new SemVerRequirements(JsonUtil.GetStringArrayOrThrow(root, "versions"));
		string orderRequirement = JsonUtil.GetStringOrThrow(root, "order", "irrelevant");
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
public class InvalidOrderRequirementException(string message) : InvalidModException(message);



public readonly struct Credit {
	public Credit(JsonElement element) {
		if (element.ValueKind == JsonValueKind.String)
			Name = element.GetString()!;
		else if (element.ValueKind == JsonValueKind.Object) {
			bool hasName = element.TryGetProperty("name", out JsonElement nameElement);
			if (!hasName || nameElement.ValueKind != JsonValueKind.String)
				throw new InvalidCreditException("Found element in the \"credits\" field without a \"name\" field, or the \"name\" field did not contain a string.");
			Name = nameElement.GetString()!;
			foreach (JsonProperty other in element.EnumerateObject()) {
				if (!other.Value.ValueKind.Equals(JsonValueKind.String)) {
					throw new InvalidCreditException(
						"Found element in the \"credits\" field with an inner field that isn't a string");
				}
				OtherFields.Add(other.Name, other.Value.GetString()!);
			}
			
		}
		else
			throw new InvalidCreditException("Each element in the \"credits\" field must be a string or an object with a \"name\" field.");
	}
	public readonly string Name;
	public readonly Dictionary<string, string> OtherFields = new Dictionary<string, string>();
}

public class InvalidCreditException(string message) : InvalidModException(message);


public readonly struct Import {
	public readonly string Name;
	public readonly string ContingencyType;
	public readonly Contingency Contingency;

	public Import(JsonElement root) {
		if (root.ValueKind == JsonValueKind.String) {
			Name = root.GetString()!;
			ContingencyType = "give_up";
			Contingency = new GiveUpContingency();
		}
		else if (root.ValueKind == JsonValueKind.Object) {
			Name = JsonUtil.GetStringOrThrow(root, "name");
			ContingencyType = JsonUtil.GetStringOrThrow(root, "contingency_type");
			if (ContingencyType == "give_up")
				Contingency = new GiveUpContingency();
			else if (ContingencyType == "suggest")
				Contingency = new RecommendContingency(JsonUtil.GetPropertyOrThrow(root, "contingency"));
			else
				throw new InvalidImportException(
					$"{ContingencyType} is not a valid contingency type. Valid: \"give_up\", \"suggest\")");
		}
		else
			throw new InvalidCreditException("Elements in the \"imports\" field can only be strings or objects.");
	}
}
public class InvalidImportException(string message) : InvalidModException(message);

public interface Contingency { }

public readonly struct GiveUpContingency : Contingency {
	public GiveUpContingency() {}
}
// i have a feeling it isn't optimal to do it like this and i'm getting tired
public readonly struct RecommendContingency : Contingency {
	public readonly string Name;
	public readonly string Link;
	public RecommendContingency(JsonElement root) {
		Name = JsonUtil.GetStringOrThrow(root, "name");
		Link = JsonUtil.GetStringOrThrow(root, "link");
	}
}


public class InvalidNamespacingOptionsException(string message) : InvalidModException(message);


public readonly struct NamespacingOptions {
	public readonly SuffixingScheme Scheme;

	private NamespacingOptions(SuffixingScheme scheme) {
		Scheme = scheme;
	}
	public static NamespacingOptions None = new(new NoneNamespacingScheme());
	public static NamespacingOptions All = new(new AllNamespacingScheme());
	public NamespacingOptions(JsonElement root) {
		string type = JsonUtil.GetStringOrThrow(root, "type");
		switch (type) {
			case "exclude_prefix":
				string[] includeList = JsonUtil.GetStringArrayOrThrow(root, "list");
				Scheme = new PrefixNamespacingScheme(includeList);
				break;
			case "exclude":
				string[] excludeList = JsonUtil.GetStringArrayOrThrow(root, "list");
				Scheme = new ListNamespacingScheme(excludeList);
				break;
			default:
				throw new InvalidNamespacingOptionsException($"Invalid namespacing scheme type \"{type}\". Supported types: exclude, exclude_prefix");
		}
	}
}



public interface SuffixingScheme {
	public bool IsExcluded(string name);
}

public class NoneNamespacingScheme : SuffixingScheme {
	public bool IsExcluded(string name) {
		return true;
	}
}
public class AllNamespacingScheme : SuffixingScheme {
	public bool IsExcluded(string name) {
		return false;
	}
}


public class ListNamespacingScheme(string[] list) : SuffixingScheme {
	public bool IsExcluded(string name) {
		return list.Contains(name);
	}
}

public class PrefixNamespacingScheme(string[] prefixes) : SuffixingScheme {
	public bool IsExcluded(string name) {
		return prefixes.Any(name.StartsWith);
	}
}