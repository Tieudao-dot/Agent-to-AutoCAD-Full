# Tổng quan dự án ClaudeCAD — 11/11/2026

## Tóm tắt

- **Sản phẩm:** plugin AutoCAD 2022–2027 (bản đang phát hành v0.9.3 nhận 2022–2025; 2026 / 2027 nằm trong bản thử). Khung chat (palette WPF) điều khiển trợ lý Claude
  (sidecar Node, Claude Agent SDK) để vẽ, sửa, đo, hỏi đáp trên bản vẽ đang mở.
- **Bản đang phát hành:** v0.9.3 (09/11). Mọi thứ sau đó đã code xong, **CI xanh, chưa phát hành**:
  khóa key + quản lý thiết bị, 30 bản sửa sau rà soát, skill theo đoạn chat, giao diện kiểu AutoCAD.
- **Đã gộp (11/11):** cả 5 nhánh nằm trên nhánh tích hợp `claude/release-0.9.5`; một bộ cài thử build từ nhánh này.
  Các nhánh riêng coi như đóng băng, sửa tiếp làm trên nhánh tích hợp.
- **Đang chờ cậu:** thử bộ cài thử một lần theo [`checklist-thu-that.md`](checklist-thu-that.md), rồi quyết định
  phát hành (chi tiết ở mục "Việc cần cậu").

## Các thread

| Thread | Phạm vi | Nhánh | Trạng thái | Ngữ cảnh · chi phí quy đổi* |
| --- | --- | --- | --- | --- |
| Chính (thread này) | Tính năng, rà soát, sửa lỗi, phát hành | `claude/wizardly-heisenberg-ugup5c` | Xong việc, chờ phát hành | ~563k token · ~$345 |
| Bảo mật, khóa key & quản lý thiết bị | Key theo máy, Apps Script + Google Sheet duyệt / khóa máy | `claude/device-management` | **Chờ cậu chạy test 6 bước trên AutoCAD** | ~413k · ~$58 |
| Giao diện & UX kiểu AutoCAD | Lớp vỏ, theme, thu gọn, icon thanh trạng thái | `claude/ux-autocad` | Xong đợt hiện tại, chờ cậu thử / giao việc | ~120k · ~$1,3 |
| AutoCAD 2026 / 2027 | 2026 dùng chung bản .NET 8, thêm bản .NET 10 cho 2027, bộ cài chung 2022–2027 | `claude/acad-2026-2027` | Chờ cậu thử trên AutoCAD 2027 dùng thử | — |

\* Số hệ thống ghi nhận cho từng thread (quy đổi theo giá API). Ngữ cảnh càng dài thì mỗi bước càng đắt.

## Đã làm

| Phiên bản | Nội dung | Trạng thái |
| --- | --- | --- |
| ≤ 0.9.3 | 89 tool vẽ / sửa, khung chat 20+ tính năng, macro, phiên bản 30 phút, ghi nhận sửa tay, chọn layer, báo cáo lỗi, tài khoản AI, đăng nhập trong bộ cài | Đã phát hành |
| 0.9.5 (bản thử; gồm phần từng ghi 0.9.4) | Khóa bằng key theo máy + quản lý thiết bị qua Google Sheet (Active / Disabled); 30 bản sửa sau rà soát toàn dự án + 3 đợt sửa tiếp; skill theo từng đoạn chat | Code xong, CI xanh, chưa phát hành |
| 0.9.5 (bản thử) | AutoCAD 2026 / 2027 (bộ cài chung 2022–2027, thêm bản .NET 10); checklist thử thật + tự kiểm tra mở rộng; thẻ "Bắt đầu" 3 bước, danh sách bước và tiến độ theo kế hoạch. Giao diện kiểu AutoCAD: màu + font lấy từ theme AutoCAD, nút phẳng + icon có tooltip, cài đặt dạng lưới, thẻ xác nhận / lỗi gọn, gửi thêm ý khi Claude đang làm, Esc để dừng, thu gọn còn ô nhập, icon + bong bóng trên thanh trạng thái | Code xong, CI xanh, chưa thử trên AutoCAD thật |

Kiểm thử tự động (nhánh tích hợp): lõi plugin 164 test, sidecar 136 test, UI test WPF trên Windows (chụp ảnh giao diện), build
net48 + net8 + net10 cho AutoCAD 2022–2027.

## Nhánh và CI

| Nhánh | Commit mới nhất | CI |
| --- | --- | --- |
| `main` | `47baddb` v0.9.3 | Đã phát hành |
| **`claude/release-0.9.5`** (nhánh tích hợp, gộp cả 4 nhánh dưới + 2026/2027) | bản thử | CI chạy tay `release: false`; bộ cài thử ở mục Artifacts của lượt mới nhất |
| `claude/wizardly-heisenberg-ugup5c` | `4e31fe7` | Xanh (run 42816573516) |
| `claude/ux-autocad` (tách từ nhánh trên) | `c03a472` hàng nút dưới ô nhập thành icon | Xanh (run 42818258362) |
| `claude/acad-2026-2027` (tách từ `0d4056a`) | hỗ trợ AutoCAD 2026 / 2027, chưa phát hành | xem [`autocad-2026-2027.md`](autocad-2026-2027.md) |
| `claude/device-management` | `7839765` (ghi chú) · code `8067a98` | Xanh (run 42779174835) |

## Việc cần cậu

1. **Thử bộ cài thử một lần** theo [`checklist-thu-that.md`](checklist-thu-that.md) (~20 phút; phần A là bài test
   key của thread bảo mật) trên bản AutoCAD đang dùng và bản dùng thử 2027. Lỗi thì báo ở thread gộp nhánh.
2. **Quyết định phát hành:** tên phiên bản (0.9.5 hay 1.0.0) và thời điểm. Merge `main` có phiên bản mới là tự
   phát hành bộ cài, nên chỉ làm khi cậu đồng ý.

## Lưu ý

- **Đã gộp trên `claude/release-0.9.5`.** Xung đột code duy nhất là `ChatController.cs` (khởi động lại trợ lý):
  giữ cả luật khóa máy lẫn phần giao diện; còn lại là tài liệu. Xem [`ke-hoach-gop-nhanh.md`](ke-hoach-gop-nhanh.md).
- **Phiên bản:** csproj vẫn ghi 0.9.4 (bộ cài thử mang tên `ClaudeCAD-0.9.4`); nâng phiên bản khi cậu chốt tên.
- **Chưa thử thật:** nhiều mục trong `docs/test-log.md` (K, R, S, D, I, UX) vẫn "chưa chạy" trên AutoCAD thật.
  CI không thay được phần này (theme AutoCAD, thanh trạng thái, chọn đối tượng, phím tắt).

## Chi phí và cách tiết kiệm

- Thread này đã dùng ~56% ngữ cảnh; mỗi bước đọc lại ~563k token. **Đề xuất:** việc mới mở thread mới
  ngắn gọn (như thread giao diện: ~$1,3 cho cả một đợt), thread này chỉ dùng cho bước gộp nhánh + phát hành.
- Quy tắc đã áp dụng cho cả 3 thread: báo trước việc nặng, chỉ 1 tác vụ chờ CI một lúc, push noti khi xong việc.

## Liên kết

- Thread bảo mật: https://claude.ai/code/session_01D56HmEsRHYufkFu48qFmGR
- Thread giao diện: https://claude.ai/code/session_01D56HmHQPBueRAkYDSecN9n
- Báo cáo nghiên cứu giao diện: https://claude.ai/artifact/Bo11py5ZRq4Qi8kpWaoc5y
- Màn mẫu giao diện: https://claude.ai/artifact/GKFbubPTL9QEWNdV3kn4JQ
- Báo cáo tình hình trước đó: https://claude.ai/artifact/2rQMPkVh2gS5F7R7yLeftk
