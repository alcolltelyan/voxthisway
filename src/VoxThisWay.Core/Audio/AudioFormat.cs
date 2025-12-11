namespace VoxThisWay.Core.Audio;

public readonly record struct AudioFormat(
    int SampleRate,
    int BitsPerSample,
    int Channels);
