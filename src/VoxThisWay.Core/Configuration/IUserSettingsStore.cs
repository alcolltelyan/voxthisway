namespace VoxThisWay.Core.Configuration;

public interface IUserSettingsStore
{
    /// <summary>
    /// Gets the current in-memory user settings. Changes to this object are not
    /// persisted until <see cref="Save"/> is called.
    /// </summary>
    UserSettings Current { get; }

    /// <summary>
    /// Persists the provided settings to the user settings store.
    /// </summary>
    void Save(UserSettings settings);
}
