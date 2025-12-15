using System;
using System.Windows;
using Microsoft.Extensions.Options;
using FluentWindow = Wpf.Ui.Controls.FluentWindow;
using VoxThisWay.Core.Audio;
using VoxThisWay.Core.Configuration;
using VoxThisWay.Core.Secrets;
using VoxThisWay.App.OnboardingPages;

namespace VoxThisWay.App;

public partial class OnboardingWindow : FluentWindow
{
    private readonly IOptions<SpeechEngineOptions> _speechOptions;
    private readonly IOptions<WhisperLocalOptions> _whisperOptions;
    private readonly IAzureSpeechCredentialStore _azureCredentialStore;
    private readonly IAudioCaptureService _audioCaptureService;
    private readonly IUserSettingsStore _settingsStore;
    private readonly Action _openSettingsAction;

    internal static OnboardingSession? CurrentSession { get; private set; }

    private OnboardingSession? _session;
    private int _pageIndex;
    private readonly Type[] _pageTypes =
    {
        typeof(WelcomePage),
        typeof(MicrophonePage),
        typeof(HotkeyPage),
        typeof(ChecksPage),
        typeof(FinalPage)
    };

    public OnboardingWindow(
        IOptions<SpeechEngineOptions> speechOptions,
        IOptions<WhisperLocalOptions> whisperOptions,
        IAzureSpeechCredentialStore azureCredentialStore,
        IAudioCaptureService audioCaptureService,
        IUserSettingsStore settingsStore,
        Action openSettingsAction)
    {
        InitializeComponent();

        _speechOptions = speechOptions;
        _whisperOptions = whisperOptions;
        _azureCredentialStore = azureCredentialStore;
        _audioCaptureService = audioCaptureService;
        _settingsStore = settingsStore;
        _openSettingsAction = openSettingsAction;

        Loaded += async (_, _) =>
        {
            var settings = _settingsStore.Current;
            DontShowAgainCheckBox.IsChecked = settings is { ShowOnboarding: false } ? true : false;

            _session = new OnboardingSession(_speechOptions, _whisperOptions, _azureCredentialStore, _audioCaptureService, _settingsStore);
            CurrentSession = _session;
            await _session.UpdateStatusAsync();

            _pageIndex = 0;
            NavigateToIndex(_pageIndex);
        };

        Closed += (_, _) =>
        {
            if (ReferenceEquals(CurrentSession, _session))
            {
                CurrentSession = null;
            }
        };
    }

    private void NavigateToIndex(int index)
    {
        if (index < 0 || index >= _pageTypes.Length)
        {
            return;
        }

        ContentFrame.Navigate(Activator.CreateInstance(_pageTypes[index]));

        var step = index + 1;
        var total = _pageTypes.Length;
        ProgressText.Text = $"Step {step} of {total}";
        ProgressBar.Minimum = 0;
        ProgressBar.Maximum = total - 1;
        ProgressBar.Value = index;

        BackButton.IsEnabled = index > 0;
        NextButton.IsEnabled = index < _pageTypes.Length - 1;

        var onChecksPage = _pageTypes[index] == typeof(ChecksPage);
        RerunChecksButton.IsEnabled = onChecksPage;
        RerunChecksButton.Visibility = onChecksPage ? Visibility.Visible : Visibility.Collapsed;

        var onFinalPage = index == _pageTypes.Length - 1;
        OpenSettingsButton.Visibility = onFinalPage ? Visibility.Visible : Visibility.Collapsed;

        CloseButton.Content = index == _pageTypes.Length - 1 ? "Finish" : "Close";
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_pageIndex <= 0)
        {
            return;
        }

        _pageIndex--;
        NavigateToIndex(_pageIndex);
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        if (_pageIndex >= _pageTypes.Length - 1)
        {
            return;
        }

        _pageIndex++;
        NavigateToIndex(_pageIndex);
    }

    private async void RerunChecks_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        await _session.UpdateStatusAsync();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        _openSettingsAction();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var settings = _settingsStore.Current ?? new UserSettings();

        var isFinal = _pageIndex == _pageTypes.Length - 1;
        if (isFinal && _session is not null)
        {
            settings.AudioInputDeviceId = _session.SelectedDevice?.DeviceId;
            settings.HotkeyVirtualKey = _session.HotkeyVirtualKey != 0 ? _session.HotkeyVirtualKey : settings.HotkeyVirtualKey;
            settings.HotkeyUseCtrl = _session.HotkeyUseCtrl;
            settings.HotkeyUseAlt = _session.HotkeyUseAlt;
            settings.HotkeyUseShift = _session.HotkeyUseShift;
            settings.HotkeyUseWin = _session.HotkeyUseWin;
        }

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
