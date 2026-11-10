// Makes saving to the given file fail on every OS: the file is swapped for a directory, so the atomic replace cannot succeed.
internal sealed class BlockedSaveFile : IDisposable
{
    private readonly string _path;
    private readonly byte[] _content;

    public BlockedSaveFile(string path)
    {
        _path = path;
        _content = File.ReadAllBytes(path);
        File.Delete(path);
        Directory.CreateDirectory(path);
    }

    public void Dispose()
    {
        Directory.Delete(_path);
        File.WriteAllBytes(_path, _content);
    }
}
