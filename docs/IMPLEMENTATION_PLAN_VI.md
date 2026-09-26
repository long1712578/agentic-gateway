# Kế hoạch triển khai Agentic Gateway (.NET, local-first)

> Kế hoạch baseline để theo dõi phần còn lại. Phạm vi đã được khởi động; xem [IMPLEMENTATION_STATUS_VI.md](IMPLEMENTATION_STATUS_VI.md) để biết lát cắt đã có và giới hạn kiểm chứng.

**Goal:** Codex và các coding client gọi một gateway local để dùng model từ API, server local hoặc proxy đang có; ưu tiên bảo toàn Responses, streaming và tool loop.

**Architecture:** Modular monolith .NET; ingress protocol → routing/capability → connection/provider adapter → native relay hoặc translated events. SQLite lưu cấu hình và metadata, một container Docker, không chạy agent/tool trong gateway.

**Tech stack:** .NET 10, ASP.NET Core, System.Text.Json/Pipelines, HttpClientFactory, EF Core SQLite, Razor Pages, ILogger/Activity/Meter, xUnit/WebApplicationFactory, Docker Compose.

**Spec:** [ARCHITECTURE_VI.md](ARCHITECTURE_VI.md). **Research:** [RESEARCH_VI.md](RESEARCH_VI.md).
**Scope bổ sung:** [SHARED_MEMORY_VI.md](SHARED_MEMORY_VI.md), MEM-1–MEM-5: memory dùng chung qua MCP và dreaming nền, nằm giữa M1 và V1. Mức capture chưa chốt; selective writes là đề xuất hiện tại.

**Trạng thái:** Implementation đang tiến hành. Các checkbox theo mốc bên dưới chưa được cập nhật đầy đủ; không xem chúng là trạng thái hiện tại của source.

## 1. Phạm vi đã chốt và nguyên tắc thực hiện

- Các agent chủ yếu là **client**, ưu tiên Codex. Codex client không bắt buộc dùng Codex subscription backend.
- Deployment đầu: **local cá nhân**, một .NET process, một SQLite database, Docker; không PostgreSQL, Redis, Kubernetes hay tenant/RBAC doanh nghiệp.
- MVP dùng một Responses-compatible upstream đã chọn. Có thể là API hoặc một proxy trong workspace đã được người dùng chạy; không cần port tất cả provider ngay.
- V1 thêm Messages/Chat, bridge có giới hạn và hướng dẫn client. Account connectors Codex/Kiro/Copilot là module tùy chọn theo nhu cầu.
- Gateway không thực thi filesystem/shell của coding-agent. Client giữ agent loop, approval và workspace. Shared Memory module cùng host phục vụ MCP tools remember/recall/handoff và worker dreaming riêng.
- Không tự sửa client config, import credential từ home hay gọi model thật trong quá trình lập plan.
- Không sửa/commit/push các repository tham khảo. Không sao chép implementation trước khi ghi nguồn/attribution và rà soát license liên quan.
- Test offline là mặc định; live acceptance chỉ chạy với connection và credential người dùng cấu hình cho việc kiểm thử.
- Pin SDK/dependency/image/client version khi implement. Không dùng model ID trong guide cũ làm default.
- Native contract được ưu tiên; translation không hỗ trợ feature phải trả lỗi rõ, không âm thầm xóa dữ liệu.
- Không log prompt, tool output hoặc credential mặc định. Không tuyên bố full compatibility chỉ từ một request trả text.

Các đường dẫn trong tasks là mục tiêu kiến trúc, không nhất thiết trùng với lát cắt source ban đầu. Viết tắt `Host/`, `Core/`, `Protocols/`, `Providers/`, `Infrastructure/` tương ứng `src/AgenticGateway.<tên>/`; `UnitTests/`, `ContractTests/`, `IntegrationTests/` tương ứng `tests/AgenticGateway.<tên>/`. Khi tiếp tục một task, cập nhật acceptance và trạng thái trong [IMPLEMENTATION_STATUS_VI.md](IMPLEMENTATION_STATUS_VI.md).

## 2. Mốc bàn giao và dự kiến thời gian

| Mốc | Tasks | Kết quả có thể dùng | Gate |
|---|---|---|---|
| M0: contract | 0 | Ma trận client/upstream và phạm vi feature | Một upstream ứng viên; gaps ghi rõ |
| M1: Codex local alpha | 1–6 | Responses native, SQLite, admin tối thiểu, Docker, Codex CLI | Tool loop/cancel/restart trên profile đã test |
| M-memory: dùng chung kiến thức | MEM-1–MEM-5 trong spec memory | MCP, project memory, handoff, dreaming cơ bản | Agent ghi → agent khác recall; scope/provenance/restart đúng |
| M2: local V1 | 7–11 | Messages/Chat, client setup, diagnostics, backup | Contract, client matrix và load/soak đạt |
| Tùy nhu cầu | A–F | Account connector, stored state, WS, automation | Spec và gate riêng; không chặn M1 |

Ước lượng cho **một senior .NET làm tập trung**, chưa tính thời gian chờ account hoặc thay đổi upstream: M1 khoảng **10–15 ngày làm việc**; phần inference V1 khoảng **25–40 ngày** tổng cộng. Shared Memory/dreaming bổ sung **8–14 ngày**, đưa V1 đầy đủ scope mới lên khoảng **33–54 ngày**. Mỗi subscription connector thường cần thêm **4–10 ngày**. Đây là ước lượng planning, cập nhật sau Task 0 và lựa chọn mức capture, không phải cam kết lịch.

Không biến Kiro client chưa xác minh hoặc thiếu subscription account thành blocker cho core local. Tính năng chưa qua live test giữ nhãn Unverified.

## 3. Phụ thuộc và hợp đồng chung

```text
0 -> 1 -> 2 -> 3 -> 4 -> 5 -> 6  (Codex local alpha)
                              -> MEM-1 -> MEM-2 -> MEM-3 -> MEM-4 -> MEM-5
                              -> 7 -> 8 -> 9 -> 10 -> 11 (local V1, gồm memory gate)
Sau M1/V1: chọn A/B/C theo upstream thực sự cần; D/E/F có spec riêng.
```

Task 1 tạo Compose phát triển tối thiểu; Task 11 hoàn thiện release/backup/soak. Task 2 tạo trang cấu hình tối thiểu; Task 9 mở rộng diagnostics. Không để Docker hoặc client acceptance tới cuối mới phát hiện sai network/contract.
Ưu tiên làm mốc Shared Memory ngay sau M1 trước các bridge mở rộng. Store/MCP memory chỉ phụ thuộc nền tảng Task 1–2 và có thể kiểm thử độc lập nếu thiếu upstream credential; chỉ phần LLM dreaming phụ thuộc connection. Task 11 phải backup/restore cả gateway.db và memory.db; memory acceptance thuộc release gate V1.

**Contract theo architecture:**

| Interface | Input → output |
|---|---|
| IIngressReader.ReadAsync | Stream, ProtocolKind, CancellationToken → ValueTask<RequestEnvelope> |
| IModelRouter.ResolveAsync | RequestEnvelope, CancellationToken → ValueTask<RouteDecision> |
| IConnectionManager.AcquireAsync | RouteDecision, CancellationToken → ValueTask<ConnectionLease> |
| IProviderAdapter.OpenAsync | ProviderInvocation, CredentialSnapshot, CancellationToken → ValueTask<ProviderExchange> |
| IUpstreamDecoder.DecodeAsync | ProviderExchange, CancellationToken → IAsyncEnumerable<GatewayEvent> |
| IProtocolWriter.WriteAsync | Stream, events, ResponseContext, CancellationToken → ValueTask |

Native relay bảo toàn unknown semantic fields/events được policy cho phép; normalized events phục vụ translation/usage. Một body chỉ được đọc một lần. Request giữ config/credential snapshot suốt vòng đời. Không giữ SQLite transaction trong lúc chờ upstream.

**Mặc định đồng nhất với spec:** request sau giải nén 16 MiB; event 8 MiB; tool arguments 1 MiB; non-stream accumulation 16 MiB; relay buffer ngoài event đang parse 256 KiB. Connect/first-event/idle 10s/90s/90s; total 20 phút. Concurrency 8/instance, 4/connection; chờ slot 2s rồi 429. Tối đa 2 upstream attempts nếu replay-safe; OAuth retry tối đa 1 nằm trong budget 2. Catalog TTL 15 phút/stale 24 giờ; shutdown drain 30s. Metadata retention 7 ngày; payload capture mặc định tắt.

**Review focus:**

1. UTF-8/SSE phân mảnh, interleaved tools, EOF thiếu terminal — Tasks 3/4/7/8.
2. Unknown delivery, hosted-tool side effect và retry lặp — Task 5.
3. Native/translated path làm mất custom tool, opaque reasoning, schema hoặc history — Tasks 4/6/7/8.
4. SQLite bị khóa, mất master key, log lộ secret — Tasks 2/9/11.
5. Reload/cancel/disconnect/shutdown để lại connection lease hoặc task sống — Tasks 5/11.

## Task 0 — Khóa compatibility profile và chọn upstream đầu tiên

**Dự kiến:** 1–2 ngày. **Dependency:** `start implement`.

**Files:** `docs/compatibility/CLIENT_MATRIX.md`, `UPSTREAM_MATRIX.md`, `SOURCE_PROVENANCE.md`; `tests/AgenticGateway.ContractTests/Fixtures/`.

- [ ] Ghi exact version/surface của Codex CLI, IDE/App, Claude Code, VS Code Copilot; không suy pass giữa các surface.
- [ ] Chọn một Responses-compatible upstream thực tế: URL, auth, model, stream/non-stream, tools, compact và state capabilities. Mock không thay cho live chứng nhận.
- [ ] Tách model alias công khai khỏi upstream model. Xác định `coding-default` bằng cấu hình, không hard-code model đang thịnh hành.
- [ ] Tạo fixtures tổng hợp/sanitized: text tiếng Việt, parallel tools, function/custom tool, tool result/error, opaque reasoning, image, schema, usage, error event, premature EOF.
- [ ] Spike Kiro custom endpoint theo version đang dùng. Nếu không xác minh được, giữ Unverified và tiếp tục MVP; không gọi Kiro-upstream support là Kiro-client support.
- [ ] Đánh dấu source công ty chưa có: guide là requirement/reference, không claim port parity.
- [ ] Lập provenance theo file/package nếu định port code; ưu tiên thiết kế .NET độc lập dựa trên protocol và invariant.

**Acceptance:** matrix có các trạng thái Verified/Unsupported/Unverified với version, protocol, evidence, limitation. Credentials không xuất hiện trong fixtures. Account thiếu chỉ chặn live acceptance tương ứng.

## Task 1 — Solution, host, local authentication và Docker baseline

**Dự kiến:** 1–2 ngày. **Dependency:** 0.

**Files:**

- `AgenticGateway.slnx`, `global.json`, `Directory.Build.props`, `Directory.Packages.props`.
- Năm project `src/AgenticGateway.{Host,Core,Protocols,Providers,Infrastructure}/`; ba project test UnitTests/ContractTests/IntegrationTests.
- `Host/Program.cs`, `Security/GatewayKeyAuthenticationHandler.cs`, `Endpoints/HealthEndpoints.cs`.
- `Core/Contracts/{RequestEnvelope,ProtocolKind,GatewayFailure}.cs`.
- `deploy/Dockerfile`, `deploy/compose.yaml`, `.dockerignore`.
- Tests: `IntegrationTests/IngressAuthenticationTests.cs`, `HealthTests.cs`.

- [ ] Test thiếu/sai inference key trả 401 trước bất kỳ upstream call; health không lộ secret.
- [ ] Test Bearer cho Responses/Chat, x-api-key hoặc Bearer cho Messages; hai header mâu thuẫn bị reject. Header gateway không thay thế upstream credential.
- [ ] Test request quá giới hạn sau giải nén trả 413; malformed JSON trả protocol error; config invalid làm startup fail rõ.
- [ ] Implement request ID, cancellation, options validation, exception mapping; không tạo endpoint giả trả thành công.
- [ ] Compose publish `127.0.0.1:8580:8580`, trong container bind `0.0.0.0:8580`; data volume và secret mount riêng.
- [ ] Tạo setup hướng dẫn sinh local inference/admin/master secrets một lần ngoài Git; không dùng default password chung.
- [ ] Run `dotnet test --filter "FullyQualifiedName~IngressAuthenticationTests|FullyQualifiedName~HealthTests"`.
- [ ] Run `docker compose -f deploy/compose.yaml config`; khởi động dev container với secrets test để kiểm tra health.

**Acceptance:** build/start được cả host và container; inference không mở unauthenticated dù chỉ loopback. Chưa dùng mock endpoint để tuyên bố Responses đã hoạt động.

## Task 2 — SQLite, secrets, connection/alias và admin tối thiểu

**Dự kiến:** 2–3 ngày. **Dependency:** 1.

**Files:**

- `Infrastructure/Persistence/GatewayDbContext.cs`, `Entities/`, `Migrations/`.
- `Infrastructure/Credentials/{CredentialProtector,CredentialStore}.cs`.
- `Core/Connections/{ConnectionManager,ConnectionLease,ConnectionSnapshot}.cs`.
- `Core/Routing/{ModelRouter,CapabilityValidator,ModelCatalogSnapshot}.cs`.
- `Host/Pages/Admin/{Connections,Models,Setup}/`, `Host/Security/AdminAuthentication.cs`.
- Tests: `UnitTests/RoutingTests.cs`, `IntegrationTests/{SqliteStore,CredentialStore,AdminSecurity}Tests.cs`.

- [ ] Test explicit alias trước prefix; `local/org/model` giữ upstream ID `org/model`; unknown model không fallback.
- [ ] Test DB không chứa upstream key plaintext; restart giải mã được với master key cũ; sai/mất key fail rõ, không tự ghi đè key mới.
- [ ] Test inference key chỉ lưu hash; revoke chặn request mới; admin cookie/CSRF riêng, inference key không mở admin.
- [ ] Test transaction SQLite thực trên file tạm: WAL, busy timeout, versioned config update; không dùng EF in-memory để chứng minh locking.
- [ ] Implement Connections/Models/Quick setup tối thiểu; cho phép nhập URL API/local server/proxy đang có bằng admin config.
- [ ] Giữ immutable snapshot mỗi request; disabled connection ngừng admission mới, không mutate snapshot của stream cũ.
- [ ] Capability catalog có provenance/version/timestamp, configurable entries khi upstream không discover được.
- [ ] Run `dotnet test --filter "FullyQualifiedName~RoutingTests|FullyQualifiedName~SqliteStore|FullyQualifiedName~CredentialStore|FullyQualifiedName~AdminSecurity"`.

**Acceptance:** cấu hình survive restart; master key được backup riêng; URL/credential không lấy từ inference payload. Chưa cần OAuth/account pool.

## Task 3 — Streaming contracts và mock upstream có kiểm soát

**Dự kiến:** 2–3 ngày. **Dependency:** 2.

**Files:**

- `Core/Contracts/{GatewayEvent,ResponseContext}.cs`.
- `Protocols/Streaming/{SseReader,SseWriter,BoundedBuffer}.cs`.
- `Protocols/Responses/{ResponsesIngressReader,ResponsesEventReducer}.cs`.
- `ContractTests/Support/{ScriptedUpstream,FragmentedReadStream,EventTraceAssertions}.cs`.
- Tests: `ContractTests/{SseFraming,ResponsesReducer}Tests.cs`.

- [ ] Test chuỗi `Tiếng Việt 👩‍💻` chia ở từng byte boundary; LF/CRLF, multiline data, heartbeat, nhiều frame/chunk và frame chưa hoàn chỉnh.
- [ ] Test hai tool xen kẽ giữ đúng call ID/index/order; delta/done/completed không nhân đôi output.
- [ ] Test HTTP 200 + error event, EOF trước terminal, cancellation giữa UTF-8 chunk: không sinh Completed giả.
- [ ] Test event/tool args quá limit; slow consumer gây backpressure, không tạo unbounded queue.
- [ ] Implement reader/reducer, raw event preservation và bounded buffers; kiểm tra theo semantic trace thay vì chỉ JSON string snapshot.
- [ ] Run `dotnet test tests/AgenticGateway.ContractTests --filter "FullyQualifiedName~SseFraming|FullyQualifiedName~ResponsesReducer"`.

**Acceptance:** mock điều khiển được delay, disconnect và status/event lỗi; dùng lại để chứng minh streaming thật ở các adapter.

## Task 4 — Native Responses và OpenAI-compatible upstream

**Dự kiến:** 2–3 ngày. **Dependency:** 3.

**Files:**

- `Core/Execution/{InferencePipeline,ProviderInvocation,ProviderExchange,IProviderAdapter}.cs`.
- `Infrastructure/Http/UpstreamClientFactory.cs`.
- `Providers/OpenAICompatible/{OpenAICompatibleAdapter,EndpointProfile}.cs`.
- `Protocols/Responses/{ResponsesProtocolWriter,ResponsesAccumulator}.cs`.
- `Host/Endpoints/{ResponsesEndpoints,ModelsEndpoints}.cs`.
- Tests: `ContractTests/{NativeResponses,NonStreamingResponses,CapabilityValidation}Tests.cs`.

- [ ] Test client nhận event đầu trước terminal khi mock upstream chủ động chờ; không buffer toàn answer rồi phát SSE.
- [ ] Test giữ instructions, IDs, function/custom tools, opaque reasoning, unknown semantic fields/events trên native profile; chỉ rewrite model/headers theo contract.
- [ ] Test native non-stream JSON được relay đúng; nếu upstream chỉ stream, accumulate cùng một invocation với limit 16 MiB, không gọi model lần hai.
- [ ] Test terminal trùng delta không làm nhân đôi; fallback cho terminal output rỗng chỉ bật trên upstream profile có fixture chứng minh.
- [ ] Test unsupported background/hosted tool/state feature bị từ chối trước upstream khi capability không có.
- [ ] Mặc định full history + store=false. Không forward `previous_response_id` khi chưa có verified continuation/binding; không âm thầm bỏ ID.
- [ ] Endpoint `/v1/responses/compact` chỉ native-forward khi verified; không tạo fake summary/compaction.
- [ ] Implement `ResponseHeadersRead`, approved headers, dispose exchange và tách ingress/upstream auth.
- [ ] Run `dotnet test tests/AgenticGateway.ContractTests --filter "FullyQualifiedName~NativeResponses|FullyQualifiedName~NonStreamingResponses|FullyQualifiedName~CapabilityValidation"`.

**Acceptance:** stream/non-stream/tools/error trace đạt offline; `GET /v1/models` trả aliases đúng. Đây là Responses adapter, không claim đã tích hợp Codex subscription.

## Task 5 — Retry, cancellation, concurrency và network đúng nghĩa

**Dự kiến:** 1–2 ngày. **Dependency:** 4.

**Files:**

- `Core/Execution/{RetryCoordinator,AttemptState,RequestLifetime}.cs`.
- `Core/Connections/ConnectionAdmission.cs`.
- `Infrastructure/Http/{ProxyProfile,UpstreamHandlerPool}.cs`.
- Tests: `IntegrationTests/{ReplaySafety,Cancellation,ConnectionAdmission,ConfigReload,Egress}Tests.cs`.

- [ ] Test connect fail trước gửi request có thể retry, tối đa 2 attempts; tắt retry/hedging POST ở handler/SDK khác.
- [ ] Test body có thể đã tới upstream nhưng chưa thấy token: unknown delivery không tự replay, kể cả upstream chưa trả response headers.
- [ ] Test sau downstream commit không đổi provider hoặc nối JSON error vào SSE; phát protocol error/abort đúng profile.
- [ ] Test disconnect/cancel/timeout giải phóng response/lease/slot; shutdown drain 30s rồi cancel phần còn lại.
- [ ] Test 8 instance/4 connection, chờ slot quá 2s trả 429; release lease idempotent.
- [ ] Test reload giữa stream giữ URL/key/model generation cũ; request sau dùng generation mới.
- [ ] Test egress profile theo connection, redirects/header allowlist, gateway key không đi upstream.
- [ ] Kiểm tra Docker → host upstream bằng `host.docker.internal`; Linux host-gateway và service-name network có hướng dẫn.
- [ ] Run `dotnet test --filter "FullyQualifiedName~ReplaySafety|FullyQualifiedName~Cancellation|FullyQualifiedName~ConnectionAdmission|FullyQualifiedName~ConfigReload|FullyQualifiedName~Egress"`.

**Acceptance:** một retry coordinator duy nhất; không có automatic provider/model fallback; cancellation cleanup target <2s trong harness.

## Task 6 — Chứng nhận Codex sớm và bàn giao M1

**Dự kiến:** 1–2 ngày. **Dependency:** 5.

**Files:** `docs/clients/CODEX.md`, `docs/compatibility/CODEX_RESULTS.md`, `tests/client-e2e/codex/`, repository fixture nhỏ dành riêng cho tool tests.

- [ ] Tạo config mẫu custom provider: base_url `http://127.0.0.1:8580/v1`, env_key gateway, wire_api responses, supports_websockets=false.
- [ ] Dùng config home/profile test tách biệt và workspace fixture; không ghi đè cấu hình cá nhân.
- [ ] Live Codex CLI: đọc file → tool result → sửa file fixture → chạy test fixture → trả kết quả. Gateway chỉ truyền tool request/result; Codex thực thi tool.
- [ ] Test nhiều lượt, hai session/subagent xen kẽ, cancel, restart gateway và full-history resume; kiểm tra opaque data không bị trộn connection.
- [ ] Test custom/freeform tool khi client/model dùng; nếu bridge chưa hỗ trợ thì công bố giới hạn, không quảng cáo agent mode đầy đủ.
- [ ] Kiểm tra long-context/compact: pass native khi có; nếu thiếu, xác nhận client fallback hoặc ghi limitation cụ thể.
- [ ] IDE/App local test riêng theo version nếu người dùng dùng; CLI pass không tự đánh dấu IDE/App pass.
- [ ] Chạy bằng Docker từ setup mới, chứng minh alias/key/credential survive restart.
- [ ] Ghi model, client version, mode, request IDs redacted, outcome và known gaps vào matrix.

**Acceptance M1:** ít nhất Codex CLI thực hiện được tool loop qua một upstream thực, stream thật, cancel và restart đúng; offline suite pass. Nếu thiếu live credential, chỉ ghi "offline-ready", không đánh dấu M1 client-certified.

## Task 7 — Messages native và Responses ↔ Messages bridge

**Dự kiến:** 3–5 ngày. **Dependency:** 6.

**Files:**

- `Providers/Anthropic/AnthropicAdapter.cs`.
- `Protocols/Messages/{MessagesIngressReader,MessagesProtocolWriter,MessagesReducer}.cs`.
- `Protocols/Translation/{ResponsesToMessages,MessagesToResponses}.cs`.
- `Host/Endpoints/MessagesEndpoints.cs`.
- Tests: `ContractTests/{NativeMessages,MessagesResponsesBridge,CountTokens}Tests.cs`.

- [ ] Test native Messages text/tool_use/tool_result/thinking/signature/usage/stop_reason; approved version/beta headers và query flags không bị mất.
- [ ] Bridge subset: text, function tools, parallel tool IDs/order, tool result/error, stop reason và usage. Không map opaque reasoning thành text giả.
- [ ] Test schema/tool_choice/image/cached blocks theo capability; feature ngoài subset fail rõ trước upstream.
- [ ] Test message_start/content_block*/message_delta/message_stop và lỗi giữa stream đúng protocol; không buffer xong mới phát.
- [ ] Count tokens: ưu tiên native upstream endpoint; local estimator trả status/diagnostics công bố estimate, không coi exact billing.
- [ ] Run `dotnet test tests/AgenticGateway.ContractTests --filter "FullyQualifiedName~NativeMessages|FullyQualifiedName~MessagesResponsesBridge|FullyQualifiedName~CountTokens"`.

**Acceptance:** Claude-native profile ưu tiên; cross-model bridge được gắn nhãn compatibility của gateway. Không tuyên bố Anthropic chứng nhận Claude Code chạy với model khác.

## Task 8 — Chat Completions và bridge cho model/local server

**Dự kiến:** 2–4 ngày. **Dependency:** 7.

**Files:** `Protocols/ChatCompletions/{ChatIngressReader,ChatProtocolWriter,ChatReducer}.cs`, `Protocols/Translation/{ResponsesToChat,ChatToResponses,MessagesToChat,ChatToMessages}.cs`, `Host/Endpoints/ChatEndpoints.cs`; `ContractTests/{NativeChat,ChatBridge}Tests.cs`.

- [ ] Native Chat stream/non-stream, n=1, function tools, finish_reason, usage và [DONE] semantics theo upstream profile.
- [ ] Bridge Codex Responses → Chat upstream và Claude Messages → Chat upstream trên subset text/function tools; không tự nhận hỗ trợ custom/freeform tools.
- [ ] Test tool arguments chia chunk, ID chỉ có ở chunk đầu, parallel tools, malformed args, usage sau finish và stream cắt.
- [ ] Test developer/system role, multimodal/structured output chỉ khi model + deployment + translator cùng hỗ trợ.
- [ ] Unsupported audio/video/multiple choices/hosted tool/opaque continuation trả lỗi cụ thể.
- [ ] Dùng direct translator cho cặp cần thiết; không mặc định Messages → Chat → Responses gây mất fidelity.
- [ ] Run `dotnet test tests/AgenticGateway.ContractTests --filter "FullyQualifiedName~NativeChat|FullyQualifiedName~ChatBridge"`.

**Acceptance:** model chat-only dùng được trên feature profile đã chứng nhận; không hứa có mọi khả năng Codex chỉ vì endpoint trả Responses JSON.

## Task 9 — Diagnostics, usage và trải nghiệm setup local

**Dự kiến:** 2–3 ngày. **Dependency:** 8.

**Files:** `Core/Usage/{UsageNormalizer,UsageRecord}.cs`, `Infrastructure/Observability/{RequestRecorder,MetadataWriter,RetentionWorker}.cs`, `Host/Pages/Admin/{Requests,Usage,Diagnostics,ClientSetup}/`; `IntegrationTests/{UsageRecording,MetadataBackpressure,LogRedaction}Tests.cs`.

- [ ] Hiển thị requested/resolved model, connection, status, TTFT, duration, attempts và usage Reported/Estimated/Unknown.
- [ ] Test cached/reasoning counters không cộng hai lần; missing usage không biến thành zero; giữ raw counters cho chẩn đoán.
- [ ] Test SQLite metadata writer bounded; lock/disk error không replay inference, không giữ stream chờ DB vô hạn; diagnostics báo dropped/unwritten records.
- [ ] Retention metadata 7 ngày; debug payload off mặc định, opt-in TTL 1 giờ nếu triển khai. Không cần payload capture để nghiệm thu V1.
- [ ] Test redaction Authorization, token, query secret; metadata không chứa prompt/tool output.
- [ ] Connection test báo chính xác feature vừa thử, không đánh dấu "all supported" từ một câu hello.
- [ ] Setup cung cấp mẫu Codex/Claude/VS Code/OMP; người dùng copy/chọn áp dụng, không tự ghi client files.
- [ ] OTLP optional, collector mất kết nối không chặn inference.
- [ ] Run `dotnet test --filter "FullyQualifiedName~UsageRecording|FullyQualifiedName~MetadataBackpressure|FullyQualifiedName~LogRedaction"`.

**Acceptance:** người dùng thêm connection, map alias, lấy setup mẫu và tìm lỗi request được trong UI; không cần chạy stack monitoring riêng.

## Task 10 — Chứng nhận từng client và giới hạn tương thích

**Dự kiến:** 2–3 ngày. **Dependency:** 9.

**Files:** `docs/clients/{CLAUDE_CODE,COPILOT,OMP,KIRO}.md`, cập nhật `docs/compatibility/CLIENT_MATRIX.md`; `tests/client-e2e/`.

- [ ] Claude Code native Anthropic: auth/version headers, text, tools, count_tokens behavior, cancel; bridge khác model được test/ghi kết quả riêng.
- [ ] VS Code Copilot: Custom Endpoint profile có apiType phù hợp; test chat và tool workflow theo version, không dùng deprecated setting làm mẫu.
- [ ] Copilot CLI/SDK BYOK chỉ chứng nhận khi người dùng cần và version thực hỗ trợ; không suy ra từ VS Code.
- [ ] OMP custom provider gọi gateway, test text/tool/cancel như client bổ sung; không nhúng runtime vào server.
- [ ] Kiro IDE/CLI cập nhật spike kết luận theo evidence; nếu không có custom endpoint được hỗ trợ, để Unverified/Unavailable trên version ấy và mô tả rõ.
- [ ] Smoke lại Codex vì translation/routing mới có thể gây regression.
- [ ] Ghi evidence/version/model/limitations từng cell; tách native fidelity, translated fidelity và model quality.

**Acceptance:** tài liệu không có ô "supported" thiếu evidence. V1 local có thể phát hành với Kiro client chưa verified, nhưng không được quảng cáo đã hỗ trợ đủ bốn client.

## Task 11 — Đóng gói, backup/restore, load/soak và release local V1

**Dự kiến:** 2–3 ngày. **Dependency:** 10.

**Files:** hoàn thiện `deploy/`; `docs/operations/{LOCAL_SETUP,BACKUP_RESTORE,TROUBLESHOOTING,RELEASE_CHECKLIST}.md`; `tests/AgenticGateway.IntegrationTests/LifecycleTests.cs`, `tests/performance/`.

- [ ] Pin images/dependencies; container chạy non-root với volume permissions đúng; readiness kiểm tra dependency local thiết yếu, không gọi model.
- [ ] Test install mới, restart, schema migration và restore trên volume test; migration có backup/khả năng rollback được mô tả.
- [ ] Backup SQLite bằng cơ chế consistent snapshot/backup API hoặc dừng service; không copy file .db sống mà bỏ WAL. Master key backup riêng.
- [ ] Test restore connection/credential với key gốc; key thiếu/sai fail rõ; secrets không vào image layer hoặc Git.
- [ ] Kiểm tra Windows Docker Desktop, host upstream route và Linux host-gateway trong hướng dẫn; publish chỉ loopback.
- [ ] Chạy offline `dotnet test` và client smoke đúng profiles cần phát hành.
- [ ] Mock load trên 2 vCPU/2 GiB: 8 streams/30 phút, request 64 KiB, p95 overhead tới event đầu <100ms; đo overhead so với direct mock cùng môi trường.
- [ ] Slow-client/buffer-limit/cancel tests; cleanup <2s; shutdown drain; soak 2 giờ không RSS/connections tăng liên tục sau warm-up.
- [ ] Capture số đo và environment; vượt target thì sửa nguyên nhân hoặc điều chỉnh spec có lý do, không ghi target như kết quả.
- [ ] Kiểm tra `git diff`, không credential/generated runtime data; ghi known limitations và phiên bản certified.

**Acceptance V1:** từ máy có Docker, setup secrets + Compose tạo được gateway local, cấu hình model và dùng client đã certified; restore được. Không cần DB/Redis/n8n ngoài.

## 4. Module tùy chọn sau M1/V1

Chỉ chọn khi phục vụ connection cần dùng. Connector nào không được chọn thì không tạo placeholder dependency buộc core phải có nó.

### A — Codex account backend

**Files dự kiến:** `Providers/Codex/{CodexAdapter,CodexRequestShaper,CodexCredentialProvider,CodexModelDiscovery,CodexCompactionAdapter}.cs`; `Infrastructure/Credentials/AccountRefreshCoordinator.cs`.

- [ ] Phân biệt public API key và ChatGPT/Codex account backend; auth/import do admin chủ động với ownership rõ.
- [ ] Request allowlist/header/system→developer quirk theo profile backend, không áp cho toàn bộ Responses upstream.
- [ ] Test token rotation single-flight: lock → reload persistent credential → compare generation → refresh/persist atomic; không stale write.
- [ ] Auth retry tối đa 1 trong tổng 2 attempts và chỉ khi replay-safe; invalid grant cần login lại.
- [ ] Preserve reasoning/compaction; continuity binding account/model/session nếu feature dùng state; không account hopping tự động với opaque history.
- [ ] Contract fixtures + live Codex-client tool loop; test `CodexContractTests`, `CodexCredentialTests`, `CodexCompactionTests`.

**Gate:** có account được cấu hình, auth/refresh/restart và feature profile đạt; nếu không, dùng connection tới proxy đang có như bước chuyển tiếp.

### B — Kiro account backend

**Files dự kiến:** `Providers/Kiro/{KiroAdapter,KiroPayloadBuilder,AwsEventStreamDecoder,KiroCredentialProvider,KiroModelDiscovery}.cs`.

- [ ] Implement binary AWS EventStream: lengths/prelude/message CRC, fragmented frame, max frame, exception event; không parse như SSE.
- [ ] Auth/region/profile đúng account; refresh qua cùng egress profile; lưu secret mã hóa.
- [ ] Context overflow/model unavailable/quota/rate limit/auth được phân loại riêng; không xoay account vì bad request.
- [ ] Thinking/tool/terminal integrity và usage theo fixture; EOF không tự completed.
- [ ] Tests `AwsEventStreamTests`, `KiroContractTests`, `KiroCredentialTests`; live tool loop và cancellation.

**Gate:** Kiro upstream certified độc lập với Kiro client. Đối chiếu source công ty nếu được cung cấp sau; không nhận parity chỉ từ guide.

### C — Copilot account backend

**Files dự kiến:** `Providers/Copilot/{CopilotAdapter,CopilotCredentialProvider,CopilotModelDiscovery,CopilotEndpointSelector}.cs`.

- [ ] Device/auth/token exchange do admin khởi tạo; expiry/refresh ownership và entitlement model rõ.
- [ ] Chọn Messages/Responses/Chat theo capability deployment/model, không dựa regex tên model.
- [ ] Preserve required metadata/headers, phân biệt lỗi auth/quota/model; tôn trọng retry budget.
- [ ] Tests `CopilotContractTests`, `CopilotCredentialTests`, `CopilotEndpointTests`; live stream/tool/cancel.

**Gate:** chứng minh inference endpoint thực; cài Copilot SDK không được coi là hoàn thành provider.

### D — Stored responses, continuation và WebSocket

- [ ] Viết ADR trước khi thêm state: identity local profile/client key/session/agent namespace, TTL 24h opt-in, encrypted history, binding connection/account/model.
- [ ] Test previous_response_id, store=false, get/delete authorization, stale completion, parent/subagent collisions và delete race.
- [ ] WebSocket capability thương lượng theo client/upstream, reconnect/connection affinity/cancel; chưa có thì giữ supports_websockets=false.
- [ ] Không suy session từ hash prompt; không dùng state của account/provider khác.

**Gate:** module có conformance suite riêng. Không bật global stored semantics chỉ bằng thêm một table.

### E — n8n, OMP execution worker và memory nâng cao

- [ ] n8n nhận event/notification bất đồng bộ; inference không chờ webhook.
- [ ] OMP worker qua API job/RPC/ACP riêng nếu cần chạy agent từ server; phải có workspace isolation, approval và job lifecycle.
- [ ] Shared Memory/dreaming cơ bản đã chuyển lên MEM-1–MEM-5 trong scope chính; phần sau V1 là full-transcript capture, hooks nâng cao và engine parity nếu cần.
- [ ] Benchmark trước khi thêm compression hoặc tự động chèn memory vào inference request; MCP recall vẫn là đường mặc định.

**Gate:** sản phẩm phụ có spec riêng; không nhúng vào inference MVP.

### F — Mở rộng team/production

PostgreSQL, OIDC/RBAC, tenant isolation, distributed refresh/state/quota, audit/TLS và nhiều replica cần thiết kế lại deployment/control plane. Đây là hướng sau local V1, không phải đổi SQLite connection string là đủ.

## 5. Definition of Done và các quyết định còn mở

Mỗi task xong phải có implementation diff, test hành vi tương ứng, tài liệu capability/limitation và evidence từ lệnh vừa chạy. Release cần offline tests, live client profile được ghi nhận, Docker setup/restart/restore, số đo hiệu năng và không lộ secrets. Chưa chạy test thì không đánh dấu pass.

**Đã chốt:** client-first, Codex ưu tiên, local cá nhân, Docker đơn giản, .NET modular monolith + SQLite và shared memory/dreaming để tránh phân tán kiến thức.

**Cần chọn khi thực thi:** mức tự động capture memory; upstream/model đầu tiên; phiên bản/surface client thực dùng; có cần account connector ngay không; kết quả feasibility Kiro custom inference endpoint. Có thể làm core bằng fixtures trong khi thiếu account, nhưng không gọi đó là live-compatible.

**Hành động tiếp theo hiện tại:** người dùng review ba tài liệu. Không scaffold project, cài dependency, sửa code hoặc client config cho đến khi nhận **`start implement`**.
