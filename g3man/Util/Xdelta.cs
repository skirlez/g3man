using System.Diagnostics;
using System.Runtime.InteropServices;
using g3man.Models;

namespace g3man.Util;



public readonly struct Xdelta(string containingFolder, string relativePath) {
	/*
	 * Filename of the .xdelta file.
	 */
	private readonly string inputPath = Path.Combine(containingFolder, relativePath);
	public string Filename => Path.GetFileName(inputPath);
	
	public static List<Xdelta> GetDatafileXdeltaPatches(IEnumerable<IMod> mods, string profileFolder) {
		return mods.SelectMany(x => x.GetDatafileXdeltaPatches(profileFolder)).ToList();
	}
	
	private const string libg3man =
		#if LINUX
			"libg3man.so"
		#elif WINDOWS
			"libg3man.dll"
		#endif
	;
	[DllImport(libg3man)]
	private static extern int start_decode(string source_path, string input_path);
	
	[DllImport(libg3man)]
	private static extern unsafe int start_decode_from_memory(byte* source, int source_length, string input_path);
	
	[DllImport(libg3man)]
	private static extern ReturnCode decode(out IntPtr written_buffer, out int written_count);
		
	
	private enum ReturnCode {
		TAKE_OUTPUT = 0,
		CALL_AGAIN = 1,
		DONE = 2,
		ERRORED = 3
	}
	
	public int Decode(string sourcePath, Stream outputStream) {
		int start_ret = start_decode(sourcePath, inputPath);
		if (start_ret != 0)
			return start_ret;
		return DecodeLoop(outputStream);
	}
	
	public int DecodeFromMemory(byte[] source, Stream outputStream) {
		unsafe {
			fixed (byte* ptr = source) {
				int start_ret = start_decode_from_memory(ptr, source.Length, inputPath);
				if (start_ret != 0)
					return start_ret;
				return DecodeLoop(outputStream);
			}
		}
	}

	private int DecodeLoop(Stream outputStream) {
		while (true) {
			ReturnCode ret = decode(out IntPtr outputBuffer, out int outputWritten);
			switch (ret) {
				case ReturnCode.TAKE_OUTPUT:
					unsafe {
						ReadOnlySpan<byte> span = new ReadOnlySpan<byte>((void*)outputBuffer, outputWritten);
						outputStream.Write(span);
					}
					continue;
				case ReturnCode.CALL_AGAIN:
					continue;
				case ReturnCode.DONE:
					return 0;
				case ReturnCode.ERRORED:
				default:
					Console.WriteLine(ret);
					return 1;
			}
		}
	}

	public static bool SequenceEquals(List<Xdelta> a, List<Xdelta> b) {
		return a.Select(x => x.inputPath).Order().SequenceEqual(b.Select(x => x.inputPath).Order());
	}
}

public readonly struct XdeltaSourcePair(string gameFolder, string relativeSourcePath, string containingFolder, string relativeInputPath)
{
	private readonly Xdelta xdelta = new Xdelta(containingFolder, relativeInputPath);
	public string Filename => xdelta.Filename;
	public string RelativeSourcePath => relativeSourcePath;
	
	
	public int Decode(Stream outputStream) {
		return xdelta.Decode(Path.Combine(gameFolder, relativeSourcePath), outputStream);
	}
}

