using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VoxThisWay.Core.Audio;
using VoxThisWay.Core.Transcription;

namespace VoxThisWay.Services.Transcription;

public sealed class MockSpeechTranscriber : ISpeechTranscriber
{
    private readonly ILogger<MockSpeechTranscriber> _logger;
    private readonly StringBuilder _buffer = new();
    private bool _isRunning;

    public MockSpeechTranscriber(ILogger<MockSpeechTranscriber> logger)
    {
        _logger = logger;
    }

    public string EngineName => "mock";

    public event EventHandler<TranscriptSegment>? TranscriptAvailable;

    public Task StartAsync(TranscriptionConfig config, CancellationToken cancellationToken = default)
    {
        _isRunning = true;
        _buffer.Clear();
        _logger.LogInformation("Mock transcriber started for engine {EngineName}.", config.EngineName);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _isRunning = false;
        _logger.LogInformation("Mock transcriber stopped.");
        return Task.CompletedTask;
    }

    public Task PushAudioAsync(ReadOnlyMemory<byte> buffer, AudioFormat format, CancellationToken cancellationToken = default)
    {
        if (!_isRunning)
        {
            return Task.CompletedTask;
        }

        // Fake some text.
        _buffer.Append(" ...");

        TranscriptAvailable?.Invoke(
            this,
            new TranscriptSegment(
                $"[mock transcript length {_buffer.Length}]",
                false,
                TimeSpan.Zero,
                TimeSpan.Zero));

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _isRunning = false;
        return ValueTask.CompletedTask;
    }
}
