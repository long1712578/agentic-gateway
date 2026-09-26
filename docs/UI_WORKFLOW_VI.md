# Quy trình dùng Agentic Gateway UI

Gateway quản trị tại `/admin` trên địa chỉ local đang chạy (`http://localhost:5096/admin` với launch profile, hoặc `http://127.0.0.1:8580/admin` với Docker Compose). UI và endpoint model/MCP cùng một .NET process.

## 1. Khởi động và đăng nhập

Trước khi mở UI, Host cần `AGENTIC_GATEWAY_API_KEY` và `AGENTIC_GATEWAY_MCP_API_KEY` (hoặc hai giá trị `Gateway:ApiKey`, `Gateway:MemoryApiKey` trong .NET User Secrets). Mỗi key là chuỗi ngẫu nhiên ít nhất 32 byte. Nếu thiếu, Host dừng ngay và UI chưa thể mở. `dotnet run` không tự đọc `.env`; Docker Compose mới đọc file đó.

Admin key là key thứ ba, tách khỏi hai key của client. Mặc định gateway tạo `admin.key` trong thư mục `data` cạnh file chạy và ghi **đường dẫn** vào log; trang đăng nhập cũng hiển thị đường dẫn. Trên Docker, file là `/app/data/admin.key` trong volume. Có thể đặt `AGENTIC_GATEWAY_ADMIN_KEY` để quản lý key qua môi trường thay cho file. Không đưa key vào Git/chat.

## 2. Cấu hình upstream và model

Vào **Upstream & model**, nhập Base URL kết thúc bằng `/v1`, API key của upstream và model ID thực tế của upstream. HTTP được UI chấp nhận cho loopback và `host.docker.internal` khi Docker gọi server trên máy host; URL ngoài máy cần HTTPS. Gateway xuất alias `coding-default` cho client, rồi đổi alias thành model ID đã lưu khi forward request.

Nút **Gửi yêu cầu thử** gọi Responses API thật với prompt nhỏ; thao tác này có thể tính phí. Kết quả thành công chỉ xác nhận upstream trả lời yêu cầu thử, chưa chứng nhận mọi tính năng của coding client.

Nếu dùng tài khoản ChatGPT Plus trong Codex, **không** dùng nút này để thử Plus: nó chỉ kiểm tra API-key upstream. Xem luồng Codex account ở bước 3.

Thứ tự cấu hình của upstream là: biến môi trường đang có giá trị → cấu hình đã lưu trong UI → appsettings/User Secrets. Nếu môi trường đang quản lý upstream, form chuyển sang chỉ đọc; hãy sửa biến môi trường và khởi động lại. Key lưu bằng UI nằm trong `data/upstream-settings.json`, không mã hóa; bảo vệ data volume và backup.

## 3. Kết nối client

Vào **Kết nối client**, sao chép key inference/MCP và đoạn cấu hình của client. Key của Host trong User Secrets không tự có trong môi trường của Codex/Claude Code/Kiro/Copilot; đặt key ở môi trường hoặc secret store của từng client. Không copy nguyên một file config đè lên file đang có: chỉ ghép phần server/provider được UI tạo.

| Client | Model qua gateway | Shared Memory MCP |
|---|---|---|
| Codex | Responses với alias `coding-default`; cần kiểm thử E2E theo bản Codex đang dùng | Có cấu hình mẫu |
| Claude Code | Chưa có Claude Messages adapter | Có cấu hình mẫu |
| Kiro | Chưa chứng nhận custom inference endpoint | Có cấu hình mẫu |
| VS Code Copilot | Chưa chứng nhận custom inference endpoint | Có cấu hình mẫu |

**Thử Codex Plus qua gateway:** chạy `codex login` và xác nhận `codex login status` hiển thị đăng nhập ChatGPT. Tại trang **Kết nối client**, copy mục **Model qua tài khoản Codex Plus** vào `~/.codex/config.toml` của user (không đặt model provider ở `.codex/config.toml` cấp project). Đặt `AGENTIC_GATEWAY_API_KEY` trong môi trường tiến trình Codex; khởi động lại Codex và thử một prompt streaming. Gateway xác thực key này bằng header `X-Agentic-Gateway-Key`, còn `Authorization` mang phiên ChatGPT của Codex. Gateway không đọc `auth.json`. Chỉ dùng model mà tài khoản Codex hỗ trợ; thay `gpt-5.5` trong ví dụ nếu cần. Endpoint này hiện chỉ phục vụ Codex account streaming, chưa phải connector dùng chung cho Claude/Kiro/Copilot. Nếu muốn thử Memory MCP, thêm snippet MCP riêng và đặt `AGENTIC_GATEWAY_MCP_API_KEY`.

Sau khi cấu hình, kiểm tra client đã nhận MCP server và ba tools `memory_remember`, `memory_recall`, `memory_dream`. Nếu không, kiểm tra URL `/mcp`, MCP key, phiên bản client và log client. Một test inference riêng: Codex gọi model `coding-default` và xác nhận response/stream/tool loop theo workload thực tế.

## 4. Dùng chung Memory

Chọn một `projectId` ổn định, ví dụ `agent-1`. Dùng **đúng cùng giá trị** ở mọi client và worktree của cùng dự án. Trang **Shared Memory** cho phép ghi quyết định, fact, lesson, preference, handoff, tìm lại chúng và xem nguồn.

Quy trình đề xuất: agent A ghi một quyết định với file/commit làm `sourceReference`; agent B gọi `memory_recall` với cùng projectId và một query liên quan; trước khi bàn giao, agent A lưu `Handoff` ngắn. Memory là ghi chú có nguồn, không tự thay thế code/config hiện tại. Chỉ chạy `memory_dream` khi muốn tổng hợp; nó gửi các memory được chọn tới upstream và có thể phát sinh chi phí. Chưa có tự động capture hay lịch dreaming.

## 5. Chẩn đoán và bảo vệ dữ liệu

Trang **Chẩn đoán** chỉ báo readiness của SQLite, URL, key và alias; nó chưa có thống kê request/quota. Lỗi 401 ở `/v1/*` thường là inference key, còn 401 ở `/mcp` là MCP key. Nếu Host không lên được, đọc lỗi khởi động; UI không thể sửa key bootstrap khi Host chưa chạy.

Với `/codex/v1/responses`, lỗi 401 xảy ra khi thiếu gateway key hoặc phiên đăng nhập Codex không hợp lệ. Token chỉ được chuyển đến host Codex cố định qua HTTPS; không copy `auth.json` vào repo hay container. Luồng này chưa được xác nhận bằng tài khoản thật trong bộ kiểm tra offline.

Backup volume hoặc thư mục `data` bao gồm memory, upstream key đã nhập bằng UI và admin key. File `.gitignore` và `.dockerignore` ngăn dữ liệu local lọt vào commit/build context, nhưng không mã hóa backup. Khi từng có key trong Git history, hãy đổi key đó; xóa file ở commit mới không thu hồi key cũ.
