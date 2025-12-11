using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VoxThisWay.Core.Audio;
using VoxThisWay.Core.Configuration;
using VoxThisWay.Core.Secrets;
using VoxThisWay.Core.Transcription;

namespace VoxThisWay.Services.Transcription;

public sealed class AzureSpeechTranscriber : ISpeechTranscriber
{
    private readonly IOptionsMonitor<SpeechEngineOptions> _options;
    private readonly IAzureSpeechCredentialStore _credentialStore;
    private readonly ILogger<AzureSpeechTranscriber> _logger;
    private SpeechRecognizer? _recognizer;
    private TaskCompletionSource? _sessionTcs;
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private PushAudioInputStream? _pushStream;

    public AzureSpeechTranscriber(
        IOptionsMonitor<SpeechEngineOptions> options,
        IAzureSpeechCredentialStore credentialStore,
        ILogger<AzureSpeechTranscriber> logger)
    {
        _options = options;
        _credentialStore = credentialStore;
        _logger = logger;
    }

    public string EngineName => "azure";

    public event EventHandler<TranscriptSegment>? TranscriptAvailable;

    public async Task StartAsync(TranscriptionConfig config, CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (_recognizer is not null)
            {
                return;
            }

            var key = await _credentialStore.GetApiKeyAsync();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("Azure Speech key is not configured.");
            }

            var speechConfig = SpeechConfig.FromSubscription(key, _options.CurrentValue.AzureSpeech.Region);
            speechConfig.SpeechRecognitionLanguage = config.Language;

            if (_options.CurrentValue.AzureSpeech.UseCustomEndpoint && !string.IsNullOrWhiteSpace(_options.CurrentValue.AzureSpeech.Endpoint))
            {
                speechConfig.EndpointId = _options.CurrentValue.AzureSpeech.Endpoint;
            }

            var audioFormat = AudioStreamFormat.GetWaveFormatPCM(
                (uint)config.InputFormat.SampleRate,
                (byte)config.InputFormat.BitsPerSample,
                (byte)config.InputFormat.Channels);

            var pushStream = AudioInputStream.CreatePushStream(audioFormat);
            var audioConfig = AudioConfig.FromStreamInput(pushStream);

            _recognizer = new SpeechRecognizer(speechConfig, audioConfig);
            _sessionTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            _recognizer.Recognizing += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Result.Text))
                {
                    TranscriptAvailable?.Invoke(this,
                        new TranscriptSegment(e.Result.Text, false, TimeSpan.Zero, TimeSpan.Zero));
                }
            };

            _recognizer.Recognized += (_, e) =>
            {
                if (e.Result.Reason == ResultReason.RecognizedSpeech && !string.IsNullOrEmpty(e.Result.Text))
                {
                    TranscriptAvailable?.Invoke(this,
                        new TranscriptSegment(e.Result.Text, true, TimeSpan.Zero, TimeSpan.Zero));
                }
            };

            _recognizer.Canceled += (_, e) =>
            {
                if (e.Reason == CancellationReason.Error)
                {
                    _logger.LogError("Azure Speech canceled: {Error}", e.ErrorDetails);
                }

                _sessionTcs?.TrySetResult();
            };

            _recognizer.SessionStopped += (_, _) => _sessionTcs?.TrySetResult();

            await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
            _pushStream = pushStream;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _stateLock.WaitAsync();
        try
        {
            if (_recognizer is null)
            {
                return;
            }

            await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            if (_sessionTcs is not null)
            {
                await _sessionTcs.Task.ConfigureAwait(false);
            }

            _recognizer.Dispose();
            _recognizer = null;
            _pushStream?.Dispose();
            _pushStream = null;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    public Task PushAudioAsync(ReadOnlyMemory<byte> buffer, AudioFormat format, CancellationToken cancellationToken = default)
    {
        if (_pushStream is null)
        {
            return Task.CompletedTask;
        }

        _pushStream.Write(buffer.ToArray());
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _stateLock.Dispose();
    }
}
