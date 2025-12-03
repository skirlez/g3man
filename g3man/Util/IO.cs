using System.Diagnostics;
using System.Security.Cryptography;
using g3man.Models;
using UndertaleModLib;

namespace g3man;

public static class IO {
	
	public const string TempDataName = "g3man_temp_data.win";
	public const string AppliedProfileSymlinkName = "g3man_applied_profile";
	public const string OutputHashTextFileName = "g3man_output_hash.txt";
	
	public static void Apply(UndertaleData data, string gameDirectory, string appliedProfileDirectory, string datafileName) {
		string tempFilePath = Path.Combine(gameDirectory, TempDataName);
		
		using (FileStream stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write)){
			UndertaleIO.Write(stream, data);
		}
		byte[] hashBytes;
		using (FileStream stream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read)){
			hashBytes = MD5.HashData(stream);
		}
		
		string hash = HashToString(hashBytes);			
		string outputHashTextFilePath = Path.Combine(gameDirectory, "g3man", OutputHashTextFileName);
		File.WriteAllText(outputHashTextFilePath, hash);
		
		File.Move(tempFilePath, Path.Combine(gameDirectory, datafileName), true);
		File.Delete(tempFilePath);
		
		string g3manFolder = Path.Combine(gameDirectory, "g3man");
		if (!Directory.Exists(g3manFolder))
			Directory.CreateDirectory(g3manFolder);
		
		string appliedProfileSymlink = Path.Combine(gameDirectory, AppliedProfileSymlinkName);
		if (Directory.Exists(appliedProfileSymlink))
			Directory.Delete(appliedProfileSymlink, false);


		SymlinkFolder(appliedProfileDirectory, appliedProfileSymlink);
		
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
			Console.Error.WriteLine(e);
		}
	}


	public static void Deapply(Game game) {
		string appliedProfileSymlink = Path.Combine(game.Directory, AppliedProfileSymlinkName);
		if (Directory.Exists(appliedProfileSymlink))
			Directory.Delete(appliedProfileSymlink, false);
		File.Copy(Program.GetGame()!.GetCleanDatafilePath(), Program.GetGame()!.GetOutputDatafilePath(), true);
	}

	
	/**
	 * Gets last output hash. Does not throw, in case the file is not readable, returns an empty string.
	 */
	public static string GetLastOutputHash(Game game) {
		string fullPath = Path.Combine(game.Directory, "g3man", OutputHashTextFileName);
		try {
			return File.ReadAllText(fullPath);
		}
		catch (Exception e) {
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
}