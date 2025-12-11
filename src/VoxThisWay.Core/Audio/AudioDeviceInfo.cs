namespace VoxThisWay.Core.Audio;

public sealed record AudioDeviceInfo(
    string DeviceId,
    string DisplayName,
    int Channels,
    int PreferredSampleRate);
