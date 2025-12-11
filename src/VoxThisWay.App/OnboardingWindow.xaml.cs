using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Options;
using VoxThisWay.Core.Configuration;
using VoxThisWay.Core.Secrets;

namespace VoxThisWay.App;

public partial class OnboardingWindow : Window
{
    private readonly IOptions<SpeechEngineOptions> _speechOptions;
    private readonly IOptions<WhisperLocalOptions> _whisperOptions;
    private readonly IAzureSpeechCredentialStore _azureCredentialStore;
    private readonly IUserSettingsStore _settingsStore;
    private readonly Action _openSettingsAction;

    public OnboardingWindow(
        IOptions<SpeechEngineOptions> speechOptions,
        IOptions<WhisperLocalOptions> whisperOptions,
        IAzureSpeechCredentialStore azureCredentialStore,
        IUserSettingsStore settingsStore,
        Action openSettingsAction)
    {
        InitializeComponent();

        _speechOptions = speechOptions;
        _whisperOptions = whisperOptions;
        _azureCredentialStore = azureCredentialStore;
        _settingsStore = settingsStore;
        _openSettingsAction = openSettingsAction;

        Loaded += async (_, _) =>
        {
            await UpdateStatusAsync();
            var settings = _settingsStore.Current;
            DontShowAgainCheckBox.IsChecked = settings is { ShowOnboarding: false } ? true : false;
        };
    }

    private async System.Threading.Tasks.Task UpdateStatusAsync()
    {
        // Whisper local check
        var whisperExec = _whisperOptions.Value.ResolveExecutablePath();
        var whisperModel = _whisperOptions.Value.ResolveModelPath();
        var hasExec = File.Exists(whisperExec);
        var hasModel = File.Exists(whisperModel);

        if (hasExec && hasModel)
        {
            WhisperStatusIcon.Text = "✔";
            WhisperStatusIcon.Foreground = Brushes.Green;

            WhisperStatusText.Text =
                $"Whisper local looks ready.\nExecutable: {whisperExec}\nModel: {whisperModel}";
        }
        else
        {
            WhisperStatusIcon.Text = "✖";
            WhisperStatusIcon.Foreground = Brushes.Red;

            WhisperStatusText.Text =
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
            // ignore failures; handled below
        }

        var hasKey = !string.IsNullOrWhiteSpace(key);
        var hasRegionOrEndpoint = !string.IsNullOrWhiteSpace(azureRegion) || !string.IsNullOrWhiteSpace(azureEndpoint);

        if (hasKey && hasRegionOrEndpoint)
        {
            AzureStatusIcon.Text = "✔";
            AzureStatusIcon.Foreground = Brushes.Green;

            var last4 = key!.Length >= 4 ? key[^4..] : key;
            AzureStatusText.Text =
                "Azure Speech appears configured.\n" +
                $"Key: (stored securely, ending …{last4})\n" +
                $"Region: {azureRegion ?? "(not set)"}\n" +
                $"Endpoint: {azureEndpoint ?? "(not set)"}";
        }
        else
        {
            AzureStatusIcon.Text = "✖";
            AzureStatusIcon.Foreground = Brushes.Red;

            AzureStatusText.Text =
                "Azure Speech is optional and not fully configured.\n" +
                "To use Azure: open Settings → Speech engine → Azure Speech, enter your API key and region/endpoint.";
        }
    }

    private async void RerunChecks_Click(object sender, RoutedEventArgs e)
    {
        await UpdateStatusAsync();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        _openSettingsAction();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsStore.Current ?? new UserSettings();
        // If the user checked "Don’t show this again", persist ShowOnboarding = false.
        if (DontShowAgainCheckBox.IsChecked == true)
        {
            settings.ShowOnboarding = false;
        }
        else
        {
            settings.ShowOnboarding = true;
        }

        _settingsStore.Save(settings);
        DialogResult = true;
        Close();
    }
}
