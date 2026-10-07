namespace Dbvprovas.TestSupport;

public static class RepoPaths
{
    public static string Root { get; } = FindRoot();

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Dbvprovas.slnx")))
                return dir.FullName;
        }

        throw new InvalidOperationException("Repository root (Dbvprovas.slnx) not found.");
    }
}
