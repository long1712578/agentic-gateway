namespace AgenticGateway.Providers.OpenAICompatible;

public interface IUpstreamSettings
{
    ResponsesUpstreamOptions GetCurrent();
    bool IsEnvironmentManaged { get; }
}
