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
    private int _isProcessing;

    public ListeningStateService(
        ILogger<ListeningStateService> logger,
        ITranscriptionSessionManager sessionManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
    }

    public bool IsListening => Interlocked.CompareExchange(ref _isListening, 0, 0) == 1;

    public bool IsProcessing => Interlocked.CompareExchange(ref _isProcessing, 0, 0) == 1;

    public event EventHandler<bool>? ListeningStateChanged;

    public event EventHandler<bool>? ProcessingStateChanged;

    public void RequestStart()
    {
        if (Interlocked.Exchange(ref _isListening, 1) == 1)
        {
            return;
        }

        if (Interlocked.Exchange(ref _isProcessing, 0) == 1)
        {
            ProcessingStateChanged?.Invoke(this, false);
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

        if (Interlocked.Exchange(ref _isProcessing, 1) == 0)
        {
            ProcessingStateChanged?.Invoke(this, true);
        }

        _ = StopSessionAndClearProcessingAsync();
    }

    private async System.Threading.Tasks.Task StopSessionAndClearProcessingAsync()
    {
        try
        {
            await _sessionManager.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop transcription session.");
        }
        finally
        {
            if (Interlocked.Exchange(ref _isProcessing, 0) == 1)
            {
                ProcessingStateChanged?.Invoke(this, false);
            }
        }
    }
}
