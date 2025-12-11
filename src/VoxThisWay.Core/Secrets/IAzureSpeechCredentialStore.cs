using System.Threading.Tasks;

namespace VoxThisWay.Core.Secrets;

public interface IAzureSpeechCredentialStore
{
    Task<string?> GetApiKeyAsync();

    Task SetApiKeyAsync(string apiKey);
}
