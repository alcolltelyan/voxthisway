using System.Threading;
using System.Threading.Tasks;

namespace VoxThisWay.Core.Abstractions.Text;

public interface ITextInjectionService
{
    Task InjectTextAsync(string text, CancellationToken cancellationToken = default);

    void Reset();
}
