namespace Paytness.Scenario;

public static class ContainedPathResolver
{
    public static string Resolve(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new ScenarioValidationException("Contract path must be a non-empty relative path.");

        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string lexicalRoot = Path.GetFullPath(root);
        string lexicalCandidate = Path.GetFullPath(Path.Combine(lexicalRoot, relativePath));
        EnsureContained(lexicalRoot, lexicalCandidate, comparison);

        string physicalRoot = ResolvePhysical(lexicalRoot);
        string physicalCandidate = ResolvePhysical(lexicalCandidate);
        EnsureContained(physicalRoot, physicalCandidate, comparison);
        return physicalCandidate;
    }

    private static void EnsureContained(string root, string candidate, StringComparison comparison)
    {
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, comparison))
            throw new ScenarioValidationException("Contract path escapes the scenario root.");
    }

    private static string ResolvePhysical(string fullPath)
    {
        fullPath = Path.GetFullPath(fullPath);
        string pathRoot = Path.GetPathRoot(fullPath) ?? throw new ScenarioValidationException("Path has no filesystem root.");
        string current = pathRoot;
        foreach (string segment in fullPath[pathRoot.Length..].Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(candidate) ? new DirectoryInfo(candidate) : new FileInfo(candidate);
            if (info.Exists && !string.IsNullOrEmpty(info.LinkTarget))
            {
                FileSystemInfo? target = info.ResolveLinkTarget(returnFinalTarget: true);
                if (target is null) throw new ScenarioValidationException($"Unable to resolve symbolic link '{candidate}'.");
                current = Path.GetFullPath(target.FullName);
            }
            else
            {
                current = candidate;
            }
        }
        return Path.GetFullPath(current);
    }
}
