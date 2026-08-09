using System;
using System.IO;
using System.Text.Json;

namespace Alchemy;

internal sealed class AlchemySettings
{
    public string? RootPath { get; set; }
    public int ThemeMode { get; set; }
}

internal static class AlchemySettingsStore
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Alchemy");

    private static readonly string SettingsPath = Path.Combine(
        SettingsDirectory,
        "settings.json");

    public static AlchemySettings Load()
    {
        try
        {
            return File.Exists(SettingsPath)
                ? JsonSerializer.Deserialize<AlchemySettings>(
                      File.ReadAllText(SettingsPath)) ?? new AlchemySettings()
                : new AlchemySettings();
        }
        catch
        {
            return new AlchemySettings();
        }
    }

    public static void Save(AlchemySettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(
            SettingsPath,
            JsonSerializer.Serialize(
                settings,
                new JsonSerializerOptions { WriteIndented = true }));
    }
}
