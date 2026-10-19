namespace Stgadgets.Cli.Tests;

/// <summary>A scratch folder for tests that need files on disk; deleted on dispose.</summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("stgadgets-tests-").FullName;

    /// <summary>Writes a file under the folder, creating subfolders as needed, and returns its full path.</summary>
    public string Write(string relativePath, string contents) => Write(relativePath, path => File.WriteAllText(path, contents));

    public string Write(string relativePath, byte[] contents) => Write(relativePath, path => File.WriteAllBytes(path, contents));

    private string Write(string relativePath, Action<string> write)
    {
        var path = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        write(path);
        return path;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
