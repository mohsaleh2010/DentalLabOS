namespace DentalLabOS.ArchitectureTests;

internal static class Repo
{
    public static readonly string Root = Find();

    public static string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    /// <summary>Source files under src/, excluding build output.</summary>
    public static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(Path("src"), pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{System.IO.Path.DirectorySeparatorChar}bin{System.IO.Path.DirectorySeparatorChar}")
                        && !f.Contains($"{System.IO.Path.DirectorySeparatorChar}obj{System.IO.Path.DirectorySeparatorChar}"));

    public static string Relative(string file) => System.IO.Path.GetRelativePath(Root, file).Replace('\\', '/');

    private static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(System.IO.Path.Combine(dir.FullName, "DentalLabOS.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Repository root (DentalLabOS.sln) not found.");
    }
}
