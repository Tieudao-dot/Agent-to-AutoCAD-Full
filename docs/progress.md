# Tiến độ triển khai ClaudeCAD

Nhánh làm việc: `claude/wizardly-heisenberg-ugup5c` (nhánh được cấp cho phiên cloud này; không merge vào `main` khi chưa được đồng ý).

Chú thích: "Cloud" = đã kiểm chứng tự động trên Linux (build + test). "Cần test" = phải chạy trên Windows + AutoCAD 2022 thật (ghi vào `docs/test-log.md`).

## Phase 1a — khung xương ✅ code xong

Đã làm:
- Plugin C# (`plugin/ClaudeCAD.Plugin`): `IExtensionApplication`, lệnh `CLAUDECHAT` / `CLAUDECHATRESTART` / `CLAUDECHATLOG`; palette WPF (lịch sử chat, ô nhập Enter/Shift+Enter, text stream, dòng trạng thái tool, Stop, New); `IpcServer` named pipe `claudecad-{pid}` JSON Lines; `SidecarManager` spawn `node agent/dist/index.js`, tự restart tối đa 3 lần, job object để kill node khi AutoCAD đóng; `ToolDispatcher` (map tool → handler, validate, transaction, error envelope); `UndoScope`; `Logger`; tool `ping` (echo hai chiều).
- Sidecar (`agent/`): Agent SDK `query()` streaming input, in-process MCP server `claudecad`, quyền chỉ bộ tool ClaudeCAD + Read/Skill, stream text/tool_status/turn_end, cancel bằng `interrupt()`, new session, timeout 30 s mỗi request, log file.

Cloud: sidecar build ✅, 32/32 test IPC + session ✅; plugin build net48 ✅; 16/16 unit test lõi C# ✅.

Cần test trên AutoCAD: mục "Phase 1a" trong `docs/test-log.md` (NETLOAD, palette, chat stream, `ping`, Stop, New, kill node.exe).

## Phase 1b — vẽ hình học, layer, batch, undo group ✅ code xong

Đã làm:
- Plugin: handler 7.1 (14 tool vẽ), layer 7.4 (`create_layer` idempotent + tự load linetype, `list_layers`, `set_current_layer`, `set_layer_props`), `batch_execute` (một top transaction, nested transaction cho từng call, tối đa 500 call, lỗi từng call không rollback cả batch), `get_drawing_info` (cần cho quy tắc "luôn gọi trước"). Thuộc tính chung `layer`/`color`/`linetype`/`lineweight` cho mọi tool vẽ; layer thiếu → `LAYER_NOT_FOUND`, layer khóa → `LOCKED`.
- Undo theo lượt: `UndoScope` mở group lười trước tool ghi đầu tiên (COM `StartUndoMark`), đóng ở `turn_end`.
- Hình học thuần (`Core/Geometry.cs`): hình chữ nhật, đa giác đều, cung 3 điểm, revcloud bằng bulge, lineweight — có unit test.
- Sidecar: schema zod + mô tả tiếng Anh cho toàn bộ tool trên.

Cloud: `npm test` 38/38 (có test đối chiếu tên tool sidecar ↔ handler C#), `dotnet test` 29/29, build plugin ✅.

Cần test trên AutoCAD: mục "Phase 1b" trong `docs/test-log.md`, quan trọng nhất là **một lệnh U hoàn tác cả lượt** và **batch 200 line < 2 s**.

## Phase 1c — đọc/truy vấn 7.6, sửa 7.5, chốt xác nhận ✅ code xong

Đã làm:
- Plugin: `list_entities` (lọc types + layers wildcard + màu hiệu dụng + cửa sổ inside/crossing, limit mặc định 200, offset, tóm tắt gọn), `count_entities`, `get_entity` (đầy đủ hình học, attribute, dim), `find_text` (TEXT/MTEXT/attribute/dim override/mleader/ô bảng), `measure_distance`, `measure_area` (m²), `get_bounding_box`.
- Sửa: move/copy (nhiều vị trí, deep clone giữ attribute)/rotate/scale/mirror (giữ text đọc xuôi theo MIRRTEXT)/offset (chọn phía theo side_point)/array_rect/array_polar/fillet & chamfer hai LINE/explode/join (line, arc, polyline)/erase (chốt cứng 1000)/set_entity_props/edit_text (thay toàn bộ hoặc find→replace, đổi chiều cao)/edit_polyline.
- Chốt xác nhận hai tầng: (1) system prompt quy tắc 6; (2) sidecar chặn cứng mọi call xóa/sửa > 20 đối tượng có sẵn (kể cả cộng dồn trong `batch_execute`) nếu tin nhắn hiện tại của người dùng không phải câu xác nhận.

Cloud: `npm test` 42/42, `dotnet test` 34/34, build plugin ✅.

Cần test trên AutoCAD: mục "Phase 1c" trong `docs/test-log.md` và kịch bản nghiệm thu 3, 4, 5.

## Phase 1d — ghi chú 7.2, block 7.3, style, view 7.7 ✅ code xong

Đã làm:
- Ghi chú: TEXT (justify, style, width factor), MTEXT (xuống dòng, wrap), 8 loại dim (linear tự chọn ngang/dọc như DIMLINEAR, aligned, angular 3 điểm / 2 line, radial, diameter, ordinate, continue, baseline theo DIMDLI), multileader, bảng (title tùy chọn, bỏ merge dòng đầu).
- Block: define_block (deep clone vào block, base point = gốc, attribute definition, mặc định thay bằng block reference), insert_block (scale, rotation, attribute, nhiều vị trí), list_block_defs, get/set attributes, set_dynamic_props (đổi đơn vị góc sang độ, liệt kê allowed values).
- Style: create_text_style (TrueType cho tiếng Việt hoặc SHX), create_dim_style (từ biến dim hiện hành + mặc định hệ mét, mũi tên archtick/oblique..., DIMSCALE), set_current_styles, list_styles (thêm).
- View: zoom_extents / zoom_window (đặt view qua ViewTableRecord), regen, save_drawing (QSAVE/SaveAs khi có path), purge_unused (lặp tới khi hết), undo_last_turn (gửi một U, chỉ khi lượt hiện tại chưa sửa gì).

Cloud: 72 tool, `npm test` 42/42 (khớp 1-1 với handler C#), build plugin ✅. Các API AutoCAD dùng ở phase này (dim, mleader, table, dynamic block) chỉ kiểm chứng được ở mức biên dịch.

Cần test trên AutoCAD: mục "Phase 1d" trong `docs/test-log.md`, kịch bản nghiệm thu 2 và 6.
