using System;
using System.Windows.Controls;

namespace VoxThisWay.App.OnboardingPages;

public partial class FinalPage : Page
{
    private readonly OnboardingSession _session;

    public FinalPage()
    {
        InitializeComponent();

        _session = VoxThisWay.App.OnboardingWindow.CurrentSession
                   ?? throw new InvalidOperationException("Onboarding session is not available.");

        Loaded += (_, _) =>
        {
            var deviceName = _session.SelectedDevice?.DisplayName ?? "(not selected)";
            var hotkey = _session.FormatHotkey();
            SummaryText.Text = $"Microphone: {deviceName}\nHotkey: {hotkey}";
        };
    }
}
