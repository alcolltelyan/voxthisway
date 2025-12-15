using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Extensions.Options;
using VoxThisWay.Core.Audio;
using VoxThisWay.Core.Configuration;
using VoxThisWay.Core.Secrets;

namespace VoxThisWay.App.OnboardingPages;

public sealed class OnboardingSession
{
    private readonly IOptions<SpeechEngineOptions> _speechOptions;
    private readonly IOptions<WhisperLocalOptions> _whisperOptions;
    private readonly IAzureSpeechCredentialStore _azureCredentialStore;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly IUserSettingsStore _settingsStore;

    public List<AudioDeviceInfo> Devices { get; } = new();

    public AudioDeviceInfo? SelectedDevice { get; set; }

    public int HotkeyVirtualKey { get; set; }

    public bool HotkeyUseCtrl { get; set; }

    public bool HotkeyUseAlt { get; set; }

    public bool HotkeyUseShift { get; set; }

    public bool HotkeyUseWin { get; set; }

    public string WhisperStatusIcon { get; private set; } = "";

    public Brush WhisperStatusBrush { get; private set; } = Brushes.Gray;

    public string WhisperStatusText { get; private set; } = "";

    public string AzureStatusIcon { get; private set; } = "";

    public Brush AzureStatusBrush { get; private set; } = Brushes.Gray;

    public string AzureStatusText { get; private set; } = "";

    public event EventHandler? StatusUpdated;

    public OnboardingSession(
        IOptions<SpeechEngineOptions> speechOptions,
        IOptions<WhisperLocalOptions> whisperOptions,
        IAzureSpeechCredentialStore azureCredentialStore,
        IAudioCaptureService audioCaptureService,
        IUserSettingsStore settingsStore)
    {
        _speechOptions = speechOptions;
        _whisperOptions = whisperOptions;
        _azureCredentialStore = azureCredentialStore;
        _audioCaptureService = audioCaptureService;
        _settingsStore = settingsStore;

        Devices.AddRange(_audioCaptureService.EnumerateInputDevices().ToList());

        var current = _settingsStore.Current ?? new UserSettings();
        if (!string.IsNullOrWhiteSpace(current.AudioInputDeviceId))
        {
            SelectedDevice = Devices.FirstOrDefault(d => string.Equals(d.DeviceId, current.AudioInputDeviceId, StringComparison.OrdinalIgnoreCase));
        }
        SelectedDevice ??= Devices.FirstOrDefault();

        HotkeyVirtualKey = current.HotkeyVirtualKey != 0 ? current.HotkeyVirtualKey : 0x20;
        HotkeyUseCtrl = current.HotkeyUseCtrl;
        HotkeyUseAlt = current.HotkeyUseAlt;
        HotkeyUseShift = current.HotkeyUseShift;
        HotkeyUseWin = current.HotkeyUseWin;
    }

    public async Task<string> TestMicrophoneAsync(AudioDeviceInfo device)
    {
        var format = new AudioFormat(16000, 16, 1);
        var options = new AudioCaptureOptions(device.DeviceId ?? string.Empty, format, AutoGainControl: false);

        var bufferCount = 0;
        void Handler(object? s, AudioBufferReadyEventArgs args) => bufferCount++;

        _audioCaptureService.AudioBufferReady += Handler;
        try
        {
            await _audioCaptureService.StartAsync(options, CancellationToken.None);
            await Task.Delay(1500);
            await _audioCaptureService.StopAsync();

            return bufferCount > 0
                ? $"Microphone test succeeded. Received {bufferCount} audio buffer(s)."
                : "No audio buffers were received during the test. Check your device and system settings.";
        }
        finally
        {
            _audioCaptureService.AudioBufferReady -= Handler;
        }
    }

    public string FormatHotkey()
    {
        var parts = new List<string>();
        if (HotkeyUseCtrl) parts.Add("Ctrl");
        if (HotkeyUseAlt) parts.Add("Alt");
        if (HotkeyUseShift) parts.Add("Shift");
        if (HotkeyUseWin) parts.Add("Win");

        var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey(HotkeyVirtualKey != 0 ? HotkeyVirtualKey : 0x20);
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    public async Task UpdateStatusAsync()
    {
        // Whisper local check
        var whisperExec = _whisperOptions.Value.ResolveExecutablePath();
        var whisperModel = _whisperOptions.Value.ResolveModelPath();
        var hasExec = File.Exists(whisperExec);
        var hasModel = File.Exists(whisperModel);

        if (hasExec && hasModel)
        {
            WhisperStatusIcon = "✔";
            WhisperStatusBrush = Brushes.Green;
            WhisperStatusText = $"Whisper local looks ready.\nExecutable: {whisperExec}\nModel: {whisperModel}";
        }
        else
        {
            WhisperStatusIcon = "✖";
            WhisperStatusBrush = Brushes.Red;
            WhisperStatusText =
                "Whisper local is not fully ready.\n" +
                $"Executable present: {hasExec} ({whisperExec})\n" +
                $"Model present: {hasModel} ({whisperModel})\n" +
                "Ensure the Speech folder from the ZIP (including whisper_cli.exe and the model file) is placed next to VoxThisWay.App.exe.";
        }

        // Azure Speech check
        var engineOptions = _speechOptions.Value;
        var azureRegion = engineOptions.AzureSpeech.Region;
        var azureEndpoint = engineOptions.AzureSpeech.Endpoint;

        string? key = null;
        try
        {
            key = await _azureCredentialStore.GetApiKeyAsync();
        }
        catch
        {
        }

        var hasKey = !string.IsNullOrWhiteSpace(key);
        var hasRegionOrEndpoint = !string.IsNullOrWhiteSpace(azureRegion) || !string.IsNullOrWhiteSpace(azureEndpoint);

        if (hasKey && hasRegionOrEndpoint)
        {
            AzureStatusIcon = "✔";
            AzureStatusBrush = Brushes.Green;

            var last4 = key!.Length >= 4 ? key[^4..] : key;
            AzureStatusText =
                "Azure Speech appears configured.\n" +
                $"Key: (stored securely, ending …{last4})\n" +
                $"Region: {azureRegion ?? "(not set)"}\n" +
                $"Endpoint: {azureEndpoint ?? "(not set)"}";
        }
        else
        {
            AzureStatusIcon = "✖";
            AzureStatusBrush = Brushes.Red;
            AzureStatusText =
                "Azure Speech is optional and not fully configured.\n" +
                "To use Azure: open Settings → Speech engine → Azure Speech, enter your API key and region/endpoint.";
        }

        StatusUpdated?.Invoke(this, EventArgs.Empty);
    }
}
