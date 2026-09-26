# Agentic Gateway — tài liệu kiến trúc và triển khai

Ngày khảo sát: **2026-09-26**. Implementation đã bắt đầu theo chỉ thị `start implement`; xem trạng thái source và mức kiểm chứng ở [IMPLEMENTATION_STATUS_VI.md](IMPLEMENTATION_STATUS_VI.md).

Đọc theo thứ tự:

1. [Khảo sát workspace và đánh giá OMP](RESEARCH_VI.md): nguồn tham khảo, khác biệt giữa guide công ty và checkout, phần nên học và phần không nên đưa vào proxy.
2. [Thiết kế kiến trúc .NET](ARCHITECTURE_VI.md): mục tiêu, phạm vi, kiến trúc, giao thức, routing, account, state, bảo mật và vận hành.
3. [Kế hoạch triển khai](IMPLEMENTATION_PLAN_VI.md): milestone, task, file dự kiến, interface, kiểm thử và điều kiện phát hành.
4. [Shared Memory và Dreaming](SHARED_MEMORY_VI.md): yêu cầu bổ sung dùng chung kiến thức giữa Kiro/Claude/Copilot/Codex, MCP, scope theo project và worker hợp nhất memory.
5. [Trạng thái implementation](IMPLEMENTATION_STATUS_VI.md): phần đã có trong source, build hiện tại và các khoảng chưa triển khai.
6. [Quy trình dùng UI](UI_WORKFLOW_VI.md): khởi động, cấu hình upstream, kết nối client và dùng chung memory.

Thiết kế chính: **ASP.NET Core .NET 10, modular monolith theo Ports & Adapters, ưu tiên Codex Responses, native forwarding khi phù hợp và adapter chuyển đổi có kiểm tra capability khi khác giao thức.** OMP là nguồn tham khảo cho provider/catalog; gateway không nhúng coding-agent runtime.

Phạm vi đã được bạn xác nhận:

- **Chủ yếu phục vụ client:** Codex/Claude Code/Kiro/Copilot gọi gateway để dùng các model phía sau; ưu tiên Codex.
- **Chạy local cho cá nhân, đơn giản và Docker.** Một process/container .NET, SQLite trong persistent volume.
- MVP không phụ thuộc việc phải proxy được subscription Codex/Copilot/Kiro. Chọn upstream theo model/account bạn có; connector đặc thù là adapter bổ sung.

Các giả định còn lại:

- `Claude` ở phía upstream mặc định là Anthropic API. Claude subscription/OAuth là nhánh tích hợp riêng cần kiểm chứng.
- Shared memory/dreaming đã được người dùng bổ sung để tránh phân tán kiến thức. Đề xuất một module MCP trong cùng host; mức tự động capture chưa chốt, mặc định đề xuất ghi có chọn lọc.
- Chưa yêu cầu multi-user, billing thương mại, Kubernetes hay server chạy coding-agent.

PostgreSQL, tenant isolation/RBAC và nhiều replica nằm ngoài V1 local. Chiều Kiro client → gateway vẫn cần kiểm chứng theo phiên bản; không đồng nhất với Kiro upstream. Việc chốt phạm vi chưa phải chỉ thị bắt đầu code.
