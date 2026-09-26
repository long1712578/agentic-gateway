# Trạng thái implementation

**Ngày:** 2026-09-26. Scope hiện tại: lát cắt nền đầu tiên cho proxy Responses và Shared Memory/Dreaming. Đây chưa phải V1 hoàn chỉnh.

## Đã có trong source

- Solution .NET 10 modular monolith gồm Host, Core, Protocols, Providers, Infrastructure; reference chỉ hướng ra ngoài từ Host.
- Auth Bearer riêng cho inference (`AGENTIC_GATEWAY_API_KEY`) và MCP (`AGENTIC_GATEWAY_MCP_API_KEY`); thiếu key hoặc key ngắn hơn 32 bytes làm app từ chối khởi động.
- SQLite file store cho memory, EF migration/version history, FTS5 search tiếng Việt/Anh, expiration, idempotency, provenance và scope theo projectId.
- MCP Streamable HTTP `/mcp`: `memory_remember`, `memory_recall`, `memory_dream`.
- Dreaming chạy theo lệnh của agent. Chọn tối đa 20 source, cap input và output, timeout 30 giây, `store=false`; output được xác thực summary + source IDs trước khi ghi thành derived memory. Nguồn được liên kết transactionally để không chạy lặp trên cùng records. Mỗi dream dùng upstream alias trong `Dreaming:ModelAlias`, mặc định `coding-default`.
- OpenAI Responses-compatible `POST /v1/responses` stream native qua `ResponseHeadersRead`; request đổi duy nhất model alias đã khai báo. `GET /v1/models` liệt kê alias. Không có model/account mặc định.
- Local Docker compose bind 127.0.0.1, volume SQLite bền vững, non-root container và hướng dẫn MCP cho bốn client.

## Chưa implement

- Claude Messages và Chat Completions adapter/bridge.
- Tự động lịch dreaming; hiện chỉ chạy khi agent chủ động gọi `memory_dream`. Chưa bắt transcript/session ngầm.
- Handoff-specific CRUD/update/delete/review; project registry và generated agent instruction files.
- UI quản lý, OAuth/account connectors, usage dashboards, OpenTelemetry, stored Responses, WebSocket.
- Client live certification. Kiro MCP có config hướng dẫn nhưng chưa chạy client E2E; Codex/Claude/Copilot cũng cần profile/phiên bản thực tế.
- Docker image build và runtime DB/FTS migration smoke chưa chạy.

## Hiện trạng kiểm tra

- `dotnet restore AgenticGateway.slnx --configfile NuGet.Config` hoàn tất sau khi cần quyền đọc NuGet configuration ngoài sandbox.
- `dotnet build AgenticGateway.slnx --no-restore`: pass, 0 warning và 0 error. Release build đang được chạy cho lần bàn giao này.
- Chưa có test project hoặc runtime test; chưa gọi upstream/model thật.

Các code file nằm dưới `src/`; không có thay đổi vào sáu repository tham khảo. Workspace `agentic-gateway` là Git repository; implementation hiện nằm trong working tree chưa commit.
