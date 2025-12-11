using System;
using Microsoft.Extensions.DependencyInjection;
using VoxThisWay.Core.Configuration;
using VoxThisWay.Core.Transcription;

namespace VoxThisWay.Services.Transcription;

public sealed class SpeechTranscriberFactory : ISpeechTranscriberFactory
{
    private readonly IServiceProvider _serviceProvider;

    public SpeechTranscriberFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ISpeechTranscriber Create(SpeechEngineKind engineKind)
    {
        return engineKind switch
        {
            SpeechEngineKind.Azure => _serviceProvider.GetRequiredService<AzureSpeechTranscriber>(),
            SpeechEngineKind.WhisperLocal => _serviceProvider.GetRequiredService<WhisperLocalTranscriber>(),
            SpeechEngineKind.Mock => _serviceProvider.GetRequiredService<MockSpeechTranscriber>(),
            _ => _serviceProvider.GetRequiredService<MockSpeechTranscriber>()
        };
    }
}
