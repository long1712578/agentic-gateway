# Agentic Gateway

Agentic Gateway is a local .NET service for coding agents. It provides an OpenAI Responses-compatible model endpoint and shared project memory over MCP. The initial deployment is one ASP.NET Core process with SQLite, packaged as a Docker container.

## Architecture

The solution is a modular monolith with ports and adapters:

- **Host** composes the application and owns HTTP/MCP endpoints, authentication, and middleware.
- **Core** contains routing contracts and memory use cases, independent of SQLite and ASP.NET.
- **Protocols** contains protocol-specific request/response logic.
- **Providers** adapts configured model providers behind Core contracts.
- **Infrastructure** implements persistence and external technical services.

Requests enter through Host and depend inward on Core interfaces. SQLite and provider HTTP implementations stay behind those ports. This keeps local deployment simple while preserving seams for later provider, storage, and transport changes.

## Run locally

Requires the .NET 10 SDK. For Visual Studio/Development, keep local keys in .NET User Secrets instead of the tracked `appsettings.json`:

```powershell
$inferenceKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$memoryKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "Gateway:ApiKey" $inferenceKey --project src/AgenticGateway.Host
dotnet user-secrets set "Gateway:MemoryApiKey" $memoryKey --project src/AgenticGateway.Host
dotnet run --project src/AgenticGateway.Host
```

The included `http` launch profile sets `ASPNETCORE_ENVIRONMENT=Development`, so User Secrets are loaded by the default host configuration. The profile listens on `http://localhost:5096`. `/health/live` and `/health/ready` report process and SQLite readiness.

Configure one Responses-compatible upstream if you want to use `/v1/responses`:

```powershell
$env:AGENTIC_GATEWAY_OPENAI_BASE_URL = 'https://api.openai.com/v1'
$env:AGENTIC_GATEWAY_OPENAI_API_KEY = 'set-in-your-local-secret-store'
$env:AGENTIC_GATEWAY_DEFAULT_UPSTREAM_MODEL = 'your-model-id'
```

The gateway exposes the client alias `coding-default`; it sends the configured upstream model ID. Upstream credentials never come from inference request bodies.

## Docker

Copy `.env.example` to `.env`, set gateway/MCP keys and upstream credentials, then run:

```powershell
docker compose --env-file .env -f deploy/compose.yaml up --build
```

The container publishes only `127.0.0.1:8580` and stores SQLite under the persistent `agentic-gateway-data` volume.

## Endpoints

- `GET /health/live`, `GET /health/ready`
- `GET /v1/models`, `POST /v1/responses` (Responses-native pass-through)
- `POST /mcp` (Streamable HTTP: `memory_remember`, `memory_recall`, `memory_dream`)

All application endpoints require Bearer authentication. Use `AGENTIC_GATEWAY_API_KEY` for `/v1/*`; use `AGENTIC_GATEWAY_MCP_API_KEY` for `/mcp`.

Client connection examples and shared-memory instructions: [docs/clients/MEMORY.md](docs/clients/MEMORY.md). Architecture and implementation plan: [docs/README.md](docs/README.md).

Implementation coverage and known gaps: [docs/IMPLEMENTATION_STATUS_VI.md](docs/IMPLEMENTATION_STATUS_VI.md).
