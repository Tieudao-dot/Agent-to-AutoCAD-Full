# Nhật ký smoke test thủ công

Môi trường cần: Windows 10/11 x64, AutoCAD 2022 bản full, bản vẽ trắng (acadiso.dwt) trừ khi ghi khác, Node.js >= 18, đã `claude login` hoặc có `ANTHROPIC_API_KEY`.

Trạng thái: ⬜ chưa chạy · ✅ đạt · ❌ không đạt · ⚠️ đạt một phần. Ghi ngày, phiên bản commit và ghi chú ở cột cuối.

> **Chưa có kịch bản nào được chạy trên AutoCAD thật.** Phiên phát triển chạy trên cloud Linux, không có Windows/AutoCAD. Những gì đã kiểm chứng tự động trên cloud được liệt kê ở cuối file.

## Tiêu chí nghiệm thu (spec §12)

| # | Kịch bản | Kỳ vọng | Trạng thái | Ghi chú |
| --- | --- | --- | --- | --- |
| 1 | "Vẽ lưới trục 5 trục đứng bước 4000, 4 trục ngang bước 3600, kèm ký hiệu trục" | xline/line đúng layer trục, vòng tròn + chữ ký hiệu đầu trục | ⬜ chưa chạy | |
| 2 | "Vẽ mặt bằng phòng 4x6m, tường 220, cửa đi 900 ở cạnh dài, ghi kích thước" | tường hai nét, cửa có cánh + cung mở, dim đủ bốn phía | ⬜ chưa chạy | |
| 3 | (bản vẽ có sẵn text) "Tìm tất cả text chứa 'WC' và đổi thành 'VỆ SINH'" | đúng và đủ các text | ⬜ chưa chạy | |
| 4 | "Xóa toàn bộ đối tượng trên layer A-TEMP" | Claude báo số lượng, hỏi xác nhận; trả lời "đồng ý" mới xóa | ⬜ chưa chạy | |
| 5 | "Đo diện tích polyline tô màu đỏ lớn nhất" | trả đúng diện tích m² | ⬜ chưa chạy | |
| 6 | "Chèn block GHE vừa định nghĩa thành dãy 3 hàng 8 cột giãn cách 600x900" | array đúng | ⬜ chưa chạy | |
| 7 | Một lượt vẽ 200 đoạn thẳng qua `batch_execute` | phần thực thi < 2 s (xem log `tool batch_execute ok in N ms`) | ⬜ chưa chạy | |
| 8 | Gõ U một lần sau bất kỳ kịch bản nào | hoàn tác toàn bộ lượt đó | ⬜ chưa chạy | |
| 9 | Tắt sidecar (`node.exe`) bằng Task Manager giữa phiên | palette báo mất kết nối, tự khôi phục, chat tiếp được | ⬜ chưa chạy | |
| 10 | Bật skill `ve-kien-truc`, chạy lại kịch bản 2 | tự dùng đúng tên layer và chiều cao chữ trong skill, không cần nhắc | ⬜ chưa chạy | |

## Kiểm tra theo phase

| Phase | Kiểm tra | Trạng thái | Ghi chú |
| --- | --- | --- | --- |
| 1a | NETLOAD dll không lỗi, `CLAUDECHAT` mở palette | ⬜ chưa chạy | |
| 1a | Chat "chào Claude" → text stream dần trong palette | ⬜ chưa chạy | |
| 1a | "kiểm tra kết nối với plugin" → tool `ping` trả phiên bản AutoCAD + tên bản vẽ | ⬜ chưa chạy | |
| 1a | Stop giữa lượt → lượt dừng, nút Gửi bật lại | ⬜ chưa chạy | |
| 1a | New → lịch sử xóa, Claude không nhớ ngữ cảnh cũ | ⬜ chưa chạy | |
| 1a | Đóng AutoCAD → không còn `node.exe` của ClaudeCAD | ⬜ chưa chạy | |
| 1b | "Vẽ lưới trục 5x4 bước 4000" → 5 trục đứng + 4 trục ngang đúng bước, layer trục linetype CENTER, một `batch_execute` | ⬜ chưa chạy | |
| 1b | U một lần sau lượt trên → mất toàn bộ lưới trục | ⬜ chưa chạy | |
| 1b | Lượt chỉ đọc ("bản vẽ có bao nhiêu đối tượng?") rồi U → hoàn tác lượt vẽ trước đó, không "hụt" | ⬜ chưa chạy | |
| 1b | Mỗi tool vẽ 7.1 tạo đúng loại đối tượng (line, LWPOLYLINE có bulge, circle, arc 2 cách, ellipse, spline, rectangle, polygon nội/ngoại tiếp, point, xline, ray, hatch SOLID/ANSI31 theo handle và theo điểm, wipeout, revcloud) | ⬜ chưa chạy | |
| 1b | Layer: create (tự load linetype CENTER/DASHED), list, set current, set props (khóa, tắt, đóng băng; không cho đóng băng layer hiện hành) | ⬜ chưa chạy | |
| 1b | Batch có 1 call lỗi ở giữa → các call khác vẫn vẽ, kết quả báo lỗi đúng index | ⬜ chưa chạy | |
| 1b | Vẽ lên layer bị khóa → lỗi `LOCKED`, không crash | ⬜ chưa chạy | |
| 1c | "Đổi hết text cao 250 thành 300" → đúng các text cao 250 (TEXT + MTEXT), text khác giữ nguyên | ⬜ chưa chạy | |
| 1c | list_entities lọc theo type/layer/màu/cửa sổ (inside và crossing), limit/offset | ⬜ chưa chạy | |
| 1c | find_text tìm trong TEXT, MTEXT, attribute, dim override, mleader, ô bảng | ⬜ chưa chạy | |
| 1c | measure_area polyline kín / circle / hatch → m² đúng | ⬜ chưa chạy | |
| 1c | move/copy (nhiều vị trí)/rotate/scale/mirror (text đọc xuôi) / offset về phía side_point / array_rect / array_polar | ⬜ chưa chạy | |
| 1c | fillet (r>0 và r=0) và chamfer hai line; join line+arc thành polyline; explode block/polyline | ⬜ chưa chạy | |
| 1c | Chốt xác nhận: xóa theo layer → Claude hỏi trước; trả lời "không" → không xóa; "đồng ý" → xóa | ⬜ chưa chạy | |
| 1c | Sửa > 20 đối tượng khi chưa xác nhận → sidecar chặn (log `confirmation gate blocked`), Claude hỏi lại | ⬜ chưa chạy | |
| 1c | erase_entities > 1000 handle → `TOO_MANY_ENTITIES` | ⬜ chưa chạy | |
| 1d | Mặt bằng có dim + text + block cửa (kịch bản 2) | ⬜ chưa chạy | |
| 1d | add_text các kiểu justify; add_mtext xuống dòng; tiếng Việt có dấu hiển thị đúng với style Arial | ⬜ chưa chạy | |
| 1d | Dim: linear (auto ngang/dọc), aligned, angular (3 điểm và 2 line), radial, diameter, ordinate, continue, baseline | ⬜ chưa chạy | |
| 1d | create_dim_style 1:100 (chữ 2.5, archtick) → dim hiển thị đúng kích thước in | ⬜ chưa chạy | |
| 1d | add_mleader, add_table (có/không title; dòng đầu không bị merge khi không có title) | ⬜ chưa chạy | |
| 1d | define_block (thay bằng insert, có attribute), insert_block (attribute, nhiều vị trí), list_block_defs, get/set attributes, set_dynamic_props trên một dynamic block có sẵn | ⬜ chưa chạy | |
| 1d | zoom_extents, zoom_window; save_drawing chỉ khi yêu cầu; purge_unused; undo_last_turn | ⬜ chưa chạy | |

## Đã kiểm chứng tự động trên cloud (Linux)

| Ngày | Hạng mục | Kết quả |
| --- | --- | --- |
| 2026-11-05 | `npm run build` sidecar | ✅ |
| 2026-11-05 | `npm test` — 32 test: protocol (encode/decode, UTF-8 cắt giữa chunk, CRLF, BOM, dòng quá dài), ToolRpc (tương quan id, timeout, response muộn, mất kết nối, failAll), tích hợp qua socket thật (echo tool, lỗi plugin, timeout, đứt pipe, dòng hỏng, tiến trình sidecar thật nối pipe rồi tự thoát), AgentSession với query giả (stream text, nhiều lượt, cancel, new session, process chết, lỗi đăng nhập, skill plugin) | ✅ 32/32 |
| 2026-11-05 | `dotnet build` plugin net48 với reference AutoCAD.NET 24.1.51000 | ✅ 0 warning |
| 2026-11-05 | `dotnet test plugin/ClaudeCAD.Core.Tests` — JsonCodec, Params | ✅ 16/16 |
| 2026-11-05 | Phase 1b: `npm test` 38/38 (thêm: danh mục tool TS khớp handler C#, mọi schema chuyển được sang JSON Schema, schema nhận ví dụ spec, giới hạn 500 call/batch) | ✅ |
| 2026-11-05 | Phase 1d: 72 tool; `npm test` 42/42 (danh mục 72 tool khớp handler C#, schema hợp lệ); `dotnet build` plugin 0 warning | ✅ |
| 2026-11-05 | Phase 1c: `npm test` 42/42 (thêm chốt xác nhận: đếm đối tượng bị ảnh hưởng kể cả trong batch, nhận diện câu xác nhận/từ chối tiếng Việt); `dotnet test` 34/34 (thêm giao điểm, fillet, chamfer); build plugin | ✅ |
| 2026-11-05 | Phase 1b: `dotnet test` 29/29 (thêm Geometry: hình chữ nhật, đa giác, cung 3 điểm, revcloud, lineweight, hướng) ; `dotnet build` plugin | ✅ |
