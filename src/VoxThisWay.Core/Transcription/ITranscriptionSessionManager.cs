using System;
using System.Threading;
using System.Threading.Tasks;

namespace VoxThisWay.Core.Transcription;

public interface ITranscriptionSessionManager
{
    bool IsRunning { get; }

    event EventHandler<TranscriptSegment>? TranscriptReceived;

    /// <summary>
    /// Raised periodically while a session is running to report the current
    /// microphone activity level as a normalized value between 0.0 and 1.0.
    /// Intended for diagnostics/UX (e.g., tray tooltip level indicator).
    /// </summary>
    event EventHandler<double>? AudioLevelChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync();
}
