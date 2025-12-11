namespace VoxThisWay.Core.Configuration;

public sealed class SpeechEngineOptions
{
    public SpeechEngineKind ActiveEngine { get; set; } = SpeechEngineKind.WhisperLocal;

    public string Language { get; set; } = "en";

    public WhisperLocalOptions WhisperLocal { get; set; } = new();

    public AzureSpeechOptions AzureSpeech { get; set; } = new();
}
