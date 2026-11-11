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
| 1e | Palette tạo phiên → log sidecar có `skills loaded: ve-ha-tang, ve-kien-truc, ve-mep` | ⬜ chưa chạy | |
| 1e | Kịch bản 10 (skill tự áp dụng) | ⬜ chưa chạy | |
| 1e | Sửa SKILL.md + New → chuẩn mới được áp dụng | ⬜ chưa chạy | |
| 1e | Bundle: copy `ClaudeCAD.bundle` vào ApplicationPlugins → AutoCAD tự nạp, `CLAUDECHAT` chạy không cần NETLOAD | ⬜ chưa chạy | |
| 2 | create_layout (A3, copy_from), set_current_layout, list_layouts, delete_layout | ⬜ chưa chạy | |
| 2 | add_viewport 1:100 đúng tâm view, khóa; set_viewport đổi tỷ lệ; VP freeze layer | ⬜ chưa chạy | |
| 2 | Vẽ khung tên trên layout bằng tham số `layout` (sheet mm) | ⬜ chưa chạy | |
| 2 | plot_pdf layout và Model (extents / window), scale fit và 1:100 → file PDF đúng khổ, nét monochrome | ⬜ chưa chạy | |
| 2 | attach_xref (overlay/attach), list_xrefs trạng thái, reload, detach, bind | ⬜ chưa chạy | |
| 2 | trim_entities: line/arc/polyline/circle với 1 và nhiều cạnh cắt, đúng mảnh bị bỏ | ⬜ chưa chạy | |
| 2 | extend_entities: line/arc/polyline tới biên, auto/start/end/both | ⬜ chưa chạy | |
| 2 | insert_block với dynamic_props, get_dynamic_props (allowed values), reset_dynamic_block | ⬜ chưa chạy | |
| 3 | `install.ps1`: máy chưa có Node → báo + đề nghị winget; có Node → cài bundle; chạy lại sau khi sửa skill → skill giữ nguyên; `-Uninstall` | ⬜ chưa chạy | |
| 3 | `ClaudeCAD-Setup-<v>.exe` cài/gỡ, cảnh báo thiếu Node / chưa đăng nhập | ⬜ chưa chạy | |
| 3 | AutoCAD 2023 và 2024 nạp bản net48; AutoCAD 2025 nạp `Contents\2025` (net8); smoke 1a trên mỗi bản | ⬜ chưa chạy | |
| 3 | Cập nhật: đặt `CLAUDECAD_UPDATE_URL` tới latest.json có version cao hơn → palette báo; `CLAUDECHATUPDATE` tải, kiểm SHA-256; đóng AutoCAD → bundle được thay, skill đã sửa còn nguyên | ⬜ chưa chạy | |
| 3 | latest.json sai SHA-256 → hủy, không đụng bundle | ⬜ chưa chạy | |
| UI-1 | Nút **Chọn đối tượng**: quét chọn → chip "Kèm N"; chọn sẵn (pickfirst) → chip tự hiện; ✕ bỏ kèm; "đổi mấy cái này sang layer X" đúng đối tượng | ⬜ chưa chạy | |
| UI-2 | Nút **Lấy điểm**: 1 và nhiều điểm, tọa độ WCS đúng khi UCS xoay | ⬜ chưa chạy | |
| UI-3 | Bấm link handle trong câu trả lời → zoom + tô sáng 4 s | ⬜ chưa chạy | |
| UI-4 | **Xem kết quả (N)** sau lượt vẽ | ⬜ chưa chạy | |
| UI-5 | **Hoàn tác lượt này** = một U; chạy lệnh khác sau lượt → nút ẩn | ⬜ chưa chạy | |
| UI-6 | Ảnh: nút Ảnh, Ctrl+V ảnh chụp màn hình, kéo thả file; "vẽ theo phác thảo này" | ⬜ chưa chạy | |
| UI-7 | Dán vùng ô Excel → bảng Markdown; `/bang-toa-do` + dán bảng → bảng tọa độ | ⬜ chưa chạy | |
| UI-8 | Gõ `/` → popup mẫu, chọn bằng phím/chuột; thêm mẫu riêng ở `%AppData%\ClaudeCAD\templates.json` | ⬜ chưa chạy | |
| UI-9 | Chuẩn = Điện nước, Tỷ lệ = 1:50 → Claude dùng skill ve-mep và chữ 1:50 không cần nhắc | ⬜ chưa chạy | |
| UI-10 | ↑↓ gọi lại tin đã gửi, giữ bản nháp | ⬜ chưa chạy | |
| UI-11 | Thẻ xác nhận: Xem trước tô sáng; Đồng ý → xóa; Hủy → không xóa | ⬜ chưa chạy | |
| UI-12 | Nhật ký thao tác đúng số tool, thời gian, lỗi | ⬜ chưa chạy | |
| UI-13 | Kill node.exe giữa lượt → thẻ lỗi → **Thử lại** chạy lại đúng yêu cầu | ⬜ chưa chạy | |
| UI-14 | Đổi model / effort trong ⚙ giữa phiên; dòng usage và % hạn mức hiển thị | ⬜ chưa chạy | |
| UI-15 | Lượt dài khi đang ở cửa sổ khác → thông báo Windows; tắt được | ⬜ chưa chạy | |
| UI-16 | Markdown: đậm, danh sách, bảng, code hiển thị đúng | ⬜ chưa chạy | |
| UI-17 | Hai bản vẽ đã lưu: chuyển qua lại → lịch sử riêng; đóng/mở AutoCAD → lịch sử còn, Claude nhớ ngữ cảnh | ⬜ chưa chạy | |
| UI-18 | Copy tin nhắn; **Xuất** ra .md | ⬜ chưa chạy | |
| UI-19 | Đổi COLORTHEME của AutoCAD → palette đổi theo; A−/A+, Ctrl+lăn chuột | ⬜ chưa chạy | |
| UI-20 | **Sửa** tin cuối sau một lượt vẽ → lượt cũ được U rồi vẽ lại theo yêu cầu mới | ⬜ chưa chạy | |
| SK-1 | **+ Add skill** → hộp chọn mở đúng thư mục skill của plugin; chọn file .md hợp lệ → "✓ Đã thêm skill …", có thư mục + `skills.json` | ⬜ chưa chạy | |
| SK-2 | File thiếu description / không có phần đầu → thẻ lỗi nêu lý do, không thêm | ⬜ chưa chạy | |
| SK-3 | Thêm trùng tên → hỏi thay; chọn lại file trong thư mục plugin → chỉ bật lại | ⬜ chưa chạy | |
| SK-4 | Tin nhắn sau khi thêm dùng đúng skill mới, Claude vẫn nhớ cuộc chat trước | ⬜ chưa chạy | |
| SK-5 | **Skill (N)**: bật/tắt (skill tắt không được dùng), Sửa → lưu file → tự nạp lại, Xóa, Mở thư mục | ⬜ chưa chạy | |
| SK-6 | Ô **Chuẩn** liệt kê skill đang bật kể cả skill mới thêm | ⬜ chưa chạy | |
| V6-1 | Vẽ mặt bằng lớn → nhật ký có "Nhìn bản vẽ", khung nhìn của cậu không đổi, không thêm bước U; nút **Chụp** đính kèm đúng vùng đang xem (màu, nét rõ) | ⬜ chưa chạy | |
| V6-2 | Sau lượt vẽ có 1–3 nút gợi ý; bấm là gửi, chuột phải đưa vào ô nhập; tắt trong ⚙ thì không hiện | ⬜ chưa chạy | |
| V6-3 | **💾 Lưu macro** → **Macro ▸ ▶ Chạy** vẽ lại giống hệt (lệnh offset/move dùng đúng đối tượng mới); **📍 Tại điểm** dời đúng điểm chọn; một U hoàn tác cả macro; Stop dừng giữa chừng | ⬜ chưa chạy | |
| V6-4 | **☆ Mẫu** → gõ / thấy mẫu mới; **✨ Tạo skill từ cuộc chat** → thẻ bản nháp → Xem/sửa mở file → Thêm vào plugin → "✓ Đã thêm skill" | ⬜ chưa chạy | |
| V6-5 | Bật **🔒 Chỉ xem** → yêu cầu vẽ/xóa bị từ chối (Claude giải thích), hỏi đáp/đo vẫn được; đóng mở lại bản vẽ vẫn giữ trạng thái; macro không chạy | ⬜ chưa chạy | |
| V6-6 | **Ghi chú**: lưu ghi chú dự án → có `ClaudeCAD-ghichu.md` cạnh DWG; bản vẽ khác cùng thư mục cũng thấy; Claude áp dụng (vd. tường 220) không cần nhắc | ⬜ chưa chạy | |
| V6-7 | Gõ `@A-` → danh sách layer/block của bản vẽ; Enter chèn tên; `a@b.com` không bật | ⬜ chưa chạy | |
| V6-8 | **Ctrl+Shift+Space** khi đang vẽ → palette mở, con trỏ ở ô nhập; lệnh **CC** tương tự; tắt trong ⚙ | ⬜ chưa chạy | |
| V6-9 | "Vẽ cả mặt bằng tầng 1" → thẻ kế hoạch, chưa vẽ gì; **Làm** → vẽ đúng kế hoạch; **Sửa kế hoạch** đưa chữ vào ô nhập | ⬜ chưa chạy | |
| V7-1 | Sau một lượt chat, sửa tay (dời 2 tường, xóa 1 cửa, thêm chữ) → chat "ghi lại kích thước" → dòng ✎ đúng số liệu; Claude đọc lại đúng các đối tượng đó, không vẽ lại cửa đã xóa | ⬜ chưa chạy | |
| V7-2 | Đóng khung chat, sửa tay, mở lại khung chat → vẫn được báo; sửa trước khi mở khung chat lần đầu trong phiên cũng được báo | ⬜ chưa chạy | |
| V7-3 | Chat, lưu, đóng AutoCAD; mở lại hôm sau sửa tay rồi chat → báo "từ lần chat trước"; bản vẽ > 20.000 đối tượng không làm AutoCAD khựng quá 2–3 s | ⬜ chưa chạy | |
| V7-4 | Bật ⚙ **Lưu biên bản cạnh bản vẽ** → có thư mục ẩn `.claudecad`; máy khác có cài plugin mở cùng thư mục, sửa, chat → được báo | ⬜ chưa chạy | |
| V7-5 | Chat sửa bản vẽ → **Phiên bản (1)**; trong 30 phút chat tiếp không tạo bản mới; sau 30 phút có bản mới; quá 10 bản thì bản cũ nhất bị xóa | ⬜ chưa chạy | |
| V7-6 | **Phiên bản ▸ Xem** → tab chỉ xem đúng trạng thái trước lượt đó, thanh "Đang xem phiên bản…", ô chat khóa; **Đóng — về bản hiện tại** đóng tab, về đúng bản vẽ, lịch sử chat không đổi | ⬜ chưa chạy | |
| I-6 | Trang cuối bộ cài có ô **Đăng nhập Claude ngay** (máy chưa đăng nhập) → Finish → trình duyệt mở, đồng ý → mở AutoCAD dùng được ngay; máy đã đăng nhập: ô không hiện; `/VERYSILENT`: không mở trình duyệt | ⬜ chưa chạy | |
| I-7 | Bỏ qua đăng nhập lúc cài → lần đầu mở AutoCAD, trình duyệt tự mở trang đăng nhập (không cần bấm nút) | ⬜ chưa chạy | |
| K-1 | Máy chưa kích hoạt chạy `ClaudeCAD-Setup.exe` → trang **Kích hoạt** hiện mã máy `CC-…` + "Liên hệ Zalo owner: 0344644415"; Gmail chủ plugin nhận email có tên máy, user Windows, mã máy, KEY | ⬜ chưa chạy | |
| K-2 | Nhập key sai / key của máy khác → báo lỗi, không cho Next; nhập đúng key → cài tiếp, có `%LocalAppData%\ClaudeCAD\license.txt` | ⬜ chưa chạy | |
| K-3 | Chạy lại Setup.exe (cài đè) trên máy đã kích hoạt → không hỏi key | ⬜ chưa chạy | |
| K-4 | Xóa `license.txt` rồi mở AutoCAD → khung chat hiện thẻ 🔒 Kích hoạt, gửi tin nhắn bị chặn; dán key → "Đã kích hoạt", hiện thẻ Bắt đầu | ⬜ chưa chạy | |
| K-5 | Sao chép `license.txt` sang máy khác → plugin báo "giấy phép của máy khác" | ⬜ chưa chạy | |
| K-6 | Máy không có mạng: bộ cài báo không gửi được; mã offline (`taoKeyThuCong`, 2 dòng `CCL1|` + `CCS1|…|offline|`) dán vào ô key của khung chat → dùng được, không bị khóa sau 7 ngày | ⬜ chưa chạy | |
| K-7 | `ClaudeCAD-Setup.exe /VERYSILENT /KEY=<key>` → cài ngầm và kích hoạt | ⬜ chưa chạy | |
| K-8 | Cài **không nhập key**: bấm Next → hộp thoại "Chưa nhập key… chờ chủ plugin duyệt?" → Yes → cài xong; Google Sheet "Thiết bị" có dòng **Chờ duyệt** (tên máy, user, ngày xin); mở AutoCAD → thẻ 🔒 có nút **Kiểm tra lại**; đổi ô Trạng thái thành `Active` → bấm **Kiểm tra lại** → "Chủ plugin đã duyệt máy này", dùng được không cần key; Sheet có Ngày kích hoạt. Lần 2 (máy khác): đổi `Active` khi AutoCAD đang đóng → mở AutoCAD → **tự mở khóa** sau vài giây, không cần bấm gì | ⬜ chưa chạy | |
| K-9 | Máy đang dùng bị đổi thành `Disabled` → mở lại AutoCAD (hoặc đợi chu kỳ ~6 giờ): khung chat khóa, thẻ 🔒 "Máy này đã bị chủ plugin tạm khóa…", trợ lý dừng, gửi tin/macro bị chặn; đổi lại `Active` + **Kiểm tra lại** → dùng tiếp ngay, không cần key mới | ⬜ chưa chạy | |
| K-10 | Máy `Disabled` nhập đúng key cũ → bị từ chối ("tạm khóa"); bấm **Gửi yêu cầu kích hoạt** → cũng báo tạm khóa, chủ plugin **không** nhận thêm email | ⬜ chưa chạy | |
| K-11 | Máy đã kích hoạt, ngắt mạng → vẫn dùng bình thường; chỉnh ngày hệ thống +8 ngày (giả lập quá 7 ngày không kiểm tra được) → mở AutoCAD: thẻ 🔒 "quá 7 ngày…"; nối mạng + **Kiểm tra lại** → mở khóa | ⬜ chưa chạy | |
| K-13 | Máy đã `Active` xóa `license.txt` rồi mở AutoCAD → tự lấy lại giấy phép sau vài giây, chủ plugin **không** nhận thêm email xin kích hoạt | ⬜ chưa chạy | |
| K-12 | Sau khi mở AutoCAD, cột **Lần cuối dùng** + **Phiên bản** của máy đó trong Sheet được cập nhật (tối đa 30 phút/lần); sửa tay `status.txt` (đổi `disabled` thành `active`) → plugin coi là không hợp lệ và khóa tới khi kiểm tra lại | ⬜ chưa chạy | |
| K-14 | Máy bị `Disabled`, xóa file `Contents\activation.json` trong thư mục plugin rồi mở AutoCAD → vẫn bị khóa (link máy chủ nằm trong DLL) | ⬜ chưa chạy | |
| K-15 | Máy đã kích hoạt, ngắt mạng, chỉnh ngày Windows **lùi** 3 ngày → mở AutoCAD: thẻ 🔒 "Đồng hồ máy đang chậm hơn lần dùng trước…"; chỉnh lại giờ đúng + nối mạng + **Kiểm tra lại** → mở khóa | ⬜ chưa chạy | |
| K-16 | Trang kích hoạt của bộ cài: lúc đang gửi yêu cầu thấy chữ "Đang gửi yêu cầu… (có thể mất tới 20 giây…)" (không đứng trắng); `ClaudeCAD-Setup.exe /KEY=<key>` (không /VERYSILENT) → ô key điền sẵn, chủ plugin **không** nhận email xin key | ⬜ chưa chạy | |
| R-1 | Macro có bo góc (fillet) giữa 2 đường vừa vẽ → chạy lại macro ở chỗ khác: bo góc 2 đường mới, 2 đường cũ giữ nguyên | ⬜ chưa chạy | |
| R-2 | Bật Phiên bản, yêu cầu Claude "zoom vào khung tên rồi đổi chữ" → có phiên bản mới trước khi sửa; lượt chỉ zoom không có nút Hoàn tác | ⬜ chưa chạy | |
| R-3 | "Vẽ lại rồi xóa đi các đường cũ" trên layer có > 20 đối tượng → Claude hỏi xác nhận trước khi xóa | ⬜ chưa chạy | |
| R-4 | Hết giới hạn, bật "Tự gửi lại", chuyển sang bản vẽ khác → toast "Đã hủy tự gửi lại", không gửi gì vào bản vẽ mới | ⬜ chưa chạy | |
| R-5 | Nút Chụp / Claude tự kiểm tra bằng ảnh 20 lần liên tiếp → AutoCAD không chậm dần | ⬜ chưa chạy | |
| R-6 | Gửi báo cáo lỗi → mở các file log trong .zip: không có nội dung tin nhắn / chữ trong bản vẽ | ⬜ chưa chạy | |
| S-1 | + Add skill → chọn 3 file .md cùng lúc → cả 3 vào thư viện và có dấu ✓ trong đoạn chat hiện tại | ⬜ chưa chạy | |
| S-2 | Bấm − một skill → Skill (N) giảm, skill nằm ở phần Thư viện; yêu cầu tiếp theo Claude không theo chuẩn của skill đó | ⬜ chưa chạy | |
| S-3 | Chat mới / mở bản vẽ khác chưa có chat → dùng đúng các skill của đoạn chat gần nhất | ⬜ chưa chạy | |
| S-4 | Hai bản vẽ, mỗi bản một bộ skill khác nhau → chuyển qua lại, danh sách đúng của từng bản vẽ | ⬜ chưa chạy | |
| S-5 | Sửa SKILL.md rồi lưu → chỉ 1 toast "Skill đã thay đổi" | ⬜ chưa chạy | |
| D-1 | Đổi tài khoản / lưu API key → trợ lý khởi động lại; Task Manager không còn sót tiến trình claude.exe cũ; khung chat không đứng, không hiện "Mất kết nối" | ⬜ chưa chạy | |
| D-2 | `CLAUDECHATSELFTEST` đạt hết → thư mục `%TEMP%\ClaudeCAD-selftest-*` bị xóa, ảnh `selftest-*.png` nằm cạnh báo cáo; có mục ✗ → thư mục được giữ | ⬜ chưa chạy | |
| D-3 | Mở ⚙ hai lần trong 30 giây → lần hai hiện tài khoản ngay | ⬜ chưa chạy | |
| I-1 | Tải `ClaudeCAD-Setup.exe` từ link releases/latest, chạy trên máy **chưa có Node.js và Claude Code** → cài xong không hỏi gì thêm (ngoài SmartScreen) | ⬜ chưa chạy | |
| I-2 | Mở AutoCAD lần đầu sau khi cài → không có cảnh báo SECURELOAD; khung chat tự mở với thẻ **Bắt đầu** (Node đi kèm ✓, trợ lý ✓, tài khoản ✗) | ⬜ chưa chạy | |
| I-3 | Bấm **Đăng nhập Claude** → cửa sổ đen + trình duyệt đăng nhập Pro/Max → "✓ Đã đăng nhập Claude", gửi tin nhắn được ngay | ⬜ chưa chạy | |
| I-4 | Chưa đăng nhập mà gửi tin → thẻ lỗi có nút **Đăng nhập Claude** (không còn hướng dẫn terminal) | ⬜ chưa chạy | |
| I-5 | Mở AutoCAD lần sau → thẻ Bắt đầu không tự hiện lại; ⚙ ▸ Hướng dẫn bắt đầu mở lại được | ⬜ chưa chạy | |
| T-1 | Lưu macro / ghi chú / mẫu lệnh, đăng nhập, mất kết nối (kill node.exe) → toast hiện trên vùng chat rồi tự ẩn | ⬜ chưa chạy | |
| T-2 | Dùng gần hết hạn mức → toast "Sắp hết hạn mức"; hết hạn mức → thẻ trong chat có giờ làm mới, **Tự gửi lại** gửi lại đúng yêu cầu sau khi làm mới | ⬜ chưa chạy | |
| R-1 | ⚙ ▸ **Gửi báo cáo lỗi** → zip trên Desktop, Explorer mở; trong zip có thong-tin.txt, log, tự kiểm tra, log cài đặt; không có API key / chat | ⬜ chưa chạy | |
| A-1 | ⚙ ▸ Tài khoản AI hiện đúng tài khoản; **Đổi tài khoản** → đăng nhập tài khoản khác → chat chạy bằng tài khoản mới | ⬜ chưa chạy | |
| A-2 | **Dùng API key** → dán key → dòng trạng thái "API key"; **Xóa key đã lưu** → quay lại tài khoản Claude; **Đăng xuất** → gửi tin hiện thẻ Đăng nhập | ⬜ chưa chạy | |
| ST-1 | Gõ **CLAUDECHATSELFTEST** → mở bản vẽ tạm, chạy các bước, đóng lại, về bản vẽ của cậu; Notepad mở báo cáo — gửi file này cho người phát triển | ⬜ chưa chạy | |
| L-1 | **Chọn đối tượng** quét qua tường, cửa, chữ → chip hiện đúng các layer và số lượng; bỏ tick A-TEXT → "Kèm x/y đối tượng"; Claude chỉ sửa phần còn tick | ⬜ chưa chạy | |
| L-2 | Nút **Layer** → danh sách đúng (màu, số đối tượng, khóa/tắt), lọc theo tên, tick 2 layer → **Dùng layer đã chọn** → chip "Layer: …" | ⬜ chưa chạy | |
| L-3 | Chọn layer + "đổi màu sang đỏ" / "khóa lại" → đổi thuộc tính layer, không hỏi xác nhận; "tăng chiều cao chữ lên 300" trên layer có > 20 chữ → thẻ Đồng ý / Hủy, Đồng ý thì sửa đúng | ⬜ chưa chạy | |
| L-4 | Chọn một đối tượng → **→ Cả layer** → chip chuyển thành layer của nó; gửi xong chip tự bỏ | ⬜ chưa chạy | |
| V6-10 | Bảng **Thay đổi** đúng số thêm/sửa/xóa theo layer, bấm dòng zoom tới; còn khi mở lại lịch sử và trong file Xuất | ⬜ chưa chạy | |

## Đã kiểm chứng tự động trên cloud (Linux)

| Ngày | Hạng mục | Kết quả |
| --- | --- | --- |
| 2026-11-05 | `npm run build` sidecar | ✅ |
| 2026-11-05 | `npm test` — 32 test: protocol (encode/decode, UTF-8 cắt giữa chunk, CRLF, BOM, dòng quá dài), ToolRpc (tương quan id, timeout, response muộn, mất kết nối, failAll), tích hợp qua socket thật (echo tool, lỗi plugin, timeout, đứt pipe, dòng hỏng, tiến trình sidecar thật nối pipe rồi tự thoát), AgentSession với query giả (stream text, nhiều lượt, cancel, new session, process chết, lỗi đăng nhập, skill plugin) | ✅ 32/32 |
| 2026-11-05 | `dotnet build` plugin net48 với reference AutoCAD.NET 24.1.51000 | ✅ 0 warning |
| 2026-11-05 | `dotnet test plugin/ClaudeCAD.Core.Tests` — JsonCodec, Params | ✅ 16/16 |
| 2026-11-05 | Phase 1b: `npm test` 38/38 (thêm: danh mục tool TS khớp handler C#, mọi schema chuyển được sang JSON Schema, schema nhận ví dụ spec, giới hạn 500 call/batch) | ✅ |
| 2026-11-06 | v0.9.0 tối ưu + review: `dotnet test` 102/102 (gom macro theo lô, chỉ ánh xạ tham số handle, bỏ bước dùng đối tượng chưa tạo lại, mẫu lệnh không che mẫu có sẵn, file mẫu hỏng); `npm test` 95/95 (`turn_id`, mô tả skill trong ngoặc kép); build net48 + net8 ✅ | ✅ |
| 2026-11-06 | v0.8.0 chọn layer: `dotnet test` 98/98 (đếm theo layer, lọc theo layer bỏ tick, tìm layer, lịch sử giữ layer đã chọn); `npm test` 93/93 (`attachments.layers`, `selection.layers`, câu dặn Claude phân biệt thuộc tính layer / đối tượng); build net48 + net8 ✅ | ✅ |
| 2026-11-10 | v0.9.4 sửa sau rà soát (30 mục): `dotnet test` lõi 115/115 (macro handle1/handle2, log không lẫn chat, che thêm token); `npm test` 114/114 (cổng xác nhận câu ngắn + cộng dồn cả lượt, kết quả lượt cũ sau Dừng, `success`+`is_error`, gợi ý trùng, chuyển bản vẽ giữa lượt, Read chỉ trong thư mục skill, dữ liệu bản vẽ trong ngoặc kép, tin quá dài bỏ qua tới dòng sau, thời gian chờ theo lệnh, Apps Script giới hạn 10 email/giờ); build net48 + net8 ✅ | ✅ |
| 2026-11-10 | v0.9.4 quản lý thiết bị: `dotnet test` lõi 118/118 (VerifyStatus: chữ ký, máy khác, quá 7 ngày, ngày tương lai, mã offline; khóa thật ký được trạng thái); `npm test` 119/119 (Apps Script với giả lập Google Sheet: dòng Chờ duyệt, Active cấp giấy phép không cần key, Disabled chặn key + email, Lần cuối dùng / Phiên bản, `kiemTra`); build net48 + net8 ✅; CI Windows ✅ | ✅ |
| 2026-11-10 | Máy chủ kích hoạt **thật** của chủ plugin (gọi từ cloud): `ping` ✅; `request` mã máy thử `CC-7E57-0000-0001` → `sent: true`; key bịa → từ chối; `status` → `pending`, chữ ký khớp khóa công khai trong plugin ✅; chủ plugin nhận email + thấy dòng Chờ duyệt ✅; đổi ô thành `Active` → `status` trả `active` **kèm giấy phép** (không cần key), cả hai chữ ký hợp lệ ✅ | ✅ |
| 2026-11-10 | Máy chủ thật, **bản 3** (chặn công thức, trần 40 email/ngày, kiểm trùng trong khóa): `ping` ✅; máy thử `CC-7E57-0000-0002` tên `=THU-CONG-THUC` → `sent: true` (bộ đếm ngày PropertiesService chạy, không lỗi quyền), chủ plugin thấy ô Thiết bị hiện đúng chữ `=THU-CONG-THUC` (không chạy như công thức) ✅; `status` → `pending`, chữ ký hợp lệ ✅; máy thử cũ vẫn `active` + giấy phép ✅. Đã xóa 3 dòng máy thử | ✅ |
| 2026-11-11 | **Đổi cặp khóa kích hoạt** (trước khi phát hành, chủ plugin chọn): cặp RSA + SECRET mới tạo bằng lệnh kín, không có trong nhật ký phiên (đã kiểm: 0 lần), bản sao trên cloud đã xóa an toàn. Máy chủ thật sau khi dán Code.gs mới + Phiên bản mới: `ping` ✅; `request` máy thử `CC-7E57-0000-0004` → email ✅; key bịa bị từ chối ✅; `status` → chữ ký khớp `LicenseLogic.PublicModulus` mới ✅ (khóa cũ không còn khớp); key thử của `kiemTra` kích hoạt được máy thử, giấy phép ký bằng khóa mới ✅. CI Windows ✅ | ✅ |
| 2026-11-09 | v0.9.4 khóa key: `dotnet test` lõi 112/112 (mã máy khớp công thức bộ cài, chuẩn hóa key, chữ ký giấy phép: đúng máy / máy khác / sửa ngày / sửa chữ ký, khóa công khai nhúng khớp khóa của máy chủ); `npm test` 100/100 (Apps Script chạy trong VM: email cho chủ plugin có tên máy + key, chống gửi trùng 30 phút, key đúng → giấy phép ký đúng, key sai / máy khác bị từ chối, mã offline); build plugin net48 + net8 ✅ | ✅ |
| 2026-11-06 | v0.7.0 sửa tay + phiên bản: `dotnet test` 96/96 (biên bản nén/đọc lại, mã kiểm tra bỏ nhiễu số thực và "-0", so đầy đủ / bản vẽ lớn theo layer, báo cáo sửa tay giới hạn 300 mã, phiên bản 30 phút + giữ 10 bản); `npm test` 91/91 (`manual_changes` lọc mã sai, câu dặn Claude đọc lại); build net48 + net8 (Wblock/SaveAs, mở DWG chỉ xem, CloseAndDiscard) ✅ | ✅ |
| 2026-11-06 | v0.6.0 tiện ích khung chat: `dotnet test` lõi 88/88 (macro ghi/ánh xạ handle/lưu, bảng thay đổi, `@`, ghi chú dự án, mẫu lệnh, cài đặt mới, lịch sử giữ Chỉ xem + ghi chú + thay đổi); `npm test` 89/89 (ảnh `capture_view` thành khối image MCP, thẻ kế hoạch, gợi ý, bản nháp skill, ngữ cảnh mới, `promptSuggestions`); build plugin net48 + net8 (API GraphicsSystem off-screen, PreTranslateMessage) ✅ | ✅ |
| 2026-11-06 | v0.5.0 skill trên khung chat: `dotnet test` lõi 68/68 (SkillStore: nhập .md, tên từ file, file thiếu description, trùng tên, chọn lại file đã có, thư mục SKILL.md kèm file tham khảo, bật/tắt, xóa, `skills.json`); `npm test` 75/75 (chỉ nạp skill đang bật, reload_skills giữ ngữ cảnh) | ✅ |
| 2026-11-05 | v0.4.0 khung chat 20 tính năng: `npm test` 70/70 (nội dung tin nhắn, giao thức mới, model/effort, chuyển phiên + khôi phục lỗi, usage, rate limit, thẻ xác nhận); `dotnet test` lõi 56/56 (markdown, lịch sử, cài đặt, mẫu lệnh, bảng Excel); build plugin net48 + net8 0 warning | ✅ |
| 2026-11-05 | **CI GitHub Actions `windows-latest`** (run 3–5, commit 3b87661): `npm ci` + `npm test` 58/58 với **named pipe Windows thật** (gồm tiến trình sidecar nối pipe), `dotnet test` lõi, build plugin net48 + net8.0-windows, make-bundle (node_modules win32-x64) + zip + latest.json, Inno Setup → `ClaudeCAD-Setup-0.3.0.exe`, upload artifact | ✅ |
| 2026-11-05 | Phase 3: `npm test` 58/58 (thêm: phiên bản thống nhất 4 nơi, manifest đúng dll theo series); `dotnet test` 43/43 (thêm UpdateLogic); build **net48 + net8.0-windows** (AutoCAD.NET 24.1.51000 và 25.0.1) 0 warning; `make-bundle --zip` tạo bundle 2 dll + zip + latest.json | ✅ |
| 2026-11-05 | Phase 2: 89 tool; `npm test` 56/56 (danh mục khớp handler C#); `dotnet test` 35/35 (thêm ExtensionTarget); build plugin 0 warning | ✅ |
| 2026-11-05 | Phase 1e: `npm test` 56/56 (thêm: 3 skill đúng chuẩn Agent Skills, có "Quy trình chuẩn bị", chỉ nhắc tool có thật, JSON ví dụ hợp lệ; system prompt đủ 10 quy tắc) | ✅ |
| 2026-11-05 | Phase 1e: **Agent SDK thật** trên Linux với plugin giả: init chỉ có `Read`, `Skill` + 72 tool `mcp__claudecad__*`, MCP server `claudecad` connected, `permissionMode: dontAsk`, 3 skill nạp qua plugin `claudecad-skills`; Claude gọi `ping` và stream tiếng Việt, `turn_end` đúng | ✅ |
| 2026-11-05 | Dry run kịch bản 2 (bản vẽ giả lập): Claude gọi get_drawing_info → list_layers → list_styles → **một** batch 30 call (tạo layer/style theo skill, tường, cửa, dim 4 phía, text) → zoom_extents; layer A-WALL/A-DOOR/A-DIM/A-TEXT; chữ 350/250 đúng skill | ✅ (mô phỏng) |
| 2026-11-05 | Dry run kịch bản 4: "Xóa toàn bộ đối tượng trên layer A-TEMP" → count_entities, báo 30 đối tượng, hỏi xác nhận, dừng lượt; "đồng ý" → list_entities + erase_entities | ✅ (mô phỏng) |
| 2026-11-05 | `node bundle/make-bundle.mjs` trên Linux → bundle có dll, agent + node_modules win32-x64 (gồm `claude-agent-sdk-win32-x64`), skills, PackageContents.xml hợp lệ | ✅ |
| 2026-11-05 | Phase 1d: 72 tool; `npm test` 42/42 (danh mục 72 tool khớp handler C#, schema hợp lệ); `dotnet build` plugin 0 warning | ✅ |
| 2026-11-05 | Phase 1c: `npm test` 42/42 (thêm chốt xác nhận: đếm đối tượng bị ảnh hưởng kể cả trong batch, nhận diện câu xác nhận/từ chối tiếng Việt); `dotnet test` 34/34 (thêm giao điểm, fillet, chamfer); build plugin | ✅ |
| 2026-11-05 | Phase 1b: `dotnet test` 29/29 (thêm Geometry: hình chữ nhật, đa giác, cung 3 điểm, revcloud, lineweight, hướng) ; `dotnet build` plugin | ✅ |
