using System;
using System.IO;

namespace VoxThisWay.Core.Configuration;

public static class AppDirectories
{
    private static readonly Lazy<string> _appDataRoot = new(() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoxThisWay"));

    public static string AppDataRoot => _appDataRoot.Value;

    public static string LogsDirectory => Path.Combine(AppDataRoot, "Logs");

    public static string WhisperModelsDirectory => Path.Combine(AppDataRoot, "Models", "Whisper");

    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VoxThisWay", "Settings");

    public static string TempDirectory => Path.Combine(AppDataRoot, "Temp");

    public static void EnsureAll()
    {
        Directory.CreateDirectory(AppDataRoot);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(WhisperModelsDirectory);
        Directory.CreateDirectory(SettingsDirectory);
        Directory.CreateDirectory(TempDirectory);
    }
}
