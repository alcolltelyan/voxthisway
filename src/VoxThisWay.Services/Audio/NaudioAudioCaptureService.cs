using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using VoxThisWay.Core.Audio;

namespace VoxThisWay.Services.Audio;

public sealed class NaudioAudioCaptureService : IAudioCaptureService
{
    private readonly ILogger<NaudioAudioCaptureService> _logger;
    private readonly MMDeviceEnumerator _deviceEnumerator = new();
    private WasapiCapture? _capture;
    private MMDevice? _activeDevice;
    private AudioFormat? _format;
    private Stopwatch? _stopwatch;
    private bool _disposed;
    private CancellationTokenRegistration _stopRegistration;

    public NaudioAudioCaptureService(ILogger<NaudioAudioCaptureService> logger)
    {
        _logger = logger;
    }

    public event EventHandler<AudioBufferReadyEventArgs>? AudioBufferReady;

    public IReadOnlyList<AudioDeviceInfo> EnumerateInputDevices()
    {
#pragma warning disable CA2000
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(device =>
            {
                var format = device.AudioClient.MixFormat;
                return new AudioDeviceInfo(
                    device.ID,
                    device.FriendlyName,
                    format.Channels,
                    format.SampleRate);
            })
            .ToList();
        return devices;
#pragma warning restore CA2000
    }

    public Task StartAsync(AudioCaptureOptions options, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_capture is not null)
        {
            throw new InvalidOperationException("Audio capture already in progress.");
        }

        _activeDevice?.Dispose();
        _activeDevice = ResolveDevice(options.DeviceId);
        if (_activeDevice is null)
        {
            throw new InvalidOperationException($"Unable to find input device '{options.DeviceId}'.");
        }

        _format = options.TargetFormat;
        _stopwatch = Stopwatch.StartNew();

        var waveFormat = new WaveFormat(
            options.TargetFormat.SampleRate,
            options.TargetFormat.BitsPerSample,
            options.TargetFormat.Channels);

        var capture = new WasapiCapture(_activeDevice)
        {
            ShareMode = AudioClientShareMode.Shared,
            WaveFormat = waveFormat
        };

        capture.DataAvailable += HandleDataAvailable;
        capture.RecordingStopped += HandleRecordingStopped;

        _capture = capture;
        capture.StartRecording();

        if (cancellationToken.CanBeCanceled)
        {
            _stopRegistration = cancellationToken.Register(() =>
            {
                _ = StopAsync();
            });
        }

        _logger.LogInformation("Started audio capture on {Device}.", _activeDevice.FriendlyName);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_capture is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            _capture.StopRecording();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "StopRecording called while capture not running.");
        }

        return Task.CompletedTask;
    }

    private void HandleDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_format is null || e.BytesRecorded <= 0)
        {
            return;
        }

        var buffer = new byte[e.BytesRecorded];
        Array.Copy(e.Buffer, buffer, e.BytesRecorded);

        var timestamp = _stopwatch?.Elapsed ?? TimeSpan.Zero;
        AudioBufferReady?.Invoke(this, new AudioBufferReadyEventArgs(buffer, _format.Value, timestamp));
    }

    private void HandleRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null)
        {
            _logger.LogError(e.Exception, "Audio capture stopped due to an error.");
        }
        else
        {
            _logger.LogInformation("Audio capture stopped.");
        }

        if (_capture is not null)
        {
            _capture.DataAvailable -= HandleDataAvailable;
            _capture.RecordingStopped -= HandleRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        _activeDevice?.Dispose();
        _activeDevice = null;

        _stopwatch?.Stop();
        _stopwatch = null;

        _stopRegistration.Dispose();
        _stopRegistration = default;
    }

    private MMDevice? ResolveDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        }

        return _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .FirstOrDefault(d => string.Equals(d.ID, deviceId, StringComparison.OrdinalIgnoreCase))
            ?? _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NaudioAudioCaptureService));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopRegistration.Dispose();

        if (_capture is not null)
        {
            _capture.DataAvailable -= HandleDataAvailable;
            _capture.RecordingStopped -= HandleRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        _activeDevice?.Dispose();
        _deviceEnumerator.Dispose();
    }
}
