# Khảo sát AGENT-1 và đánh giá khả năng tái sử dụng

Ngày: **2026-09-26**. Phương pháp: đọc tài liệu, manifest, mã nguồn ở các luồng quan trọng, tên/nội dung một số regression test và đối chiếu tài liệu chính thức hiện hành. **Chưa chạy ứng dụng, test suite hay request dùng tài khoản thật.**

Phạm vi sản phẩm đã được người dùng xác nhận: **ưu tiên các agent làm client; chạy local cá nhân bằng Docker**. Những nhận xét về team/tenant/scale trong khảo sát là bài học cho mở rộng, không phải yêu cầu bắt buộc của MVP. Kế hoạch cuối chọn SQLite và một container .NET.

## 1. Phạm vi và mức độ chắc chắn

Đã kiểm kê cả 9 thư mục cấp cao. Không có `.codegraph/` ở root hoặc root của các project, vì vậy không tạo index và không dùng CodeGraph.

Không tuyên bố đã đọc từng dòng của toàn bộ workspace: có khoảng **10.783 file** theo phép đếm `rg --files --hidden`, bỏ `.git`, `node_modules`, `n8n_data`. Đây là số file được phát hiện, không phải số file đã đọc; vẫn có asset, fixture, generated code và vendor code. Khảo sát tập trung sâu vào các đường đi ảnh hưởng tới thiết kế proxy. Các package phụ của OMP được kiểm kê chức năng, không audit toàn bộ implementation.

Hai tài liệu được yêu cầu đã đọc toàn bộ:

- [README-omp.md](../../documents-research/README-omp.md), 701 dòng.
- [IMPLEMENTATION_GUIDE_VI.md](../../documents-research/IMPLEMENTATION_GUIDE_VI.md), 276 dòng.

Đã xem [research.png](../../documents-research/research.png): mô tả egress HTTP/SOCKS5 từ Kiro gateway và biến môi trường proxy. Với [dreamming-memory.pdf](../../documents-research/dreamming-memory.pdf), mới kiểm tra metadata/cấu trúc, chưa đọc được đầy đủ nội dung 7 trang bằng công cụ hiện có; không dùng PDF làm căn cứ cho kết luận về memory. Không đọc database, credential hoặc lịch sử thực thi trong `N8N/n8n_data`.

## 2. Bản đồ project

| Project | Snapshot local | Stack / business | Giá trị cho gateway |
|---|---|---|---|
| `9router` | `39e36d3d`, 1.616 file | Next.js/React + engine JS `open-sse`; quản trị nhiều provider/account, routing, quota, CLI setup | Registry, executor, auth lifecycle, account fallback và trải nghiệm admin |
| `claude-code-proxy` | `1e30e30`, 184 file | Rust/Axum/Tokio; đưa Claude Code tới Codex/Kimi/Grok/OpenCode/Cursor | Nguồn trọng tâm cho Codex live stream, native Responses, continuation và reasoning replay |
| `free-claude-code` | `8bb45098`, 867 file | Python/FastAPI; kết nối coding client với provider, launcher và UI quản trị | Ports/use cases, runtime lease, metadata, client integration, Responses output ledger |
| `kiro-gateway` | `a5292ca`, 96 file | Python/FastAPI; OpenAI Chat + Anthropic Messages tới Kiro | Chia converter/streaming core, model resolver, account circuit breaker, phân loại lỗi |
| `kiro-reverse-api` | `65dbb6b`, 160 file | Go 1.25, SQLite; Kiro account pool, ba API, web admin | Kiro binary EventStream, CRC, stream integrity, quota reservation, affinity, admin/backup |
| `oh-my-pi` | `7853b4e499`, 7.855 file | Bun/TypeScript + Rust, một số Python; coding agent và hệ sinh thái tools | Provider/catalog/types/auth, native Codex, conformance; runtime tùy chọn ở giai đoạn sau |
| `N8N` | Compose + runtime data | Workflow automation | Notification và workflow ngoài đường inference |
| `documents-research` | 4 file | Guide công ty, README OMP, sơ đồ và PDF | Business intent, bài học vận hành và hướng nghiên cứu |
| `agentic-gateway` | Trống trước khảo sát | Project .NET dự kiến | Không có kiến trúc/code cũ cần giữ tương thích |

Sáu Git checkout trên có working tree sạch khi kiểm tra. Không thay đổi Git config toàn cục; dùng `safe.directory` chỉ trong lệnh đọc của từng repository.

## 3. Kiro Reverse API: guide công ty và source local khác nhau

Guide mô tả bản đã bổ sung `ProviderType`, `codex/<model>`, `copilot/<model>`, provider pool riêng và port `8578`. Nhưng checkout tại đây:

- Không có `proxy/provider_routing.go`, `provider_upstream.go`, `provider_claude.go`, `provider_admin.go` được guide liệt kê.
- Không tìm thấy `resolveProviderModel` hay `ProviderType` tương ứng trong source Go.
- `config/config.go` mặc định port **8080**; Compose cũng map **8080:8080**.
- `/v1/responses` trong `proxy/responses_api.go` đi qua `prepareResponsesRequest`, `OpenAIToKiro` và Kiro handlers.
- Có `/v1/codex/models`, nhưng endpoint cung cấp catalog cho Codex client không chứng minh có Codex upstream.

**Kết luận:** guide là mô tả bản công ty/nhánh khác, có giá trị về yêu cầu và bài học. Không thể ghi nhận phần Codex/Copilot của guide là implementation đang có ở workspace. Chưa có source bản công ty thì không thể port hoặc kiểm thử parity của chính bản đó.

Luồng local đã kiểm tra:

```text
ServeHTTP
  -> authenticateFor...Request
  -> Messages / Chat / Responses handler
  -> translator + model/reasoning mapping
  -> reserveApiKeyUsage
  -> account pool + affinity + failover
  -> Kiro upstream / AWS EventStream
  -> protocol-specific output + usage/logging
```

Các bài học nên dùng:

1. Tách lỗi payload, lỗi account, lỗi endpoint và lỗi toàn provider trước khi quyết định retry.
2. Tách giới hạn account/provider với giới hạn API key của gateway.
3. `proxy/stream_integrity.go` phân biệt stream hoàn tất và EOF bị cắt; không suy ra thành công chỉ vì TCP đóng sạch.
4. Affinity có TTL/capacity, bind sau khi request thành công, giúp giữ prompt cache.
5. Binary framing cần kiểm tra kích thước và checksum; không xử lý như SSE text thông thường.

Các điểm cần thiết kế lại cho bản dùng chung:

- `responseState` và SQL trong `proxy/responses_persist.go` lookup bằng response ID, không có owner trong model này; get/delete handlers cũng không nhận owner để truy vấn. Khi bổ sung stored-state module, gateway mới cần binding theo client/session ngay từ thiết kế module; MVP local mặc định stateless. Đây không phải kết luận từ một security test đã chạy.
- Affinity local dựa conversation ID sinh từ nội dung; gateway nhiều người dùng cần namespace theo identity xác thực để tránh va chạm giữa phiên có prompt giống nhau.
- Không mang nguyên global configuration/account state vào .NET; dùng immutable snapshot và quản lý resource lifetime.
- Guide ghi Claude → Codex đang buffer rồi phát SSE: lấy đây làm regression requirement cần khắc phục, chưa phải bằng chứng code local đang có bug đó.

Nguồn chính: [handler.go](../../kiro-reverse-api/proxy/handler.go), [responses_api.go](../../kiro-reverse-api/proxy/responses_api.go), [kiro.go](../../kiro-reverse-api/proxy/kiro.go), [session_affinity.go](../../kiro-reverse-api/proxy/session_affinity.go), [stream_integrity.go](../../kiro-reverse-api/proxy/stream_integrity.go), [responses_persist.go](../../kiro-reverse-api/proxy/responses_persist.go), [responses_test.go](../../kiro-reverse-api/proxy/responses_test.go).

## 4. Những gì học từ các proxy còn lại

### 4.1. 9router

Next route `/v1/responses` gọi `src/sse/handlers/chat.js`; engine dùng registry translator và executor theo provider. `translator/index.js` ưu tiên direct pair khi có, sau đó mới pivot qua OpenAI Chat format. `open-sse/AGENTS.md` tự nêu các nguy cơ mất thinking, image, tool ID và `is_error` khi đi hai bước.

`executors/codex.js` thể hiện các quirk riêng của Codex backend, khác OpenAI public API. `executors/github.js` có nhiều đường ra cho Copilot: Messages, Chat và Responses tùy model. Vì vậy **provider không đồng nghĩa một protocol duy nhất**.

`services/tokenRefresh/dedup.js` chống refresh trùng trong cùng process; `accountFallback.js` phân biệt lỗi request với lock account/model. Học ý tưởng nhưng bản nhiều replica cần thêm distributed ownership/fencing.

Không đưa mặc định vào MVP các thay đổi prompt, token compression, cloaking, model fallback ngầm hoặc tính năng media ngoài phạm vi coding. Không lấy các tỷ lệ tiết kiệm token trong README làm KPI đã được chứng minh cho project mới.

Nguồn: [translator/index.js](../../9router/open-sse/translator/index.js), [codex.js](../../9router/open-sse/executors/codex.js), [github.js](../../9router/open-sse/executors/github.js), [accountFallback.js](../../9router/open-sse/services/accountFallback.js), [dedup.js](../../9router/open-sse/services/tokenRefresh/dedup.js).

### 4.2. claude-code-proxy

Phần đáng học nhất là `LiveStreamTranslator`: theo dõi output index, item ID, text/tool/thinking block và terminal event. Không chỉ đổi tên event.

`continuation.rs` giữ response/socket/transcript theo `ConversationIdentity`, có giới hạn bộ nhớ và turn reservation. Test continuation có case cha/con/subagent xen kẽ, hai phiên dùng cùng agent ID, stale completion ghi đè phiên mới. Đây là tập tình huống rất sát Codex.

`reasoning_signature.rs` giữ opaque encrypted content để replay; dữ liệu này không phải text reasoning có thể chuyển tùy ý giữa provider. `auth/manager.rs` lock rồi đọc lại token trước khi refresh, tránh request đợi lock dùng token đã bị rotate.

Giới hạn cần ghi nhận: README/tài liệu nói listener không xác thực client và mặc định loopback; Responses passthrough opt-in, WebSocket ingress/stored Responses không phải tính năng đầy đủ của proxy. Không sao chép những giới hạn đó sang sản phẩm nội bộ mà không chủ động thiết kế.

Nguồn: [provider.rs](../../claude-code-proxy/src/provider.rs), [live_stream.rs](../../claude-code-proxy/src/providers/codex/translate/live_stream.rs), [continuation.rs](../../claude-code-proxy/src/providers/codex/continuation.rs), [native.rs](../../claude-code-proxy/src/providers/codex/native.rs), [compatibility-and-limitations.md](../../claude-code-proxy/docs/src/content/docs/reference/compatibility-and-limitations.md).

### 4.3. free-claude-code

`application/ports.py`, `routing.py`, `execution.py` là ví dụ tốt về tách use case khỏi HTTP/provider implementation. `RequestRuntimeLease` giữ một generation cấu hình/provider xuyên suốt response: phù hợp để hot reload không phá stream đang chạy.

`ResponsesOutputLedger` quản lý output slot và usage; dùng làm tham khảo invariant. `harnesses/codex_integration.py` xử lý cấu hình TOML có chủ đích, không ghi đè toàn file tùy tiện.

**Giới hạn đã kiểm chứng:** `api/handlers/responses.py` từ chối `stream=false`; endpoint chỉ hỗ trợ streaming. Kế hoạch gateway mới phải tự định nghĩa và kiểm tra cả stream/non-stream.

Copilot provider trong source có HTTP egress riêng và auth manager; không thể suy ra chỉ cần tham chiếu package `github-copilot-sdk` là tự có một OpenAI-compatible inference API.

Nguồn: [ports.py](../../free-claude-code/src/free_claude_code/application/ports.py), [routing.py](../../free-claude-code/src/free_claude_code/application/routing.py), [execution.py](../../free-claude-code/src/free_claude_code/application/execution.py), [responses.py](../../free-claude-code/src/free_claude_code/api/handlers/responses.py), [Copilot provider](../../free-claude-code/src/free_claude_code/providers/github_copilot/provider.py).

### 4.4. kiro-gateway

Tổ chức shared core + thin adapters: routes → converters → shared Kiro payload/HTTP → stream parser → API encoder. Có account sticky selection, circuit breaker, model cache và async refresh.

`account_errors.py` cho thấy HTTP status riêng lẻ chưa đủ: cùng 400 nhưng context overflow khác model không có trên account. Giữ nguyên nguyên tắc này, không port nguyên bảng xử lý 5xx của Python sang .NET.

Hướng dẫn dùng per-request `httpx.AsyncClient` của project là kinh nghiệm cho implementation Python đó. Với .NET, thiết kế handler/connection pooling và dispose response/stream theo lifecycle; không tạo một `HttpClient` mới cho mỗi token stream.

Nguồn: [ARCHITECTURE.md](../../kiro-gateway/docs/en/ARCHITECTURE.md), [account_manager.py](../../kiro-gateway/kiro/account_manager.py), [account_errors.py](../../kiro-gateway/kiro/account_errors.py), [parsers.py](../../kiro-gateway/kiro/parsers.py).

## 5. OMP có giúp ích không?

**Có, giá trị cao ở lớp provider và compatibility. Không phải framework .NET có thể nhúng trực tiếp, và không phải chỉ một reverse proxy.**

| Nhóm OMP | Vai trò | Cách dùng cho agentic-gateway |
|---|---|---|
| `packages/ai` | Transport/provider, OAuth, typed message/event, native Codex | Tham khảo contract, preserve metadata, lifecycle và edge case; viết adapter .NET |
| `packages/catalog` | Model discovery, identity, capability/compatibility, auth policies | Học tách provider/model/account capability; policy có version/provenance |
| `packages/agent` | Agent loop, tool execution, state | Chỉ dùng nếu sau này cung cấp dịch vụ chạy agent |
| `packages/coding-agent` | CLI/SDK, RPC, ACP, tools, sessions, subagents | Dùng như client E2E; có thể sidecar runtime sau này |
| `packages/mnemopi` | SQLite memory, remember/recall/sleep, MCP | Nghiên cứu dịch vụ memory riêng, không tự ghi toàn bộ prompt qua proxy |
| `packages/metaharness`, `typescript-edit-benchmark` | Benchmark/evaluation | Học cách đo chất lượng coding sau khi gateway hoạt động |
| `packages/stats` | Usage dashboard | Học presentation/metrics, không cần port toàn dashboard |
| `packages/snapcompact` | Bitmap context compression | Thử nghiệm độc lập, không đưa vào đường mặc định |
| `packages/tui`, `omptype`, `utils` | UI terminal, validation, tiện ích runtime | Thường thay bằng primitives của .NET; không port máy móc |
| `packages/natives`, Rust crates | Shell/PTY, AST/edit, search, PDF/audio, workspace isolation | Thuộc execution worker, không thuộc inference proxy |
| `browser-relay`, `collab-web`, `wire` | Browser control và collaboration protocol | Không cần cho MVP |
| `python/omp-rpc`, `python/robomp`, `infra` | SDK/automation và hạ tầng phụ trợ | Tham khảo ranh giới tiến trình khi mở rộng runtime, chưa audit chi tiết |

Các điểm cụ thể đã đối chiếu source:

- `ai/src/registry/types.ts`: provider definition gồm auth, refresh, request preparation; catalog discovery tách riêng.
- `catalog/src/provider-models/descriptor-types.ts`: dynamic model discovery có thể authoritative; không tự gán capability từ model cùng tên ở provider khác.
- `catalog/src/compat/rules/README.md`: tách model lineage khỏi deployment contract. .NET có thể dùng JSON + typed validation, chưa cần port compiler KDL.
- `ai/src/providers/openai-codex-responses.ts`: Codex transport/state phức tạp hơn public Chat Completions; có SSE/WebSocket, continuation và live steering.
- `ai/src/utils/event-stream.ts`: event stream có final result, lỗi và cancellation; .NET cần thêm backpressure/bounded buffering rõ ràng cho server nhiều request.
- `agent/README.md`: vòng tool execution thuộc agent, chứng minh việc đưa cả agent loop vào proxy sẽ mở rộng trách nhiệm đáng kể.

Ba cách tích hợp:

1. **Khuyến nghị:** lấy contract/bài học thiết kế và test scenario, xây .NET native; OMP gọi gateway qua custom provider.
2. Sidecar Bun dùng `pi-ai`: nhanh để thử provider mới, đổi lại vận hành hai runtime và thêm một hop; chỉ giữ nếu spike chứng minh lợi ích.
3. OMP RPC/ACP worker để chạy job coding: sản phẩm khác, cần sandbox/workspace/approval/cancellation; triển khai sau bằng API job riêng.

Câu trả lời ngắn có thể báo cáo với sếp: **OMP giúp giảm rủi ro thiết kế provider, streaming và model capability; không thay thế gateway. Với yêu cầu mới dùng chung memory giữa agent, nghiên cứu thêm `pi-mnemopi` cùng `pi-ai` và `pi-catalog`; agent runtime vẫn là module mở rộng.**

Bổ sung sau khi người dùng làm rõ mục tiêu memory: đã đọc MCP server/tools và sleep path của mnemopi. Có shared memory tools nhưng server TypeScript local chỉ triển khai stdio; sleep path đã kiểm tra trả llm_used=0 và conflicts_resolved=0. Không gọi đây là HTTP dreaming service hoàn chỉnh có sẵn. [SHARED_MEMORY_VI.md](SHARED_MEMORY_VI.md) ghi nguồn, MCP support chính thức của bốn client và thiết kế .NET đề xuất. Shared memory được đưa vào scope chính, không phụ thuộc custom inference endpoint của Kiro.

## 6. N8N và tài liệu egress

`N8N/docker-compose.yml` hiện là cấu hình chạy n8n, không phải source một workflow engine của project này. `nodes/package.json` không có community-node dependency. Compose dùng image không pin và có cấu hình mật khẩu tĩnh; không lấy nguyên file làm deployment template cho bản dùng chung.

Ứng dụng hợp lý: webhook quota sắp hết, cần đăng nhập lại, báo cáo usage, ticket khi provider lỗi. Các workflow phải chạy bất đồng bộ sau inference qua outbox/webhook có retry; n8n bị dừng không được làm hỏng SSE.

`research.png` giúp nhận diện nhu cầu egress proxy. .NET cần `ProxyProfile` riêng theo provider/account, handler pool tương ứng, allowlist đích và tách gateway ingress proxy với proxy outbound. Không thay biến môi trường toàn process khi người dùng đổi một account.

## 7. Nguồn chính thức và điều được xác nhận

Các nguồn được mở ngày 2026-09-26; model/account thực tế vẫn cần test khi triển khai.

- [Codex configuration reference](https://learn.chatgpt.com/docs/config-file/config-reference): custom provider dùng `wire_api="responses"`; có `base_url`, auth và khả năng WebSocket. Điều này quyết định thứ tự ưu tiên protocol.
- [Codex advanced configuration](https://learn.chatgpt.com/docs/config-file/config-advanced): cấu hình custom endpoint và credential helper. Gateway key là credential client→gateway, tách với credential upstream.
- [OpenAI streaming responses](https://developers.openai.com/api/docs/guides/streaming-responses): Responses dùng semantic events; phải xử lý lifecycle và tool events chứ không chỉ nối text delta.
- [Claude gateway compatibility](https://code.claude.com/docs/en/llm-gateway-protocol): Messages, header version/beta, streaming sequence, count_tokens tùy chọn. [Gateway overview](https://code.claude.com/docs/en/llm-gateway) cũng nêu Anthropic không hỗ trợ việc route Claude Code tới model không phải Claude; bridge tới Codex là tương thích của sản phẩm mình, không gắn nhãn được Anthropic chứng nhận.
- [VS Code custom endpoints](https://code.visualstudio.com/docs/agent-customization/language-models): hỗ trợ Chat Completions, Responses, Messages. Không dùng setting `github.copilot.chat.customOAIModels` đã deprecated làm mẫu mới.
- [Copilot SDK BYOK](https://docs.github.com/en/copilot/how-tos/copilot-sdk/auth/byok): SDK có thể gọi custom provider; đây là chiều client, không tự chứng minh quyền/cách xuất subscription Copilot thành upstream API.
- [Kiro models](https://kiro.dev/docs/models/): có model offerings, nhưng nguồn đã đọc chưa xác nhận cấu hình custom base URL cho mọi Kiro client. Đánh dấu chiều Kiro client→gateway là cần spike.
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy): .NET 10 thuộc LTS. Máy hiện có SDK `10.0.303`; pin patch khi bắt đầu implementation.
- [YARP transforms](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/extensibility-transforms?view=aspnetcore-10.0): không có built-in body transforms. YARP không tự giải quyết protocol translation.

License metadata ở checkout: `9router`, `claude-code-proxy`, `kiro-reverse-api`, OMP ghi MIT; `kiro-gateway` và `free-claude-code` ghi AGPL. Backlog yêu cầu lập bảng nguồn/attribution trước khi port mã. Đây là ghi nhận metadata, không phải kết luận pháp lý cho sản phẩm công ty.

## 8. Những phần chưa xác nhận bằng runtime

- Codex/Kiro/Copilot account nào đang có entitlement, region, model và quota phù hợp.
- Source đúng bản công ty có trong guide.
- Phiên bản Codex CLI/IDE/App, Claude Code, Copilot/VS Code và Kiro cần chứng nhận.
- Kiro client có đường cấu hình gateway được hỗ trợ ở phiên bản sẽ dùng hay không.
- Claude upstream là API key, cloud provider hay subscription/OAuth.
- Ngân sách upstream và kết quả đo tải thực trên máy cá nhân; retention 7 ngày hiện là mặc định đề xuất. Phạm vi local cá nhân đã được xác nhận.
- Nội dung đầy đủ PDF memory; benchmark end-to-end; mọi test suite trong các repository.

Các phần này trở thành task khảo sát/acceptance của kế hoạch, không được trình bày như tính năng đã hoàn thành.
