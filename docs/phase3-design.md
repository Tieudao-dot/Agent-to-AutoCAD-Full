# Phase 3 — quyết định thiết kế

Phạm vi theo spec §11: đóng gói installer (kèm kiểm tra Node), cập nhật tự động, hỗ trợ AutoCAD 2023–2025.

## Hỗ trợ AutoCAD 2022–2025

| AutoCAD | Release | .NET | Bản build | Nằm ở |
| --- | --- | --- | --- | --- |
| 2022 | R24.1 | .NET Framework 4.8 | `net48` | `Contents\ClaudeCAD.Plugin.dll` |
| 2023 | R24.2 | .NET Framework 4.8 | `net48` (cùng họ R24, API tương thích nhị phân) | như trên |
| 2024 | R24.3 | .NET Framework 4.8 | `net48` | như trên |
| 2025 | R25.0 | .NET 8 | `net8.0-windows` (tham chiếu `AutoCAD.NET 25.0.1`) | `Contents\2025\ClaudeCAD.Plugin.dll` |

- Một project multi-target (`<TargetFrameworks>net48;net8.0-windows</TargetFrameworks>`), cùng một bộ mã nguồn. Hằng `ACAD_R24` / `ACAD_R25` cho phần khác biệt (hiện chỉ có TLS 1.2 cho HttpClient trên net48).
- Bản net8 dùng `FrameworkReference Microsoft.WindowsDesktop.App` (WPF + WinForms) thay vì `UseWPF`, vì UI viết bằng code — nhờ vậy build được cả trên Linux/CI không có WindowsDesktop SDK.
- `PackageContents.xml` có hai `Components`, mỗi cái `RuntimeRequirements` theo SeriesMin/SeriesMax riêng → autoloader chọn đúng dll theo phiên bản AutoCAD.
- Sidecar Node và skill dùng chung cho mọi phiên bản.

## Installer

- `installer/install.ps1` (PowerShell 5.1+): kiểm tra AutoCAD đã đóng; kiểm tra **Node.js >= 18** (PATH, `%ProgramFiles%\nodejs`), đề nghị cài bằng `winget install OpenJS.NodeJS.LTS` hoặc mở trang tải; copy bundle vào `%AppData%` (hoặc `%ProgramData%` với `-AllUsers`) bằng robocopy; **giữ nguyên skill người dùng đã sửa** (chỉ thêm tệp mới); kiểm tra đăng nhập Claude (`ANTHROPIC_API_KEY` hoặc `~\.claude\.credentials.json`) và hướng dẫn `claude login`; `-Uninstall` để gỡ.
- `installer/ClaudeCAD.iss` (Inno Setup 6): tạo `ClaudeCAD-Setup-<version>.exe` cài cho user hiện tại (không cần quyền admin), kiểm tra Node.js khi bắt đầu, skill cài với `onlyifdoesntexist` (không ghi đè), nhắc đăng nhập Claude khi cài xong.

## Cập nhật tự động

1. Mỗi ngày một lần, khi mở palette, plugin tải manifest `latest.json` `{version, url, sha256, notes}` từ `CLAUDECAD_UPDATE_URL` hoặc `Contents\update-config.json` (`manifest_url`). CI ghi `update-config.json` trỏ tới `releases/latest/download/latest.json` của repo.
2. Có bản mới → palette báo. Lệnh `CLAUDECHATUPDATE` tải zip, **kiểm tra SHA-256**, giải nén vào `%LocalAppData%\ClaudeCAD\updates\stage`, rồi chạy nền một script `.cmd` chờ `acad.exe` thoát (dll đang bị khóa khi AutoCAD chạy) và mới copy đè bundle — skill đã sửa được giữ nguyên.
3. Repo private: release asset cần xác thực → đặt `CLAUDECAD_UPDATE_TOKEN` (gửi Bearer), hoặc đơn giản hơn là đăng `latest.json` + zip ở một chỗ HTTPS công khai (repo release công khai, máy chủ nội bộ) và trỏ `CLAUDECAD_UPDATE_URL` tới đó.
- Phần logic thuần (parse manifest, so phiên bản, chu kỳ 24h, SHA-256, sinh script) nằm ở `Core/UpdateLogic.cs` và có unit test.

## CI / phát hành

`.github/workflows/build.yml` trên `windows-latest`: `npm ci` + `npm test` (test IPC chạy với **named pipe Windows thật**), `dotnet test`, build cả hai target, `make-bundle --zip`, Inno Setup → artifact `ClaudeCAD-<version>` (zip, Setup.exe, latest.json, install.ps1). Push tag `v<version>` → tạo GitHub Release với các file đó (nguồn cho cập nhật tự động).

Phiên bản thống nhất ở `agent/package.json`, `agent/src/index.ts`, `ClaudeCAD.Plugin.csproj`, `bundle/PackageContents.xml` (có test kiểm tra).
