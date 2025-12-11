using VoxThisWay.Core.Configuration;

namespace VoxThisWay.Core.Transcription;

public interface ISpeechTranscriberFactory
{
    ISpeechTranscriber Create(SpeechEngineKind engineKind);
}
