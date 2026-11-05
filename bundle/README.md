# Đóng gói ClaudeCAD.bundle

AutoCAD tự nạp plugin đặt trong `%AppData%\Autodesk\ApplicationPlugins` theo chuẩn autoloader (đọc `PackageContents.xml`).

## Tạo bundle

Từ thư mục gốc repo, sau khi build plugin (Release) và sidecar:

```bash
node bundle/make-bundle.mjs
```

Kết quả: `bundle/out/ClaudeCAD.bundle/` gồm `PackageContents.xml` và `Contents/` (dll, `agent/` có `node_modules` cho Windows x64, `skills/`, README). Script chạy được cả trên Linux: `npm ci --os=win32 --cpu=x64` tải đúng binary Claude Code cho Windows.

Tùy chọn: `--dll <đường dẫn dll>`, `--out <thư mục>`, `--skip-npm` (không cài node_modules).

## Cài đặt

1. Đóng AutoCAD.
2. Copy cả thư mục `ClaudeCAD.bundle` vào `%AppData%\Autodesk\ApplicationPlugins\` (chỉ cho user hiện tại) hoặc `%ProgramData%\Autodesk\ApplicationPlugins\` (mọi user).
3. Mở AutoCAD 2022 → command line hiện "ClaudeCAD đã nạp" → gõ `CLAUDECHAT`.
4. Nếu AutoCAD cảnh báo thư mục không tin cậy (SECURELOAD): thêm đường dẫn bundle vào `TRUSTEDPATHS` hoặc chọn *Always Load*.

Gỡ: xóa thư mục `ClaudeCAD.bundle`.

Máy chạy vẫn cần Node.js >= 18 trong PATH (hoặc đặt `CLAUDECAD_NODE`), và đã `claude login` hoặc có `ANTHROPIC_API_KEY`.
