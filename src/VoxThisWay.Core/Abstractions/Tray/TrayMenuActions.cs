using System;

namespace VoxThisWay.Core.Abstractions.Tray;

public record TrayMenuActions(
    Action StartListening,
    Action StopListening,
    Action OpenSettings,
    Action ViewLogs,
    Action OpenSupport,
    Action OpenOnboarding,
    Action ExitApplication);
