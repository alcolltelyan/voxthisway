using System.IO;
using System.Media;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using VoxThisWay.Core.Abstractions.Input;
using VoxThisWay.Core.Abstractions.Tray;
using VoxThisWay.Core.Abstractions.Text;
using VoxThisWay.Core.Audio;
using VoxThisWay.Core.Configuration;
using VoxThisWay.Core.Secrets;
using VoxThisWay.Core.Transcription;
using VoxThisWay.Services.Audio;
using VoxThisWay.Services.Configuration;
using VoxThisWay.Services.Input;
using VoxThisWay.Services.Secrets;
using VoxThisWay.Services.Tray;
using VoxThisWay.Services.Text;
using VoxThisWay.Services.Transcription;

namespace VoxThisWay.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppDirectories.EnsureAll();
        ConfigureLogging();
        _host = BuildHost();
        _host.Start();

        var trayService = _host.Services.GetRequiredService<ITrayIconService>();
        var listeningService = _host.Services.GetRequiredService<IListeningStateService>();
        var textInjectionService = _host.Services.GetRequiredService<ITextInjectionService>();
        var sessionManager = _host.Services.GetRequiredService<ITranscriptionSessionManager>();
        var hotkeyService = _host.Services.GetRequiredService<IHotkeyService>();
        var settingsStore = _host.Services.GetRequiredService<IUserSettingsStore>();
        var processingIndicatorWindow = new ProcessingIndicatorWindow();

        // Diagnostics: verify Whisper local assets when that engine is active.
        var engineOptions = _host.Services.GetRequiredService<IOptions<SpeechEngineOptions>>();
        var whisperOptions = _host.Services.GetRequiredService<IOptions<WhisperLocalOptions>>();
        if (engineOptions.Value.ActiveEngine == SpeechEngineKind.WhisperLocal)
        {
            var execPath = whisperOptions.Value.ResolveExecutablePath();
            var modelPath = whisperOptions.Value.ResolveModelPath();

            if (!File.Exists(execPath) || !File.Exists(modelPath))
            {
                Log.Warning("Whisper local assets missing or incomplete. ExecPath={ExecPath}, ModelPath={ModelPath}", execPath, modelPath);
                trayService.ShowNotification(
                    "Whisper local not ready",
                    "The Whisper executable or model file is missing. Ensure the 'Speech' folder from the ZIP is placed next to VoxThisWay.App.exe.",
                    TrayNotificationType.Warning);
            }
        }

        void OpenOnboarding()
        {
            try
            {
                var speechOptions = _host.Services.GetRequiredService<IOptions<SpeechEngineOptions>>();
                var whisperOptions = _host.Services.GetRequiredService<IOptions<WhisperLocalOptions>>();
                var azureCredentialStore = _host.Services.GetRequiredService<IAzureSpeechCredentialStore>();

                var window = new OnboardingWindow(
                    speechOptions,
                    whisperOptions,
                    azureCredentialStore,
                    settingsStore,
                    OpenSettings)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open onboarding wizard.");
                UiErrorReporter.ShowError(
                    "Onboarding failed",
                    $"The onboarding wizard could not be opened. You can still configure VoxThisWay via Settings.\n\nDetails: {ex.Message}");
            }
        }

        static string GetEngineLabel(SpeechEngineKind kind) => kind switch
        {
            SpeechEngineKind.Azure => "Azure",
            SpeechEngineKind.WhisperLocal => "Whisper",
            SpeechEngineKind.Mock => "Mock",
            _ => "Unknown"
        };

        string ResolveCurrentEngineLabel()
        {
            try
            {
                var userSettings = settingsStore.Current;
                var kind = userSettings?.SpeechEngine ?? engineOptions.Value.ActiveEngine;
                return GetEngineLabel(kind);
            }
            catch
            {
                return GetEngineLabel(engineOptions.Value.ActiveEngine);
            }
        }

        var engineLabel = ResolveCurrentEngineLabel();

        trayService.Initialize(new TrayMenuActions(
            listeningService.RequestStart,
            listeningService.RequestStop,
            OpenSettings,
            OpenLogs,
            OpenOnboarding,
            ExitApplication));

        var transcriptCoordinator = new TranscriptCoordinator(textInjectionService);

        try
        {
            hotkeyService.Start();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start global hotkey service.");
            trayService.ShowNotification(
                "Hotkey unavailable",
                "Ctrl+Space could not be registered. Use tray menu to control dictation.",
                TrayNotificationType.Warning);
            UiErrorReporter.ShowError(
                "Hotkey unavailable",
                $"VoxThisWay could not register the global hotkey. You can still control dictation from the tray menu.\n\nDetails: {ex.Message}");
        }

        var lastLevelBucket = -1;

        sessionManager.AudioLevelChanged += (_, level) =>
        {
            if (!listeningService.IsListening)
            {
                return;
            }

            // Map RMS level to a small number of discrete buckets to
            // avoid excessively frequent tooltip updates.
            var bucket = level switch
            {
                < 0.02 => 0, // silence / very low
                < 0.05 => 1,
                < 0.15 => 2,
                _ => 3
            };

            if (bucket == lastLevelBucket)
            {
                return;
            }

            lastLevelBucket = bucket;
            var bars = bucket switch
            {
                0 => "▁▁▁",
                1 => "▂▁▁",
                2 => "▂▃▁",
                _ => "▂▃▅"
            };

            trayService.UpdateStatus($"VoxThisWay — {engineLabel} (Listening {bars})");
        };

        listeningService.ListeningStateChanged += (_, isListening) =>
        {
            engineLabel = ResolveCurrentEngineLabel();
            var status = isListening ? $"{engineLabel} (Listening…)" : $"{engineLabel} (Idle)";
            trayService.UpdateStatus($"VoxThisWay — {status}");

            Dispatcher.Invoke(() =>
            {
                if (isListening)
                {
                    processingIndicatorWindow.Start();
                }
                else
                {
                    processingIndicatorWindow.Stop();
                }
            });

            if (isListening)
            {
                SystemSounds.Asterisk.Play();
                trayService.ShowNotification("Dictation ready", "Hold Ctrl+Space to stream text.", TrayNotificationType.Success);
                transcriptCoordinator.Reset();
            }
            else
            {
                trayService.ShowNotification("Dictation paused", "Hold Ctrl+Space again to resume.", TrayNotificationType.Info);
                _ = textInjectionService.InjectTextAsync(" ");
                textInjectionService.Reset();
            }
        };

        sessionManager.TranscriptReceived += (_, segment) =>
        {
            Log.Information(
                "Transcript segment received. IsFinal={IsFinal}, Length={Length}, TextPreview=\"{Preview}\"",
                segment.IsFinal,
                segment.Text?.Length ?? 0,
                TruncateForLog(segment.Text, 200));

            transcriptCoordinator.HandleTranscript(segment);
        };

        hotkeyService.PushToTalkStarted += (_, _) => listeningService.RequestStart();
        hotkeyService.PushToTalkEnded += (_, _) => listeningService.RequestStop();

        var mainWindow = new MainWindow();
        mainWindow.Hide();

        // Show onboarding wizard on first run (or until disabled).
        try
        {
            var userSettings = settingsStore.Current;
            if (userSettings is null || userSettings.ShowOnboarding)
            {
                OpenOnboarding();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed during onboarding auto-launch.");
            UiErrorReporter.ShowError(
                "Onboarding failed",
                $"Automatic onboarding could not complete. You can still open the wizard later from the tray menu.\n\nDetails: {ex.Message}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.File(
                System.IO.Path.Combine(AppDirectories.LogsDirectory, "voxthisway.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .WriteTo.Debug()
            .CreateLogger();
    }

    private static IHost BuildHost() =>
        Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                services.Configure<SpeechEngineOptions>(context.Configuration.GetSection("SpeechEngine"));
                services.Configure<WhisperLocalOptions>(context.Configuration.GetSection("SpeechEngine:WhisperLocal"));

                services.AddSingleton<ITrayIconService, TrayIconService>();
                services.AddSingleton<IListeningStateService, ListeningStateService>();
                services.AddSingleton<IAudioCaptureService, NaudioAudioCaptureService>();
                services.AddSingleton<IHotkeyService, GlobalHotkeyService>();
                services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
                services.AddSingleton<IAzureSpeechCredentialStore, AzureSpeechCredentialStore>();
                services.AddSingleton<IUserSettingsStore, JsonUserSettingsStore>();
                services.AddSingleton<ITextInjectionService, TextInjectionService>();
                services.AddSingleton<MockSpeechTranscriber>();
                services.AddSingleton<WhisperLocalTranscriber>();
                services.AddSingleton<AzureSpeechTranscriber>();
                services.AddSingleton<ISpeechTranscriberFactory, SpeechTranscriberFactory>();
                services.AddSingleton<ITranscriptionSessionManager, TranscriptionSessionManager>();
            })
            .Build();

    private void OpenSettings()
    {
        Log.Information("Settings requested via tray menu.");
        if (_host is null)
        {
            Log.Warning("Cannot open settings; host is not initialized.");
            return;
        }

        try
        {
            var audioCaptureService = _host.Services.GetRequiredService<IAudioCaptureService>();
            var settingsStore = _host.Services.GetRequiredService<IUserSettingsStore>();
            var hotkeyService = _host.Services.GetRequiredService<IHotkeyService>();
            var azureCredentialStore = _host.Services.GetRequiredService<IAzureSpeechCredentialStore>();
            var whisperOptions = _host.Services.GetRequiredService<IOptions<WhisperLocalOptions>>();

            var window = new SettingsWindow(audioCaptureService, settingsStore, azureCredentialStore, whisperOptions.Value)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };

            var result = window.ShowDialog();
            if (result == true)
            {
                hotkeyService.Stop();
                hotkeyService.Start();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open settings window.");
        }
    }

    private void OpenLogs()
    {
        var logFolder = AppDirectories.LogsDirectory;
        if (!System.IO.Directory.Exists(logFolder))
        {
            System.IO.Directory.CreateDirectory(logFolder);
        }

        System.Diagnostics.Process.Start("explorer.exe", logFolder);
    }

    private void ExitApplication()
    {
        Shutdown();
    }

    private static string TruncateForLog(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text ?? string.Empty;
        }

        return text.Substring(0, maxLength) + "";
    }

    private sealed class TranscriptCoordinator
    {
        private readonly ITextInjectionService _textInjectionService;
        private string _finalizedText = string.Empty;

        public TranscriptCoordinator(ITextInjectionService textInjectionService)
        {
            _textInjectionService = textInjectionService;
        }

        public void HandleTranscript(TranscriptSegment segment)
        {
            var pending = segment.IsFinal
                ? AppendFinal(segment.Text)
                : _finalizedText + segment.Text;

            _ = _textInjectionService.InjectTextAsync(pending);
        }

        public void Reset()
        {
            _finalizedText = string.Empty;
            _textInjectionService.Reset();
        }

        private string AppendFinal(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                _finalizedText = $"{_finalizedText}{text.Trim()} ".Replace("  ", " ");
            }

            return _finalizedText;
        }
    }
}

