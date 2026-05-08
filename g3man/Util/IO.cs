using System.Diagnostics;
using System.Security.Cryptography;
using g3man.Models;
using UndertaleModLib;

namespace g3man.Util;

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
		SymlinkFolder(profilePath, profileLink);
	}

	private static void deleteLegacySymlink(string gameDirectory) {
		string appliedProfileSymlink = Path.Combine(gameDirectory, AppliedProfileSymlinkName);
		DeleteSymlink(appliedProfileSymlink);
	}
	
	public static void CreateLegacySymlink(string gameDirectory, string profilePath) {
		string appliedProfileSymlink = Path.Combine(gameDirectory, AppliedProfileSymlinkName);
		deleteLegacySymlink(gameDirectory);
		SymlinkFolder(profilePath, appliedProfileSymlink);
	}
	
	public static void Apply(UndertaleData data, 
							string gameDirectory, 
							string outputDatafileRelativePath,
							bool writeHash) 
	{
		
		string tempFilePath = Path.Combine(gameDirectory, TempDataName);
		byte[] hashBytes = null!;
		
		using MemoryStream memoryStream = new MemoryStream();
		UndertaleIO.Write(memoryStream, data);
		memoryStream.Position = 0;
		if (writeHash)
			hashBytes = MD5.HashData(memoryStream);
		File.WriteAllBytes(tempFilePath, memoryStream.GetBuffer().AsSpan(0, (int)memoryStream.Length));
	

		string g3manFolder = Path.Combine(gameDirectory, "g3man");
		if (!Directory.Exists(g3manFolder))
			Directory.CreateDirectory(g3manFolder);
		
		deleteLegacySymlink(gameDirectory);
		if (writeHash) {
			WriteGameLastOutputHash(gameDirectory, hashBytes);
		}

		File.Move(tempFilePath, Path.Combine(gameDirectory, outputDatafileRelativePath), true);
		File.Delete(tempFilePath);
	}

	private static void WriteGameLastOutputHash(string gameDirectory, byte[] hashBytes) {
		string hash = HashToString(hashBytes);
		string outputHashTextFilePath = Path.Combine(gameDirectory, "g3man", OutputHashTextFileName);
		File.WriteAllText(outputHashTextFilePath, hash);
	}
	
	
	/* On normal operating systems, this makes a symlink.
	 * On Windows, this makes a "Junction". */
	private static void SymlinkFolder(string targetDirectory, string path) {
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

	private static void DeleteSymlink(string path) {
		if (File.Exists(path))
			File.Delete(path);
		else if (Directory.Exists(path))
			Directory.Delete(path, false);
	}

	public static void OpenFileExplorer(string directory) {
		try {
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
		catch (Exception e) {
			Program.Logger.Error(e);
		}
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
	 * Deletes the last output hash. Can throw exceptions.
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