namespace VoxThisWay.Core.Configuration;

public sealed class AzureSpeechOptions
{
    public string Region { get; set; } = string.Empty;

    public string? Endpoint { get; set; }

    public bool UseCustomEndpoint => !string.IsNullOrWhiteSpace(Endpoint);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Region);
}
