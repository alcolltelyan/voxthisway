using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using VoxThisWay.Core.Configuration;
using VoxThisWay.Core.Secrets;

namespace VoxThisWay.Services.Secrets;

public sealed class AzureSpeechCredentialStore : IAzureSpeechCredentialStore
{
    private readonly ISecretProtector _secretProtector;
    private readonly string _secretFile;

    private sealed record SecretPayload(string Key);

    public AzureSpeechCredentialStore(ISecretProtector secretProtector)
    {
        _secretProtector = secretProtector;
        _secretFile = Path.Combine(AppDirectories.SettingsDirectory, "azure-speech-key.json");
    }

    public async Task<string?> GetApiKeyAsync()
    {
        if (!File.Exists(_secretFile))
        {
            return null;
        }

        try
        {
            var payload = await JsonSerializer.DeserializeAsync<SecretPayload>(
                File.OpenRead(_secretFile));

            if (payload is null || string.IsNullOrEmpty(payload.Key))
            {
                return null;
            }

            return _secretProtector.Unprotect(payload.Key);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetApiKeyAsync(string apiKey)
    {
        Directory.CreateDirectory(AppDirectories.SettingsDirectory);
        var protectedValue = _secretProtector.Protect(apiKey);
        var payload = new SecretPayload(protectedValue);
        await using var stream = File.Create(_secretFile);
        await JsonSerializer.SerializeAsync(stream, payload, new JsonSerializerOptions { WriteIndented = false });
    }
}
