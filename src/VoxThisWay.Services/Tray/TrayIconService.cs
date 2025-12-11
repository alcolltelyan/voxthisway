using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using VoxThisWay.Core.Abstractions.Tray;

namespace VoxThisWay.Services.Tray;

public sealed class TrayIconService : ITrayIconService
{
    private readonly ILogger<TrayIconService> _logger;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private bool _disposed;
    private readonly SemaphoreSlim _menuLock = new(1, 1);

    public TrayIconService(ILogger<TrayIconService> logger)
    {
        _logger = logger;
    }

    public void Initialize(TrayMenuActions menuActions)
    {
        ThrowIfDisposed();

        _menuLock.Wait();
        try
        {
            _contextMenu?.Dispose();
            _notifyIcon ??= CreateNotifyIcon();

            _contextMenu = BuildContextMenu(menuActions);
            _notifyIcon.ContextMenuStrip = _contextMenu;
            _notifyIcon.Visible = true;
        }
        finally
        {
            _menuLock.Release();
        }
    }

    public void UpdateStatus(string statusText)
    {
        if (_notifyIcon is null) return;
        _notifyIcon.Text = TrimToolTip(statusText);
    }

    public void ShowNotification(string title, string message, TrayNotificationType notificationType = TrayNotificationType.Info)
    {
        if (_notifyIcon is null) return;

        _notifyIcon.BalloonTipTitle = title;
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = notificationType switch
        {
            TrayNotificationType.Success => ToolTipIcon.Info,
            TrayNotificationType.Warning => ToolTipIcon.Warning,
            TrayNotificationType.Error => ToolTipIcon.Error,
            _ => ToolTipIcon.None
        };

        _notifyIcon.ShowBalloonTip(3000);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _menuLock.Wait();
        try
        {
            _contextMenu?.Dispose();
            if (_notifyIcon is not null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }

            _disposed = true;
        }
        finally
        {
            _menuLock.Release();
            _menuLock.Dispose();
        }
    }

    private NotifyIcon CreateNotifyIcon()
    {
        var icon = SystemIcons.Application;
        try
        {
            icon = Icon.ExtractAssociatedIcon(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty)
                   ?? SystemIcons.Application;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract application icon; falling back to default.");
        }

        var notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "VoxThisWay — Idle"
        };

        notifyIcon.DoubleClick += (_, _) =>
        {
            try
            {
                _contextMenu?.Show(Cursor.Position);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open tray menu on double-click.");
            }
        };

        return notifyIcon;
    }

    private static string TrimToolTip(string text)
    {
        const int maxLength = 63; // NotifyIcon tooltip max
        if (string.IsNullOrWhiteSpace(text)) return "VoxThisWay";
        var trimmed = text.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static ContextMenuStrip BuildContextMenu(TrayMenuActions actions)
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add("Start Listening", null, (_, _) => actions.StartListening());
        menu.Items.Add("Stop Listening", null, (_, _) => actions.StopListening());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings…", null, (_, _) => actions.OpenSettings());
        menu.Items.Add("Onboarding…", null, (_, _) => actions.OpenOnboarding());
        menu.Items.Add("View Logs", null, (_, _) => actions.ViewLogs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Support VoxThisWay…", null, (_, _) => actions.OpenSupport());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => actions.ExitApplication());

        return menu;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TrayIconService));
        }
    }
}
