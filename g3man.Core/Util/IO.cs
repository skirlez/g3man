using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using g3man.Core.Models;
using g3man.Core.Patching;
using UndertaleModLib;
using UndertaleModLib.Models;

#if WINDOWS
	using System.Runtime.InteropServices;
#endif

namespace g3man.Core.Util;

public static class IO {
	
	public const string TempDataName = "g3man_temp_data.win";
	public const string AppliedProfileSymlinkName = "g3man_applied_profile";
	public const string OutputHashTextFileName = "last_hash.txt";
	
	public static readonly string[] DatafileNames = ["data.win", "game.unx", "game.ios", "game.droid"];
	
	// i am completely guessing on where game.ios would be lol
	public static readonly string[] DatafileRelativePaths = ["data.win", "assets/game.unx", "assets/game.ios", "assets/game.droid"];

	public static string CommaSeparatedDatafilePaths() {
		return string.Join(", ", DatafileRelativePaths);
	}


	public static void CreateLiveFolder(string profilePath, string profileLivePath) {
		string profileLink = Path.Combine(profileLivePath, "profile");
		// TODO: don't delete folder itself, just contents
		if (Directory.Exists(profileLivePath)) {
			DeleteSymlink(profileLink);
			Directory.Delete(profileLivePath, true);
		}
		Directory.CreateDirectory(profileLivePath);
		LinkFolder(profilePath, profileLink);
	}

	private static void deleteLegacySymlink(string gameDirectory) {
		string appliedProfileSymlink = Path.Combine(gameDirectory, AppliedProfileSymlinkName);
		DeleteSymlink(appliedProfileSymlink);
	}
	
	public static void CreateLegacySymlink(string gameDirectory, string profilePath) {
		string appliedProfileSymlink = Path.Combine(gameDirectory, AppliedProfileSymlinkName);
		deleteLegacySymlink(gameDirectory);
		LinkFolder(profilePath, appliedProfileSymlink);
	}

	public static void Apply(UndertaleData data, 
			int vanillaAudioGroupCount, 
			List<AudioGroupTransfer> audioGroupTransfers,
			Game game,
			Profile profile) {

		string tempFolder = Directory.CreateTempSubdirectory("g3man").FullName;
		
		string g3manFolder = Path.Combine(game.Directory, "g3man");
		if (!Directory.Exists(g3manFolder))
			Directory.CreateDirectory(g3manFolder);

		DeleteModdedAudioGroups(game.Directory, vanillaAudioGroupCount);
		
		string outFolder;
		if (game.OverwriteGameFiles)
			outFolder = game.Directory;
		else {
			string stageDirectory = Path.Combine(g3manFolder, "stages", profile.ID);
			if (Directory.Exists(stageDirectory)) {
				DeleteSymlink(Path.Combine(stageDirectory, "g3man"));
				Directory.Delete(stageDirectory, true);
			}
			
			// to create the stage, we create all the same folders as the original, but link the files instead of copying.
			// I prefer this to linking all top-level files and folders, as it cannot create issues when deleting the stage
			// (since you can't somehow traverse back to the original game folder)
			
			// if needed, in the future, in environments where we can't have symlinks, this code could easily be modified
			// to copy files instead
			
			Directory.CreateDirectory(stageDirectory);
			List<string> ignoreFiles = [
				game.GetInputDatafileRelativePath(), .. getAllAudioGroupDatFiles(game.Directory)
			];
			List<string> ignoreFolders = ["g3man"];
			HashSet<string> files = [];
			HashSet<string> folders = [];
			GetRecursiveDirectoryInfo(game.Directory, game.Directory, ignoreFiles, ignoreFolders, files, folders);
			foreach (string folder in folders)
				Directory.CreateDirectory(Path.Combine(stageDirectory, folder));
			LinkFolder(g3manFolder, Path.Combine(stageDirectory, "g3man"));
			foreach (string file in files)
				LinkFileRelativelyIfPossible(Path.Combine(game.Directory, file), Path.Combine(stageDirectory, file));

			// we can link any audiogroup that isn't involved in any merges (can be used as-is from the game's folder)
			foreach (int index in Enumerable.Range(1, vanillaAudioGroupCount - 1).Where(i => !audioGroupTransfers.Any(t => t.Merge && t.NewIndex == i))) {
				LinkFileRelativelyIfPossible(Path.Combine(game.Directory, $"audiogroup{index}.dat"), Path.Combine(stageDirectory, $"audiogroup{index}.dat"));
			}
			
			outFolder = stageDirectory;
		}

		string modsFolder = game.GetProfileFolderPath(profile);
		
		// link to mod audiogroup files
		foreach (AudioGroupTransfer transfer in audioGroupTransfers) {
			string oldDatPath = Path.Combine(modsFolder, transfer.Mod.ModId, $"audiogroup{transfer.OriginalIndex}.dat");
			string newDatPath = Path.Combine(outFolder, $"audiogroup{transfer.NewIndex}.dat");
			if (!transfer.Merge) {
				LinkFileRelativelyIfPossible(oldDatPath, newDatPath);
			}
		}
		
		foreach (IGrouping<int, AudioGroupTransfer> mergeTransfers in audioGroupTransfers.Where(t => t.Merge)
					.GroupBy(t => t.NewIndex))
		{
			string audioGroupName = $"audiogroup{mergeTransfers.Key}.dat";
			string targetDatPath = Path.Combine(game.Directory, audioGroupName);
			UndertaleData audiogroupDat = MergeAudioGroups(targetDatPath, mergeTransfers, modsFolder, createRecord: false);

			string tempOutPath = Path.Combine(tempFolder, audioGroupName);
			{
				using FileStream output = new(tempOutPath, FileMode.Create, FileAccess.Write);
				UndertaleIO.Write(output, audiogroupDat);
			}
			File.Move(tempOutPath, Path.Combine(outFolder, audioGroupName), overwrite: true);
		}

		byte[] hash = null!;
		{
			using MemoryStream memoryStream = new();
			UndertaleIO.Write(memoryStream, data);
			if (game.OverwriteGameFiles) {
				memoryStream.Seek(0, SeekOrigin.Begin);
				hash = MD5.HashData(memoryStream);
			}

			File.WriteAllBytes($"{tempFolder}/datafile", memoryStream.GetBuffer().AsSpan(0, (int)memoryStream.Length));
		}
		
		File.Move($"{tempFolder}/datafile", Path.Combine(outFolder, game.GetInputDatafileRelativePath()), overwrite: true);
		if (game.OverwriteGameFiles)
			WriteGameLastOutputHash(outFolder, hash);
	}
	
	private static void GetRecursiveDirectoryInfo(string basis, string path, List<string> ignoreFiles, List<string> ignoreFolders, HashSet<string> outputFiles, HashSet<string> outputFolders) {
		foreach (string file in Directory.GetFiles(path).Select(x => Path.GetRelativePath(basis, x))) {
			if (ignoreFiles.Any(x => ProgramPaths.FilePathsEqual(x, file)))
				continue;
			outputFiles.Add(file);
		}
		foreach (string directoryPath in Directory.GetDirectories(path)) {
			string directory = Path.GetRelativePath(basis, directoryPath);
			if (ignoreFolders.Any(x => ProgramPaths.FolderPathsEqual(x, directory)))
				continue;
			outputFolders.Add(directory);
			GetRecursiveDirectoryInfo(basis, directoryPath, ignoreFiles, ignoreFolders, outputFiles, outputFolders);
		}
	}
	
	private static UndertaleData MergeAudioGroups(string targetPath, IGrouping<int, AudioGroupTransfer> grouping, string modsFolder, bool createRecord) {
		UndertaleData targetDat;
		byte[] vanillaHash;
		byte[] potentialHash; 
		{
			using FileStream s = new(targetPath, FileMode.Open, FileAccess.Read);
			potentialHash = MD5.HashData(s);
			targetDat = UndertaleIO.Read(s);
		}
			
		// TODO: right now this system just assumes an error with finding the header just means the header doesn't exist...
		// cause it's very convenient to do that
		if (targetDat.EmbeddedAudio.Count > 0) {
			UndertaleEmbeddedAudio last = targetDat.EmbeddedAudio.Last();
			AudioRecord? record = AudioRecord.Read(last.Data);
			if (record != null) {
				while (record.OriginalEntriesCount != (uint)targetDat.EmbeddedAudio.Count)
					targetDat.EmbeddedAudio.RemoveAt((int)record.OriginalEntriesCount);
				vanillaHash = record.OriginalHash;
			}
			else
				vanillaHash = potentialHash;
		}
		else
			vanillaHash = potentialHash;

		int vanillaCount = targetDat.EmbeddedAudio.Count;
			
		foreach (AudioGroupTransfer transfer in grouping) {
			string modDatPath = Path.Combine(modsFolder, transfer.Mod.ModId, $"audiogroup{transfer.OriginalIndex}.dat");
			UndertaleData modDat;
			{
				using FileStream s = new(modDatPath, FileMode.Open, FileAccess.Read);
				modDat = UndertaleIO.Read(s);
			}
			foreach (UndertaleEmbeddedAudio audio in modDat.EmbeddedAudio)
				targetDat.EmbeddedAudio.Add(audio);
		}

		if (createRecord) {
			UndertaleEmbeddedAudio recordHolder = new();
			recordHolder.Data = AudioRecord.Write((uint)vanillaCount, vanillaHash);
			targetDat.EmbeddedAudio.Add(recordHolder);
		}

		return targetDat;
	}




	public static void WriteGameLastOutputHash(string gameDirectory, byte[] hashBytes) {
		string hash = HashToString(hashBytes);
		WriteGameLastOutputHash(gameDirectory, hash);
	}
	public static void WriteGameLastOutputHash(string gameDirectory, string hash) {
		string outputHashTextFilePath = Path.Combine(gameDirectory, "g3man", OutputHashTextFileName);
		File.WriteAllText(outputHashTextFilePath, hash);
	}

	private static List<string> getAllAudioGroupDatFiles(string gameDirectory) {
		return Directory.GetFiles(gameDirectory)
			.Select(x => Path.GetFileName(x)).Where(x => Regex.IsMatch(x, @"audiogroup\d\.dat")).ToList();
	}
	
	public static void DeleteModdedAudioGroups(string gameDirectory, int vanillaAudioGroupsCount) {
		List<string> audioGroupFiles = getAllAudioGroupDatFiles(gameDirectory);
		foreach (string audioGroupFile in audioGroupFiles) {
			int number = int.Parse(audioGroupFile.Remove(0, "audiogroup".Length).Replace(".dat", ""));
			if (vanillaAudioGroupsCount <= number)
				File.Delete(Path.Combine(gameDirectory, audioGroupFile));
		}
	}
	
	/* On normal operating systems, this makes a symlink.
	 * On Windows, this makes a "Junction". */
	private static void LinkFolder(string targetDirectory, string path) {
		#if LINUX || OSX
			File.CreateSymbolicLink(path, targetDirectory);
		#elif WINDOWS
			Process.Start(new ProcessStartInfo {
			    FileName = "cmd.exe",
			    Arguments = $"/c mklink /J \"{path}\" \"{targetDirectory}\"",
			    RedirectStandardOutput = true,
			    UseShellExecute = false,
			    CreateNoWindow = true
			});
		#else
			throw new Exception("Function not implemented for this OS");
		#endif
	}
	
	/* On normal operating systems, this makes a symlink.
	* On Windows, this makes a hard link. */
#if WINDOWS
	[DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
	static extern bool CreateHardLink
	(
		string lpFileName,
		string lpExistingFileName,
		IntPtr lpSecurityAttributes
	);
#endif
	
	private static void LinkFile(string targetFile, string path) {
		#if LINUX || OSX
			File.CreateSymbolicLink(path, targetFile);
		#elif WINDOWS
			// TODO this requires admin (i think)
			CreateHardLink(path, targetFile, IntPtr.Zero);
			// Switch to File.CreateHardLink when we upgrade to .NET 11
		#else
			throw new Exception("Function not implemented for this OS");
		#endif
	}
	
	private static void LinkFileRelativelyIfPossible(string targetFile, string path) {
		#if LINUX || OSX
			if (Path.GetDirectoryName(path) is null || Path.GetDirectoryName(targetFile) is null)
				LinkFile(targetFile, path);
			LinkFile(Path.GetRelativePath(Path.GetDirectoryName(path)!, targetFile), path);
		#else
			LinkFile(targetFile, path);
		#endif
	}
	private static void DeleteSymlink(string path) {
		if (File.Exists(path))
			File.Delete(path);
		else if (Directory.Exists(path))
			Directory.Delete(path, false);
	}


	public static void OpenFileExplorer(string directory) {
		#if LINUX
			ProcessStartInfo info = new ProcessStartInfo() {
				FileName = "xdg-open",
				Arguments = $"\"{directory}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
			};
			Process.Start(info);
		#elif WINDOWS
			Process.Start(new ProcessStartInfo {
				FileName = directory,
				UseShellExecute = true,
			});
		#else
			throw new Exception("Function not implemented for this OS");
		#endif
	}


	public static void Deapply(Game game) {
		string appliedProfileSymlink = Path.Combine(game.Directory, AppliedProfileSymlinkName);
		if (Directory.Exists(appliedProfileSymlink))
			Directory.Delete(appliedProfileSymlink, false);
		byte[] hashBytes;
		using (FileStream stream = new FileStream(game.GetCleanDatafilePath(), FileMode.Open, FileAccess.Read)) {
			hashBytes = MD5.HashData(stream);		
		}
		File.Copy(game.GetCleanDatafilePath(), game.GetInputDatafilePath(), true);
		WriteGameLastOutputHash(game.Directory, hashBytes);
	}

	
	/**
	 * Gets last output hash. Does not throw, in case the file is not readable, returns an empty string.
	 */
	public static string GetLastOutputHash(Game game) {
		string fullPath = Path.Combine(game.Directory, "g3man", OutputHashTextFileName);
		try {
			return File.ReadAllText(fullPath);
		}
		catch {
			return "";
		}
	}

	/**
	 * Deletes the last output hash.
	 */
	public static void RemoveLastOutputHash(Game game) {
		string fullPath = Path.Combine(game.Directory, "g3man", OutputHashTextFileName);
		File.Delete(fullPath);
	}

	public static string HashToString(byte[] hashBytes) {
		return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
	}

	// https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
	public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive) {
		// Get information about the source directory
		var dir = new DirectoryInfo(sourceDir);

		// Check if the source directory exists
		if (!dir.Exists)
			throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

		// Cache directories before we start copying
		DirectoryInfo[] dirs = dir.GetDirectories();

		// Create the destination directory
		Directory.CreateDirectory(destinationDir);

		// Get the files in the source directory and copy to the destination directory
		foreach (FileInfo file in dir.GetFiles())
		{
			string targetFilePath = Path.Combine(destinationDir, file.Name);
			file.CopyTo(targetFilePath);
		}

		// If recursive and copying subdirectories, recursively call this method
		if (recursive)
		{
			foreach (DirectoryInfo subDir in dirs)
			{
				string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
				CopyDirectory(subDir.FullName, newDestinationDir, true);
			}
		}
	}
	
	
	public static (T Mod, XdeltaSourcePair FailedPatch)? CreateXdeltaFoldersAndApply<T>(string gameDirectory, string profilePath, string profileLivePath, List<T> mods) where T : IMod {
		foreach (T mod in mods) {
			List<XdeltaSourcePair> patches = mod.GetXdeltaTargetPairs(gameDirectory, profilePath);
			if (patches.Count == 0)
				continue;

			string xdeltaFolder = Path.Combine(profileLivePath, "xdelta", mod.ModId);
			Directory.CreateDirectory(xdeltaFolder);
			foreach (XdeltaSourcePair patch in patches) {
				if (DatafileNames.Contains(Path.GetFileName(patch.RelativeSourcePath)))
					continue;
				string? relativeSourcePathFolder = Path.GetDirectoryName(patch.RelativeSourcePath);
				if (relativeSourcePathFolder is null)
					return (mod, patch);
				Directory.CreateDirectory(Path.Combine(xdeltaFolder, relativeSourcePathFolder));
				using FileStream stream = new FileStream(Path.Combine(xdeltaFolder, patch.RelativeSourcePath), FileMode.Create);
				int ret = patch.Decode(stream);
				if (ret != 1) {
					return (mod, patch);
				}
			}
		}
		return null;
	}
}

public record AudioGroupTransfer(Mod Mod, int OriginalIndex, int NewIndex, bool Merge);
