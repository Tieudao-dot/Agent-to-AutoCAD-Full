# Bộ cài ClaudeCAD

## Tải bộ cài (luôn là bản mới nhất)

**[⬇ ClaudeCAD-Setup.exe](https://github.com/Tieudao-dot/Agent-to-Autocad/releases/latest/download/ClaudeCAD-Setup.exe)**

- Link này không đổi: mỗi khi plugin có bản mới, link tự trỏ sang bộ cài mới. Không cần đăng nhập GitHub.
- Các bản trước và file khác (`ClaudeCAD-<version>.zip`, `install.ps1`, `latest.json`): [trang phát hành](https://github.com/Tieudao-dot/Agent-to-Autocad/releases).
- File bộ cài không nằm trực tiếp trong thư mục này vì GitHub không nhận file lớn hơn 100 MB trong repo (bộ cài khoảng 140 MB vì kèm sẵn Node.js và Claude Code). CI tự build và đăng bộ cài mỗi khi có phiên bản mới trên `main`.

## Cài đặt (3 bước)

1. Tải `ClaudeCAD-Setup.exe` ở link trên.
2. Chạy file. Nếu Windows hiện "Windows protected your PC" → **More info → Run anyway** (bộ cài chưa ký số). Không cần quyền admin, không cần cài Node.js hay Claude Code. Nên đóng AutoCAD trước khi cài.
3. Mở AutoCAD 2022–2025: khung chat tự mở với thẻ **Bắt đầu** → bấm **Đăng nhập Claude**, đăng nhập tài khoản Claude Pro/Max trên trình duyệt (một lần trên mỗi máy). Xong — gõ yêu cầu bằng tiếng Việt.

Sau khi cài: gõ `CLAUDECHATSELFTEST` để tự kiểm tra (báo cáo ✓/✗ mở trong Notepad). Mở khung chat: `CLAUDECHAT`, `CC` hoặc **Ctrl+Shift+Space**.

## Cập nhật

- Khung chat tự kiểm tra bản mới mỗi ngày và báo trong chat. Gõ `CLAUDECHATUPDATE` để tải; bản mới được cài khi đóng AutoCAD.
- Hoặc tải lại `ClaudeCAD-Setup.exe` ở link trên và chạy đè. Skill cậu đã thêm/sửa, macro, mẫu lệnh, lịch sử chat đều được giữ nguyên.

## Cài cho nhiều máy

- Cài ngầm, không hỏi gì: `ClaudeCAD-Setup.exe /VERYSILENT` (mỗi máy vẫn cần bấm **Đăng nhập Claude** một lần).
- Hoặc bằng PowerShell: giải nén `ClaudeCAD-<version>.zip`, đặt `install.ps1` cạnh thư mục `ClaudeCAD.bundle`, chạy `powershell -ExecutionPolicy Bypass -File install.ps1`.

## Gỡ cài đặt

Windows **Settings → Apps → ClaudeCAD for AutoCAD → Uninstall** (hoặc `install.ps1 -Uninstall`). Dữ liệu của cậu (lịch sử chat, macro, mẫu lệnh) nằm trong `%LocalAppData%\ClaudeCAD` và `%AppData%\ClaudeCAD`, xóa tay nếu không cần nữa.

## Lịch sử phiên bản

| Phiên bản | Nội dung chính |
| --- | --- |
| **0.9.1** | Cài 3 bước: kèm Node.js, nút **Đăng nhập Claude** ngay trong khung chat (không cần cài Claude Code / mở terminal), tự thêm thư mục plugin vào danh sách tin cậy của AutoCAD (hết cảnh báo SECURELOAD), thẻ **Bắt đầu** lần đầu mở AutoCAD, link tải cố định. |
| 0.9.0 | Lệnh `CLAUDECHATSELFTEST`; so biên bản lúc AutoCAD rảnh, lưu phiên bản nhanh, bản vẽ lớn cập nhật dần, macro chạy theo lô; sửa 14 lỗi từ lượt soát code thứ ba. |
| 0.8.0 | Chọn layer rồi chat chỉnh sửa: layer của vùng chọn (bỏ tick, → Cả layer), nút **Layer**. |
| 0.7.0 | Claude biết phần cậu sửa tay (kể cả lần mở sau / máy khác); lịch sử phiên bản 30 phút một lần, giữ 10 bản, mở tab chỉ xem. |
| 0.6.0 | Claude tự nhìn bản vẽ, nút Chụp, gợi ý bước tiếp theo, macro, lưu mẫu lệnh, tạo skill từ cuộc chat, Chỉ xem, ghi chú dự án, gõ `@`, phím tắt, thẻ kế hoạch, bảng Thay đổi. |
| 0.5.0 | **+ Add skill** và quản lý skill trên khung chat. |
| 0.4.0 | Khung chat 20 tính năng (chọn đối tượng/điểm, ảnh, Excel, mẫu `/`, lịch sử theo bản vẽ, hoàn tác lượt, …). |
| 0.3.0 | AutoCAD 2022–2025, bộ cài, cập nhật tự động. |

Chi tiết từng tính năng: [`docs/palette-features.md`](../docs/palette-features.md). Danh sách cần thử trên AutoCAD thật: [`docs/test-log.md`](../docs/test-log.md).
