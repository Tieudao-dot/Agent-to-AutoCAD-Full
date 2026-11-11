# Trạng thái nhánh `claude/test-checklist`

san-sang-gop: 98ca0ed

- **Đã thêm:** `docs/checklist-thu-that.md` (một lượt ~20 phút cho bộ cài gộp, phần A là bài test key) và 8 mục mới
  trong `CLAUDECHATSELFTEST`: bản plugin đúng đời AutoCAD, theme palette, style, icon thanh trạng thái, thu gọn /
  mở lại, hàng chờ gửi, skill theo đoạn chat, bản vẽ của người dùng không bị đụng.
- **CI:** `build.yml` (release = false) xanh trên `b4ee1cd`, lượt 42989148543; `98ca0ed` và commit chứa file này chỉ
  sửa tài liệu.
- **Dễ xung đột:** `plugin/ClaudeCAD.Plugin/SelfTest.cs` (nhánh 2026/2027 sửa dòng "Phiên bản AutoCAD / plugin" —
  nhánh này không đụng dòng đó, giữ cả hai); dòng 0.9.5 trong `bo-cai/README.md`; `docs/test-log.md` (thêm ST-2 … ST-5
  sau V6-10 và 1 dòng đầu bảng kiểm chứng tự động).
- **Chỉ đổi 1 dòng (thêm `partial`)** ở `ChatController.cs`, `UI/ChatControl.cs`, `UI/StatusTray.cs`,
  `UI/ChatPalette.cs`, `UI/AcadTheme.cs`; phần mới nằm ở các file `*.SelfTest.cs` và `Core/SelfTestRules.cs`.
- **Sau khi gộp nhánh 2026/2027:** `SelfTestRules.ExpectedBuild` đã tính sẵn R25.1 → net8 và R26.0 → net10, không
  phải sửa. Chạy: gõ `CLAUDECHATSELFTEST` trong AutoCAD; luật ✓ / ✗ có unit test `SelfTestRulesTests`.
