using AgenticGateway.Core.Routing;

namespace AgenticGateway.Providers.OpenAICompatible;

public sealed class ConfigurationModelRouter(IUpstreamSettings settings) : IModelRouter
{
    private const string ConnectionId = "openai-compatible";

    public IReadOnlyDictionary<string, ModelRoute> GetAvailableModels()
    {
        var aliases = ResolveAliases(settings.GetCurrent());
        return aliases.ToDictionary(
            entry => entry.Key,
            entry => new ModelRoute(entry.Key, entry.Value, ConnectionId),
            StringComparer.Ordinal);
    }

    public bool TryResolve(string publicModel, out ModelRoute route)
    {
        if (ResolveAliases(settings.GetCurrent()).TryGetValue(publicModel, out var upstreamModel))
        {
            route = new ModelRoute(publicModel, upstreamModel, ConnectionId);
            return true;
        }

        route = default!;
        return false;
    }

    private static IReadOnlyDictionary<string, string> ResolveAliases(ResponsesUpstreamOptions current)
    {
        var aliases = new Dictionary<string, string>(current.ModelAliases, StringComparer.Ordinal);
        if (!aliases.ContainsKey("coding-default")
            && !string.IsNullOrWhiteSpace(current.ModelAliases.GetValueOrDefault("default")))
        {
            aliases["coding-default"] = current.ModelAliases["default"];
        }

        return aliases;
    }
}
