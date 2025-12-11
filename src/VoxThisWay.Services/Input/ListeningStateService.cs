using System;
using System.Threading;
using Microsoft.Extensions.Logging;
using VoxThisWay.Core.Abstractions.Input;
using VoxThisWay.Core.Transcription;

namespace VoxThisWay.Services.Input;

public sealed class ListeningStateService : IListeningStateService
{
    private readonly ILogger<ListeningStateService> _logger;
    private readonly ITranscriptionSessionManager _sessionManager;
    private int _isListening;

    public ListeningStateService(
        ILogger<ListeningStateService> logger,
        ITranscriptionSessionManager sessionManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
    }

    public bool IsListening => Interlocked.CompareExchange(ref _isListening, 0, 0) == 1;

    public event EventHandler<bool>? ListeningStateChanged;

    public void RequestStart()
    {
        if (Interlocked.Exchange(ref _isListening, 1) == 1)
        {
            return;
        }

        _logger.LogInformation("Listening requested (placeholder implementation).");
        ListeningStateChanged?.Invoke(this, true);
        _ = _sessionManager.StartAsync();
    }

    public void RequestStop()
    {
        if (Interlocked.Exchange(ref _isListening, 0) == 0)
        {
            return;
        }

        _logger.LogInformation("Listening stopped (placeholder implementation).");
        ListeningStateChanged?.Invoke(this, false);
        _ = _sessionManager.StopAsync();
    }
}
