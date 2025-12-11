using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using VoxThisWay.Core.Audio;
using VoxThisWay.Core.Configuration;
using VoxThisWay.Core.Secrets;

namespace VoxThisWay.App;

public partial class SettingsWindow : Window
{
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly IUserSettingsStore _settingsStore;
    private readonly IAzureSpeechCredentialStore _azureCredentialStore;
    private readonly WhisperLocalOptions _whisperOptions;
    private readonly List<AudioDeviceInfo> _devices;
    private bool _capturingHotkey;
    private int _hotkeyVk;
    private bool _hotkeyCtrl;
    private bool _hotkeyAlt;
    private bool _hotkeyShift;
    private bool _hotkeyWin;

    public SettingsWindow(IAudioCaptureService audioCaptureService, IUserSettingsStore settingsStore, IAzureSpeechCredentialStore azureCredentialStore, WhisperLocalOptions whisperOptions)
    {
        InitializeComponent();

        _audioCaptureService = audioCaptureService;
        _settingsStore = settingsStore;
        _azureCredentialStore = azureCredentialStore;
        _whisperOptions = whisperOptions;

        _devices = _audioCaptureService.EnumerateInputDevices().ToList();
        DeviceCombo.ItemsSource = _devices;

        var currentSettings = _settingsStore.Current;
        var currentId = currentSettings.AudioInputDeviceId;
        AudioDeviceInfo? selected = null;
        if (!string.IsNullOrWhiteSpace(currentId))
        {
            selected = _devices.FirstOrDefault(d => string.Equals(d.DeviceId, currentId, StringComparison.OrdinalIgnoreCase));
        }

        if (selected is null)
        {
            selected = _devices.FirstOrDefault();
        }

        if (selected is not null)
        {
            DeviceCombo.SelectedItem = selected;
        }

        // Initialize Whisper model selection (default to Tiny if unset)
        var modelKind = currentSettings.WhisperModel ?? WhisperModelKind.Tiny;
        switch (modelKind)
        {
            case WhisperModelKind.Base:
                WhisperModelCombo.SelectedItem = WhisperBaseItem;
                break;
            default:
                WhisperModelCombo.SelectedItem = WhisperTinyItem;
                break;
        }

        // Initialize speech engine selection (default to WhisperLocal if unset)
        var engine = currentSettings.SpeechEngine ?? SpeechEngineKind.WhisperLocal;
        if (engine == SpeechEngineKind.Azure)
        {
            EngineCombo.SelectedItem = AzureEngineItem;
            AzurePanel.Visibility = Visibility.Visible;
            WhisperPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            EngineCombo.SelectedItem = WhisperEngineItem;
            AzurePanel.Visibility = Visibility.Collapsed;
            WhisperPanel.Visibility = Visibility.Visible;
            _ = LoadWhisperBuildInfoAsync();
        }

        _hotkeyVk = currentSettings.HotkeyVirtualKey != 0 ? currentSettings.HotkeyVirtualKey : 0x20;
        _hotkeyCtrl = currentSettings.HotkeyUseCtrl;
        _hotkeyAlt = currentSettings.HotkeyUseAlt;
        _hotkeyShift = currentSettings.HotkeyUseShift;
        _hotkeyWin = currentSettings.HotkeyUseWin;

        HotkeyTextBox.Text = FormatHotkey();

        // Initialize Azure key summary asynchronously (do not block UI thread).
        _ = LoadAzureKeySummaryAsync();
    }

    private async System.Threading.Tasks.Task LoadAzureKeySummaryAsync()
    {
        try
        {
            var existing = await _azureCredentialStore.GetApiKeyAsync();
            if (!string.IsNullOrEmpty(existing))
            {
                var last4 = existing.Length >= 4 ? existing[^4..] : existing;
                AzureKeySummary.Text = $"An API key is configured (…{last4}).";
            }
            else
            {
                AzureKeySummary.Text = "No API key configured.";
            }
        }
        catch
        {
            AzureKeySummary.Text = "Unable to determine current key status.";
        }
    }

    private async System.Threading.Tasks.Task LoadWhisperBuildInfoAsync()
    {
        try
        {
            var execPath = _whisperOptions.ResolveExecutablePath();
            if (!File.Exists(execPath))
            {
                WhisperBuildInfoText.Text = $"Executable not found at {execPath}.";
                return;
            }

            WhisperBuildInfoText.Text = "Detecting Whisper build…";

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

            var exited = await System.Threading.Tasks.Task.WhenAny(
                process.WaitForExitAsync(),
                System.Threading.Tasks.Task.Delay(3000)) == process.WaitForExitAsync();

            if (!exited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // ignore kill failures
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
                buildKind = exited
                    ? "Unknown (no help output captured)"
                    : "Unknown (process did not finish within timeout)";
            }
            else
            {
                buildKind = isGpu
                    ? "GPU-accelerated (CUDA)"
                    : "CPU-only (no GPU indicators in help output)";
            }

            WhisperBuildInfoText.Text =
                $"Executable: {execPath}\n" +
                $"Build: {buildKind}";
        }
        catch (Exception ex)
        {
            WhisperBuildInfoText.Text = $"Unable to determine Whisper build: {ex.Message}";
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var selected = DeviceCombo.SelectedItem as AudioDeviceInfo;
        var settings = _settingsStore.Current ?? new UserSettings();
        settings.AudioInputDeviceId = selected?.DeviceId;

        // Persist speech engine selection
        if (EngineCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item &&
            item.Tag is string tag)
        {
            settings.SpeechEngine = tag switch
            {
                "Azure" => SpeechEngineKind.Azure,
                "WhisperLocal" => SpeechEngineKind.WhisperLocal,
                _ => settings.SpeechEngine
            };

            // If Azure is selected and a key is provided, persist it via the credential store.
            if (settings.SpeechEngine == SpeechEngineKind.Azure)
            {
                var key = AzureKeyBox.Password;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    await _azureCredentialStore.SetApiKeyAsync(key);
                }
            }
        }

        settings.HotkeyVirtualKey = _hotkeyVk != 0 ? _hotkeyVk : settings.HotkeyVirtualKey;
        settings.HotkeyUseCtrl = _hotkeyCtrl;
        settings.HotkeyUseAlt = _hotkeyAlt;
        settings.HotkeyUseShift = _hotkeyShift;
        settings.HotkeyUseWin = _hotkeyWin;

        // Persist Whisper model selection
        if (WhisperModelCombo.SelectedItem is System.Windows.Controls.ComboBoxItem modelItem &&
            modelItem.Tag is string modelTag)
        {
            settings.WhisperModel = modelTag switch
            {
                "Base" => WhisperModelKind.Base,
                _ => WhisperModelKind.Tiny
            };
        }

        // Apply selected Whisper model to runtime options so changes take effect without restart.
        var effectiveModel = settings.WhisperModel ?? WhisperModelKind.Tiny;
        var modelFileName = effectiveModel == WhisperModelKind.Base
            ? "ggml-base.en.bin"
            : "ggml-tiny.en.bin";

        _whisperOptions.ModelPath = Path.Combine(
            AppContext.BaseDirectory,
            "Speech",
            "Models",
            modelFileName);

        _settingsStore.Save(settings);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ChangeHotkey_Click(object sender, RoutedEventArgs e)
    {
        _capturingHotkey = true;
        HotkeyTextBox.Text = "Press new hotkey...";
        HotkeyTextBox.Focus();
    }

    private async void TestDevice_Click(object sender, RoutedEventArgs e)
    {
        var selected = DeviceCombo.SelectedItem as AudioDeviceInfo;
        if (selected is null)
        {
            MessageBox.Show(this, "Please select an input device first.", "Microphone test", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var testButton = (System.Windows.Controls.Button)sender;
        testButton.IsEnabled = false;

        var format = new VoxThisWay.Core.Audio.AudioFormat(16000, 16, 1);
        var options = new VoxThisWay.Core.Audio.AudioCaptureOptions(selected.DeviceId ?? string.Empty, format, AutoGainControl: false);

        var bufferCount = 0;
        void Handler(object? s, VoxThisWay.Core.Audio.AudioBufferReadyEventArgs args)
        {
            bufferCount++;
        }

        _audioCaptureService.AudioBufferReady += Handler;

        try
        {
            await _audioCaptureService.StartAsync(options, System.Threading.CancellationToken.None);
            await System.Threading.Tasks.Task.Delay(2000);
            await _audioCaptureService.StopAsync();

            var message = bufferCount > 0
                ? $"Microphone test succeeded. Received {bufferCount} audio buffer(s)."
                : "No audio buffers were received during the test. Check your device and system settings.";

            MessageBox.Show(this, message, "Microphone test", MessageBoxButton.OK, bufferCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Microphone test failed: {ex.Message}", "Microphone test", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _audioCaptureService.AudioBufferReady -= Handler;
            testButton.IsEnabled = true;
        }
    }

    private void EngineCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (EngineCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item &&
            item.Tag is string tag)
        {
            if (tag == "Azure")
            {
                AzurePanel.Visibility = Visibility.Visible;
                WhisperPanel.Visibility = Visibility.Collapsed;
            }
            else if (tag == "WhisperLocal")
            {
                AzurePanel.Visibility = Visibility.Collapsed;
                WhisperPanel.Visibility = Visibility.Visible;
                _ = LoadWhisperBuildInfoAsync();
            }
        }
    }

    private void HotkeyTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingHotkey)
        {
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        _hotkeyCtrl = modifiers.HasFlag(ModifierKeys.Control);
        _hotkeyAlt = modifiers.HasFlag(ModifierKeys.Alt);
        _hotkeyShift = modifiers.HasFlag(ModifierKeys.Shift);
        _hotkeyWin = modifiers.HasFlag(ModifierKeys.Windows);

        _hotkeyVk = KeyInterop.VirtualKeyFromKey(key);
        _capturingHotkey = false;
        HotkeyTextBox.Text = FormatHotkey();
        e.Handled = true;
    }

    private void SupportLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            var psi = new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Unable to open link: {ex.Message}", "VoxThisWay", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        e.Handled = true;
    }

    private string FormatHotkey()
    {
        var parts = new List<string>();
        if (_hotkeyCtrl) parts.Add("Ctrl");
        if (_hotkeyAlt) parts.Add("Alt");
        if (_hotkeyShift) parts.Add("Shift");
        if (_hotkeyWin) parts.Add("Win");

        var key = KeyInterop.KeyFromVirtualKey(_hotkeyVk != 0 ? _hotkeyVk : 0x20);
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }
}
