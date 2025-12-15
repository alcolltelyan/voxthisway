using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VoxThisWay.Core.Audio;
using VoxThisWay.Core.Configuration;
using VoxThisWay.Core.Secrets;

namespace VoxThisWay.App.SettingsPages;

public sealed class SettingsSession
{
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly IUserSettingsStore _settingsStore;
    private readonly IAzureSpeechCredentialStore _azureCredentialStore;
    private readonly WhisperLocalOptions _whisperOptions;

    public List<AudioDeviceInfo> Devices { get; } = new();

    public AudioDeviceInfo? SelectedDevice { get; set; }

    public SpeechEngineKind SelectedEngine { get; set; }

    public WhisperModelKind SelectedWhisperModel { get; set; }

    public int HotkeyVirtualKey { get; set; }

    public bool HotkeyUseCtrl { get; set; }

    public bool HotkeyUseAlt { get; set; }

    public bool HotkeyUseShift { get; set; }

    public bool HotkeyUseWin { get; set; }

    public string AzureKeySummary { get; private set; } = "";

    public string WhisperBuildInfo { get; private set; } = "";

    public string PendingAzureKey { get; set; } = "";

    public SettingsSession(
        IAudioCaptureService audioCaptureService,
        IUserSettingsStore settingsStore,
        IAzureSpeechCredentialStore azureCredentialStore,
        WhisperLocalOptions whisperOptions)
    {
        _audioCaptureService = audioCaptureService;
        _settingsStore = settingsStore;
        _azureCredentialStore = azureCredentialStore;
        _whisperOptions = whisperOptions;

        Devices.AddRange(_audioCaptureService.EnumerateInputDevices().ToList());

        var currentSettings = _settingsStore.Current ?? new UserSettings();

        if (!string.IsNullOrWhiteSpace(currentSettings.AudioInputDeviceId))
        {
            SelectedDevice = Devices.FirstOrDefault(d => string.Equals(d.DeviceId, currentSettings.AudioInputDeviceId, StringComparison.OrdinalIgnoreCase));
        }

        SelectedDevice ??= Devices.FirstOrDefault();

        SelectedWhisperModel = currentSettings.WhisperModel ?? WhisperModelKind.Tiny;
        SelectedEngine = currentSettings.SpeechEngine ?? SpeechEngineKind.WhisperLocal;

        HotkeyVirtualKey = currentSettings.HotkeyVirtualKey != 0 ? currentSettings.HotkeyVirtualKey : 0x20;
        HotkeyUseCtrl = currentSettings.HotkeyUseCtrl;
        HotkeyUseAlt = currentSettings.HotkeyUseAlt;
        HotkeyUseShift = currentSettings.HotkeyUseShift;
        HotkeyUseWin = currentSettings.HotkeyUseWin;
    }

    public async Task RefreshAzureKeySummaryAsync()
    {
        try
        {
            var existing = await _azureCredentialStore.GetApiKeyAsync();
            if (!string.IsNullOrEmpty(existing))
            {
                var last4 = existing.Length >= 4 ? existing[^4..] : existing;
                AzureKeySummary = $"An API key is configured (…{last4}).";
            }
            else
            {
                AzureKeySummary = "No API key configured.";
            }
        }
        catch
        {
            AzureKeySummary = "Unable to determine current key status.";
        }
    }

    public async Task RefreshWhisperBuildInfoAsync()
    {
        try
        {
            var execPath = _whisperOptions.ResolveExecutablePath();
            if (!File.Exists(execPath))
            {
                WhisperBuildInfo = $"Executable not found at {execPath}.";
                return;
            }

            WhisperBuildInfo = "Detecting Whisper build…";

            var psi = new ProcessStartInfo(execPath, "--help")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            var exitTask = process.WaitForExitAsync();
            var completed = await Task.WhenAny(exitTask, Task.Delay(3000)) == exitTask;

            if (!completed)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore
                }
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            var combined = (stdout + "\n" + stderr) ?? string.Empty;

            var isGpu = combined.IndexOf("CUDA", StringComparison.OrdinalIgnoreCase) >= 0
                        || combined.IndexOf("GPU", StringComparison.OrdinalIgnoreCase) >= 0;

            string buildKind;
            if (string.IsNullOrWhiteSpace(combined))
            {
                buildKind = completed
                    ? "Unknown (no help output captured)"
                    : "Unknown (process did not finish within timeout)";
            }
            else
            {
                buildKind = isGpu
                    ? "GPU-accelerated (CUDA)"
                    : "CPU-only (no GPU indicators in help output)";
            }

            WhisperBuildInfo = $"Executable: {execPath}\nBuild: {buildKind}";
        }
        catch (Exception ex)
        {
            WhisperBuildInfo = $"Unable to determine Whisper build: {ex.Message}";
        }
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
            await Task.Delay(2000);
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

    public async Task SaveAsync()
    {
        var settings = _settingsStore.Current ?? new UserSettings();

        settings.AudioInputDeviceId = SelectedDevice?.DeviceId;
        settings.SpeechEngine = SelectedEngine;
        settings.WhisperModel = SelectedWhisperModel;

        settings.HotkeyVirtualKey = HotkeyVirtualKey != 0 ? HotkeyVirtualKey : settings.HotkeyVirtualKey;
        settings.HotkeyUseCtrl = HotkeyUseCtrl;
        settings.HotkeyUseAlt = HotkeyUseAlt;
        settings.HotkeyUseShift = HotkeyUseShift;
        settings.HotkeyUseWin = HotkeyUseWin;

        if (SelectedEngine == SpeechEngineKind.Azure && !string.IsNullOrWhiteSpace(PendingAzureKey))
        {
            await _azureCredentialStore.SetApiKeyAsync(PendingAzureKey);
        }

        var modelFileName = SelectedWhisperModel == WhisperModelKind.Base
            ? "ggml-base.en.bin"
            : "ggml-tiny.en.bin";

        _whisperOptions.ModelPath = Path.Combine(
            AppContext.BaseDirectory,
            "Speech",
            "Models",
            modelFileName);

        _settingsStore.Save(settings);
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
}
