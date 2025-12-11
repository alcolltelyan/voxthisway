using System;
using System.Threading;
using System.Threading.Tasks;
using VoxThisWay.Core.Audio;

namespace VoxThisWay.Core.Transcription;

public interface ISpeechTranscriber : IAsyncDisposable
{
    string EngineName { get; }

    event EventHandler<TranscriptSegment>? TranscriptAvailable;

    Task StartAsync(TranscriptionConfig config, CancellationToken cancellationToken = default);

    Task StopAsync();

    Task PushAudioAsync(ReadOnlyMemory<byte> buffer, AudioFormat format, CancellationToken cancellationToken = default);
}
