using VoxThisWay.Core.Audio;

namespace VoxThisWay.Core.Transcription;

public sealed record TranscriptionConfig(
    string EngineName,
    AudioFormat InputFormat,
    string Language = "en");
