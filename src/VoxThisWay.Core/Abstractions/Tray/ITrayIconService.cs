using System;

namespace VoxThisWay.Core.Abstractions.Tray;

public interface ITrayIconService : IDisposable
{
    void Initialize(TrayMenuActions menuActions);

    void UpdateStatus(string statusText);

    void ShowNotification(string title, string message, TrayNotificationType notificationType = TrayNotificationType.Info);
}
