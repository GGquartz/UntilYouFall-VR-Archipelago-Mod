namespace UntilYouFall.Shared;

internal static class AtomicFile
{
    public static void Write(string path, Action<string> write, bool overwrite = true)
    {
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            write(temporary);
            File.Move(temporary, path, overwrite);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}
