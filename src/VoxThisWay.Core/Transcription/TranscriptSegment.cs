using System;

namespace VoxThisWay.Core.Transcription;

public sealed record TranscriptSegment(
    string Text,
    bool IsFinal,
    TimeSpan StartTime,
    TimeSpan EndTime);
