# Giao thức IPC plugin ↔ sidecar

Triển khai mục 6 của spec. Nguồn: `agent/src/ipc/protocol.ts` (TypeScript) và `plugin/ClaudeCAD.Plugin/Ipc/IpcServer.cs` (C#).

## Kênh truyền

- Named pipe `\\.\pipe\claudecad-{processId của acad.exe}`; plugin là server (`NamedPipeServerStream`, 1 client), sidecar là client (`net.connect`). Sidecar nhận tên pipe qua `--pipe`.
- Mỗi thông điệp là **một dòng JSON** (JSON Lines), UTF-8 không BOM, kết thúc bằng `\n`. Bên đọc chấp nhận `\r\n`, bỏ dòng trống, bỏ BOM đầu luồng, ghép đúng ký tự UTF-8 bị cắt giữa hai chunk.
- Dòng không parse được hoặc không đúng loại: ghi log và bỏ qua, không làm đứt kết nối.
- Pipe đóng → sidecar fail mọi call đang chờ rồi tự thoát; plugin thấy tiến trình thoát và spawn lại (tối đa 3 lần liên tiếp, bộ đếm reset khi tiến trình sống > 1 phút). Plugin luôn mở lại pipe chờ client mới.

## Sidecar → plugin

| `type` | Trường | Ý nghĩa |
| --- | --- | --- |
| `tool_call` | `id`, `tool`, `params` | Claude gọi tool. `id` dạng `req-N`, tăng dần. |
| `agent_text` | `delta` | Đoạn text stream của Claude. |
| `agent_text_end` | — | *(mở rộng)* hết một khối text; palette mở bong bóng mới cho text sau. |
| `tool_status` | `tool`, `state` = `running`/`done`/`error` | Dòng trạng thái "đang vẽ gì". |
| `turn_end` | `error?`, `cancelled?` | Kết thúc lượt. Plugin đóng undo group, mở khóa ô nhập. |
| `ready` | `info`, `version` | *(mở rộng)* sidecar đã nối pipe / phiên đã khởi tạo (model, kiểu đăng nhập). |
| `agent_error` | `message` | *(mở rộng)* lỗi không chặn lượt (đăng nhập, rate limit...) để hiện trên palette. |
| `session_reset` | — | *(mở rộng)* xác nhận `new_session`. |
| `confirm_request` | `id`, `text`, `count?`, `handles?` | *(v0.4)* thẻ xác nhận Đồng ý / Hủy. |
| `session_info`, `options`, `rate_limit`, `skills_reloaded` | — | *(v0.4–0.5)* phiên, model/effort, hạn mức, skill đang dùng. |
| `suggestions` | `items[]`, `source` = `tool`/`sdk` | *(v0.6)* nút gợi ý bước tiếp theo. `sdk` (dự đoán của Agent SDK) đến sau `turn_end`; plugin bỏ nếu lượt mới đã bắt đầu. |
| `plan_request` | `id`, `summary`, `steps[]` | *(v0.6)* thẻ kế hoạch Làm / Sửa kế hoạch / Bỏ. |
| `skill_draft` | `name`, `description`, `content` | *(v0.6)* bản nháp `SKILL.md` Claude soạn từ cuộc chat. |

## Plugin → sidecar

| Thông điệp | Ý nghĩa |
| --- | --- |
| `{"type":"user_message","text":"...","drawing":"a.dwg"}` | Người dùng gửi chat. `drawing` (tùy chọn) là tên bản vẽ đang mở, được ghi vào đầu message cho Claude. |
| `user_message` mở rộng | `attachments` (`selection`, `images`), `context` (`standard`, `scale`, *v0.6:* `read_only`, `notes`, `visual_check:false`, `plan_first:false`, `suggestions:false`), `edited`, `confirm`, *v0.6:* `plan:"approved"`. |
| `{"type":"cancel"}` | Nút Stop, hoặc bản vẽ bị đóng/đổi giữa lượt. Sidecar `interrupt()` lượt và fail mọi call đang chờ. |
| `{"type":"new_session"}` | Nút New: đóng phiên agent, phiên mới bắt đầu ở message kế tiếp. |
| `{"id":"req-17","ok":true,"result":{...}}` | Kết quả tool call. Tool tạo/sửa luôn trả `handles`. |
| `{"id":"req-18","ok":false,"error":{"code":"...","message":"..."}}` | Lỗi tool. `message` viết cho Claude đọc và tự sửa. |

## Quy ước dữ liệu

- Tọa độ `[x, y]` hoặc `[x, y, z]`, mm, WCS; thiếu z = 0. Plugin cũng nhận `{x, y, z}`.
- Góc: độ, ngược chiều kim đồng hồ từ +X.
- Màu: ACI 0–256 hoặc `"bylayer"` / `"byblock"`.
- Handle: chuỗi hex như AutoCAD hiển thị (không phân biệt hoa thường).
- Số thực được ghi tối đa 9 chữ số thập phân; số nguyên không có phần thập phân.

## Timeout và mã lỗi

- Mỗi request 30 s (`CLAUDECAD_TOOL_TIMEOUT_MS`). Quá hạn → sidecar trả cho Claude lỗi `TIMEOUT` kèm hướng dẫn (bấm Esc trong AutoCAD, chia nhỏ batch). Response đến muộn bị bỏ qua.
- Mã lỗi: `INVALID_PARAMS`, `ENTITY_NOT_FOUND`, `LAYER_NOT_FOUND`, `BLOCK_NOT_FOUND`, `LOCKED`, `ACAD_ERROR`, `TIMEOUT`, `TOO_MANY_ENTITIES`, *(v0.6)* `READ_ONLY` (bản vẽ đang bật Chỉ xem).
- `capture_view` trả `image_png_base64` trong `result`; sidecar chuyển thành khối ảnh MCP (`type: "image"`) cho Claude, phần còn lại (cửa sổ WCS, `mm_per_pixel`) là text.
  - Tool không tồn tại → `INVALID_PARAMS` kèm danh sách tool.
  - Không có bản vẽ / bản vẽ bị đóng hoặc đổi giữa lượt → `ACAD_ERROR` với lời dặn Claude dừng lượt.
  - Entity trên layer khóa → `LOCKED`.
- Kết quả gửi Claude: thành công là JSON gọn của `result`; lỗi là text `ERROR <CODE>: <message>` với `isError: true`.

## Ví dụ một lượt

```text
plugin  → {"type":"user_message","text":"vẽ hình chữ nhật 4x6m","drawing":"Drawing1.dwg"}
sidecar → {"type":"agent_text","delta":"Tớ vẽ "}
sidecar → {"type":"tool_status","tool":"get_drawing_info","state":"running"}
sidecar → {"id":"req-1","type":"tool_call","tool":"get_drawing_info","params":{}}
plugin  → {"id":"req-1","ok":true,"result":{"units":"mm",...}}
sidecar → {"type":"tool_status","tool":"get_drawing_info","state":"done"}
sidecar → {"id":"req-2","type":"tool_call","tool":"draw_rectangle","params":{"corner1":[0,0],"corner2":[4000,6000]}}
plugin  → {"id":"req-2","ok":true,"result":{"handles":["2F4A"]}}
sidecar → {"type":"agent_text","delta":"Đã vẽ 1 hình chữ nhật..."}
sidecar → {"type":"turn_end"}
```
