# Bàn giao: Test & sửa đợt F4 (người rà)

Nhánh `full/nang-cap-f4-test`, tách từ `full/nang-cap-f4` @ `d6c15d6` (code cuối `587f7ce`). Rà phần F4 so với bản gộp
`c67bc79` (`git diff c67bc79 d6c15d6`), đối chiếu Claude Agent SDK trong `agent/node_modules`, thêm test, sửa lỗi.
Chưa thử với AutoCAD / Claude thật.

## Lỗi tìm được và đã sửa

| # | Lỗi | Mức | Sửa |
| --- | --- | --- | --- |
| NF4-1 | Không có đường lui khi nạp gọn mô tả lệnh làm Claude tìm sót lệnh (phần này chưa chạy với Claude thật): muốn nạp đủ như cũ phải sửa code và build lại | Trung bình (luồng chính yêu cầu) | **Công tắc tắt, không cần build lại:** biến môi trường `CLAUDECAD_DEFER_TOOLS=0` (`false` / `off` / `no`), hoặc file trống `%LocalAppData%\ClaudeCAD\tool-search-off`. Khi tắt: không có `ToolSearch` trong bộ công cụ và quyền, `ENABLE_TOOL_SEARCH=false`, server lệnh `alwaysLoad: true` (mọi lệnh nạp đủ), câu hướng dẫn tìm lệnh bị bỏ khỏi system prompt → đúng bộ công cụ trước F4. Mặc định vẫn bật nạp gọn. Log trợ lý ghi một dòng `tools: …` cho biết đang chạy kiểu nào (`agent/src/agent/session.ts` `deferToolsEnabled`, `agent/src/index.ts`) |
| NF4-2 | Macro chạy lại một bảng có `excel` (vd thống kê block có bảng trên bản vẽ + xuất Excel): lần chạy lại gặp "file đã có" nên **cả lệnh thất bại, mất luôn bảng trên bản vẽ**; nếu lần ghi có `overwrite: true` thì macro lặng lẽ ghi đè file khách có thể đã sửa | Trung bình | Bộ ghi macro bỏ `excel` / `overwrite` của 4 bảng (`MacroLogic.ExcelTools`, `MacroRecorder.Record`): macro chỉ vẽ lại bảng, không ghi file Excel. `overwrite` của lệnh khác (`define_block`) giữ nguyên |
| NF4-3 | Tên block / layer / chữ có ký tự XML không nhận (U+FFFE, U+FFFF, nửa cặp surrogate do chữ bị cắt) → Excel báo file hỏng | Nhỏ | `XlsxWriter.Esc` bỏ các ký tự đó; emoji (cặp đầy đủ) và tiếng Việt giữ nguyên |

Không có điểm cần chủ dự án chốt.

## Đã kiểm (không thấy lỗi)

**Nạp gọn mô tả lệnh** (SDK `@anthropic-ai/claude-agent-sdk` 0.3.318, CLI 2.1.318):
- `createSdkMcpServer` đưa `_meta` của từng lệnh vào `registerTool` (đọc `sdk.mjs`); `_meta["anthropic/alwaysLoad"]` là
  cờ SDK ghi trong `sdk.d.ts` ("never deferred behind tool search"); mặc định lệnh MCP được hoãn khi tool search bật.
  CLI có công cụ `ToolSearch` và biến `ENABLE_TOOL_SEARCH`; `true` còn bật lại tìm lệnh cho cuộc chat cũ đã chạy không có
  ToolSearch (thông báo trong CLI: "ENABLE_TOOL_SEARCH=true overrides this"). Nếu máy chủ API từ chối tìm lệnh, CLI tự
  tắt và gửi đủ (thông báo "the endpoint refused it") — vẫn chạy như cũ.
- Lệnh bị hoãn là **cùng đối tượng lệnh**, cùng đường gọi plugin: test gọi thẳng `rotate_entities`, `solid_union`,
  `set_material`, `block_schedule` (đều bị hoãn) → đi qua cầu nối như lệnh nạp sẵn. Các lớp chặn nằm ở cầu nối /
  phiên (thẻ xác nhận, luật > 20 đối tượng) và ở plugin (gói, chế độ, "Chỉ xem"), không phụ thuộc lệnh có nạp sẵn hay không;
  danh sách lệnh của gói vẫn lọc trước khi tạo server nên lệnh ngoài gói không có cả tên.
- `ToolSearch` không mở thêm công cụ: bộ công cụ gốc `['Read', 'Skill', 'ToolSearch']`, quyền tự cho chỉ gồm server CAD,
  `Read` trong thư mục skill, `Skill`, `ToolSearch`; `DISALLOWED_BUILTINS` (Bash, Write, Edit, Web…) không đổi;
  `permissionMode: dontAsk`, `strictMcpConfig`. Dòng test cũ trong `session.test.ts` chỉ thêm đúng `ToolSearch`, không
  nới chỗ khác (test mới kiểm lại).
- System prompt mọi gói / chế độ có câu chỉ cách tìm lệnh (`TOOL_SEARCH_HINT`); 25 lệnh nạp sẵn: máy 2D còn 19, máy 3D 22
  (phần ngoài gói tự bỏ), hợp lý.

**Khởi động sẵn:** nhiều lần yêu cầu → một tiến trình; đóng (AutoCAD đóng) thì hẹn giờ dựng lại bị hủy, không tiến trình
nào chạy sau đó (test mới); plugin chỉ gửi khi `Activation.IsAllowed` (đã kích hoạt, còn hạn, chủ dự án cho phép);
tin đầu đến khi tiến trình khởi động sẵn đang chạy thì dùng luôn tiến trình đó (một phiên, không mất tin — test của F4);
đổi chế độ áp dụng từ tin kế tiếp (tin mang chế độ mới → khởi động lại cùng cuộc chat).

**Excel:** `& < > " '`, ký tự điều khiển, chữ dạng công thức (`=`, `+`, `-`, `@`) luôn là chữ (ô inline string, không có
`<f>`); số ghi dấu chấm cả trên máy đặt vùng Việt Nam (test đổi `CurrentCulture` sang vi-VN); tên sheet dài / ký tự cấm /
dấu nháy; ghi hỏng không để lại file tạm; file đang mở báo lỗi dễ hiểu (`IOException` → "đóng Excel rồi thử lại"); đường
dẫn / ghi đè như STL; gói theo lệnh (2D: thống kê block, diện tích; 3D: khối lượng, va chạm); số không phụ thuộc UCS, đơn vị
mét đổi theo `mmPerUnit` như bảng trên bản vẽ.

**Bản gộp `c67bc79`:** mọi dòng của F2 (`full/nang-cap-f2-test`) và F3 đã rà (`full/nang-cap-f3-test`) có trong bản gộp; các
chỗ khác nhau chỉ là chỗ hai đợt cùng sửa một dòng và bản gộp ghép đủ cả hai (`Plans.cs`, `PLAN_SCOPE` gói 3D, thẻ xác
nhận, bước tự kiểm mái 120).

## Con số và quyết định

- Không đổi số nào của F4. Số mới: không có (công tắc là chữ / tên file: `CLAUDECAD_DEFER_TOOLS`, `tool-search-off`).
- Quyết định tự chọn thêm:

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Công tắc tắt nạp gọn | Biến môi trường (như `CLAUDECAD_MODEL`) hoặc file trống trong `%LocalAppData%\ClaudeCAD` (khách không phải biết biến môi trường); có hiệu lực khi trợ lý khởi động (mở lại AutoCAD) | Thêm một ô trong ⚙ của khung chat (cần plugin gửi cờ cho trợ lý) |
| Macro và Excel | Macro không ghi file Excel (chỉ vẽ lại) | Cho macro ghi file với tên mới mỗi lần |
| Máy bị chủ dự án khóa giữa chừng | Trợ lý vẫn có thể giữ một tiến trình Claude chờ (không gửi gì, không tốn token) tới khi đóng AutoCAD | Thêm tin "dừng khởi động sẵn" từ plugin |

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **1126** (1113 + 13 `NangCapF4TestSuaTests.cs`).
- Trợ lý `cd agent && npm ci && npm test`: **355** (347 + 8 `test/nangcapf4-testsua.test.ts`). Không sửa / bỏ test cũ.
- Build 3 target (xóa obj / bin giữa các lần): xem CI.

## CI

*(điền sau khi chạy)*

## Chưa kiểm được

- Claude Code thật: hoãn lệnh và tìm lệnh bằng ToolSearch, số token thật, công tắc tắt (chỉ kiểm các tùy chọn trao cho
  SDK), tiến trình khởi động sẵn không gọi API trước tin đầu (theo thiết kế SDK: chỉ gửi khi có tin; không đo được ở đây).
- Excel thật mở file (test đọc lại cấu trúc XML); thông báo khi file đang mở trong Excel (khóa file chỉ có trên Windows).
- Khối thử tay: `docs/checklist-dot-f4.md` (thêm mục C cho 3 sửa trên).
