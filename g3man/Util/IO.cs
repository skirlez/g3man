using System.Diagnostics;
using UndertaleModLib;

namespace g3man;

public static class IO {
	
	public const string TempDataName = "g3man_temp_data.win";
	public const string AppliedProfileSymlinkName = "g3man_applied_profile";
	
	public static void Apply(UndertaleData data, string gameDirectory, string appliedProfileDirectory, string datafileName) {
		string tempFilePath = Path.Combine(gameDirectory, TempDataName);
		{
			using FileStream stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write);
			UndertaleIO.Write(stream, data);
		}
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
				Process.Start("xdg-open", [directory]);
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
	
	
}