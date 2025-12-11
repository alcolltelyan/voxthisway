using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VoxThisWay.Core.Audio;
using VoxThisWay.Core.Configuration;

namespace VoxThisWay.App;

public partial class SettingsWindow : Window
{
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly IUserSettingsStore _settingsStore;
    private readonly List<AudioDeviceInfo> _devices;
    private bool _capturingHotkey;
    private int _hotkeyVk;
    private bool _hotkeyCtrl;
    private bool _hotkeyAlt;
    private bool _hotkeyShift;
    private bool _hotkeyWin;

    public SettingsWindow(IAudioCaptureService audioCaptureService, IUserSettingsStore settingsStore)
    {
        InitializeComponent();

        _audioCaptureService = audioCaptureService;
        _settingsStore = settingsStore;

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

        _hotkeyVk = currentSettings.HotkeyVirtualKey != 0 ? currentSettings.HotkeyVirtualKey : 0x20;
        _hotkeyCtrl = currentSettings.HotkeyUseCtrl;
        _hotkeyAlt = currentSettings.HotkeyUseAlt;
        _hotkeyShift = currentSettings.HotkeyUseShift;
        _hotkeyWin = currentSettings.HotkeyUseWin;

        HotkeyTextBox.Text = FormatHotkey();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var selected = DeviceCombo.SelectedItem as AudioDeviceInfo;
        var settings = _settingsStore.Current ?? new UserSettings();
        settings.AudioInputDeviceId = selected?.DeviceId;
        settings.HotkeyVirtualKey = _hotkeyVk != 0 ? _hotkeyVk : settings.HotkeyVirtualKey;
        settings.HotkeyUseCtrl = _hotkeyCtrl;
        settings.HotkeyUseAlt = _hotkeyAlt;
        settings.HotkeyUseShift = _hotkeyShift;
        settings.HotkeyUseWin = _hotkeyWin;
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
