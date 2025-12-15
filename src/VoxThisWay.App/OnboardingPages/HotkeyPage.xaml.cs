using System;
using System.Windows.Controls;
using System.Windows.Input;

namespace VoxThisWay.App.OnboardingPages;

public partial class HotkeyPage : Page
{
    private readonly OnboardingSession _session;
    private bool _capturingHotkey;

    public HotkeyPage()
    {
        InitializeComponent();

        _session = VoxThisWay.App.OnboardingWindow.CurrentSession
                   ?? throw new InvalidOperationException("Onboarding session is not available.");

        HotkeyTextBox.Text = _session.FormatHotkey();
    }

    private void ChangeHotkey_Click(object sender, System.Windows.RoutedEventArgs e)
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
        _session.HotkeyUseCtrl = modifiers.HasFlag(ModifierKeys.Control);
        _session.HotkeyUseAlt = modifiers.HasFlag(ModifierKeys.Alt);
        _session.HotkeyUseShift = modifiers.HasFlag(ModifierKeys.Shift);
        _session.HotkeyUseWin = modifiers.HasFlag(ModifierKeys.Windows);

        _session.HotkeyVirtualKey = KeyInterop.VirtualKeyFromKey(key);

        _capturingHotkey = false;
        HotkeyTextBox.Text = _session.FormatHotkey();
        e.Handled = true;
    }
}
