# ClaudeCAD — chat với Claude trong AutoCAD 2022

Plugin cho AutoCAD 2022 (bản full, Windows): chat tiếng Việt trong một palette, Claude **vẽ, đọc, sửa, xóa đối tượng trực tiếp trên bản vẽ đang mở**. Spec đầy đủ: [`docs/spec.md`](docs/spec.md). Tiến độ từng phase: [`docs/progress.md`](docs/progress.md).

```
plugin/   C# .NET Framework 4.8 — ClaudeCAD.Plugin (palette WPF, IPC server, tool handlers) + unit test lõi
agent/    Node.js/TypeScript sidecar — @anthropic-ai/claude-agent-sdk, định nghĩa tool, load skill
skills/   skill mẫu (chuẩn vẽ của người dùng, sửa không cần build)
bundle/   PackageContents.xml + script đóng gói ClaudeCAD.bundle
docs/     spec, giao thức IPC, ghi chú thiết kế, tiến độ, nhật ký smoke test
```

Kiến trúc: AutoCAD nạp `ClaudeCAD.Plugin.dll` → lệnh `CLAUDECHAT` mở palette, plugin mở named pipe `claudecad-<pid>` và spawn `node agent/dist/index.js`. Sidecar chạy Claude (động cơ Claude Code qua Agent SDK); mỗi tool call của Claude được gửi về plugin qua pipe (JSON Lines), plugin thực thi trên main thread trong `DocumentLock` + `Transaction`. Chi tiết: [`docs/ipc-protocol.md`](docs/ipc-protocol.md), [`docs/development.md`](docs/development.md).

---

## 1. Yêu cầu

| Hạng mục | Yêu cầu |
| --- | --- |
| Máy | Windows 10/11 x64, AutoCAD 2022 bản full (không hỗ trợ LT) |
| Build plugin | Visual Studio 2022 (workload *.NET desktop development*) hoặc .NET SDK 8 (`dotnet build`) |
| Sidecar | Node.js >= 18 (khuyến nghị 20/22 LTS) có trong `PATH` |
| Tài khoản | Claude Pro/Max đã `claude login`, **hoặc** biến môi trường `ANTHROPIC_API_KEY` |

Đăng nhập Claude một lần (cài Claude Code CLI: `npm install -g @anthropic-ai/claude-code`):

```bash
claude login
```

Nếu dùng API key thay cho gói Pro/Max: đặt biến môi trường người dùng `ANTHROPIC_API_KEY` rồi mở lại AutoCAD.

## 2. Build

### 2.1. Sidecar (Node)

```bash
cd agent
```

```bash
npm install
```

```bash
npm run build
```

```bash
npm test
```

`npm test` chạy test tích hợp lớp IPC (serialize/deserialize, cắt dòng UTF-8, timeout, mất kết nối, tiến trình sidecar thật nối pipe) và test phiên agent với `query()` giả lập — không gọi Claude thật.

### 2.2. Plugin (C#)

Mở `plugin/ClaudeCAD.sln` bằng Visual Studio 2022, chọn **Release | x64**, Build. Hoặc dòng lệnh:

```bash
dotnet build plugin/ClaudeCAD.Plugin/ClaudeCAD.Plugin.csproj -c Release
```

Kết quả: `plugin/ClaudeCAD.Plugin/bin/Release/ClaudeCAD.Plugin.dll`.

- Nếu có `C:\Program Files\Autodesk\AutoCAD 2022\acmgd.dll`, project tham chiếu `accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll` từ thư mục cài (Copy Local = false). AutoCAD cài chỗ khác: thêm `-p:AcadDir="D:\Autodesk\AutoCAD 2022"`.
- Nếu không có AutoCAD (máy build/CI), project tự dùng package NuGet chính thức `AutoCAD.NET 24.1.51000` (= AutoCAD 2022) làm reference assemblies. Ép chế độ: `-p:AcadRefSource=Install` hoặc `-p:AcadRefSource=NuGet`.
- Không phụ thuộc Newtonsoft.Json (tránh xung đột phiên bản trong acad.exe): plugin có bộ JSON riêng.

Unit test phần lõi (JSON, kiểm tra tham số) chạy được trên mọi OS:

```bash
dotnet test plugin/ClaudeCAD.Core.Tests
```

## 3. Chạy thử khi phát triển (NETLOAD)

1. Build sidecar (`npm run build`) và plugin như trên.
2. Mở AutoCAD 2022, một bản vẽ trắng (acadiso.dwt).
3. Gõ `NETLOAD`, chọn `plugin\ClaudeCAD.Plugin\bin\Release\ClaudeCAD.Plugin.dll`. (Nếu AutoCAD hỏi về file không đáng tin cậy: chọn *Load*; hoặc thêm thư mục vào `TRUSTEDPATHS`.)
4. Gõ `CLAUDECHAT` → palette ClaudeCAD hiện ra, dòng trạng thái chuyển "Đã kết nối · claude-opus-5-5 · Claude login" sau vài giây ở lượt chat đầu.

Plugin tự tìm `agent\dist\index.js` và `skills\` bằng cách đi ngược từ thư mục dll lên (layout repo), hoặc cạnh dll (layout bundle). Ghi đè bằng biến môi trường:

| Biến | Ý nghĩa |
| --- | --- |
| `CLAUDECAD_AGENT_DIR` | thư mục `agent` (chứa `dist\index.js`) |
| `CLAUDECAD_SKILLS_DIR` | thư mục skill |
| `CLAUDECAD_NODE` | đường dẫn `node.exe` |
| `CLAUDECAD_MODEL` | model (mặc định `claude-opus-5-5`, dự phòng `claude-sonnet-5-5` qua `CLAUDECAD_FALLBACK_MODEL`) |
| `CLAUDECAD_TOOL_TIMEOUT_MS` | timeout mỗi tool call (mặc định 30000) |

Lệnh AutoCAD:

| Lệnh | Tác dụng |
| --- | --- |
| `CLAUDECHAT` | mở palette chat |
| `CLAUDECHATRESTART` | khởi động lại sidecar (khi báo lỗi liên tục) |
| `CLAUDECHATLOG` | mở thư mục log `%LocalAppData%\ClaudeCAD\logs` |

Palette: Enter gửi, Shift+Enter xuống dòng, **Stop** hủy lượt đang chạy, **New** tạo phiên chat mới. Sau mỗi lượt, gõ `U` một lần trong AutoCAD là hoàn tác toàn bộ thay đổi của lượt đó.

## 4. Chạy thử theo từng phase

Ghi kết quả vào [`docs/test-log.md`](docs/test-log.md).

### Phase 1a — khung xương

- NETLOAD + `CLAUDECHAT`: palette hiện, không lỗi trên command line.
- Gõ "chào Claude, cậu là ai?" → chữ trả lời chạy dần (stream) trong palette.
- Gõ "kiểm tra kết nối với plugin" → Claude gọi tool `ping`, dòng trạng thái hiện "Kiểm tra kết nối plugin", câu trả lời có phiên bản AutoCAD + tên bản vẽ (IPC hai chiều).
- Bấm Stop giữa lúc Claude đang viết dài → lượt dừng, nút Gửi bật lại.
- Mở Task Manager, kill tiến trình `node.exe` của ClaudeCAD → palette báo mất kết nối, tự khởi động lại (tối đa 3 lần liên tiếp), chat tiếp được.

### Phase 1b — vẽ hình học, layer, batch, undo theo lượt

Tool mới: `draw_line`, `draw_polyline`, `draw_circle`, `draw_arc`, `draw_ellipse`, `draw_spline`, `draw_rectangle`, `draw_polygon`, `draw_point`, `draw_xline`, `draw_ray`, `draw_hatch`, `draw_wipeout`, `draw_revcloud`, `create_layer`, `list_layers`, `set_current_layer`, `set_layer_props`, `batch_execute`, `get_drawing_info`.

- "Vẽ lưới trục 5x4 bước 4000" → layer trục được tạo (linetype CENTER), 5 + 4 trục bằng một `batch_execute` (xem dòng trạng thái "Thực thi nhiều lệnh").
- Gõ `U` một lần → toàn bộ lưới trục (và layer vừa tạo) biến mất. `REDO` đưa lại.
- "Vẽ hình chữ nhật 4x6m, hatch ANSI31 bên trong, thêm mây sửa đổi quanh nó" → kiểm tra hatch, revcloud.
- "Vẽ 200 đoạn thẳng song song cách nhau 100" → trong log plugin tìm dòng `tool batch_execute ok in N ms`, N < 2000.
- Khóa một layer rồi bảo Claude vẽ lên layer đó → Claude nhận lỗi `LOCKED` và hỏi lại cậu.

### Phase 1c — đọc/truy vấn, sửa đổi, chốt xác nhận

Tool mới: `list_entities`, `count_entities`, `get_entity`, `find_text`, `measure_distance`, `measure_area`, `get_bounding_box`, `move_entities`, `copy_entities`, `rotate_entities`, `scale_entities`, `mirror_entities`, `offset_entity`, `array_rect`, `array_polar`, `fillet_entities`, `chamfer_entities`, `explode_entities`, `join_entities`, `erase_entities`, `set_entity_props`, `edit_text`, `edit_polyline`.

- Chuẩn bị: vài TEXT/MTEXT cao 250 (lệnh TEXT) → "Đổi hết text cao 250 thành 300" → mọi text 250 thành 300, text khác giữ nguyên.
- Kịch bản 3: có các text "WC", "WC 1", "Phòng WC" → "Tìm tất cả text chứa 'WC' và đổi thành 'VỆ SINH'".
- Kịch bản 4: tạo layer A-TEMP có vài đối tượng → "Xóa toàn bộ đối tượng trên layer A-TEMP" → Claude báo số lượng và hỏi; trả lời "đồng ý" mới xóa. Trả lời "không" → không xóa.
- Kịch bản 5: vài polyline kín màu đỏ → "Đo diện tích polyline tô màu đỏ lớn nhất" → đúng m².
- Offset ("offset đường này 220 vào trong"), fillet/chamfer hai line, join, explode, mirror (text vẫn đọc xuôi).
- Chốt cứng: bất kỳ lượt nào xóa/sửa > 20 đối tượng mà tin nhắn của cậu không phải câu xác nhận → sidecar từ chối, Claude phải hỏi lại. `erase_entities` > 1000 handle → plugin trả `TOO_MANY_ENTITIES`.

## 5. Đóng gói bundle (autoloader)

Xem [`bundle/README.md`](bundle/README.md) (thêm ở phase sau): tạo `ClaudeCAD.bundle` đặt vào `%AppData%\Autodesk\ApplicationPlugins` để AutoCAD tự nạp, không cần NETLOAD.

## 6. Log và gỡ lỗi

- Plugin: `%LocalAppData%\ClaudeCAD\logs\claudecad-YYYYMMDD.log` (mọi request/response IPC — cắt ngắn tham số dài — và exception kèm stack trace).
- Sidecar: `%LocalAppData%\ClaudeCAD\logs\sidecar-YYYYMMDD.log`.
- Phiên Claude (transcript) nằm trong `%LocalAppData%\ClaudeCAD\workspace` (thư mục làm việc của sidecar).
