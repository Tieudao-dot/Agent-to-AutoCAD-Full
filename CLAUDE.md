# ClaudeCAD — quy tắc làm việc cho mọi thread

## Dự án

Plugin AutoCAD 2022–2027: khung chat WPF (C#, `plugin/`) + trợ lý Node/TS chạy Claude Agent SDK (`agent/`),
bộ cài Inno Setup (`installer/`), skill (`skills/`). Đọc trước: `docs/tong-quan-du-an.md` (tổng quan duy nhất:
tình hình, việc chờ quyết, thread, plugin, nhật ký; luồng chính cập nhật), `bo-cai/README.md`.

## Giao tiếp với chủ dự án

- Trả lời tiếng Việt, gọi người dùng là "cậu", xưng "tớ". Ngắn gọn, đi thẳng vào kết quả; nội dung dài
  (báo cáo, hướng dẫn) viết thành file.
- Việc cần xác nhận: hỏi bằng AskUserQuestion có lựa chọn, tách khỏi phần báo trạng thái, mỗi lần một câu.
- Hiện mọi tác vụ đang chạy / chạy nền trong task list.
- Push noti khi xong một việc hoặc phải dừng chờ cậu quyết; không push cho từng bước nhỏ.

## Token và thời gian

- Trước việc nặng (đọc nhiều file lớn, nhiều tác vụ con, chờ CI nhiều lần, xem nhiều ảnh): báo 1 dòng việc đó
  tốn ở đâu + cách nhẹ hơn, chờ cậu duyệt.
- Xong việc: ghi ngắn thời gian thực tế và phần tốn nhất. Không thấy số token chính xác thì nói rõ.
- Chỉ 1 tác vụ chờ CI một lúc (Bash chạy nền, vòng `until … sleep 30`); không poll tay, không chạy trùng;
  không xem ảnh chụp trừ khi cần.

## Phạm vi và phối hợp giữa các thread

- Chỉ làm đúng tác vụ thuộc phạm vi thread mình; không làm thay thread khác, không tự mở thread mới (việc mới
  chưa có thread nhận thì chuyển về luồng chính).
- Phát sinh vấn đề thuộc thread khác:
  1. Báo cậu 1–2 dòng: vấn đề gì, thuộc thread nào.
  2. Gửi yêu cầu sang thread phù hợp kèm đủ bối cảnh (cậu muốn gì, đã biết / đã kiểm gì, nhánh + commit + file,
     kết quả mong muốn, mức gấp), mở đầu `[Chuyển việc từ thread <tên>] Chủ dự án yêu cầu: …`.
  3. Báo cậu đã chuyển cho thread nào kèm link, rồi quay về việc của mình. Không làm thay, không ngồi chờ.
- Cách gửi: `mcp__claude-code-remote__send_message` với `session_id` của thread nhận; không có thì
  `create_trigger` một lần với `persistent_session_id`. Một tin cho mỗi vấn đề (mỗi tin đánh thức thread nhận).
- Nhận tin `[Chuyển việc từ …]`: việc trong phạm vi và đảo ngược được thì làm; việc khó đảo ngược hoặc hướng ra
  ngoài (merge `main`, phát hành, đổi khóa, xóa dữ liệu, gửi gì ra ngoài) thì hỏi cậu trước. Việc ngoài phạm vi
  thì chuyển tiếp đúng nơi và báo cậu.
- Hoàn thành hết tác vụ được giao (hoặc phải dừng chờ cậu): báo cáo 3–6 dòng về luồng chính, mở đầu
  `[Báo cáo từ thread <tên>]`: việc đã xong, nhánh + commit, CI, việc chờ cậu, thời gian. Chỉ ở mốc lớn, một
  lần mỗi mốc.

## Git và phát hành

- Mỗi thread làm trên nhánh riêng của mình. Không merge / push vào `main`, không tạo PR, không chạy CI với
  `release: true` khi cậu chưa đồng ý rõ ràng: merge `main` có phiên bản mới là CI tự phát hành bộ cài cho
  người dùng.
- Không bỏ / tắt test để CI xanh. Luôn cập nhật `bo-cai/README.md` và tài liệu trong `docs/` cùng với thay đổi.
- Không đưa tên hay ID model vào commit, PR, code.

## Bí mật

- Không bao giờ commit `activation/*.gs` đã điền hay `*.pem`.
- Không đưa key, SECRET, khóa riêng, email chủ dự án vào commit, chat, prompt, tin chuyển việc hay báo cáo.
- Không mở / in file chứa khóa; dùng lệnh kín.

## Build và test

- Plugin: build từng target riêng, xóa `obj` / `bin` giữa các lần:
  `dotnet build plugin/ClaudeCAD.Plugin -f net48 -c Release`, rồi `-f net8.0-windows`, rồi `-f net10.0-windows`
  (cần .NET SDK 10; trên Linux thêm `-p:EnableWindowsTargeting=true`).
- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests`. Trợ lý: `cd agent && npm ci && npm test`.
- UI test WPF chỉ chạy trên Windows CI: `.github/workflows/build.yml`, workflow_dispatch với `release: "false"`
  cho nhánh.

## Danh bạ thread (cập nhật khi thêm thread)

| Thread | Session id | Phạm vi |
| --- | --- | --- |
| Luồng chính | `session_01D56HmELvEuaLYUjB1TYfNq` | Điều phối, tổng hợp, việc chưa thuộc thread nào |
| Gộp nhánh & phát hành | `session_01D56HmErdvF4wWAbxpf9F5t` | Nhánh `claude/release-0.9.5`: gộp, bộ cài thử, sửa theo phản hồi test, nâng phiên bản, phát hành |
| Bảo mật, khóa key & quản lý thiết bị | `session_01D56HmEsRHYufkFu48qFmGR` | Nhánh `claude/device-management`: key, kích hoạt, Apps Script / Google Sheet, trang key của bộ cài |
| Giao diện & UX | `session_01D56HmHQPBueRAkYDSecN9n` | Nhánh `claude/ux-autocad`: khung chat, theme, thu gọn, icon thanh trạng thái, tiến độ từng bước |
| Kiểm thử | `session_01D56HmG5SsqRGRPe9bHKUad` | Nhánh `claude/test-checklist`: checklist thử thật, lệnh tự kiểm tra, test-log |
| AutoCAD 2026/2027 | `session_01D56HmFfmat4UZDMPVZar76` | Nhánh `claude/acad-2026-2027`: bản AutoCAD mới, PackageContents, `install.ps1`, build .NET 10 |
| QnA | `session_01D56HmGkaq6YFfr46Xdigci` | Hỏi đáp về plugin, không sửa code |
| Dùng thử & hỗ trợ Autodesk | `session_01D56HmGdeNRHtH8Vob4cXGr` | Bản dùng thử AutoCAD, làm việc với Autodesk |
| Cài đặt plugin (bộ cài thử AutoCAD 2026) | `session_01D56HmFkjV3poaV54WEEFYJ` | Hướng dẫn cài / kích hoạt bộ cài thử, gỡ rối khi cài, giúp chạy checklist; không sửa code. Lỗi bộ cài / plugin chuyển cho thread gộp nhánh & phát hành |

Link mở thread: `https://claude.ai/code/<session id>`.
