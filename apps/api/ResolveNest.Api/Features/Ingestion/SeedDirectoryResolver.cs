namespace ResolveNest.Api.Features.Ingestion;

public sealed record ResolvedSeedDirectory(string FullPath, string DisplayPath);

public static class SeedDirectoryResolver
{
    public const string RepositoryRootMarker = "ResolveNest.slnx";
    public const string ExternalDisplayPrefix = "external";

    public static ResolvedSeedDirectory Resolve(string requestedDirectory, string currentDirectory, string applicationBaseDirectory)
    {
        var repositoryRoot = FindRepositoryRoot(currentDirectory) ?? FindRepositoryRoot(applicationBaseDirectory);
        var baseDirectory = repositoryRoot ?? currentDirectory;
        var fullPath = Path.TrimEndingDirectorySeparator(
            Path.GetFullPath(Path.IsPathRooted(requestedDirectory) ? requestedDirectory : Path.Combine(baseDirectory, requestedDirectory)));

        if (repositoryRoot is not null)
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, fullPath);
            var isInsideRepository = relativePath != ".." && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) && !Path.IsPathRooted(relativePath);
            if (isInsideRepository)
            {
                return new ResolvedSeedDirectory(fullPath, relativePath.Replace('\\', '/'));
            }
        }

        return new ResolvedSeedDirectory(fullPath, $"{ExternalDisplayPrefix}/{Path.GetFileName(fullPath)}");
    }

    private static string? FindRepositoryRoot(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryRootMarker)))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
