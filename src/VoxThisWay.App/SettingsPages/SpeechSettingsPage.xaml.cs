using System;
using System.Windows;
using System.Windows.Controls;
using VoxThisWay.Core.Configuration;

namespace VoxThisWay.App.SettingsPages;

public partial class SpeechSettingsPage : Page
{
    private readonly SettingsSession _session;

    public SpeechSettingsPage()
    {
        InitializeComponent();

        _session = VoxThisWay.App.SettingsWindow.CurrentSession
                   ?? throw new InvalidOperationException("Settings session is not available.");

        Loaded += async (_, _) =>
        {
            await _session.RefreshAzureKeySummaryAsync();
            AzureKeySummary.Text = _session.AzureKeySummary;

            await _session.RefreshWhisperBuildInfoAsync();
            WhisperBuildInfoText.Text = _session.WhisperBuildInfo;
        };

        if (_session.SelectedEngine == SpeechEngineKind.Azure)
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
        }

        WhisperModelCombo.SelectedItem = _session.SelectedWhisperModel == WhisperModelKind.Base
            ? WhisperBaseItem
            : WhisperTinyItem;
    }

    private void EngineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EngineCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            if (tag == "Azure")
            {
                _session.SelectedEngine = SpeechEngineKind.Azure;
                AzurePanel.Visibility = Visibility.Visible;
                WhisperPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                _session.SelectedEngine = SpeechEngineKind.WhisperLocal;
                AzurePanel.Visibility = Visibility.Collapsed;
                WhisperPanel.Visibility = Visibility.Visible;
            }
        }
    }

    private void WhisperModelCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WhisperModelCombo.SelectedItem is ComboBoxItem item && item.Tag is string modelTag)
        {
            _session.SelectedWhisperModel = modelTag == "Base" ? WhisperModelKind.Base : WhisperModelKind.Tiny;
        }
    }

    private void AzureKeyBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        _session.PendingAzureKey = AzureKeyBox.Password;
    }
}
