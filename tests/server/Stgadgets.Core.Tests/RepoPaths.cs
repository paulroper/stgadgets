namespace Stgadgets.Core.Tests;

internal static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    public static string Maps => Path.Combine(Root, "maps");

    public static string Recording(string game, string name) => Path.Combine(Root, "tests", "fixtures", "recordings", game, name);

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "global.json")))
                return dir.FullName;
        }
        throw new InvalidOperationException("Couldn't find the repository root (global.json)");
    }
}
