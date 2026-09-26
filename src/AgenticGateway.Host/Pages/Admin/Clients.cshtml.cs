using System.Text;
using AgenticGateway.Host.Security;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AgenticGateway.Host.Pages.Admin;

public sealed class ClientsModel(GatewayApiKeys keys) : PageModel
{
    public string RootUrl => $"{Request.Scheme}://{Request.Host}";
    public string InferenceKey => Encoding.UTF8.GetString(keys.Inference);
    public string MemoryKey => Encoding.UTF8.GetString(keys.Memory);
    public string ProjectId { get; private set; } = "agent-1";

    public void OnGet(string? projectId)
    {
        if (!string.IsNullOrWhiteSpace(projectId) && projectId.Length <= 120) ProjectId = projectId.Trim();
    }

    public string CodexInference => $"model = \"coding-default\"\nmodel_provider = \"agentic_gateway\"\n\n[model_providers.agentic_gateway]\nname = \"Agentic Gateway\"\nbase_url = \"{RootUrl}/v1\"\nenv_key = \"AGENTIC_GATEWAY_API_KEY\"\nwire_api = \"responses\"";
    public string CodexPlusInference => $"model = \"gpt-5.5\"\nmodel_provider = \"agentic_gateway_codex\"\n\n[model_providers.agentic_gateway_codex]\nname = \"Agentic Gateway · Codex account\"\nbase_url = \"{RootUrl}/codex/v1\"\nwire_api = \"responses\"\nsupports_websockets = false\nrequires_openai_auth = true\nenv_http_headers = {{ \"X-Agentic-Gateway-Key\" = \"AGENTIC_GATEWAY_API_KEY\" }}";
    public string CodexMemory => $"[mcp_servers.agentic_memory]\nurl = \"{RootUrl}/mcp\"\nbearer_token_env_var = \"AGENTIC_GATEWAY_MCP_API_KEY\"\nenabled = true";
    public string ClaudeMemory => "{\n  \"mcpServers\": {\n    \"agentic-memory\": {\n      \"type\": \"http\",\n      \"url\": \"" + RootUrl + "/mcp\",\n      \"headers\": { \"Authorization\": \"Bearer ${AGENTIC_GATEWAY_MCP_API_KEY}\" }\n    }\n  }\n}";
    public string KiroMemory => "{\n  \"mcpServers\": {\n    \"agentic-memory\": {\n      \"url\": \"" + RootUrl + "/mcp\",\n      \"headers\": { \"Authorization\": \"Bearer ${AGENTIC_GATEWAY_MCP_API_KEY}\" }\n    }\n  }\n}";
    public string CopilotMemory => "{\n  \"servers\": {\n    \"agentic-memory\": {\n      \"type\": \"http\",\n      \"url\": \"" + RootUrl + "/mcp\",\n      \"headers\": { \"Authorization\": \"Bearer ${input:agenticGatewayMemoryKey}\" }\n    }\n  },\n  \"inputs\": [{ \"id\": \"agenticGatewayMemoryKey\", \"type\": \"promptString\", \"description\": \"Gateway MCP key\", \"password\": true }]\n}";
    public string SharedInstruction => $"At task start, call memory_recall with projectId '{ProjectId}' and a query about relevant decisions and handoffs. When you learn a durable fact, decision or lesson, call memory_remember with the same projectId, a concise statement, your client name and a source reference. Do not store secrets or full transcripts. Before handing work to another agent, save a Handoff memory with next steps.";
}
