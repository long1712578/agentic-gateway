using AgenticGateway.Core.Routing;

namespace AgenticGateway.Core.Responses;

public interface IResponsesUpstream
{
    Task<UpstreamResponseLease> ForwardAsync(
        ModelRoute route,
        ReadOnlyMemory<byte> requestBody,
        bool streaming,
        CancellationToken cancellationToken = default);
}
