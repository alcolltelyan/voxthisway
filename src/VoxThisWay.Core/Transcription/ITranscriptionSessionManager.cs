using System;
using System.Threading;
using System.Threading.Tasks;

namespace VoxThisWay.Core.Transcription;

public interface ITranscriptionSessionManager
{
    bool IsRunning { get; }

    event EventHandler<TranscriptSegment>? TranscriptReceived;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}
