// See https://aka.ms/new-console-template for more information

using System.Runtime.InteropServices;

namespace gmlpweb;

public class Program {
    
    [UnmanagedCallersOnly(EntryPoint = "concat_test")]
    public static IntPtr ConcatTest(IntPtr a, int aLength, IntPtr b, int bLength)
    {
        return a + b;
    }
}