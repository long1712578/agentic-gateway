using System.Text;

namespace AgenticGateway.Host.Security;

public sealed class GatewayApiKeys(IConfiguration configuration)
{
    public byte[] Inference { get; } = Load(configuration, "Gateway:ApiKey", "AGENTIC_GATEWAY_API_KEY");
    public byte[] Memory { get; } = Load(configuration, "Gateway:MemoryApiKey", "AGENTIC_GATEWAY_MCP_API_KEY");

    private static byte[] Load(IConfiguration configuration, string configurationKey, string environmentKey)
    {
        var value = configuration[configurationKey] ?? Environment.GetEnvironmentVariable(environmentKey);
        if (string.IsNullOrWhiteSpace(value) || Encoding.UTF8.GetByteCount(value) < 32)
        {
            throw new InvalidOperationException($"{environmentKey} must be set to a random value of at least 32 bytes.");
        }

        return Encoding.UTF8.GetBytes(value);
    }
}
