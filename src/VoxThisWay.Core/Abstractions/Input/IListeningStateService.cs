using System;

namespace VoxThisWay.Core.Abstractions.Input;

public interface IListeningStateService
{
    bool IsListening { get; }

    event EventHandler<bool>? ListeningStateChanged;

    void RequestStart();

    void RequestStop();
}
