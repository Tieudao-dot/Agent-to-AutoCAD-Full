# Kế hoạch gộp nhánh và phát hành — 11/11/2026

**Đề xuất:** gộp cả 5 nhánh vào **một nhánh tích hợp**, build **một bộ cài thử** (không phát hành), cậu test
**một lần** theo checklist, rồi mới merge `main` để phát hành **một bản**. Chưa gộp gì cho tới khi cậu duyệt.

## Hiện trạng các nhánh

| Nhánh | Nội dung | Commit | So với nhánh chính | Trạng thái |
| --- | --- | --- | --- | --- |
| `main` | v0.9.3 đang phát hành | `47baddb` | — | Thiếu 16 commit của nhánh chính |
| `claude/wizardly-heisenberg-ugup5c` (chính) | Sửa sau rà soát, skill theo đoạn chat, 5 đợt giao diện, tài liệu | `0d4056a` | — | Xong, CI xanh |
| `claude/acad-2026-2027` | AutoCAD 2026 / 2027 (thêm bản build .NET 10), sửa `install.ps1` | `4ba9faa` | +6, đã chứa đủ nhánh chính | Xong, CI xanh; chờ cậu thử trên 2027 |
| `claude/ux-autocad` | Giao diện tiếp (hàng nút icon…); đang làm tiến độ từng bước | `d76c8d4` | +4, thiếu 2 commit tài liệu | Đang làm mục 5 |
| `claude/test-checklist` | Checklist thử thật + tự kiểm tra mở rộng | (mới tạo từ nhánh giao diện) | — | Đang làm |
| `claude/device-management` | Khóa key theo máy, quản lý thiết bị qua Google Sheet | `7839765` | +9, thiếu 11 commit của nhánh chính | Code xong, CI xanh; chờ cậu test 6 bước |

Tất cả các nhánh đều ghi phiên bản 0.9.4 trong csproj; chưa nhánh nào nâng phiên bản.

## Xung đột khi gộp (đã chạy thử, chưa gộp thật)

| Gộp | Kết quả |
| --- | --- |
| chính ← 2026/2027 | Sạch (chỉ là tiến thẳng 6 commit) |
| chính ← giao diện | Sạch |
| giao diện ↔ 2026/2027 | 1 file: `bo-cai/README.md` (dòng lịch sử phiên bản) — dễ |
| **bảo mật ↔ ba nhánh còn lại** | 4 file: `bo-cai/README.md`, `docs/progress.md`, `docs/test-log.md` (dễ) và **`ChatController.cs`** (code, phải gộp tay cẩn thận) |
| kiểm thử ↔ 2026/2027 | Cả hai cùng sửa `SelfTest.cs` — tớ đã dặn thread kiểm thử đặt phần mới ở chỗ riêng để tránh |

Rủi ro chính là **`ChatController.cs`**: nhánh bảo mật sửa phần kích hoạt, các nhánh kia sửa hàng chờ gửi, thanh
trạng thái, tự gửi lại. Phần giảm rủi ro: test tự động (lõi 118, trợ lý 124, UI test), tự kiểm tra mở rộng, và
checklist thử thật.

## Các bước

1. **Chờ việc đang chạy xong** (không cần cậu): thread giao diện làm tiến độ từng bước; thread kiểm thử làm
   checklist + tự kiểm tra.
2. **Gộp vào nhánh tích hợp** theo thứ tự ít xung đột trước: 2026/2027 → giao diện → kiểm thử → bảo mật.
   Trong lúc gộp các thread tạm ngừng đẩy code. CI xanh → **một bộ cài thử** (file tải từ CI, không phát hành).
3. **Cậu test một lần** theo checklist (~20–30 phút): trên bản AutoCAD đang dùng và bản dùng thử 2027. Bài test
   6 bước của thread bảo mật nằm trong checklist này, không phải cài riêng từng bản.
4. **Sửa theo phản hồi**, CI xanh lại.
5. **Cậu đồng ý phát hành** → nâng phiên bản → merge `main` → CI tự phát hành bộ cài. Máy chủ kích hoạt đã được
   gắn trên nhánh bảo mật nên bước chặn phát hành của CI sẽ qua.
6. **Sau phát hành:** hướng dẫn cho người dùng (mục 3), tách `ChatControl.cs` (mục 4).

## Ai làm bước gộp

Đề xuất mở thread mới **"ClaudeCAD — gộp nhánh & phát hành"** trên nhánh riêng: ngữ cảnh ngắn nên rẻ hơn nhiều
so với gộp ở thread chính (mỗi bước ở thread chính đọc lại ~570k token) hay thread bảo mật (~413k). Thread đó
vẫn phải hỏi cậu trước khi merge `main`.

## Việc cần cậu quyết

1. Duyệt cách làm ở trên (một bộ cài thử, test một lần, phát hành một bản), hay phát hành khóa key trước rồi
   giao diện + 2026/2027 sau.
2. Khi tới bước 5: tên phiên bản (0.9.5 hay 1.0.0) và thời điểm phát hành.

## Ghi chú

- Thread 2026/2027 báo `install.ps1` trên `main` đang có lỗi và bản sửa nằm trên nhánh của nó; chi tiết ở
  `docs/autocad-2026-2027.md` trên nhánh đó. Lỗi này hết khi nhánh được gộp và phát hành.
- Phương án phát hành 2 đợt (khóa key trước) vẫn làm được nhưng cậu phải cài và test 2 bộ cài, và xung đột
  `ChatController.cs` vẫn phải xử lý ở đợt đầu.
