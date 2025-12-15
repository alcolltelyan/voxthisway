using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using VoxThisWay.Core.Audio;
using VoxThisWay.Core.Configuration;
using VoxThisWay.Core.Secrets;
using VoxThisWay.App.SettingsPages;

namespace VoxThisWay.App;

public partial class SettingsWindow : FluentWindow
{
    internal static SettingsSession? CurrentSession { get; private set; }

    private readonly SettingsSession _session;

    public SettingsWindow()
    {
        InitializeComponent();

        var services = App.Services;
        if (services is null)
        {
            throw new InvalidOperationException("Application services are not available.");
        }

        var audioCaptureService = services.GetRequiredService<IAudioCaptureService>();
        var settingsStore = services.GetRequiredService<IUserSettingsStore>();
        var azureCredentialStore = services.GetRequiredService<IAzureSpeechCredentialStore>();
        var whisperOptions = services.GetRequiredService<IOptions<WhisperLocalOptions>>();

        _session = new SettingsSession(audioCaptureService, settingsStore, azureCredentialStore, whisperOptions.Value);
        CurrentSession = _session;

        Loaded += (_, _) => RootNavigation.Navigate(typeof(AudioSettingsPage));
        Closed += (_, _) =>
        {
            if (ReferenceEquals(CurrentSession, _session))
            {
                CurrentSession = null;
            }
        };
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await _session.SaveAsync();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
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
}
