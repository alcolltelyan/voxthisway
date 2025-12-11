using System;

namespace VoxThisWay.Core.Abstractions.Input;

public interface IHotkeyService : IDisposable
{
    bool IsActive { get; }

    event EventHandler? PushToTalkStarted;

    event EventHandler? PushToTalkEnded;

    void Start();

    void Stop();
}
