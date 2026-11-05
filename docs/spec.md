# Spec plugin ClaudeCAD — chat với Claude trong AutoCAD 2022

Nov 5, 2026 · @GoldenBoy

## 1. Tổng quan và phạm vi

Xây dựng plugin tên **ClaudeCAD** cho AutoCAD 2022 (bản full, Windows): người dùng chat bằng tiếng Việt trong một palette ngay trong AutoCAD, Claude hiểu yêu cầu và **vẽ, đọc, sửa, xóa đối tượng trực tiếp trên bản vẽ đang mở**. Lĩnh vực bản vẽ mục tiêu: kiến trúc/xây dựng, điện nước (MEP) và hạ tầng/quy hoạch.

Phạm vi phiên bản 1: vẽ mới + đọc/truy vấn + sửa/xóa đối tượng trong model space, quản lý layer/style/block, hệ thống skill tùy biến. **Ngoài phạm vi v1** (để phase 2): layout/paper space, in ấn, xref, sheet set.

Quy ước toàn hệ thống: đơn vị **milimet**, tọa độ **WCS**, góc tính bằng độ ngược chiều kim đồng hồ. Mọi entity định danh bằng **handle** (chuỗi hex của AutoCAD) trong toàn bộ giao tiếp.

## 2. Kiến trúc hệ thống

Hệ thống gồm hai tiến trình, giao tiếp qua named pipe:

| Thành phần | Công nghệ | Chạy ở đâu | Vai trò |
| --- | --- | --- | --- |
| `ClaudeCAD.Plugin` | C# .NET Framework 4.8, AutoCAD .NET API | In-process trong AutoCAD 2022 | Palette chat (WPF), thực thi toàn bộ tool vẽ/đọc/sửa qua transaction, IPC server |
| `ClaudeCAD.Agent` | Node.js + TypeScript, `@anthropic-ai/claude-agent-sdk` | Tiến trình con do plugin spawn | Chạy Claude (động cơ Claude Code), định nghĩa tool, load skill, gửi tool call về plugin |

Lý do chọn C# in-process cho phần thao tác bản vẽ: AutoCAD .NET API chạy cùng tiến trình nên nhanh hơn COM nhiều lần, quản lý transaction/undo chuẩn, không lỗi focus. Node.js được chọn cho sidecar vì Agent SDK TypeScript là bản đầy đủ nhất của Anthropic.

Luồng xử lý một lượt chat:

1. Người dùng gõ yêu cầu vào palette, plugin gửi message sang sidecar.
2. Sidecar đưa message vào phiên agent (kèm lịch sử hội thoại). Claude đọc skill liên quan, suy nghĩ, chọn tool.
3. Mỗi tool call được sidecar chuyển thành request JSON gửi về plugin qua pipe.
4. Plugin thực thi trên main thread của AutoCAD (document lock + transaction), trả kết quả hoặc lỗi.
5. Claude nhận kết quả, vẽ tiếp hoặc trả lời. Text trả lời stream về palette theo thời gian thực.
6. Kết thúc lượt, plugin đóng undo group — toàn bộ thay đổi của lượt đó undo được bằng một lệnh U.

Xác thực: sidecar dùng OAuth của Claude Code (người dùng `claude login` một lần bằng gói Pro/Max). Nếu có biến môi trường `ANTHROPIC_API_KEY` thì dùng API key thay thế.

## 3. Môi trường và yêu cầu kỹ thuật

| Hạng mục | Yêu cầu |
| --- | --- |
| Hệ điều hành | Windows 10/11 x64 |
| AutoCAD | 2022 bản full (không hỗ trợ LT) |
| Plugin | C# target .NET Framework 4.8; tham chiếu `accoremgd.dll`, `acdbmgd.dll`, `acmgd.dll` từ thư mục cài AutoCAD 2022, đặt Copy Local = false |
| Sidecar | Node.js >= 18, TypeScript, package `@anthropic-ai/claude-agent-sdk` (dùng bản mới nhất, tra cứu API hiện hành trên docs Anthropic khi code) |
| Tài khoản | Claude Pro hoặc Max đã `claude login`, hoặc `ANTHROPIC_API_KEY` |
| Build | Visual Studio 2022 hoặc `dotnet build` (MSBuild), `npm run build` cho sidecar |

Triển khai: đóng gói theo chuẩn autoloader của Autodesk — thư mục `ClaudeCAD.bundle` (chứa `PackageContents.xml`, dll plugin, thư mục `agent/` đã build kèm `node_modules`, thư mục `skills/`) đặt vào `%AppData%\Autodesk\ApplicationPlugins` để AutoCAD tự load. Khi dev thì load nhanh bằng lệnh `NETLOAD`.

Cấu trúc repo:

```
ClaudeCAD/
  plugin/        # C# solution (ClaudeCAD.Plugin.csproj)
  agent/         # Node sidecar (src/, package.json, tsconfig.json)
  skills/        # các skill mẫu (mục 8)
  bundle/        # PackageContents.xml + script đóng gói
  docs/          # ghi chú phát triển
```

## 4. Thành phần C# plugin

Các module chính của `ClaudeCAD.Plugin`:

| Module | Nhiệm vụ |
| --- | --- |
| `ChatPalette` | PaletteSet chứa WPF UserControl: lịch sử chat, ô nhập đa dòng (Enter gửi, Shift+Enter xuống dòng), hiển thị text stream của Claude theo thời gian thực, dòng trạng thái từng tool call (đang vẽ gì), nút Stop hủy lượt, nút New chat. Mở bằng lệnh `CLAUDECHAT` |
| `SidecarManager` | Spawn tiến trình `node agent/dist/index.js` khi palette mở lần đầu, truyền tên pipe qua tham số, theo dõi và tự restart khi crash (báo lên UI), kill khi AutoCAD đóng |
| `IpcServer` | `NamedPipeServerStream` tên `claudecad-{processId}`, giao thức JSON Lines UTF-8 (mục 6) |
| `ToolDispatcher` | Map tên tool → handler; validate tham số; trả error envelope khi tool lạ hoặc tham số sai |
| `DrawingService` | Toàn bộ handler thao tác bản vẽ (mục 7). Mọi thao tác write chạy trên main thread AutoCAD qua `Application.DocumentManager.ExecuteInApplicationContext`, trong `DocumentLock` + một `Transaction` |
| `UndoScope` | Mỗi lượt chat của người dùng bao trong một undo group (`Document.Database` undo marks) để một lệnh U hoàn tác cả lượt |
| `Logger` | Ghi log request/response và exception ra `%LocalAppData%\ClaudeCAD\logs\` |

Quy tắc bắt buộc:

- Không bao giờ đụng database ngoài transaction. Mỗi request `batch_execute` = đúng một transaction.
- Handler trả về handle của mọi entity vừa tạo/sửa để Claude tham chiếu ở các bước sau.
- Exception ở bất kỳ handler nào không được làm sập AutoCAD: bắt, rollback transaction, trả error message rõ ràng cho Claude tự xử lý.
- UI không block: gửi/nhận pipe chạy async, chỉ phần ghi database mới marshal về main thread.

## 5. Thành phần sidecar Agent SDK

`ClaudeCAD.Agent` là ứng dụng Node.js nhận message từ plugin và chạy vòng lặp agent bằng `@anthropic-ai/claude-agent-sdk`. Yêu cầu:

- **Phiên hội thoại**: duy trì một phiên agent liên tục cho mỗi cửa sổ chat (streaming input), giữ ngữ cảnh nhiều lượt; nút New chat tạo phiên mới.
- **Định nghĩa tool**: toàn bộ tool ở mục 7 khai báo bằng cơ chế in-process MCP server của SDK (`createSdkMcpServer` + `tool()` với schema zod). Thân mỗi tool chỉ làm một việc: chuyển tiếp request qua pipe sang plugin và chờ kết quả. Mô tả tool và tham số viết kỹ bằng tiếng Anh (tên tham số, đơn vị, ví dụ) vì chất lượng mô tả quyết định Claude gọi tool đúng hay sai.
- **Skill**: load các skill từ thư mục `skills/` cạnh bundle bằng cấu hình skill/plugin directory của SDK, để người dùng thêm/sửa skill không cần build lại.
- **Permission**: tắt các tool mặc định của Claude Code không cần thiết (Bash, WebSearch…) — chỉ bật bộ tool ClaudeCAD, Read cho skill, và cấu hình cho phép tự động chạy bộ tool này không hỏi lại từng lần (xác nhận thao tác nguy hiểm xử lý ở tầng system prompt, mục 9).
- **Streaming ra UI**: chuyển các sự kiện của SDK (text delta, tool call bắt đầu/kết thúc, lỗi, kết thúc lượt) thành event JSON gửi về plugin để palette hiển thị sống.
- **Hủy**: nhận lệnh `cancel` từ plugin (nút Stop) và ngắt lượt đang chạy sạch sẽ.

Lưu ý cho Claude Code khi triển khai: API của Agent SDK thay đổi nhanh — dùng skill `claude-api` có sẵn để tra cứu cú pháp hiện hành thay vì viết theo trí nhớ.

## 6. Giao thức IPC giữa plugin và sidecar

Nền: named pipe, mỗi thông điệp là một dòng JSON (JSON Lines, UTF-8 không BOM). Hai chiều:

Chiều sidecar → plugin (tool call):

```json
{"id": "req-17", "type": "tool_call", "tool": "draw_polyline", "params": {"vertices": [[0,0],[4000,0],[4000,6000],[0,6000]], "closed": true, "layer": "A-WALL"}}
```

Plugin trả lời:

```json
{"id": "req-17", "ok": true, "result": {"handles": ["2F4A"]}}
{"id": "req-18", "ok": false, "error": {"code": "LAYER_NOT_FOUND", "message": "Layer 'A-WALL' does not exist. Call create_layer first or list_layers to see existing layers."}}
```

Chiều plugin → sidecar: `{"type": "user_message", "text": "..."}` khi người dùng gửi chat, `{"type": "cancel"}` khi bấm Stop, `{"type": "new_session"}`. Chiều sidecar → plugin ngoài tool call còn có event hiển thị: `{"type": "agent_text", "delta": "..."}`, `{"type": "tool_status", "tool": "draw_line", "state": "running|done|error"}`, `{"type": "turn_end"}`.

Quy ước dữ liệu:

- Tọa độ: mảng `[x, y]` hoặc `[x, y, z]`, đơn vị mm, WCS. Thiếu z hiểu là 0.
- Góc: độ, ngược chiều kim đồng hồ từ trục X.
- Màu: index ACI 1-255 hoặc chuỗi `"bylayer"`.
- Handle entity: chuỗi hex như AutoCAD hiển thị.
- Timeout mỗi request: 30 giây; sidecar trả lỗi timeout cho Claude thay vì treo.
- Mã lỗi chuẩn: `INVALID_PARAMS`, `ENTITY_NOT_FOUND`, `LAYER_NOT_FOUND`, `BLOCK_NOT_FOUND`, `LOCKED`, `ACAD_ERROR`, `TIMEOUT`, `TOO_MANY_ENTITIES`. Message lỗi viết để Claude đọc và tự sửa được (nói rõ nên gọi tool gì tiếp).

## 7. Danh mục tool

Bộ tool chia 8 nhóm, 60+ tool. Mọi tool vẽ nhận tham số tùy chọn chung: `layer` (string), `color` (ACI/bylayer), `linetype`. Mọi tool tạo/sửa trả về `handles` của entity liên quan.

### 7.1. Vẽ hình học

| Tool | Tham số chính | Ghi chú |
| --- | --- | --- |
| `draw_line` | `start`, `end` | đoạn thẳng |
| `draw_polyline` | `vertices[]`, `closed`, `bulges[]?`, `width?` | LWPolyline; bulge cho cung tròn trong polyline |
| `draw_circle` | `center`, `radius` |  |
| `draw_arc` | `center`+`radius`+`start_angle`+`end_angle` hoặc `p1`+`p2`+`p3` | hai cách khai báo |
| `draw_ellipse` | `center`, `major_axis_end`, `ratio` |  |
| `draw_spline` | `fit_points[]` |  |
| `draw_rectangle` | `corner1`, `corner2` hoặc `corner`+`width`+`height` | tạo polyline kín |
| `draw_polygon` | `center`, `sides`, `radius`, `inscribed` | đa giác đều |
| `draw_point` | `position` |  |
| `draw_xline` | `base`, `direction` | đường trục vô hạn |
| `draw_ray` | `base`, `direction` |  |
| `draw_hatch` | `pattern`, `scale`, `angle`, `boundary_handles[]` hoặc `boundary_points[]` | solid và pattern chuẩn |
| `draw_wipeout` | `points[]` |  |
| `draw_revcloud` | `points[]`, `arc_length` | mây sửa đổi |

### 7.2. Ghi chú và kích thước

| Tool | Tham số chính | Ghi chú |
| --- | --- | --- |
| `add_text` | `position`, `text`, `height`, `rotation?`, `style?`, `justify?` | DBText |
| `add_mtext` | `position`, `text`, `height`, `width`, `style?` | hỗ trợ xuống dòng |
| `add_dim_linear` | `p1`, `p2`, `dim_line_point`, `rotation?` | ngang/dọc/xoay |
| `add_dim_aligned` | `p1`, `p2`, `dim_line_point` |  |
| `add_dim_angular` | 3 điểm hoặc 2 handle đường |  |
| `add_dim_radial` / `add_dim_diameter` | `entity_handle`, `leader_length` |  |
| `add_dim_ordinate` | `feature_point`, `leader_end`, `use_x` |  |
| `add_dim_continue` / `add_dim_baseline` | `base_dim_handle`, `points[]` | chuỗi kích thước |
| `add_mleader` | `arrow_point`, `text_point`, `text` |  |
| `add_table` | `position`, `rows`, `cols`, `col_widths[]`, `data[][]` | bảng thống kê |

### 7.3. Block và thuộc tính

| Tool | Tham số chính | Ghi chú |
| --- | --- | --- |
| `define_block` | `name`, `base_point`, `entity_handles[]` | gộp entity có sẵn thành block |
| `insert_block` | `name`, `position`, `scale?`, `rotation?`, `attributes?{}` |  |
| `list_block_defs` | `filter?` | liệt kê block trong bản vẽ |
| `get_block_attributes` | `handle` | đọc giá trị attribute của block reference |
| `set_block_attributes` | `handle`, `attributes{}` |  |
| `set_dynamic_props` | `handle`, `props{}` | dynamic block |

### 7.4. Layer, style và chuẩn bản vẽ

| Tool | Tham số chính | Ghi chú |
| --- | --- | --- |
| `create_layer` | `name`, `color?`, `linetype?`, `lineweight?`, `plottable?` | tự load linetype nếu thiếu |
| `list_layers` |  | trả tên + thuộc tính |
| `set_current_layer` | `name` |  |
| `set_layer_props` | `name`, `color?`, `linetype?`, `lineweight?`, `on?`, `frozen?`, `locked?` |  |
| `create_text_style` | `name`, `font`, `height?`, `width_factor?` |  |
| `create_dim_style` | `name`, `text_height`, `arrow_size`, `extension_offset`, `scale`… | đủ biến cho chuẩn VN |
| `set_current_styles` | `text_style?`, `dim_style?` |  |

### 7.5. Sửa đổi

| Tool | Tham số chính | Ghi chú |
| --- | --- | --- |
| `move_entities` | `handles[]`, `from`, `to` |  |
| `copy_entities` | `handles[]`, `from`, `to` hoặc `displacements[]` | copy nhiều vị trí |
| `rotate_entities` | `handles[]`, `base`, `angle` |  |
| `scale_entities` | `handles[]`, `base`, `factor` |  |
| `mirror_entities` | `handles[]`, `axis_p1`, `axis_p2`, `keep_original` |  |
| `offset_entity` | `handle`, `distance`, `side_point` |  |
| `array_rect` | `handles[]`, `rows`, `cols`, `row_spacing`, `col_spacing` |  |
| `array_polar` | `handles[]`, `center`, `count`, `fill_angle` |  |
| `fillet_entities` | `handle1`, `handle2`, `radius` |  |
| `chamfer_entities` | `handle1`, `handle2`, `dist1`, `dist2` |  |
| `explode_entities` | `handles[]` | trả handles mới |
| `join_entities` | `handles[]` | nối thành polyline |
| `erase_entities` | `handles[]` | xóa theo handle |
| `set_entity_props` | `handles[]`, `layer?`, `color?`, `linetype?`, `lineweight?` | đổi thuộc tính |
| `edit_text` | `handle`, `text?`, `height?` | sửa nội dung text/mtext/dim override |
| `edit_polyline` | `handle`, `vertices?`, `closed?` | thay đỉnh polyline |

### 7.6. Đọc và truy vấn

| Tool | Tham số chính | Ghi chú |
| --- | --- | --- |
| `list_entities` | `types[]?`, `layers[]?`, `window?{p1,p2,crossing}`, `limit?` | bộ lọc kết hợp; trả handle + tóm tắt từng entity |
| `get_entity` | `handle` | toàn bộ thuộc tính hình học |
| `find_text` | `query`, `match_case?` | tìm text/mtext/attribute chứa chuỗi |
| `measure_distance` | `p1`, `p2` |  |
| `measure_area` | `handle` hoặc `points[]` | diện tích + chu vi |
| `get_bounding_box` | `handles[]` |  |
| `get_drawing_info` |  | đơn vị, extents, số entity, layer hiện hành, style hiện hành |
| `count_entities` | cùng filter với `list_entities` | đếm nhanh không trả chi tiết |

### 7.7. View và tài liệu

| Tool | Tham số chính | Ghi chú |
| --- | --- | --- |
| `zoom_extents` |  |  |
| `zoom_window` | `p1`, `p2` |  |
| `regen` |  |  |
| `save_drawing` |  | chỉ gọi khi người dùng yêu cầu |
| `purge_unused` |  | dọn block/layer/style thừa |
| `undo_last_turn` |  | hoàn tác undo group của lượt gần nhất |

### 7.8. Batch

`batch_execute` nhận `calls[]` — mảng tool call tuần tự, thực thi trong **một transaction duy nhất**, trả kết quả từng call. Đây là tool quan trọng nhất về hiệu năng: vẽ 200 đoạn thẳng bằng một `batch_execute` thay vì 200 round-trip. Giới hạn 500 call/batch. Một call lỗi thì các call sau trong batch vẫn chạy, lỗi báo theo từng call (không rollback cả batch, trừ lỗi hệ thống).

## 8. Hệ thống skill

Skill là nơi người dùng định nghĩa "vẽ theo chuẩn của tôi" mà không sửa code. Mỗi skill là một thư mục trong `skills/` theo chuẩn Agent Skills của Anthropic: file `SKILL.md` có frontmatter `name` + `description` (mô tả quyết định khi nào skill được load — viết kỹ), thân file là hướng dẫn chi tiết, kèm file tham khảo phụ nếu dài.

Tạo sẵn 3 skill mẫu (nội dung là khung để người dùng điền chuẩn riêng, kèm ví dụ hoàn chỉnh):

| Skill | Nội dung khung |
| --- | --- |
| `ve-kien-truc` | Bảng layer chuẩn (A-WALL, A-DOOR, A-WIND, A-COL, A-DIM, A-TEXT… với màu/nét/lineweight), quy cách nét tường 220/110, quy trình vẽ mặt bằng (trục → cột → tường → cửa → dim → text), chiều cao chữ và dim style theo tỷ lệ in, cách vẽ ký hiệu cửa đi/cửa sổ bằng polyline+arc |
| `ve-mep` | Layer hệ điện (E-LIGHT, E-POWER, E-TRAY…) và nước (P-SUPL, P-DRAIN, P-VENT…), nguyên tắc đi tuyến (song song tường, offset chuẩn), ký hiệu thiết bị dạng block, ghi chú đường kính ống/tiết diện dây trên mleader |
| `ve-ha-tang` | Layer tim đường, mép đường, vỉa hè, cống, ranh giới; quy ước cao độ và lý trình; vẽ tuyến bằng polyline qua điểm tọa độ thực; bảng tọa độ bằng `add_table` |

Yêu cầu triển khai: sidecar load toàn bộ skill trong `skills/` lúc khởi động; thêm skill mới = tạo thư mục mới + restart phiên chat, không build lại gì. Trong mỗi SKILL.md mẫu phải có mục "Quy trình chuẩn bị" nhắc Claude luôn gọi `get_drawing_info` + `list_layers` trước, tạo layer/style còn thiếu theo bảng chuẩn, rồi mới vẽ.

## 9. System prompt và quy tắc hành vi của agent

System prompt của sidecar phải quy định:

1. Vai trò: trợ lý CAD làm việc trực tiếp trên bản vẽ AutoCAD đang mở; trả lời bằng tiếng Việt, ngắn gọn.
2. Đơn vị mm, WCS; khi người dùng nói mét thì tự quy đổi.
3. Luôn bắt đầu tác vụ bằng `get_drawing_info` (và `list_layers` nếu sắp vẽ) để biết trạng thái bản vẽ trước khi hành động.
4. Vẽ nhiều đối tượng → bắt buộc dùng `batch_execute`, không gọi lẻ từng tool.
5. Trước khi sửa phải đọc: dùng `list_entities`/`get_entity` xác định đúng handle, không đoán.
6. Thao tác phá hủy (xóa bất kỳ số lượng theo điều kiện lọc, hoặc sửa > 20 đối tượng một lúc): liệt kê số lượng + mô tả ngắn và **hỏi xác nhận trong chat trước khi thực thi**.
7. Không tự gọi `save_drawing` hay `purge_unused` trừ khi được yêu cầu.
8. Sau mỗi tác vụ: báo cáo một đoạn ngắn đã làm gì (số đối tượng, layer nào), nhắc rằng gõ U một lần là hoàn tác cả lượt nếu không ưng.
9. Gặp lỗi tool: đọc message lỗi, tự sửa (ví dụ tạo layer thiếu) tối đa 2 lần, quá thì dừng và báo người dùng.
10. Yêu cầu mơ hồ về vị trí/kích thước → hỏi lại một câu gọn thay vì đoán bừa; yêu cầu đơn giản đủ thông tin thì làm luôn.

## 10. Hiệu năng, an toàn dữ liệu, xử lý lỗi

Hiệu năng:

- Một `batch_execute` = một lần marshal vào main thread + một transaction. Mục tiêu: batch 200 line hoàn thành dưới 2 giây tính từ lúc plugin nhận request.
- `list_entities` có `limit` mặc định 200 và trả tóm tắt gọn (handle, type, layer, vài thông số chính) để không tràn context của Claude; chi tiết đầy đủ chỉ qua `get_entity`.
- Không regen sau từng call; regen một lần cuối batch nếu cần.

An toàn dữ liệu bản vẽ:

- Mọi write nằm trong `DocumentLock`; không write khi không có document active (trả lỗi `ACAD_ERROR` rõ nghĩa).
- Undo group theo lượt chat: thử nghiệm bắt buộc là sau một lượt Claude vẽ 50 đối tượng, gõ U đúng một lần phải mất cả 50.
- Không bao giờ tự save; không đụng file ngoài bản vẽ đang mở.
- Xóa/sửa hàng loạt đã có chốt xác nhận ở tầng agent (mục 9); plugin thêm chốt cứng: `erase_entities` quá 1000 handle một lần → trả lỗi `TOO_MANY_ENTITIES`.

Xử lý lỗi và độ bền:

- Sidecar crash → plugin restart tối đa 3 lần, hiện trạng thái trên palette; lịch sử chat giữ nguyên ở UI.
- Mất kết nối pipe giữa lượt → lượt đó kết thúc với thông báo lỗi, không treo UI.
- Log đủ để debug: mỗi request/response IPC (cắt ngắn params dài), mọi exception kèm stack trace.
- Người dùng đóng bản vẽ/đổi document giữa chừng → các call đang chờ trả lỗi, agent được báo để dừng lượt.

## 11. Lộ trình phát triển

| Phase | Nội dung | Kết quả kiểm chứng được |
| --- | --- | --- |
| 1a | Khung xương: plugin load được bằng NETLOAD, palette hiện, sidecar spawn, IPC echo hai chiều | Gõ chat thấy Claude trả lời text (chưa có tool) |
| 1b | Nhóm tool vẽ 7.1 + layer 7.4 + `batch_execute` + undo group | "Vẽ lưới trục 5x4 bước 4000" ra hình đúng |
| 1c | Nhóm đọc 7.6 + sửa 7.5 + chốt xác nhận phá hủy | "Đổi hết text cao 250 thành 300" chạy đúng |
| 1d | Ghi chú 7.2, block 7.3, style, view 7.7 | Vẽ mặt bằng có dim + text + block cửa |
| 1e | Hệ thống skill + 3 skill mẫu + system prompt hoàn chỉnh | Vẽ đúng layer/chuẩn theo skill không cần nhắc |
| 2 | Layout/paper space, viewport, in PDF, xref, dynamic block nâng cao, trim/extend hình học chính xác | — |
| 3 | Đóng gói installer (kèm kiểm tra Node), cập nhật tự động, hỗ trợ AutoCAD 2023-2025 | — |

Claude Code triển khai theo đúng thứ tự 1a → 1e, mỗi phase chạy được thật trước khi sang phase sau. Viết README hướng dẫn build, NETLOAD và chạy thử cho từng phase.

## 12. Tiêu chí nghiệm thu

Các kịch bản chat sau phải chạy đúng trên AutoCAD 2022 với bản vẽ trắng (trừ khi ghi khác):

1. "Vẽ lưới trục 5 trục đứng bước 4000, 4 trục ngang bước 3600, kèm ký hiệu trục" → xline/line đúng layer trục, vòng tròn + chữ ký hiệu đầu trục.
2. "Vẽ mặt bằng phòng 4x6m, tường 220, cửa đi 900 ở cạnh dài, ghi kích thước" → tường hai nét, cửa có cánh + cung mở, dim đủ bốn phía.
3. Trên bản vẽ có sẵn text: "Tìm tất cả text chứa 'WC' và đổi thành 'VỆ SINH'" → đúng và đủ các text.
4. "Xóa toàn bộ đối tượng trên layer A-TEMP" → Claude báo số lượng và hỏi xác nhận trước, trả lời "đồng ý" mới xóa.
5. "Đo diện tích polyline tô màu đỏ lớn nhất" → trả đúng diện tích m2.
6. "Chèn block GHE vừa định nghĩa thành dãy 3 hàng 8 cột giãn cách 600x900" → array đúng.
7. Hiệu năng: một lượt vẽ 200 đoạn thẳng qua `batch_execute` hoàn thành dưới 2 giây phần thực thi.
8. Gõ U một lần sau bất kỳ kịch bản nào → hoàn tác toàn bộ lượt đó.
9. Tắt sidecar bằng Task Manager giữa phiên → palette báo mất kết nối, tự khôi phục, chat tiếp được.
10. Với skill `ve-kien-truc` bật: yêu cầu ở kịch bản 2 phải tự dùng đúng tên layer và chiều cao chữ trong skill mà không cần nhắc.

Không yêu cầu unit test phủ kín; cần tối thiểu integration test cho lớp IPC (serialize/deserialize, timeout) và smoke test thủ công theo 10 kịch bản trên, ghi kết quả vào `docs/test-log.md`.
