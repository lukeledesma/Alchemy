using System;
using System.IO;

namespace Alchemy.Core;

public static class StoragePathRules
{
    public static bool IsContainedBy(
        string path,
        string possibleParent)
    {
        var normalizedPath = Normalize(path);
        var normalizedParent = Normalize(possibleParent);
        return normalizedPath.StartsWith(
            normalizedParent + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);
    }

    public static bool CanMove(
        string? source,
        string? targetDirectory,
        string? storageRoot)
    {
        if (string.IsNullOrWhiteSpace(source) ||
            string.IsNullOrWhiteSpace(targetDirectory) ||
            string.IsNullOrWhiteSpace(storageRoot) ||
            !Directory.Exists(targetDirectory))
        {
            return false;
        }

        var sourcePath = Normalize(source);
        var targetPath = Normalize(targetDirectory);
        if (!IsInsideStorage(sourcePath, storageRoot) ||
            !IsInsideStorage(targetPath, storageRoot) ||
            string.Equals(sourcePath, targetPath, StringComparison.Ordinal))
        {
            return false;
        }

        var sourceParent = Normalize(
            Path.GetDirectoryName(sourcePath) ?? string.Empty);
        if (string.Equals(
                sourceParent,
                targetPath,
                StringComparison.Ordinal))
        {
            return false;
        }

        return !Directory.Exists(sourcePath) ||
               !targetPath.StartsWith(
                   sourcePath + Path.DirectorySeparatorChar,
                   StringComparison.Ordinal);
    }

    private static bool IsInsideStorage(string path, string storageRoot)
    {
        var root = Normalize(storageRoot);
        return string.Equals(path, root, StringComparison.Ordinal) ||
               path.StartsWith(
                   root + Path.DirectorySeparatorChar,
                   StringComparison.Ordinal);
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}