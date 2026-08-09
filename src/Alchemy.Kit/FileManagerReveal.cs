using System;
using System.Diagnostics;
using System.IO;

namespace Alchemy.Kit;

public static class FileManagerReveal
{
    public static bool OpenPath(string path)
    {
        return TryOpen(path, reveal: false, openContainingFolderForFile: false);
    }

    public static bool RevealPath(string path)
    {
        return TryOpen(path, reveal: true, openContainingFolderForFile: true);
    }

    public static bool OpenDirectory(string path)
    {
        return TryOpen(path, reveal: false, openContainingFolderForFile: true);
    }

    private static bool TryOpen(
        string path,
        bool reveal,
        bool openContainingFolderForFile)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        var isFile = File.Exists(fullPath);
        var isDirectory = Directory.Exists(fullPath);
        if (!isFile && !isDirectory)
        {
            return false;
        }

        if (OperatingSystem.IsMacOS())
        {
            var startInfo = new ProcessStartInfo("open")
            {
                UseShellExecute = false
            };

            if (reveal)
            {
                startInfo.ArgumentList.Add("-R");
                startInfo.ArgumentList.Add(fullPath);
            }
            else
            {
                var openTarget = isDirectory
                    ? fullPath
                    : (openContainingFolderForFile
                        ? Path.GetDirectoryName(fullPath) ?? fullPath
                        : fullPath);
                startInfo.ArgumentList.Add(openTarget);
            }

            Process.Start(startInfo);
            return true;
        }

        return false;
    }
}