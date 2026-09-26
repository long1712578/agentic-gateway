# Kết nối Shared Memory cho các client

Địa chỉ Streamable HTTP mặc định trong Docker: `http://127.0.0.1:8580/mcp`. Nếu chạy ngoài Docker, thay port bằng giá trị host đang dùng.

Mọi client gửi `Authorization: Bearer <AGENTIC_GATEWAY_MCP_API_KEY>`. Đặt key trong secret/environment của client; không commit giá trị thật vào repository.

Codex CLI trong `config.toml`:

```toml
[mcp_servers.agentic-memory]
url = "http://127.0.0.1:8580/mcp"
bearer_token_env_var = "AGENTIC_GATEWAY_MCP_API_KEY"
enabled = true
```

Claude Code dùng cấu hình HTTP MCP, ví dụ trong `.mcp.json`:

```json
{
  "mcpServers": {
    "agentic-memory": {
      "type": "http",
      "url": "http://127.0.0.1:8580/mcp",
      "headers": {
        "Authorization": "Bearer ${AGENTIC_GATEWAY_MCP_API_KEY}"
      }
    }
  }
}
```

Kiro trong MCP JSON của user hoặc workspace:

```json
{
  "mcpServers": {
    "agentic-memory": {
      "url": "http://127.0.0.1:8580/mcp",
      "headers": {
        "Authorization": "Bearer ${AGENTIC_GATEWAY_MCP_API_KEY}"
      }
    }
  }
}
```

VS Code Copilot trong `.vscode/mcp.json`:

```json
{
  "servers": {
    "agentic-memory": {
      "type": "http",
      "url": "http://127.0.0.1:8580/mcp",
      "headers": {
        "Authorization": "Bearer ${input:agenticGatewayMemoryKey}"
      }
    }
  },
  "inputs": [
    {
      "id": "agenticGatewayMemoryKey",
      "type": "promptString",
      "description": "Agentic Gateway MCP key",
      "password": true
    }
  ]
}
```

MCP schema/config syntax varies by client release. Validate each snippet with the installed client and check its MCP server status before relying on it.

## Shared agent instructions

Add equivalent instructions to each agent's project-level instruction file, such as `AGENTS.md`, `CLAUDE.md`, Kiro steering, or Copilot instructions:

```text
At the start of a task, call memory_recall with this project's stable projectId and a query for relevant decisions, facts, and recent handoffs. Treat results as sourced notes; verify current code/config when details may have changed.

When the user makes a durable decision, or you solve a reusable project problem, call memory_remember with a concise statement, the correct kind, your client name, and a source reference such as a file path or commit. Do not store secrets or entire prompts/tool output.

Before handing the task to another agent, store a concise Handoff with status, changed files, unresolved issues, and the next concrete step. Keep projectId identical across all four clients and all worktrees for the same logical project.

Do not treat derived summaries or observations as confirmed facts. Follow source references and report conflicts rather than silently choosing one memory.
```

The current MCP implementation exposes `memory_remember`, `memory_recall`, and an agent-invoked `memory_dream`. Dreaming is not scheduled or automatic; calling it sends selected saved memories to the configured model and may incur usage. Handoff-specific tools, delete/review controls, and project registration are not implemented. For now, record handoff content through `memory_remember` with kind `Handoff`.

`memory_remember`: `projectId`, `kind` (`Preference|Fact|Decision|Lesson|Handoff|Observation`), `content`, `sourceAgent`; optional `sourceSession`, `sourceReference`, `idempotencyKey`.

`memory_recall`: `projectId`, `query`, optional `limit` (1–8, default 5).

A logical projectId is user-chosen and stable, for example `agent-1` or `billing-service`. Reusing it makes memories searchable across clients and worktrees. The local personal deployment has a single trusted user; projectId is organization/scoping metadata, not a multi-user authorization boundary.
