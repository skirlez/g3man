using System.IO;

public static class Directories {
    public static string TryGuessSteamDirectory()
    {
        string? home = System.Environment.GetEnvironmentVariable("HOME");
        if (home is null) {
            return "";
        }

        return "";
        #if LINUX
            
        #elif WINDOWS
            string home = Environment.GetEnvironmentVariable("HOME");
        #else
            return "";
        #endif
    }
}