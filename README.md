# ClaudeCAD — chat với Claude trong AutoCAD 2022–2025

Plugin cho AutoCAD 2022 (bản full, Windows; từ phase 3 hỗ trợ cả 2023, 2024, 2025): chat tiếng Việt trong một palette, Claude **vẽ, đọc, sửa, xóa đối tượng trực tiếp trên bản vẽ đang mở**. Spec đầy đủ: [`docs/spec.md`](docs/spec.md). Tiến độ từng phase: [`docs/progress.md`](docs/progress.md).

```
plugin/   C# .NET Framework 4.8 — ClaudeCAD.Plugin (palette WPF, IPC server, tool handlers) + unit test lõi
agent/    Node.js/TypeScript sidecar — @anthropic-ai/claude-agent-sdk, định nghĩa tool, load skill
skills/   skill mẫu (chuẩn vẽ của người dùng, sửa không cần build)
bundle/   PackageContents.xml + script đóng gói ClaudeCAD.bundle
docs/     spec, giao thức IPC, ghi chú thiết kế, tiến độ, nhật ký smoke test
installer/ install.ps1 (kiểm tra Node, cài/gỡ bundle) + ClaudeCAD.iss (Setup.exe)
```

Kiến trúc: AutoCAD nạp `ClaudeCAD.Plugin.dll` → lệnh `CLAUDECHAT` mở palette, plugin mở named pipe `claudecad-<pid>` và spawn `node agent/dist/index.js`. Sidecar chạy Claude (động cơ Claude Code qua Agent SDK); mỗi tool call của Claude được gửi về plugin qua pipe (JSON Lines), plugin thực thi trên main thread trong `DocumentLock` + `Transaction`. Chi tiết: [`docs/ipc-protocol.md`](docs/ipc-protocol.md), [`docs/development.md`](docs/development.md).

---

## 1. Yêu cầu

| Hạng mục | Yêu cầu |
| --- | --- |
| Máy | Windows 10/11 x64, AutoCAD 2022 bản full (không hỗ trợ LT); 2023/2024/2025 từ phase 3 |
| Build plugin | Visual Studio 2022 (workload *.NET desktop development*) hoặc .NET SDK 8 (`dotnet build`) |
| Sidecar | Node.js >= 18 (khuyến nghị 20/22 LTS) có trong `PATH` |
| Tài khoản | Claude Pro/Max đã `claude login`, **hoặc** biến môi trường `ANTHROPIC_API_KEY` |

Đăng nhập Claude một lần trên máy (cài Claude Code CLI: `npm install -g @anthropic-ai/claude-code`), rồi chạy:

```bash
claude
```

Lần đầu chạy, Claude Code hướng dẫn đăng nhập: chọn **tài khoản Claude có gói Pro/Max**, trình duyệt mở ra để đăng nhập, xong quay lại terminal. Nếu Claude Code đã mở mà chưa đăng nhập, gõ `/login`. Gõ `/exit` để thoát. Thông tin đăng nhập được lưu ở `%USERPROFILE%\.claude`, plugin dùng lại tự động mỗi lần mở AutoCAD — không cần mở Claude Code nữa.

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

Kết quả (từ phase 3 project build hai bản):
- `plugin/ClaudeCAD.Plugin/bin/Release/ClaudeCAD.Plugin.dll` — AutoCAD 2022/2023/2024 (.NET Framework 4.8)
- `plugin/ClaudeCAD.Plugin/bin/Release/acad2025/ClaudeCAD.Plugin.dll` — AutoCAD 2025 (.NET 8)

Chỉ build một bản: thêm `-f net48` hoặc `-f net8.0-windows`. AutoCAD 2025 cài chỗ khác: `-p:AcadDir2025="..."`.

- Nếu có `C:\Program Files\Autodesk\AutoCAD 2022\acmgd.dll`, project tham chiếu `accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll` từ thư mục cài (Copy Local = false). AutoCAD cài chỗ khác: thêm `-p:AcadDir="D:\Autodesk\AutoCAD 2022"`.
- Nếu không có AutoCAD (máy build/CI), project tự dùng package NuGet chính thức `AutoCAD.NET 24.1.51000` (= AutoCAD 2022) làm reference assemblies. Ép chế độ: `-p:AcadRefSource=Install` hoặc `-p:AcadRefSource=NuGet`.
- Không phụ thuộc Newtonsoft.Json (tránh xung đột phiên bản trong acad.exe): plugin có bộ JSON riêng.

Test giao diện WPF thật (chỉ chạy trên Windows, lưu ảnh chụp palette vào `bin\...\ui-screens`):

```bash
dotnet test plugin/ClaudeCAD.UI.Tests
```

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
| `CLAUDECHATSELFTEST` | tự kiểm tra trên một bản vẽ tạm (không đụng bản vẽ của cậu), mở báo cáo ✓/✗ — chạy lệnh này đầu tiên sau khi cài |
| `CC` | mở khung chat, con trỏ ở ô nhập (cũng là Ctrl+Shift+Space) |
| `CLAUDECHATUPDATE` | (phase 3) kiểm tra và tải bản cập nhật |
| `CLAUDECAD_PICK`, `CLAUDECAD_POINT` | lệnh phụ do nút **Chọn đối tượng** / **Lấy điểm** gọi (không cần gõ) |

Palette: Enter gửi, Shift+Enter xuống dòng, **Stop** hủy lượt đang chạy, **New** tạo phiên chat mới. Sau mỗi lượt, gõ `U` một lần trong AutoCAD (hoặc nút **Hoàn tác lượt này**) là hoàn tác toàn bộ thay đổi của lượt đó.

**Skill (v0.5.0):** nút **+ Add skill** trên khung chat → chọn file `.md` trong máy (mặc định mở thư mục skill của plugin) → plugin chép vào `Contents\skills\<tên>\SKILL.md`, ghi vào `Contents\skills\skills.json` và báo "✓ Đã thêm skill …". Nút **Skill (N)** mở danh sách để bật/tắt, sửa, xóa; ô **Chuẩn** tự liệt kê các skill đang bật. Chi tiết: [`skills/README.md`](skills/README.md).

Từ v0.4.0 khung chat có thêm 20 tính năng — chọn đối tượng / lấy điểm gửi kèm, handle bấm được để zoom, **Xem kết quả**, đính kèm ảnh, dán bảng Excel, mẫu lệnh `/`, chọn chuẩn + tỷ lệ in, ↑↓ gọi lại tin, thẻ xác nhận có nút, nhật ký thao tác, **Thử lại**, mức dùng + chọn model/effort, thông báo Windows, Markdown, lịch sử theo bản vẽ, Copy/Xuất, sáng/tối + cỡ chữ, **Sửa** và gửi lại. Hướng dẫn: [`docs/palette-features.md`](docs/palette-features.md).

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

### Phase 1d — ghi chú, kích thước, block, style, view

Tool mới: `add_text`, `add_mtext`, `add_dim_linear`, `add_dim_aligned`, `add_dim_angular`, `add_dim_radial`, `add_dim_diameter`, `add_dim_ordinate`, `add_dim_continue`, `add_dim_baseline`, `add_mleader`, `add_table`, `define_block`, `insert_block`, `list_block_defs`, `get_block_attributes`, `set_block_attributes`, `set_dynamic_props`, `create_text_style`, `create_dim_style`, `set_current_styles`, `list_styles`, `zoom_extents`, `zoom_window`, `regen`, `save_drawing`, `purge_unused`, `undo_last_turn`.

- "Vẽ mặt bằng phòng 4x6m, tường 220, cửa đi 900 ở cạnh dài, ghi kích thước" (kịch bản 2) → tường hai nét, cửa có cánh + cung mở (thường được định nghĩa thành block), dim đủ bốn phía, text tên phòng.
- "Tạo dim style tỷ lệ 1:100, chữ 2.5, mũi tên gạch chéo" → `create_dim_style` scale 100, arrow archtick; dim mới dùng style đó.
- Kịch bản 6: vẽ một cái ghế, "định nghĩa thành block GHE" → "Chèn block GHE thành dãy 3 hàng 8 cột giãn cách 600x900".
- "Lập bảng thống kê cửa" → `add_table`. "Ghi chú ống D110 vào đường này" → `add_mleader`.
- "Hoàn tác lượt trước" → `undo_last_turn` (gửi một lệnh U). `save_drawing` / `purge_unused` chỉ chạy khi cậu yêu cầu rõ.

### Khung chat v0.6 — tiện ích mới

- Mở nhanh: **Ctrl+Shift+Space** hoặc lệnh **CC**.
- "Vẽ cả mặt bằng tầng 1 nhà 5x20m" → thẻ **Kế hoạch** → **Làm**. Vẽ xong, Claude tự **nhìn bản vẽ** (`capture_view`) để soát lỗi; dưới câu trả lời có bảng **Thay đổi: Thêm · Sửa · Xóa** và các nút gợi ý bước tiếp theo.
- Nút **Chụp**: gửi vùng đang xem kèm câu "chỗ này sai, sửa lại".
- **💾 Lưu macro** sau một lượt ưng ý → **Macro (N) ▸ ▶ Chạy / 📍 Tại điểm** vẽ lại không cần Claude.
- **☆ Mẫu** lưu tin nhắn thành mẫu `/`; **Skill (N) ▸ ✨ Tạo skill từ cuộc chat này**.
- **🔒 Chỉ xem** cho bản vẽ quan trọng; **Ghi chú** cho thông số cố định của dự án; gõ **@** để chọn tên layer/block.

Chi tiết: [`docs/palette-features.md`](docs/palette-features.md) mục E.

### v0.7 — sửa tay và lịch sử phiên bản

- Sửa tay bản vẽ giữa các lượt chat (kể cả khi đóng khung chat, hay hôm sau mở lại) → tin nhắn tiếp theo tự kèm danh sách thay đổi, Claude đọc lại trước khi làm.
- **Phiên bản (N)**: bản sao bản vẽ trước khi chat sửa (30 phút một lần, giữ 10 bản) → **Xem** mở tab chỉ xem → **Đóng — về bản hiện tại**.
- Chi tiết: [`docs/palette-features.md`](docs/palette-features.md) mục F.

### v0.8 — chọn layer rồi chat

- Quét chọn đối tượng → khung chat hiện các layer của chúng (bỏ tick được, hoặc **→ Cả layer**).
- Nút **Layer** → tick layer cần sửa → gõ "đổi màu sang đỏ", "khóa lại", "tăng chiều cao chữ lên 300"... Claude tự hiểu sửa layer hay sửa đối tượng trên layer; sửa nhiều đối tượng vẫn hỏi Đồng ý / Hủy.
- Chi tiết: [`docs/palette-features.md`](docs/palette-features.md) mục G.

## 5. Đóng gói bundle (autoloader)

Xem [`bundle/README.md`](bundle/README.md) (thêm ở phase sau): tạo `ClaudeCAD.bundle` đặt vào `%AppData%\Autodesk\ApplicationPlugins` để AutoCAD tự nạp, không cần NETLOAD.

## 6. Cài đặt cho người dùng cuối (phase 3)

- **Setup.exe**: chạy `ClaudeCAD-Setup-<version>.exe` (artifact/release của CI) — cài cho user hiện tại, kiểm tra Node.js, nhắc `claude login`.
- **PowerShell**: giải nén `ClaudeCAD-<version>.zip`, đặt `install.ps1` cạnh thư mục `ClaudeCAD.bundle`, rồi:

```bash
powershell -ExecutionPolicy Bypass -File install.ps1
```

  Tùy chọn `-AllUsers`, `-InstallNode` (cài Node LTS bằng winget), `-Uninstall`. Cài đè bản cũ giữ nguyên các skill cậu đã sửa.
- **Cập nhật**: palette tự báo khi có bản mới (kiểm tra mỗi ngày); gõ `CLAUDECHATUPDATE` để tải, bản mới được cài khi đóng AutoCAD. Nguồn cập nhật: `CLAUDECAD_UPDATE_URL` hoặc `Contents\update-config.json`; repo private cần `CLAUDECAD_UPDATE_TOKEN`. Chi tiết: [`docs/phase3-design.md`](docs/phase3-design.md).
- **Phát hành**: push tag `v<version>` → GitHub Actions (`.github/workflows/build.yml`) build trên Windows, chạy test, tạo zip + Setup.exe + `latest.json` và GitHub Release.

## 7. Log và gỡ lỗi

- Plugin: `%LocalAppData%\ClaudeCAD\logs\claudecad-YYYYMMDD.log` (mọi request/response IPC — cắt ngắn tham số dài — và exception kèm stack trace).
- Sidecar: `%LocalAppData%\ClaudeCAD\logs\sidecar-YYYYMMDD.log`.
- Phiên Claude (transcript) nằm trong `%LocalAppData%\ClaudeCAD\workspace` (thư mục làm việc của sidecar).
