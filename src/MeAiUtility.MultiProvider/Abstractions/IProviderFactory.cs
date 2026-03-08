using Microsoft.Extensions.AI;

namespace MeAiUtility.MultiProvider.Abstractions;

public interface IProviderFactory
{
    IChatClient Create();
}
