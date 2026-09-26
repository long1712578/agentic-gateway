namespace AgenticGateway.Core.Routing;

public interface IModelRouter
{
    IReadOnlyDictionary<string, ModelRoute> GetAvailableModels();
    bool TryResolve(string publicModel, out ModelRoute route);
}
