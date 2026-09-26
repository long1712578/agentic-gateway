# Trạng thái implementation

**Ngày:** 2026-09-26. Scope hiện tại: lát cắt nền đầu tiên cho proxy Responses và Shared Memory/Dreaming. Đây chưa phải V1 hoàn chỉnh.

## Đã có trong source

- Solution .NET 10 modular monolith gồm Host, Core, Protocols, Providers, Infrastructure; reference chỉ hướng ra ngoài từ Host.
- Auth Bearer riêng cho inference (`AGENTIC_GATEWAY_API_KEY`) và MCP (`AGENTIC_GATEWAY_MCP_API_KEY`); thiếu key hoặc key ngắn hơn 32 bytes làm app từ chối khởi động.
- SQLite file store cho memory, EF migration/version history, FTS5 search tiếng Việt/Anh, expiration, idempotency, provenance và scope theo projectId.
- MCP Streamable HTTP `/mcp`: `memory_remember`, `memory_recall`, `memory_dream`.
- Dreaming chạy theo lệnh của agent. Chọn tối đa 20 source, cap input và output, timeout 30 giây, `store=false`; output được xác thực summary + source IDs trước khi ghi thành derived memory. Nguồn được liên kết transactionally để không chạy lặp trên cùng records. Mỗi dream dùng upstream alias trong `Dreaming:ModelAlias`, mặc định `coding-default`.
- OpenAI Responses-compatible `POST /v1/responses` stream native qua `ResponseHeadersRead`; request đổi duy nhất model alias đã khai báo. `GET /v1/models` liệt kê alias. Không có model/account mặc định.
- Codex ChatGPT account proxy riêng tại `GET /codex/v1/models` và `POST /codex/v1/responses`, Responses streaming-only. Codex client đăng nhập ChatGPT và gửi session trên từng request; gateway yêu cầu thêm key riêng, không import/lưu `auth.json`, không forward gateway key, chỉ forward tới host Codex HTTPS cố định và không theo redirect. Live Codex CLI prompt đã trả `OK` qua gateway.
- Local Docker compose bind 127.0.0.1, volume SQLite bền vững, non-root container và hướng dẫn MCP cho bốn client.
- UI quản trị Razor Pages tại `/admin`: đăng nhập cookie bằng admin key riêng; dashboard, cấu hình upstream/model, cấu hình client có nút copy, Shared Memory remember/recall/dream, chẩn đoán readiness. Upstream key do UI nhập được lưu vào thư mục data/volume, không trả lại về form. Biến môi trường upstream có ưu tiên cao hơn và khóa form tương ứng.

## Chưa implement

- Claude Messages và Chat Completions adapter/bridge.
- Tự động lịch dreaming; hiện chỉ chạy khi agent chủ động gọi `memory_dream`. Chưa bắt transcript/session ngầm.
- Handoff-specific CRUD/update/delete/review; project registry và generated agent instruction files.
- Gateway-owned OAuth/import/refresh account connectors cho nhiều client, usage dashboards, OpenTelemetry, stored Responses, WebSocket. UI hiện chưa có biểu đồ request/quota do chưa có telemetry tương ứng.
- Client live certification đầy đủ. Kiro MCP có config hướng dẫn nhưng chưa chạy client E2E; Codex prompt qua account proxy đã pass, nhưng shell tool bị policy của môi trường test chặn nên chưa xác nhận tool loop; Claude/Copilot còn cần profile/phiên bản thực tế.
- Docker image build và client E2E thực tế chưa chạy.

## Hiện trạng kiểm tra

- `dotnet restore AgenticGateway.slnx --configfile NuGet.Config` hoàn tất sau khi cần quyền đọc NuGet configuration ngoài sandbox.
- `dotnet build AgenticGateway.slnx --configuration Release --no-restore`: pass, 0 warning và 0 error.
- Runtime smoke trên cổng riêng: login với antiforgery, năm trang Admin trả 200, form upstream lưu model, memory remember/recall qua SQLite FTS5.
- Contract check Codex account dùng fake HTTP handler: fixed destination, JWT account ID, body passthrough và malformed token bị chặn trước outbound. Runtime smoke: thiếu gateway key → 401, token Codex sai → 401, non-stream → 400, `/v1/models` cũ → 200.
- Live Codex CLI qua gateway: `GET /codex/v1/models` và `POST /codex/v1/responses` đều nhận 200 từ ChatGPT backend, prompt trả `OK`; không dùng OpenAI API key. Lần thử shell tool có request model thành công nhưng hệ thống chạy Codex chặn `pwsh` theo policy trước khi tool thực thi, nên chưa chứng nhận tool loop.

Các code file nằm dưới `src/`; không có thay đổi vào sáu repository tham khảo. Workspace `agentic-gateway` là Git repository; implementation hiện nằm trong working tree chưa commit.
