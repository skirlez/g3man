using System.Runtime.InteropServices;

namespace g3man.Core.Util;

public class C {
	public const string LIBG3MAN_NAME =
		#if LINUX
			"libg3man.so"	
		#elif WINDOWS
			"libg3man.dll"
		#endif
		;
	[DllImport(C.LIBG3MAN_NAME)]
	private static extern int initialize();

	public static void Initialize() {
		int result = initialize();
	}
}