# Plugin ClaudeCAD — tổng quan

*Cập nhật 10/11/2026 · bản đang phát hành v0.9.3 · v0.9.4 và giao diện mới (0.9.5) đã code xong, chưa phát hành*

ClaudeCAD là plugin cho **AutoCAD 2022–2025** (2026 / 2027 đang làm, chưa phát hành) thêm một khung chat (palette) vào AutoCAD. Người dùng gõ yêu cầu
bằng tiếng Việt ("vẽ mặt bằng phòng 4×6 m, tường 220, cửa đi 900"). Claude đọc bản vẽ đang mở, rồi tự vẽ, sửa, đo
hoặc trả lời ngay trên bản vẽ đó. Mọi thay đổi của một lượt gộp thành một bước hoàn tác.

## 1. Plugin làm được gì

| Nhóm | Chi tiết |
| --- | --- |
| Vẽ và sửa | 89 lệnh (tool) cho Claude: đường, polyline, cung, chữ, kích thước, hatch, block, layer, style, fillet / chamfer, di chuyển / xoay / sao chép / xóa, zoom… Nhiều lệnh gộp một lần (batch) để vẽ nhanh |
| Hiểu bản vẽ | Đọc thông tin bản vẽ, layer, đối tượng, đơn vị; đo, đếm; biết những gì người dùng **sửa tay** giữa các lượt (biên bản thay đổi) |
| Theo chuẩn công ty | **Skill** = file `.md` mô tả quy chuẩn (layer, nét, chữ, khung tên…). Có 3 skill mẫu (kiến trúc, MEP, hạ tầng); người dùng tự thêm, chọn skill riêng cho từng đoạn chat |
| Nhập yêu cầu | Gõ tiếng Việt; `/` chọn mẫu lệnh; `@` gọi tên layer / block; chọn đối tượng hoặc lấy điểm trên bản vẽ; đính kèm ảnh phác thảo (dán, kéo thả); chụp vùng bản vẽ; chọn layer để giới hạn phạm vi sửa |
| Làm lại không tốn token | **Macro**: lưu một lượt vẽ ưng ý, chạy lại ở chỗ khác mà không cần Claude |
| Ghi chú dự án | Thông tin gửi kèm mọi tin nhắn (cao độ, chiều dày tường, khung tên…) |
| Tài khoản AI | Đăng nhập tài khoản Claude Pro / Max hoặc dùng API key Anthropic; xem hạn mức đã dùng; đổi / đăng xuất tài khoản |

## 2. An toàn cho bản vẽ

- **Hỏi trước khi làm việc lớn:** xóa / sửa nhiều đối tượng có sẵn phải qua thẻ xác nhận (có nút xem trước, tô sáng
  đúng các đối tượng). Việc nhiều bước thì Claude trình kế hoạch trước, người dùng bấm *Làm* hoặc sửa kế hoạch.
- **Hoàn tác cả lượt bằng 1 lần U**, hoặc nút *Hoàn tác lượt này*.
- **Phiên bản:** trước khi chat sửa, plugin lưu một bản sao DWG (tối đa 30 phút một lần, giữ 10 bản), mở ra xem được.
- **Chỉ xem:** bật cho một bản vẽ thì Claude chỉ đọc, đo, hỏi đáp, không sửa.
- **Tự kiểm tra bằng ảnh:** sau việc vẽ lớn, Claude chụp vùng vừa vẽ để tự soát lỗi.
- Dữ liệu lấy từ bản vẽ được gửi cho Claude dưới dạng *dữ liệu*, không bao giờ được coi là lệnh.

## 3. Cách hoạt động

1. Người dùng gõ yêu cầu trong khung chat.
2. Plugin gom ngữ cảnh: bản vẽ đang mở, đối tượng đang chọn, layer, ghi chú dự án, skill của đoạn chat, những gì vừa sửa tay.
3. Gửi sang **trợ lý** (một tiến trình Node chạy cạnh AutoCAD, dùng Claude Agent SDK) qua kênh nội bộ (named pipe).
4. Claude quyết định gọi lệnh nào; trợ lý chuyển từng lệnh về plugin.
5. Plugin thực hiện lệnh trong AutoCAD, gói cả lượt trong một nhóm hoàn tác, trả kết quả lại cho Claude.
6. Câu trả lời hiện trong khung chat, kèm *Xem kết quả* (zoom + tô sáng) và *Hoàn tác lượt này*.

| Thành phần | Công nghệ | Ghi chú |
| --- | --- | --- |
| Plugin | C# / WPF, `ClaudeCAD.Plugin.dll` | 2 bản build: .NET Framework 4.8 cho AutoCAD 2022–2024, .NET 8 cho AutoCAD 2025 (nhánh `claude/acad-2026-2027`: .NET 8 dùng luôn cho 2026, thêm bản .NET 10 cho 2027) |
| Trợ lý (sidecar) | Node.js + TypeScript, Claude Agent SDK | Node đi kèm bộ cài, người dùng không phải cài |
| Skill | File Markdown (chuẩn Agent Skills) | Thư viện skill + danh sách skill theo từng đoạn chat |
| Bộ cài | Inno Setup, `ClaudeCAD-Setup.exe` | Cài bundle vào ApplicationPlugins, thêm thư mục tin cậy, đăng nhập Claude ở trang cuối |
| Kích hoạt | Key theo máy + giấy phép có chữ ký số | Mã máy `CC-XXXX-XXXX-XXXX`; yêu cầu kích hoạt gửi email cho chủ plugin |

## 4. Cài đặt, kích hoạt, sử dụng

- **Yêu cầu:** Windows, AutoCAD 2022 / 2023 / 2024 / 2025 bản đầy đủ (2026 / 2027: đang làm, chưa phát hành) (AutoCAD LT không chạy plugin .NET),
  có mạng, tài khoản Claude Pro / Max hoặc API key Anthropic.
- **Cài:** chạy `ClaudeCAD-Setup.exe` → trang *Kích hoạt* hiện mã máy, gửi yêu cầu cho chủ plugin, nhập key → cài →
  đăng nhập Claude ở trang cuối. Cài ngầm cho IT: `ClaudeCAD-Setup.exe /VERYSILENT /KEY=<key>`.
- **Key vĩnh viễn** cho máy đó; cập nhật bản mới không hỏi lại. Chép plugin sang máy khác sẽ bị hỏi key.
- **Mở khung chat:** lệnh `CLAUDECHAT`, lệnh tắt `CC`, hoặc `Ctrl+Shift+Space`.
- **Lệnh khác:** `CLAUDECHATRESTART` (khởi động lại trợ lý), `CLAUDECHATUPDATE` (cập nhật), `CLAUDECHATSELFTEST`
  (tự kiểm tra trên bản vẽ tạm), `CLAUDECHATLOG` (mở log).
- **Khi lỗi:** ⚙ ▸ *Gửi báo cáo lỗi* tạo một file .zip trên Desktop (thông tin máy, log, kết quả tự kiểm tra; không có
  nội dung chat, bản vẽ hay API key).

## 5. Dữ liệu và quyền riêng tư

- Lịch sử chat, cài đặt, macro, phiên bản lưu trên máy người dùng (`%LocalAppData%\ClaudeCAD`).
- API key lưu mã hóa theo tài khoản Windows. Log không ghi nội dung chat.
- Nội dung yêu cầu và dữ liệu bản vẽ cần cho lượt đó được gửi tới Claude (Anthropic) để xử lý.

## 6. Trạng thái các phiên bản

| Phiên bản | Nội dung chính | Trạng thái |
| --- | --- | --- |
| 0.4 – 0.9.3 | Khung chat 20+ tính năng, skill, macro, phiên bản, ghi nhận sửa tay, chọn layer, báo cáo lỗi, tài khoản AI, đăng nhập trong bộ cài | Đã phát hành (v0.9.3 ngày 09/11) |
| 0.9.4 | Khóa key theo máy + quản lý thiết bị qua Google Sheet (duyệt / khóa từng máy); ~30 bản sửa sau rà soát toàn dự án; skill theo từng đoạn chat; chạy bền hơn | Code xong, CI xanh, chờ test 6 bước trên máy thật |
| 0.9.5 | Giao diện theo phong cách AutoCAD: màu + font lấy từ theme AutoCAD, nút icon có tooltip, cài đặt dạng lưới, gửi thêm ý khi Claude đang làm, Esc để dừng, thu gọn còn ô nhập, icon + bong bóng trên thanh trạng thái | Code xong, CI xanh, chưa thử trên AutoCAD thật |

**Kiểm thử tự động:** 118 test lõi plugin, 124 test trợ lý, test giao diện WPF trên Windows (có ảnh chụp), build cả 2
bản .NET và bộ cài trên mỗi lần CI.

## 7. Giới hạn hiện tại

- Cần mạng và phụ thuộc hạn mức tài khoản Claude (hạn mức 5 giờ / tuần); hết hạn mức thì phải chờ hoặc dùng API key.
- Không chạy trên AutoCAD LT. Bản đang phát hành chỉ nhận 2022–2025; AutoCAD 2026 (R25.1) và 2027 (R26.0) đã khai báo trên
  nhánh `claude/acad-2026-2027`, CI build được, chưa thử trên AutoCAD thật ([chi tiết + nguồn](autocad-2026-2027.md)).
  AutoCAD sau 2027 (R26.1+) không tự nạp cho đến khi được khai báo.
- Nhiều mục chỉ kiểm được trên AutoCAD thật (theme, thanh trạng thái, chọn đối tượng, phím tắt, kích hoạt) và vẫn
  "chưa chạy" trong `docs/test-log.md`.
- Claude có thể hiểu sai yêu cầu: các lớp an toàn ở mục 2 giúp xem trước và hoàn tác, nhưng người dùng vẫn nên
  kiểm tra kết quả trước khi lưu bản vẽ.

## Tài liệu chi tiết

`README.md` (tổng quan kỹ thuật) · `bo-cai/README.md` (cài đặt + lịch sử phiên bản) · `docs/palette-features.md`
(từng tính năng khung chat) · `docs/ux-autocad.md` (thiết kế giao diện) · `skills/README.md` (viết skill) ·
`docs/test-log.md` (kiểm thử) · `docs/tong-quan-du-an.md` (tình hình dự án, các thread, nhánh).
