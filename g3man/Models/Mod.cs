using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using g3man.Util;

namespace g3man.Models;

public class Mod : IMod {
	public static Logger logger = Logger.Make("MOD-PARSER");
	
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
	
	private Dictionary<string, string> XdeltaPatches;
	private List<string> DatafileXdeltaPatches;
	
	public string PreMergeScriptPath;
	public string PostMergeScriptPath;
	
	public string PrePatchScriptPath;
	public string PostPatchScriptPath;
	
	public RelatedMod[] Depends;
	public RelatedMod[] Suggests;
	public RelatedMod[] Breaks;
	
	public Import[] Imports;
	public string[] Exports;

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
		Description = JsonUtil.GetStringOrThrow(root, "description", "");
		IconPath = JsonUtil.GetStringOrThrow(root, "icon_path", "");
		
		Credits = JsonUtil.GetObjectArrayOrThrow(root, "credits", [])
			.Select(x => new Credit(x)).ToArray();
		
		Depends = JsonUtil.GetObjectArrayOrThrow(root, "depends", [])
			.Select(x => new RelatedMod(x)).ToArray();
		
		Homepage = JsonUtil.GetStringOrThrow(root, "homepage", "");
		Source = JsonUtil.GetStringOrThrow(root, "source", "");
		
		Version = new SemVer(JsonUtil.GetStringOrThrow(root, "version"), false);
		string target_patcher_version = JsonUtil.GetOrDefaultClass(root, "target_patcher_version", "");
		if (target_patcher_version != "") {
			logger.Info(
				$"Warning for mod \"{DisplayName}\" (ID \"{ModId}\"): The field \"target_patcher_version\" is deprecated."
				+ "\nPlease instead specify \"target_g3man_version\" as an integer instead.");

			TargetPatcherVersion = int.Parse(target_patcher_version);
		}
		else {
			TargetPatcherVersion = JsonUtil.GetNumberOrThrow(root, "target_g3man_version");
		}
		

		Patches = JsonUtil.GetObjectArrayOrThrow(root, "patches", [])
			.Select(x => new PatchLocation(x)).ToArray();
		DatafilePath = JsonUtil.GetOrDefaultClass(root, "datafile_path", "");

		if (root.TryGetProperty("xdelta_patches", out JsonElement xdeltaPatches))
			(XdeltaPatches, DatafileXdeltaPatches) = DeserializeXdeltaPatches(xdeltaPatches);
		else {
			XdeltaPatches = new Dictionary<string, string>();
			DatafileXdeltaPatches = [];
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
		
		FolderName = folderName;
	}

	public bool HasAnyScripts() {
		return PreMergeScriptPath != "" || PostMergeScriptPath != "" || PrePatchScriptPath != "" || PostPatchScriptPath != "";
	}
	
	public static List<Mod> ParseAll(string directory) {
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
			JsonDocument jsonDoc;
			try {
				string text = File.ReadAllText(fullPath);
				jsonDoc = JsonDocument.Parse(text);
			}
			catch (Exception e) {
				logger.Error("Couldn't find or load mod.json at " + fullPath + ":\n" + e.Message);
				return;
			}

			void onError(Exception e) {
				logger.Error("Invalid mod.json at " + fullPath + ":\n" + e.Message);
			}

			try {
				string folderName = Path.GetFileName(modFolder);
				Mod mod = new Mod(jsonDoc.RootElement, folderName);
				if (folderName != mod.ModId)
					throw new InvalidDataException($"Mod's ID does not match with its folder name. ID is \"{mod.ModId}\", but found it in folder \"{folderName}\"");
				mods.Add(mod);
			}
			catch (InvalidDataException e) {
				onError(e);
			}
			catch (InvalidModException e) {
				onError(e);
			}
		});


		return mods.ToList();
	}

	public List<XdeltaSourcePair> GetXdeltaTargetPairs(string gameFolder, string profileFolder) {
		return XdeltaPatches.Select(kvp => new XdeltaSourcePair(gameFolder, kvp.Key, Path.Combine(profileFolder, ModId), kvp.Value)).ToList();
	}

	public List<Xdelta> GetDatafileXdeltaPatches(string profileFolder) {
		return DatafileXdeltaPatches.Select(p => new Xdelta(Path.Combine(profileFolder, ModId), p)).ToList();
	}

	public void Delete(string profileFolder) {
		Debug.Assert(ModId != "");
		string modFolder = Path.Combine(profileFolder, ModId);
		Directory.Delete(modFolder, true);
	}
	
	private (Dictionary<string, string>, List<string>) DeserializeXdeltaPatches(JsonElement element) {
		Dictionary<string, string> dict = new Dictionary<string, string>();
		List<string> datafilePatches = [];
		foreach (JsonProperty property in element.EnumerateObject()) {
			if (property.Value.ValueKind != JsonValueKind.String) {
				throw new InvalidDataException(
					$"In field xdelta_patches: Expected a map of strings to strings but found a {property.Value.ValueKind}");
			}
			if (IO.DatafileNames.Contains(property.Name))
				datafilePatches.Add(property.Value.GetString()!);
			else
				dict[property.Name] = property.Value.GetString()!;
		}
		return (dict, datafilePatches);
	}
}
public class InvalidModException(string message) : Exception(message);

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
public class InvalidPatchTypeException(string message) : InvalidModException(message);


public enum PatchFormatType {
	GMLP
}

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

public readonly struct SemVer() {
	public readonly int Major;
	public readonly int Minor;
	public readonly int Patch;

	public SemVer(string version, bool shorteningAllowed = false) : this() {
		const string help1 = "Mods should have versions of the form \"major.minor.patch\", like \"1.0.0\", or \"2.3.4\"";
		const string help2 = "Mod relations should have versions of the form \"major.minor.patch\" (with shortening allowed), like \"1.0.0\" or \"2.3\"";
		string help = shorteningAllowed ? help2 : help1;
		string[] sections = version.Split(".");

		int ParseSection(string section) {
			return int.Parse(section);
		}

		if (!shorteningAllowed && sections.Length != 3) {
			throw new InvalidSemVerException($"Field \"version\" has too little dots. {help}");
		}
		try {
			switch (sections.Length) {
				case 0:
					throw new InvalidSemVerException($"Field \"version\" is blank. {help}");
				case 1:
					Major = ParseSection(sections[0]);
					Minor = 0;
					Patch = 0;
					break;
				case 2:
					Major = ParseSection(sections[0]);
					Minor = ParseSection(sections[1]);
					Patch = 0;
					break;
				case 3:
					Major = ParseSection(sections[0]);
					Minor = ParseSection(sections[1]);
					Patch = ParseSection(sections[2]);
					break;
				default:
					throw new InvalidSemVerException($"Field \"version\" has too many dots. {help}");
			}
		}
		catch (Exception e) {
			if (e is FormatException || e is OverflowException)
				throw new InvalidSemVerException($"Field \"version\" does not have valid numbers. {help}");
            throw;
        }
	}
	public override string ToString() {
		return $"{Major}.{Minor}.{Patch}";
	}
}
public class InvalidSemVerException(string message) : InvalidModException(message);

public readonly struct SemVerRequirements() {
	private readonly (SemVer, SemVerComparison)[] Conditions;

	private (SemVerComparison, int) GetComparison(string requirementString) {
		char first = requirementString[0];
		char second = requirementString[1];
		if (first == '~')
			return (SemVerComparison.RoughlyEquals, 1);
		if (first == '=')
			return (SemVerComparison.Equals, 1);
		if (first == '>') {
			if (second == '=')
				return (SemVerComparison.GreaterEquals, 2);
			return (SemVerComparison.Greater, 1);
		}
		if (first == '<') {
			if (second == '=')
				return (SemVerComparison.LesserEquals, 2);
			return (SemVerComparison.Lesser, 1);
		}
		return (SemVerComparison.RoughlyEquals, 0);
	}
	public SemVerRequirements(string[] requirementStrings) : this() {
		Conditions = new (SemVer, SemVerComparison)[requirementStrings.Length];
		for (int i = 0; i < requirementStrings.Length; i++) {
			string requirementString = requirementStrings[i];
			if (requirementString.Length < 2)
				throw new InvalidSemVerRequirementException("Version requirement string is too short! ");

			(SemVerComparison comparison, int start) = GetComparison(requirementString);
			string version = requirementString.Substring(start);
			Conditions[i] = (new SemVer(version, true), comparison);
		}
	}

	public bool IsCompatibleWith(SemVer other) {
		foreach ((SemVer requirement, SemVerComparison comparison) in Conditions) {
			bool compatible = isCompatibleWith(requirement, comparison, other);
			if (compatible)
				return true;
		}
		return false;
	}

	private static bool isCompatibleWith(SemVer requirement, SemVerComparison comparison, SemVer other) {
		bool exactEqual = requirement.Major == other.Major 
		                  && requirement.Minor == other.Minor 
		                  && requirement.Patch == other.Patch;
		bool greater = semVerGreaterCompatible(other, requirement);
		bool lesser = semVerGreaterCompatible(requirement, other);
		switch (comparison) {
			case SemVerComparison.RoughlyEquals:
				if (requirement.Major != other.Major)
					return false;
				if (requirement.Minor != other.Minor)
					return false;
				return (requirement.Patch <= other.Patch);
			case SemVerComparison.Equals:
				return exactEqual;
			case SemVerComparison.Greater:
				return greater;
			case SemVerComparison.Lesser:
				return lesser;
			case SemVerComparison.GreaterEquals:
				return exactEqual || greater;
			case SemVerComparison.LesserEquals:
				return exactEqual || lesser;
			default:
				return false;
		}
	}
	
	private static bool semVerGreaterCompatible(SemVer one, SemVer two) {
		if (one.Major > two.Major)
			return true;
		if (one.Major < two.Major)
			return false;
		if (one.Minor > two.Minor)
			return true;
		if (one.Minor < two.Minor)
			return false;
		return one.Patch < two.Patch;
	}

	public override string ToString() {
		if (Conditions.Length == 0)
			return "None";
		
		string result = conditionToString(Conditions[0].Item1, Conditions[0].Item2);
		for (int i = 1; i < Conditions.Length; i++) {
			result += $" OR {conditionToString(Conditions[i].Item1, Conditions[i].Item2)}";
		}

		return result;
	}

	private string conditionToString(SemVer version, SemVerComparison comparison) {
		string operation = comparison switch {
			SemVerComparison.Equals => "=",
			SemVerComparison.RoughlyEquals => "~",
			SemVerComparison.Greater => ">",
			SemVerComparison.Lesser => "<",
			SemVerComparison.GreaterEquals => ">=",
			SemVerComparison.LesserEquals => "<=",

			_ => "="
		};

		return $"{operation}{version}";
	}
}

public enum SemVerComparison {
	GreaterEquals,
	LesserEquals,
	Greater,
	Lesser,
	RoughlyEquals,
	Equals
}
public class InvalidSemVerRequirementException(string message) : InvalidModException(message);

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



public class InvalidContingencyException(string message) : InvalidImportException(message);