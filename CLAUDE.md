# Agent to AutoCAD Full — quy tắc làm việc cho mọi thread

## Dự án

Repo `Tieudao-dot/Agent-to-AutoCAD-Full`: bản sao đầy đủ của plugin ClaudeCAD (chép 12/11/2026) để giữ bản 2D và
phát triển tiếp **vẽ 3D** cùng **gói dịch vụ theo máy (2D / 3D / 2D + 3D)**.

Plugin AutoCAD 2022–2027: khung chat WPF (C#, `plugin/`) + trợ lý Node/TS chạy Claude Agent SDK (`agent/`),
bộ cài Inno Setup (`installer/`), skill (`skills/`). Đọc trước: `docs/ke-hoach-3d-goi-dich-vu.md` (kế hoạch 3D và
gói), `docs/cho-tro-ve-repo-goc.md` (các chỗ còn trỏ về dự án gốc), `docs/tong-quan-du-an.md` (tổng quan chép từ
dự án gốc), `bo-cai/README.md`.

## Độc lập với dự án gốc (bắt buộc)

- Không push, không sửa gì ở repo gốc `Tieudao-dot/Agent-to-Autocad`.
- Không nhắn tin hay đánh thức thread nào của dự án gốc. Chỉ dùng danh bạ ở cuối file này.
- Không đụng máy chủ kích hoạt (Apps Script), Google Sheet "Thiết bị" hay secret CI của dự án gốc. Bản Full sẽ có
  máy chủ kích hoạt riêng; chủ dự án quyết cách làm.
- Các chỗ trong code, bộ cài, CI còn trỏ về dự án gốc: chỉ sửa khi chủ dự án đã chốt (xem
  `docs/cho-tro-ve-repo-goc.md`).

## Nhánh

- `main` = v0.9.3 (mã đã phát hành của bản gốc).
- `luu-tru/2d-base` = bản 2D mới nhất lúc tách, chưa thử trên AutoCAD thật. Nhánh lưu trữ: **không sửa, không xóa**.
- Các nhánh `claude/*` có sẵn lúc tách là bản chép lưu trữ của dự án gốc: không sửa.
- Proxy chặn push tag, nên lưu trữ bằng nhánh.
- Repo chưa có secret CI nên bước phát hành của CI bị chặn. Giữ nguyên tới khi chủ dự án tự cài.

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

- Hiện chỉ có luồng chính; mọi việc do luồng chính nhận và báo cáo thẳng cho chủ dự án.
- Không tự mở thread mới. Khi chủ dự án mở thêm thread, ghi vào danh bạ ở cuối file.
- Khi đã có nhiều thread:
  - mỗi thread chỉ làm việc trong phạm vi của mình; việc của thread khác thì báo cậu 1–2 dòng rồi gửi một tin
    `[Chuyển việc từ thread <tên>] Chủ dự án yêu cầu: …` kèm đủ bối cảnh (nhánh, commit, file, kết quả mong muốn,
    mức gấp) qua `mcp__claude-code-remote__send_message`, không làm thay và không ngồi chờ;
  - nhận tin chuyển việc: việc trong phạm vi và đảo ngược được thì làm; việc khó đảo ngược hoặc hướng ra ngoài
    (merge `main`, phát hành, đổi khóa, xóa dữ liệu, gửi gì ra ngoài) thì hỏi cậu trước;
  - xong mốc lớn thì báo 3–6 dòng về luồng chính, mở đầu `[Báo cáo từ thread <tên>]`.

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
| Luồng chính | `session_01D56HmFT5bwapmbypmLY5nr` | Điều phối và làm mọi việc của dự án Full: gói dịch vụ, vẽ 3D, bộ cài, tài liệu |

Link mở thread: `https://claude.ai/code/<session id>`.
