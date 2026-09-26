using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgenticGateway.Core.Memory;
using ModelContextProtocol.Server;

namespace AgenticGateway.Host.Mcp;

[McpServerToolType]
public sealed class MemoryTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [McpServerTool(Name = "memory_remember"), Description("Save a project fact, decision, lesson, preference, observation, or handoff for other coding agents to recall.")]
    public static async Task<string> Remember(
        [Description("Stable logical project key, shared by all agent clients for this project.")] string projectId,
        [Description("One of: Preference, Fact, Decision, Lesson, Handoff, Observation.")] string kind,
        [Description("A concise, durable statement. Do not include credentials, full prompts, or tool output.")] string content,
        [Description("Client that produced this memory, such as codex, claude-code, kiro, or copilot.")] string sourceAgent,
        MemoryService memory,
        CancellationToken cancellationToken,
        [Description("Optional client session identifier.")] string? sourceSession = null,
        [Description("Optional file path, commit, or documentation reference.")] string? sourceReference = null,
        [Description("Optional stable key that makes retries safe.")] string? idempotencyKey = null)
    {
        if (!Enum.TryParse<MemoryKind>(kind, ignoreCase: true, out var parsedKind))
        {
            throw new ArgumentException("kind must be Preference, Fact, Decision, Lesson, Handoff, or Observation.", nameof(kind));
        }

        var entry = await memory.RememberAsync(
            new RememberMemory(projectId, parsedKind, content, sourceAgent, sourceSession, sourceReference, idempotencyKey),
            cancellationToken);
        return JsonSerializer.Serialize(entry, JsonOptions);
    }

    [McpServerTool(Name = "memory_recall"), Description("Search the shared memory of one project and return matching items with provenance.")]
    public static async Task<string> Recall(
        [Description("Stable logical project key.")] string projectId,
        [Description("A short natural language query about a decision, project fact, previous lesson, or handoff.")] string query,
        MemoryService memory,
        CancellationToken cancellationToken,
        [Description("Number of results, from 1 to 8.")] int limit = 5)
    {
        var results = await memory.RecallAsync(projectId, query, limit, cancellationToken);
        return JsonSerializer.Serialize(results, JsonOptions);
    }

    [McpServerTool(Name = "memory_dream"), Description("Consolidate this project's saved memories into a sourced summary using the configured model. This sends selected memory records to that provider and may incur usage; call only when the user asks to consolidate or at an agreed handoff.")]
    public static async Task<string> Dream(
        [Description("Stable logical project key.")] string projectId,
        DreamingService dreaming,
        CancellationToken cancellationToken) =>
        JsonSerializer.Serialize(await dreaming.DreamAsync(projectId, cancellationToken), JsonOptions);
}
