# Tổng quan dự án ClaudeCAD — 10/11/2026

## Tóm tắt

- **Sản phẩm:** plugin AutoCAD 2022–2025. Khung chat (palette WPF) điều khiển trợ lý Claude
  (sidecar Node, Claude Agent SDK) để vẽ, sửa, đo, hỏi đáp trên bản vẽ đang mở.
- **Bản đang phát hành:** v0.9.3 (09/11). Mọi thứ sau đó đã code xong, **CI xanh, chưa phát hành**:
  khóa key + quản lý thiết bị, 30 bản sửa sau rà soát, skill theo đoạn chat, giao diện kiểu AutoCAD.
- **Không còn tác vụ nền nào chạy.** Ba thread đều đứng chờ.
- **Đang chờ cậu ở 3 việc:** chạy bài test 6 bước của thread bảo mật, thử giao diện mới trên
  AutoCAD thật, và đồng ý gộp nhánh để phát hành (chi tiết ở mục "Việc cần cậu").

## Các thread

| Thread | Phạm vi | Nhánh | Trạng thái | Ngữ cảnh · chi phí quy đổi* |
| --- | --- | --- | --- | --- |
| Chính (thread này) | Tính năng, rà soát, sửa lỗi, phát hành | `claude/wizardly-heisenberg-ugup5c` | Xong việc, chờ phát hành | ~563k token · ~$345 |
| Bảo mật, khóa key & quản lý thiết bị | Key theo máy, Apps Script + Google Sheet duyệt / khóa máy | `claude/device-management` | **Chờ cậu chạy test 6 bước trên AutoCAD** | ~413k · ~$58 |
| Giao diện & UX kiểu AutoCAD | Lớp vỏ, theme, thu gọn, icon thanh trạng thái | `claude/ux-autocad` | Xong đợt hiện tại, chờ cậu thử / giao việc | ~120k · ~$1,3 |

\* Số hệ thống ghi nhận cho từng thread (quy đổi theo giá API). Ngữ cảnh càng dài thì mỗi bước càng đắt.

## Đã làm

| Phiên bản | Nội dung | Trạng thái |
| --- | --- | --- |
| ≤ 0.9.3 | 89 tool vẽ / sửa, khung chat 20+ tính năng, macro, phiên bản 30 phút, ghi nhận sửa tay, chọn layer, báo cáo lỗi, tài khoản AI, đăng nhập trong bộ cài | Đã phát hành |
| 0.9.4 | Khóa bằng key theo máy + quản lý thiết bị qua Google Sheet (Active / Disabled); 30 bản sửa sau rà soát toàn dự án + 3 đợt sửa tiếp; skill theo từng đoạn chat | Code xong, CI xanh, chưa phát hành |
| 0.9.5 (đang làm) | Giao diện kiểu AutoCAD: màu + font lấy từ theme AutoCAD, nút phẳng + icon có tooltip, cài đặt dạng lưới, thẻ xác nhận / lỗi gọn, gửi thêm ý khi Claude đang làm, Esc để dừng, thu gọn còn ô nhập, icon + bong bóng trên thanh trạng thái | Code xong, CI xanh, chưa thử trên AutoCAD thật |

Kiểm thử tự động: lõi plugin 118 test, sidecar 124 test, UI test WPF trên Windows (chụp ảnh giao diện), build
net48 + net8 cho cả 4 bản AutoCAD.

## Nhánh và CI

| Nhánh | Commit mới nhất | CI |
| --- | --- | --- |
| `main` | `47baddb` v0.9.3 | Đã phát hành |
| `claude/wizardly-heisenberg-ugup5c` | `4e31fe7` | Xanh (run 42816573516) |
| `claude/ux-autocad` (tách từ nhánh trên) | `c03a472` hàng nút dưới ô nhập thành icon | Xanh (run 42818258362) |
| `claude/device-management` | `7839765` (ghi chú) · code `8067a98` | Xanh (run 42779174835) |

## Việc cần cậu

1. **Thread bảo mật:** chạy bài test 6 bước trên Windows + AutoCAD (bản cài đã sẵn), báo kết quả từng bước,
   lỗi thì gửi ảnh. Phát hành v0.9.4 đang chờ bước này.
2. **Giao diện mới:** thử theo `docs/test-log.md` UX-1 … UX-17 (theme, nút, thu gọn, icon thanh trạng thái),
   phản hồi ở thread giao diện.
3. **Quyết định phát hành:** gộp 3 nhánh thành một rồi merge `main`. Cần cậu chọn:
   phát hành chung một bản (khóa key + giao diện mới), hay phát hành 0.9.4 trước rồi 0.9.5 sau.
   Merge `main` có phiên bản mới là tự phát hành bộ cài, nên tớ chỉ làm khi cậu đồng ý.

## Lưu ý

- **Ba nhánh tách nhau.** Nhánh bảo mật đã gộp nhánh chính tới "đợt 3", chưa có các bản sửa sau rà soát lại
  và phần giao diện. Khi gộp có thể xung đột nhỏ ở `bo-cai/README.md` và `docs/`; code ít chồng nhau.
- **Phiên bản:** csproj vẫn ghi 0.9.4; changelog ghi giao diện mới là 0.9.5 "đang làm".
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
