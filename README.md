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

Requires the .NET 10 SDK. For Visual Studio/Development, keep local keys in .NET User Secrets instead of putting them in `appsettings.json`:

```powershell
$inferenceKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$memoryKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet user-secrets set "Gateway:ApiKey" $inferenceKey --project src/AgenticGateway.Host
dotnet user-secrets set "Gateway:MemoryApiKey" $memoryKey --project src/AgenticGateway.Host
dotnet run --project src/AgenticGateway.Host
```

The included `http` launch profile sets `ASPNETCORE_ENVIRONMENT=Development`, so User Secrets are loaded by the default host configuration. The profile listens on `http://localhost:5096`. Open `http://localhost:5096/admin` for the local dashboard. On first start, the gateway creates a separate admin login key under `src/AgenticGateway.Host/bin/Debug/net10.0/data/admin.key`; the login page shows its exact path. `/health/live` and `/health/ready` report process and SQLite readiness.

If you also have `appsettings.Development.local.json`, remove `Gateway:ApiKey` and `Gateway:MemoryApiKey` from that file when using User Secrets. Even empty values in the local file override the secrets and prevent startup.

Configure one Responses-compatible upstream if you want to use `/v1/responses`:

```powershell
$env:AGENTIC_GATEWAY_OPENAI_BASE_URL = 'https://api.openai.com/v1'
$env:AGENTIC_GATEWAY_OPENAI_API_KEY = 'set-in-your-local-secret-store'
$env:AGENTIC_GATEWAY_DEFAULT_UPSTREAM_MODEL = 'your-model-id'
```

The gateway exposes the client alias `coding-default`; it sends the configured upstream model ID. Upstream credentials never come from inference request bodies.

You can instead enter the upstream URL, API key, and model ID at **Admin → Upstream & model**. The UI writes them to the local data directory, outside Git. An upstream variable supplied through the environment takes priority and makes the UI form read-only. The stored upstream key is not encrypted; protect the local directory/Docker volume and its backups. The admin login key is separate from the inference and MCP keys.

## Codex ChatGPT account proxy (local preview)

Codex CLI/IDE can also use the gateway with its existing ChatGPT sign-in, without OpenAI API credits. This is a separate streaming-only endpoint and does not use the API key configured on **Upstream & model**. Run `codex login` with ChatGPT and confirm `codex login status`, then copy the **Model qua tài khoản Codex Plus** snippet from **Admin → Kết nối client** into your **user-level** `~/.codex/config.toml`. Set `AGENTIC_GATEWAY_API_KEY` in the Codex process environment to the gateway inference key. Use a model available to your Codex account. Keep the MCP snippet independently if you want shared memory.

The client sends its ChatGPT session to the gateway via `Authorization` and a separate gateway key via `X-Agentic-Gateway-Key`. The gateway never reads, imports, writes, or refreshes `auth.json`; it forwards the session only to the fixed HTTPS Codex backend with redirects disabled. The gateway key is never forwarded. The gateway is local-only, and malformed sessions are rejected before any upstream request. Never paste `auth.json` or its tokens into the UI, Git, Docker volume, logs, or chat. ChatGPT account backend behavior can change independently of the public API. A live Codex CLI prompt passed; the shell tool was blocked by the test environment's policy, so a complete tool loop remains unverified.

## Docker

Copy `.env.example` to `.env`, set gateway/MCP keys and upstream credentials, then run:

```powershell
docker compose --env-file .env -f deploy/compose.yaml up --build
```

The container publishes only `127.0.0.1:8580` and stores SQLite, the UI-managed upstream settings, and the admin login key under the persistent `agentic-gateway-data` volume. Open `http://127.0.0.1:8580/admin`. If you leave `AGENTIC_GATEWAY_ADMIN_KEY` blank, read the generated key from `/app/data/admin.key` inside the container. Docker build context excludes all local `appsettings*.json` files so local secrets cannot be baked into the image.

## Endpoints

- `GET /health/live`, `GET /health/ready`
- `GET /admin` (local dashboard with a separate admin login)
- `GET /v1/models`, `POST /v1/responses` (Responses-native pass-through)
- `GET /codex/v1/models`, `POST /codex/v1/responses` (Codex ChatGPT account; Responses streaming only)
- `POST /mcp` (Streamable HTTP: `memory_remember`, `memory_recall`, `memory_dream`)

All application endpoints require authentication. Use `AGENTIC_GATEWAY_API_KEY` as the Bearer token for `/v1/*`, or as `X-Agentic-Gateway-Key` alongside the Codex session Bearer token for `/codex/v1/*`. Use `AGENTIC_GATEWAY_MCP_API_KEY` as the Bearer token for `/mcp`.

Client connection examples and shared-memory instructions: [docs/clients/MEMORY.md](docs/clients/MEMORY.md). Architecture and implementation plan: [docs/README.md](docs/README.md).

Step-by-step UI setup and client workflow: [docs/UI_WORKFLOW_VI.md](docs/UI_WORKFLOW_VI.md).

Implementation coverage and known gaps: [docs/IMPLEMENTATION_STATUS_VI.md](docs/IMPLEMENTATION_STATUS_VI.md).
