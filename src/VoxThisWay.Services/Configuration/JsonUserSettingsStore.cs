using System;
using System.IO;
using System.Text.Json;
using VoxThisWay.Core.Configuration;

namespace VoxThisWay.Services.Configuration;

public sealed class JsonUserSettingsStore : IUserSettingsStore
{
    private readonly string _settingsFilePath;
    private readonly object _lock = new();
    private UserSettings _current;

    public JsonUserSettingsStore()
    {
        _settingsFilePath = Path.Combine(AppDirectories.SettingsDirectory, "user-settings.json");
        _current = LoadInternal();
    }

    public UserSettings Current
    {
        get
        {
            lock (_lock)
            {
                return _current;
            }
        }
    }

    public void Save(UserSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        lock (_lock)
        {
            Directory.CreateDirectory(AppDirectories.SettingsDirectory);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            using var stream = File.Create(_settingsFilePath);
            JsonSerializer.Serialize(stream, settings, options);
            _current = settings;
        }
    }

    private UserSettings LoadInternal()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new UserSettings();
            }

            using var stream = File.OpenRead(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<UserSettings>(stream);
            return settings ?? new UserSettings();
        }
        catch
        {
            // On any failure, fall back to defaults rather than crashing startup.
            return new UserSettings();
        }
    }
}
