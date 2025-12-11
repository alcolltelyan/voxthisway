namespace VoxThisWay.Core.Audio;

public sealed record AudioCaptureOptions(
    string DeviceId,
    AudioFormat TargetFormat,
    bool AutoGainControl);
