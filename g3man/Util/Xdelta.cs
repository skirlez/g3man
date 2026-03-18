using System.Runtime.InteropServices;
using g3man.Models;

namespace g3man.Util;



public readonly struct Xdelta(string path) {
	public readonly string Filename = System.IO.Path.GetFileName(path);
	public readonly string Filepath = path;

	public static List<Xdelta> FromMods(IEnumerable<IMod> mods, string profileFolder) {
		return mods.Select(mod => mod.GetXdeltaPath(profileFolder)).Where(path => path != "").Select(x => new Xdelta(x)).ToList();
	}
	
	
	#if LINUX
		[DllImport("libg3man.so")]
		private static extern int start_decode(string source_path, string input_path);
		
		
		[DllImport("libg3man.so")]
		private static extern unsafe int start_decode_from_memory(byte* source, int source_length, string input_path);
		
		[DllImport("libg3man.so")]
		private static extern ReturnCode decode(out IntPtr written_buffer, out int written_count);
	#endif
		
	
	private enum ReturnCode {
		TAKE_OUTPUT = 0,
		CALL_AGAIN = 1,
		DONE = 2,
		ERROR = 3
	}
	
	public int Decode(string sourcePath, MemoryStream outputStream) {
		int start_ret = start_decode(sourcePath, Filepath);
		if (start_ret != 0)
			return start_ret;
		return DecodeLoop(outputStream);
	}
	
	public int DecodeFromMemory(byte[] source, MemoryStream outputStream) {
		unsafe {
			fixed (byte* ptr = source) {
				int start_ret = start_decode_from_memory(ptr, source.Length, Filepath);
				if (start_ret != 0)
					return start_ret;
				return DecodeLoop(outputStream);
			}
		}
	}

	private int DecodeLoop(MemoryStream outputStream) {
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
				case ReturnCode.ERROR:
				default:
					Console.WriteLine(ret);
					return 1;
			}
		}
	}
}

