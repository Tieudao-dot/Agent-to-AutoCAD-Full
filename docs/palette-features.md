# Khung chat ClaudeCAD — 20 tính năng (v0.4.0)

Phần giao diện của palette `CLAUDECHAT` sau đợt nâng cấp. Code: `plugin/ClaudeCAD.Plugin/UI/*`, `ChatController.cs`, `Drawing/Interaction.cs`, lõi thuần trong `Core/MarkdownLite.cs`, `ChatStore.cs`, `PaletteSettings.cs`, `QuickTemplates.cs`; phía sidecar `agent/src/agent/content.ts`, `session.ts`, `tools/interaction.ts`.

## A. Tương tác hai chiều với bản vẽ

| # | Tính năng | Cách dùng | Ghi chú kỹ thuật |
| --- | --- | --- | --- |
| 1 | Chọn đối tượng gửi kèm | Nút **Chọn đối tượng** → quét chọn trên bản vẽ → chip "Kèm N đối tượng". Nếu đang có đối tượng được chọn sẵn (pickfirst) thì chip tự hiện; bấm ✕ để không gửi kèm, bấm chữ để xem chúng trên bản vẽ. | Lệnh phụ `CLAUDECAD_PICK` (`GetSelection`); theo dõi `Document.ImpliedSelectionChanged`. Handle + tóm tắt loại được ghi vào đầu tin nhắn cho Claude. |
| 2 | Lấy điểm | Nút **Lấy điểm** → bấm một hoặc nhiều điểm (Enter để xong) → `[12400, 5600], …` được chèn vào ô nhập. | Lệnh phụ `CLAUDECAD_POINT`, điểm đổi từ UCS sang WCS. |
| 3 | Handle bấm được | Claude viết `[3 cửa đi](handle:2F4A,2F4B)` → bấm để zoom và tô sáng. | Quy ước trong system prompt; `MarkdownView` bắt link `handle:`. |
| 4 | Xem kết quả | Nút **Xem kết quả (N)** dưới mỗi câu trả lời → zoom tới vùng vừa vẽ/sửa, tô sáng 4 giây. | Handle lấy từ kết quả các tool trong lượt. |
| 5 | Hoàn tác lượt này | Nút dưới câu trả lời mới nhất (khi lượt đó có sửa bản vẽ) → một lệnh U. | Nút tự ẩn khi cậu chạy bất kỳ lệnh nào khác (U lúc đó sẽ không còn đúng lượt). |

## B. Đầu vào

| # | Tính năng | Cách dùng | Ghi chú |
| --- | --- | --- | --- |
| 6 | Đính kèm ảnh | Nút **Ảnh**, dán **Ctrl+V** ảnh từ clipboard, hoặc kéo thả file ảnh vào palette (tối đa 5 ảnh). | Thu nhỏ còn cạnh dài 1568 px, PNG (JPEG nếu > 3.5 MB), gửi dạng image block của Agent SDK. |
| 7 | Dán bảng Excel | Copy vùng ô trong Excel → Ctrl+V vào ô nhập → thành bảng Markdown. | ≥ 2 hàng, ≥ 2 cột, phân cách tab. |
| 8 | Mẫu lệnh nhanh | Gõ `/` → danh sách mẫu (`/luoi-truc`, `/mat-bang`, `/bang-toa-do`…), ↑↓ chọn, Enter chèn, giá trị đầu tiên được bôi đen để sửa. | Mẫu có sẵn: `templates/templates.json` (bundle: `Contents\templates`). Mẫu riêng: `%AppData%\ClaudeCAD\templates.json` (cùng tên thì ghi đè). Cú pháp chỗ điền: `{{nhãn=giá trị mặc định}}`. |
| 9 | Thanh ngữ cảnh | Chọn **Chuẩn** (Tự động / Kiến trúc / Điện nước / Hạ tầng) và **Tỷ lệ** (1:20 … 1:1000). | Gửi kèm mọi tin nhắn; Claude nạp đúng skill và chỉnh chiều cao chữ/dim theo tỷ lệ. Lưu trong `settings.json`. |
| 10 | Gọi lại tin đã gửi | ↑ / ↓ trong ô nhập (khi con trỏ ở dòng đầu / cuối). | 100 tin gần nhất, giữ cả bản nháp đang gõ. |

## C. Theo dõi và kiểm soát

| # | Tính năng | Cách dùng | Ghi chú |
| --- | --- | --- | --- |
| 11 | Thẻ xác nhận | Khi Claude cần xác nhận (xóa theo điều kiện, sửa > 20 đối tượng…): thẻ có **Đồng ý / Hủy / Xem trước**. | Tool cục bộ `request_confirmation` của sidecar. Nút Đồng ý gửi `confirm: "yes"`, được chốt xác nhận ở sidecar chấp nhận. |
| 12 | Nhật ký thao tác | "Thao tác (N) · 1.2 s" ở đầu mỗi câu trả lời → mở ra xem từng tool: kết quả, thời gian, lỗi. | `tool_status` mở rộng: `ms`, `summary`, `error`, `handles`. |
| 13 | Thử lại | Thẻ lỗi (mất kết nối, timeout, hết hạn mức, lỗi đăng nhập…) có nút **Thử lại** gửi lại đúng yêu cầu (kèm ảnh/vùng chọn). | |
| 14 | Mức dùng, model, effort | Dòng nhỏ dưới trạng thái: token lượt vừa rồi, chi phí ước tính của phiên, % hạn mức gói. ⚙ → chọn model (Opus/Sonnet/Haiku 5.5) và mức suy nghĩ. | `setModel` + `applyFlagSettings({effortLevel})` áp dụng ngay giữa phiên; sự kiện `rate_limit_event`. |
| 15 | Thông báo Windows | Lượt chạy > 8 s xong khi cậu đang ở ứng dụng khác → thông báo góc màn hình. Tắt trong ⚙. | NotifyIcon balloon (toast trên Windows 10/11). |

## D. Tiện ích chat

| # | Tính năng | Cách dùng | Ghi chú |
| --- | --- | --- | --- |
| 16 | Markdown | In đậm, nghiêng, danh sách, bảng, code, trích dẫn, link. | Render lại tối đa mỗi 150 ms khi đang stream. |
| 17 | Lịch sử theo bản vẽ | Mỗi bản vẽ một cuộc chat; chuyển bản vẽ là chuyển chat; mở lại AutoCAD vẫn còn và Claude nhớ ngữ cảnh. | `%LocalAppData%\ClaudeCAD\chats\chat-<hash>.json` + session id của Agent SDK (`switch_session` → `resume`). Không khôi phục được thì tự mở phiên mới và gửi lại tin nhắn. Bản vẽ chưa lưu không giữ lịch sử qua lần mở AutoCAD sau. |
| 18 | Copy / xuất | Nút **Copy** ở từng tin; **Xuất** → file `.md` (kèm nhật ký thao tác). | |
| 19 | Sáng/tối, cỡ chữ | Theo `COLORTHEME` của AutoCAD (hoặc chọn cố định trong ⚙); cỡ chữ bằng A−/A+ hoặc Ctrl + lăn chuột. | |
| 20 | Sửa và gửi lại | Nút **Sửa** trên tin nhắn cuối → sửa → Gửi. Nếu lượt trước đã thay đổi bản vẽ thì tự U trước rồi mới gửi (Claude được báo lượt trước đã hoàn tác). | |

## E. v0.6.0 — 10 tiện ích mới

| # | Tính năng | Cách dùng | Ghi chú |
| --- | --- | --- | --- |
| 21 | Claude tự nhìn bản vẽ | Sau việc vẽ lớn Claude tự chụp vùng vừa vẽ (`capture_view`) để soát lỗi (chữ đè, ký hiệu lệch tường, thiếu lỗ cửa) rồi sửa. Nút **Chụp** gửi vùng đang xem kèm tin nhắn ("chỗ này sai"). Tắt tự kiểm tra trong ⚙. | Vẽ model space vào thiết bị đồ họa ẩn (GraphicsSystem off-screen): khung nhìn của cậu không đổi, không thêm bước undo. Mỗi ảnh ≈ 1–1,6k token. |
| 22 | Gợi ý bước tiếp theo | 1–3 nút dưới câu trả lời ("Ghi kích thước các phòng"…). Bấm là gửi; chuột phải để sửa trước khi gửi. Tắt trong ⚙. | Tool `suggest_next_steps` + gợi ý dự đoán của Agent SDK (`promptSuggestions`, gần như miễn phí). |
| 23 | Macro — chạy lại không cần Claude | Dưới câu trả lời: **💾 Lưu macro** → đặt tên. Nút **Macro (N)**: **▶ Chạy** (đúng tọa độ cũ) hoặc **📍 Tại điểm** (chọn điểm chèn, macro được dời tới đó), **Xóa**, **Mở thư mục**. | Ghi lại các lệnh vẽ của lượt (không ghi zoom/lưu/purge/undo); khi chạy lại, handle tạo ở bước trước được ánh xạ sang handle mới. Không tốn token, một lệnh U hoàn tác cả macro. Lưu ở `%AppData%\ClaudeCAD\macros\*.json`. |
| 24 | Lưu mẫu lệnh, tạo skill từ cuộc chat | **☆ Mẫu** trên tin nhắn của cậu → thành mẫu `/` mới. **Skill (N) ▸ ✨ Tạo skill từ cuộc chat này** → Claude soạn `SKILL.md` từ các chuẩn đã chỉnh → thẻ **Xem / sửa** · **Thêm vào plugin**. | Mẫu lưu vào `%AppData%\ClaudeCAD\templates.json`. Bản nháp skill ở `%LocalAppData%\ClaudeCAD\skill-drafts`; "Thêm" đi qua đúng luồng + Add skill. |
| 25 | Chỉ xem | Nút **🔓 Sửa được / 🔒 Chỉ xem** trên thanh ngữ cảnh, lưu theo bản vẽ. Khi bật, Claude chỉ đọc, đo, xem, trả lời. | Plugin chặn mọi tool ghi (cả trong batch và `undo_last_turn`) với mã `READ_ONLY`; zoom vẫn được. Macro cũng không chạy. |
| 26 | Ghi chú dự án | Nút **Ghi chú** → hai ô: **Dự án** (dùng chung mọi bản vẽ trong thư mục, file `ClaudeCAD-ghichu.md` cạnh DWG) và **Riêng bản vẽ này**. Gửi kèm mọi tin nhắn; nút có dấu • khi đang có ghi chú. | Tối đa 4000 ký tự mỗi ô. Ghi chú thắng skill khi mâu thuẫn. |
| 27 | Gõ `@` chọn tên trong bản vẽ | Gõ `@` → danh sách layer, block, kiểu chữ, kiểu kích thước, layout của bản vẽ; ↑↓ Enter để chèn tên (tên có dấu cách được đặt trong ngoặc kép). | Không bật với địa chỉ e-mail (`a@b`). Layer/block của xref bị bỏ. |
| 28 | Phím tắt | **Ctrl+Shift+Space** trong AutoCAD hoặc lệnh **CC** → mở palette, con trỏ ở ô nhập. Tắt phím tắt trong ⚙. | `Application.PreTranslateMessage`. |
| 29 | Kế hoạch trước việc lớn | Việc nhiều bước (cả mặt bằng, cả hệ MEP, > ~50 đối tượng) → thẻ **Kế hoạch** với các bước, kích thước, giả định; **Làm** / **Sửa kế hoạch** / **Bỏ**. Tắt trong ⚙. | Tool `propose_plan`; "Làm" gửi `plan: "approved"`. |
| 30 | Bảng thay đổi của lượt | Dưới câu trả lời: **Thay đổi: Thêm 15 · Sửa 3 · Xóa 0**, từng dòng theo layer; bấm dòng để zoom + tô sáng. Có cả khi mở lại lịch sử và trong file Xuất. | Theo dõi sự kiện database trong lượt; đối tượng tạo rồi xóa trong cùng lượt không tính; chỉ tính đối tượng trong model/paper space (không tính bên trong định nghĩa block). |

## F. v0.7.0 — Claude biết cậu sửa tay; lịch sử phiên bản

| # | Tính năng | Cách dùng | Ghi chú |
| --- | --- | --- | --- |
| 31 | Ghi nhận sửa tay | Không cần làm gì. Sửa tay xong rồi chat tiếp → khung chat hiện "✎ Bản vẽ đã được sửa ngoài khung chat từ lượt chat trước: sửa 12, xóa 3, thêm 40 — Claude sẽ đọc lại…" và Claude đo lại đúng các đối tượng đó trước khi làm. | Plugin nghe sự kiện thêm/sửa/xóa của bản vẽ **từ lúc AutoCAD nạp plugin** (khung chat mở hay đóng đều được); chỉ ghi mã đối tượng, không tốn token. Gửi kèm tin nhắn tối đa 300 mã + tóm tắt theo layer. Thuộc tính block được tính là block đó bị sửa. Bản vẽ chưa từng chat thì không báo. |
| 32 | Biên bản sau mỗi lượt | Không cần làm gì. Cuối mỗi lượt chat, plugin lưu biên bản của bản vẽ; lần sau mở lại (hôm sau, AutoCAD khác) so với bản vẽ hiện tại → báo phần sửa tay như trên. ⚙ **Lưu biên bản cạnh bản vẽ** để máy khác cũng so được. | Biên bản = mỗi đối tượng: layer, loại, mã kiểm tra (khung bao, độ dài, chữ, block, thuộc tính, màu, nét…), nén gzip. Lưu ở `%LocalAppData%\ClaudeCAD\snapshots`, ghi đè mỗi lượt, tự xóa sau 90 ngày không dùng; tùy chọn thêm `.claudecad\<tên>.dwg.snap.gz` cạnh DWG. Bản vẽ > 20.000 đối tượng: chỉ so số lượng theo layer + các đối tượng Claude/cậu đã đụng tới. Bấm **New** xóa biên bản. Không ghi gì vào file DWG. |
| 33 | Lịch sử phiên bản | Tự động: ngay trước khi chat sửa bản vẽ, nếu bản sao gần nhất đã cũ hơn **30 phút** thì plugin lưu một bản sao DWG. Giữ **10 bản** gần nhất. Nút **Phiên bản (N)** → **Xem**: mở bản sao trong **tab chỉ xem**; thanh trên cùng có **Đóng — về bản hiện tại**. Tắt trong ⚙. | Bản sao bằng `Database.Wblock()` + `SaveAs` → bản vẽ đang mở không bị đụng tới. Lưu ở `%LocalAppData%\ClaudeCAD\versions\v-<mã bản vẽ>\`. Chỉ lưu khi **chat** sửa (sửa tay không tạo phiên bản), bỏ qua bản vẽ chưa lưu (Drawing1). Khi đang xem bản cũ, ô chat bị khóa. Xref đường dẫn tương đối có thể không hiện trong bản sao. |

## Kiểm chứng

- Lõi thuần (markdown, lịch sử, cài đặt, mẫu lệnh, bảng Excel) có unit test chạy mọi OS: `dotnet test plugin/ClaudeCAD.Core.Tests`.
- Phía sidecar (nội dung tin nhắn, giao thức, đổi model/effort, chuyển phiên, khôi phục phiên lỗi, usage, rate limit, thẻ xác nhận): `npm test`.
- **Giao diện WPF thật** dựng trên CI Windows (`plugin/ClaudeCAD.UI.Tests`): mô phỏng một cuộc chat đủ tính năng, kiểm tra không lỗi layout và chụp ảnh palette sáng/tối → artifact `palette-screenshots`.
- Phần dính tới AutoCAD (chọn đối tượng, lấy điểm, tô sáng, U, thông báo khi AutoCAD ở nền) cần thử trên AutoCAD thật — xem `docs/test-log.md`.
- v0.6: lõi macro / bảng thay đổi / `@` / ghi chú / mẫu lệnh có unit test (`ChatExtrasTests`); phía sidecar `test/v06.test.ts` (ảnh `capture_view`, thẻ kế hoạch, gợi ý, bản nháp skill, ngữ cảnh mới); UI test chụp thêm `palette-v06-dark/light.png`. Ảnh `capture_view`, phím tắt, macro tại điểm, Chỉ xem cần thử trên AutoCAD thật (V6-1 … V6-10).
