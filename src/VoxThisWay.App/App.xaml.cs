using System.Media;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        var processingIndicatorWindow = new ProcessingIndicatorWindow();

        trayService.Initialize(new TrayMenuActions(
            listeningService.RequestStart,
            listeningService.RequestStop,
            OpenSettings,
            OpenLogs,
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
        }

        listeningService.ListeningStateChanged += (_, isListening) =>
        {
            var status = isListening ? "Listening…" : "Idle";
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

            var window = new SettingsWindow(audioCaptureService, settingsStore)
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

