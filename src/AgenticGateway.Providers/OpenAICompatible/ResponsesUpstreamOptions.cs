namespace AgenticGateway.Providers.OpenAICompatible;

public sealed class ResponsesUpstreamOptions
{
    public const string SectionName = "ResponsesUpstream";

    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public Dictionary<string, string> ModelAliases { get; set; } = new(StringComparer.Ordinal);
}
