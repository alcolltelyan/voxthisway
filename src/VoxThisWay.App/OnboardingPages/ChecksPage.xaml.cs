using System;
using System.Windows.Controls;

namespace VoxThisWay.App.OnboardingPages;

public partial class ChecksPage : Page
{
    private readonly OnboardingSession _session;

    public ChecksPage()
    {
        InitializeComponent();

        _session = VoxThisWay.App.OnboardingWindow.CurrentSession
                   ?? throw new InvalidOperationException("Onboarding session is not available.");

        Loaded += (_, _) => ApplySessionToUi();
        Unloaded += (_, _) => _session.StatusUpdated -= SessionOnStatusUpdated;
        _session.StatusUpdated += SessionOnStatusUpdated;
    }

    private void SessionOnStatusUpdated(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(ApplySessionToUi);
    }

    private void ApplySessionToUi()
    {
        WhisperStatusIcon.Text = _session.WhisperStatusIcon;
        WhisperStatusIcon.Foreground = _session.WhisperStatusBrush;
        WhisperStatusText.Text = _session.WhisperStatusText;

        AzureStatusIcon.Text = _session.AzureStatusIcon;
        AzureStatusIcon.Foreground = _session.AzureStatusBrush;
        AzureStatusText.Text = _session.AzureStatusText;
    }
}
