using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace Alchemy.Kit;

public sealed record DroppedFileItem(
    string Name,
    string? LocalPath,
    IStorageFile? StorageFile);

public static class ExternalDropFiles
{
    public static bool TryGetDroppedItems(
        DragEventArgs e,
        out List<DroppedFileItem> items)
    {
        items = [];
        var files = e.DataTransfer.TryGetFiles();
        if (files is null)
        {
            return false;
        }

        foreach (var file in files)
        {
            var localPath = file.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                items.Add(new DroppedFileItem(file.Name, localPath, file as IStorageFile));
                continue;
            }

            if (file is IStorageFile storageFile)
            {
                items.Add(new DroppedFileItem(file.Name, null, storageFile));
            }
        }

        return items.Count > 0;
    }

    public static string GetDestinationFileName(DroppedFileItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.LocalPath))
        {
            return Path.GetFileName(item.LocalPath);
        }

        return item.Name;
    }

    public static bool DestinationExists(string destinationPath) =>
        File.Exists(destinationPath) || Directory.Exists(destinationPath);

    public static async Task<bool> CopyToPathAsync(
        DroppedFileItem item,
        string destinationPath)
    {
        if (!string.IsNullOrWhiteSpace(item.LocalPath))
        {
            var localPath = item.LocalPath;
            if (!File.Exists(localPath))
            {
                return false;
            }

            File.Copy(localPath, destinationPath);
            return true;
        }

        if (item.StorageFile is null)
        {
            return false;
        }

        await using var sourceStream = await item.StorageFile.OpenReadAsync();
        await using var destinationStream = File.Create(destinationPath);
        await sourceStream.CopyToAsync(destinationStream);
        return true;
    }
}