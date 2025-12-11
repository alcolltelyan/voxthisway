using VoxThisWay.Core.Configuration;
using Xunit;

namespace VoxThisWay.Tests;

public class SpeechEngineOptionsTests
{
    [Fact]
    public void Defaults_AreWhisperLocal_English()
    {
        var options = new SpeechEngineOptions();

        Assert.Equal(SpeechEngineKind.WhisperLocal, options.ActiveEngine);
        Assert.Equal("en", options.Language);
        Assert.NotNull(options.WhisperLocal);
        Assert.NotNull(options.AzureSpeech);
    }
}
