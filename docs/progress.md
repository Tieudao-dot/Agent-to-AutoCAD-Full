# Tiến độ triển khai ClaudeCAD

Nhánh làm việc: `claude/wizardly-heisenberg-ugup5c` (nhánh được cấp cho phiên cloud này; không merge vào `main` khi chưa được đồng ý).

Chú thích: "Cloud" = đã kiểm chứng tự động trên Linux (build + test). "Cần test" = phải chạy trên Windows + AutoCAD 2022 thật (ghi vào `docs/test-log.md`).

## Phase 1a — khung xương ✅ code xong

Đã làm:
- Plugin C# (`plugin/ClaudeCAD.Plugin`): `IExtensionApplication`, lệnh `CLAUDECHAT` / `CLAUDECHATRESTART` / `CLAUDECHATLOG`; palette WPF (lịch sử chat, ô nhập Enter/Shift+Enter, text stream, dòng trạng thái tool, Stop, New); `IpcServer` named pipe `claudecad-{pid}` JSON Lines; `SidecarManager` spawn `node agent/dist/index.js`, tự restart tối đa 3 lần, job object để kill node khi AutoCAD đóng; `ToolDispatcher` (map tool → handler, validate, transaction, error envelope); `UndoScope`; `Logger`; tool `ping` (echo hai chiều).
- Sidecar (`agent/`): Agent SDK `query()` streaming input, in-process MCP server `claudecad`, quyền chỉ bộ tool ClaudeCAD + Read/Skill, stream text/tool_status/turn_end, cancel bằng `interrupt()`, new session, timeout 30 s mỗi request, log file.

Cloud: sidecar build ✅, 32/32 test IPC + session ✅; plugin build net48 ✅; 16/16 unit test lõi C# ✅.

Cần test trên AutoCAD: mục "Phase 1a" trong `docs/test-log.md` (NETLOAD, palette, chat stream, `ping`, Stop, New, kill node.exe).
