using System;

namespace VoxThisWay.Core.Abstractions.Input;

public interface IListeningStateService
{
    bool IsListening { get; }

    bool IsProcessing { get; }

    event EventHandler<bool>? ListeningStateChanged;

    event EventHandler<bool>? ProcessingStateChanged;

    void RequestStart();

    void RequestStop();
}
