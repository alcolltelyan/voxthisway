using System;

namespace VoxThisWay.Core.Audio;

public sealed class AudioBufferReadyEventArgs : EventArgs
{
    public AudioBufferReadyEventArgs(ReadOnlyMemory<byte> buffer, AudioFormat format, TimeSpan timestamp)
    {
        Buffer = buffer;
        Format = format;
        Timestamp = timestamp;
    }

    public ReadOnlyMemory<byte> Buffer { get; }

    public AudioFormat Format { get; }

    public TimeSpan Timestamp { get; }
}
