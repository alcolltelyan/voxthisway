using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VoxThisWay.Core.Audio;
using VoxThisWay.Core.Configuration;
using VoxThisWay.Core.Transcription;

namespace VoxThisWay.Services.Transcription;

public sealed class TranscriptionSessionManager : ITranscriptionSessionManager, IDisposable
{
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly ISpeechTranscriberFactory _transcriberFactory;
    private readonly IOptionsMonitor<SpeechEngineOptions> _engineOptions;
    private readonly IUserSettingsStore _userSettingsStore;
    private readonly ILogger<TranscriptionSessionManager> _logger;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private bool _isRunning;
    private readonly AudioFormat _defaultFormat = new(16000, 16, 1);
    private ISpeechTranscriber? _activeTranscriber;

    public TranscriptionSessionManager(
        IAudioCaptureService audioCaptureService,
        ISpeechTranscriberFactory transcriberFactory,
        IOptionsMonitor<SpeechEngineOptions> engineOptions,
        IUserSettingsStore userSettingsStore,
        ILogger<TranscriptionSessionManager> logger)
    {
        _audioCaptureService = audioCaptureService;
        _transcriberFactory = transcriberFactory;
        _engineOptions = engineOptions;
        _userSettingsStore = userSettingsStore;
        _logger = logger;

        _audioCaptureService.AudioBufferReady += HandleAudioBufferReady;
    }

    public event EventHandler<TranscriptSegment>? TranscriptReceived;

    public bool IsRunning => _isRunning;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _stateLock.WaitAsync(cancellationToken);
        try
        {
            if (_isRunning)
            {
                return;
            }

            var settings = _userSettingsStore.Current;
            var deviceId = settings.AudioInputDeviceId ?? string.Empty;
            var engineKind = settings.SpeechEngine ?? _engineOptions.CurrentValue.ActiveEngine;

            var audioOptions = new AudioCaptureOptions(deviceId, _defaultFormat, AutoGainControl: false);
            _activeTranscriber = _transcriberFactory.Create(engineKind);
            _activeTranscriber.TranscriptAvailable += HandleTranscriptAvailable;

            await _activeTranscriber.StartAsync(
                new TranscriptionConfig(_activeTranscriber.EngineName, _defaultFormat, _engineOptions.CurrentValue.Language),
                cancellationToken);

            await _audioCaptureService.StartAsync(audioOptions, cancellationToken);

            _isRunning = true;
            _logger.LogInformation("Transcription session started using engine {Engine}.", _activeTranscriber.EngineName);
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
            if (!_isRunning)
            {
                return;
            }

            await _audioCaptureService.StopAsync();
            if (_activeTranscriber is not null)
            {
                // Important: stop the transcriber (which may flush remaining audio
                // and raise final transcript events) before unsubscribing.
                await _activeTranscriber.StopAsync();
                _activeTranscriber.TranscriptAvailable -= HandleTranscriptAvailable;
                await _activeTranscriber.DisposeAsync();
                _activeTranscriber = null;
            }

            _isRunning = false;
            _logger.LogInformation("Transcription session stopped.");
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private void HandleAudioBufferReady(object? sender, AudioBufferReadyEventArgs e)
    {
        if (!_isRunning)
        {
            return;
        }

        if (_activeTranscriber is not null)
        {
            _logger.LogDebug("Forwarding audio buffer to transcriber. Bytes={Bytes}, Format={SampleRate}Hz/{Bits}bit/{Channels}ch", e.Buffer.Length, e.Format.SampleRate, e.Format.BitsPerSample, e.Format.Channels);
            _ = _activeTranscriber.PushAudioAsync(e.Buffer, e.Format);
        }
    }

    private void HandleTranscriptAvailable(object? sender, TranscriptSegment e)
    {
        _logger.LogInformation("Transcript received from engine {Engine}. IsFinal={IsFinal}, Length={Length}", _activeTranscriber?.EngineName ?? "unknown", e.IsFinal, e.Text?.Length ?? 0);
        TranscriptReceived?.Invoke(this, e);
    }

    public void Dispose()
    {
        _audioCaptureService.Dispose();
        _activeTranscriber?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _stateLock.Dispose();
    }
}
