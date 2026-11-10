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
| 9 | Thanh ngữ cảnh | Chọn **Tỷ lệ** (1:20 … 1:1000); skill chọn theo từng đoạn chat ở nút **Skill (N)** (v0.9.4, thay ô Chuẩn cũ). | Gửi kèm mọi tin nhắn; Claude chỉnh chiều cao chữ/dim theo tỷ lệ. Lưu trong `settings.json`. |
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
| 24 | Lưu mẫu lệnh, tạo skill từ cuộc chat | **☆ Mẫu** trên tin nhắn của cậu → thành mẫu `/` mới. (v0.9.4: bỏ nút "Tạo skill từ cuộc chat"; gõ yêu cầu "tạo skill từ cuộc chat này" thì Claude vẫn soạn được bản nháp → thẻ **Xem / sửa** · **Thêm vào plugin**.) | Mẫu lưu vào `%AppData%\ClaudeCAD\templates.json`. Bản nháp skill ở `%LocalAppData%\ClaudeCAD\skill-drafts`; "Thêm" đi qua đúng luồng + Add skill. |
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

## G. v0.8.0 — chọn layer rồi chat chỉnh sửa

| # | Tính năng | Cách dùng | Ghi chú |
| --- | --- | --- | --- |
| 34 | Layer của phần đang chọn | Quét chọn đối tượng (nút **Chọn đối tượng** hoặc chọn sẵn trên bản vẽ) → chip "Kèm 128 đối tượng đang chọn trên 3 layer", dưới đó là từng layer kèm số đối tượng: **☑ A-WALL 120 · ☑ A-DOOR 8 · ☑ A-TEXT 0**. Bỏ tick layer nào thì đối tượng trên layer đó không được gửi. **→ Cả layer**: dùng toàn bộ các layer đã tick (mọi đối tượng trên chúng) thay vì chỉ phần đang chọn. | Chọn bằng nút **Chọn đối tượng**: đọc layer tới 20.000 đối tượng; chọn sẵn trên bản vẽ: tới 2.000 (để AutoCAD không khựng khi quét chọn). |
| 35 | Chọn layer trong khung chat | Nút **Layer** → danh sách mọi layer của bản vẽ: ô lọc theo tên, ô màu, số đối tượng, trạng thái (hiện hành / tắt / đóng băng / khóa / xref) → tick → **Dùng layer đã chọn** → chip "Layer: A-WALL, A-DOOR · 128 đối tượng". | Chip được gửi kèm tin nhắn tiếp theo rồi tự bỏ (như vùng chọn). |
| 36 | Claude hiểu theo câu | "Đổi màu sang đỏ", "khóa lại", "tắt đi", "đổi tên thành…" → sửa **thuộc tính layer**, làm luôn. "Tăng chiều cao chữ lên 300", "dời sang phải 500", "xóa hết" → sửa **các đối tượng trên layer**: quá 20 đối tượng thì vẫn hiện thẻ **Đồng ý / Hủy** (kèm Xem trước). Câu mơ hồ thì Claude hỏi lại một câu. | `attachments.layers` + `selection.layers`; quy tắc trong system prompt. |

## H. v0.9.2 — báo cáo lỗi và tài khoản AI

| # | Tính năng | Cách dùng | Ghi chú |
| --- | --- | --- | --- |
| 37 | Gửi báo cáo lỗi | ⚙ ▸ **Gửi báo cáo lỗi**, hoặc nút trên thẻ lỗi → `ClaudeCAD-baocao-<máy>-<ngày giờ>.zip` trên Desktop, Explorer mở sẵn. | Gồm `thong-tin.txt` (Windows, AutoCAD, plugin, đường dẫn, Node, Claude Code, trạng thái trợ lý, tài khoản, TRUSTEDPATHS, SECURELOAD), log 3 ngày, 3 báo cáo tự kiểm tra gần nhất, log cài đặt Inno Setup (`SetupLogging=yes`). Lọc API key / token (`sk-ant-…`, `Bearer …`, `access/refresh_token`). Không có chat, bản vẽ, ghi chú. |
| 39 | Toast trong khung chat | Thông báo nhỏ nổi trên vùng chat rồi tự ẩn (rê chuột vào để giữ, bấm để đóng): đã lưu macro / mẫu / ghi chú / phiên bản / API key, đã đăng nhập, mất / có lại kết nối, có bản cập nhật, skill thay đổi, sắp hết hạn mức. | Tối đa 3 toast cùng lúc, không lặp cùng nội dung. |
| 40 | Giới hạn sử dụng AI | Từ khoảng 80% hạn mức (hoặc khi Claude báo `allowed_warning`): toast cảnh báo, mỗi kỳ hạn mức một lần. Hết hạn mức (`rejected`, lỗi `rate_limit` / `billing_error`): **thẻ trong cuộc chat** với loại hạn mức (5 giờ / tuần), giờ làm mới, đếm ngược, nút **Tự gửi lại lúc HH:mm** (gửi lại yêu cầu vừa rồi khoảng 1 phút sau khi hạn mức làm mới) và **Dùng API key**. | Dòng mức dùng phía trên vẫn hiển thị %; gửi tin mới hoặc New thì hủy tự gửi lại. |
| 38 | Tài khoản AI | ⚙ ▸ **Tài khoản AI**: "Đang dùng: …" · **Đăng nhập Claude** · **Đổi tài khoản** · **Đăng xuất** · **Dùng API key**. | Đăng xuất = `claude auth logout` (Claude Code đi kèm); API key lưu bằng DPAPI (`%LocalAppData%\ClaudeCAD\apikey.bin`, chỉ tài khoản Windows đó đọc được), truyền cho trợ lý qua `ANTHROPIC_API_KEY`; mỗi thay đổi khởi động lại trợ lý. |

## Kiểm chứng

- Lõi thuần (markdown, lịch sử, cài đặt, mẫu lệnh, bảng Excel) có unit test chạy mọi OS: `dotnet test plugin/ClaudeCAD.Core.Tests`.
- Phía sidecar (nội dung tin nhắn, giao thức, đổi model/effort, chuyển phiên, khôi phục phiên lỗi, usage, rate limit, thẻ xác nhận): `npm test`.
- **Giao diện WPF thật** dựng trên CI Windows (`plugin/ClaudeCAD.UI.Tests`): mô phỏng một cuộc chat đủ tính năng, kiểm tra không lỗi layout và chụp ảnh palette sáng/tối → artifact `palette-screenshots`.
- Phần dính tới AutoCAD (chọn đối tượng, lấy điểm, tô sáng, U, thông báo khi AutoCAD ở nền) cần thử trên AutoCAD thật — xem `docs/test-log.md`.
- v0.6: lõi macro / bảng thay đổi / `@` / ghi chú / mẫu lệnh có unit test (`ChatExtrasTests`); phía sidecar `test/v06.test.ts` (ảnh `capture_view`, thẻ kế hoạch, gợi ý, bản nháp skill, ngữ cảnh mới); UI test chụp thêm `palette-v06-dark/light.png`. Ảnh `capture_view`, phím tắt, macro tại điểm, Chỉ xem cần thử trên AutoCAD thật (V6-1 … V6-10).

## Skill theo từng đoạn chat (v0.9.4)

| # | Tính năng | Cách dùng | Ghi chú |
| --- | --- | --- | --- |
| 40 | Thư viện skill + skill theo đoạn chat | **+ Add skill** chọn nhiều file `.md` → vào thư viện và dùng luôn trong đoạn chat này. **Skill (N)**: *Đang dùng* (✓, nút **−**) / *Thư viện* (nút **+**), **Sửa**. | Danh sách lưu cùng lịch sử chat của bản vẽ; chat mới dùng skill của chat gần nhất; skill mẫu không tự dùng. Trợ lý nạp đúng danh sách của đoạn chat (đổi danh sách → khởi động lại Claude, vẫn giữ cuộc chat) và dặn Claude khi một skill bị bỏ / thêm. |

## Giao diện theo phong cách AutoCAD (v0.9.5)

Báo cáo nghiên cứu: giao diện lấy màu, font và các điểm vào từ chính AutoCAD thay vì tự đặt
(xem thêm `docs/ux-autocad.md`).

| # | Tính năng | Cách dùng | Ghi chú |
| --- | --- | --- | --- |
| 41 | Màu và font theo theme AutoCAD | Tự động. ⚙ ▸ Giao diện = *Theo AutoCAD* (mặc định) | `UI/AcadTheme.cs` đọc `Autodesk.Windows.Palettes.PaletteTheme` qua reflection (`ThemeManager.Instance.CurrentPaletteTheme`): nền palette, chữ, ô nhập, viền, nút, link, hover/press và font palette. Đổi theme trong AutoCAD → palette đổi ngay (sự kiện `ThemeManager.PropertyChanged`, dự phòng biến `COLORTHEME`). Thuộc tính nào AutoCAD không có thì giữ màu dựng sẵn. Cỡ chữ theo AutoCAD đến khi cậu tự chọn cỡ (Ctrl+lăn chuột / ⚙), sau đó giữ cỡ của cậu. |
| 42 | Lớp vỏ kiểu palette AutoCAD | Tự động | Góc vuông, tin nhắn là khối phẳng có nhãn CẬU / CLAUDE, thanh công cụ là hàng icon (⊞ chat mới, ■ dừng, ⤓ xuất, ⚙ cài đặt, ≡ layer, ＋ add skill, ✦ skill, ▷ macro, ✎ ghi chú, ⟲ phiên bản). Icon có nội dung (macro đã lưu, phiên bản, ghi chú) chuyển màu nhấn, số lượng nằm trong tooltip. Mọi nút có tooltip "Tên — giải thích ngắn". |
| 43 | Thanh hạn mức | Tự hiện khi đã dùng từ 50% | "Hạn mức 5 giờ · đã dùng 41%" + thanh 3 px + giờ làm mới, ngay dưới thanh công cụ. |
| 44 | Cài đặt dạng lưới 2 cột | ⚙ | Nhóm thu gọn ▾/▸ như Properties: *Chung — giao diện*, *Cách Claude làm việc*, *An toàn bản vẽ*, *Tài khoản AI*, *Trợ giúp*. Nhóm Tài khoản AI tự mở khi chưa đăng nhập. |
| 45 | Thẻ xác nhận | Khi Claude sắp xóa / sửa nhiều | Số đối tượng bị ảnh hưởng in to, nút **⌖ Xem trên bản vẽ** (chọn sẵn bằng kiểu chọn của AutoCAD), **Đồng ý** là nút duy nhất có màu, ghi rõ hoàn tác = 1 lần U. |
| 46 | Thẻ lỗi gọn | Tự động | Icon ⊗ (lỗi) / ⚠ (cảnh báo) + chữ thường + nút xử lý; không tô đỏ cả thẻ. |
| 47 | Gửi thêm ý khi Claude đang làm | Gõ rồi Enter (nút đổi thành **Gửi thêm**) | Tin được xếp hàng ("⏳ Chờ gửi khi Claude xong lượt này…", nút **Bỏ**) và tự gửi ngay khi lượt đang chạy kết thúc sạch. Lượt bị lỗi, hết hạn mức, bị Dừng hoặc đang hỏi "đồng ý?" → chữ trả về ô nhập để cậu quyết. Claude **không** đọc tin này giữa lượt. |
| 48 | Dòng "đang làm" + Esc để dừng | Tự động | "● Đang làm · 1 phút 12 giây · 5 thao tác — Esc để dừng": thời gian và số thao tác thật của lượt (không có % ước lượng). Esc khi con trỏ ở ô nhập = Dừng; Esc trong bản vẽ vẫn là của AutoCAD. |
| 49 | Thu gọn khung chat | Nút **⌄** trên thanh công cụ (bấm **⌃** hoặc dòng trạng thái để mở lại) | Palette co lại tại chỗ, chỉ còn hàng công cụ + ô nhập một dòng; **Enter** gửi ngay. Đoạn chat chỉ bị ẩn, không bị tải lại: mở rộng là thấy nguyên như cũ. Lượt xong khi đang thu gọn hiện một dòng "✓ Đã xong: … · bấm để xem" (lỗi: "⊗ …"). Thẻ cần cậu quyết (xác nhận, kế hoạch, đăng nhập, kích hoạt) tự mở rộng palette. Palette nổi: chiều cao co còn ~170 px và trả lại chiều cao cũ khi mở; palette neo trái/phải giữ nguyên cột, ô nhập nằm sát thanh công cụ. |
| 50 | Icon ClaudeCAD trên thanh trạng thái AutoCAD | Tự có từ lúc mở AutoCAD (góc dưới bên phải, cạnh icon Xref / in ấn). Bấm để mở khung chat | Xám mờ = chưa kết nối · thường = sẵn sàng · **chấm xanh** = Claude đang làm · **chấm vàng** = cần cậu xem (chờ xác nhận, kế hoạch chờ duyệt, chưa đăng nhập, lượt lỗi, hết hạn mức). Lượt xong lúc khung chat đang đóng hoặc tự ẩn → **bong bóng** "Claude đã xong: …" có link *Mở khung chat*. Tắt bong bóng: ⚙ ▸ *Báo khi lượt xong*. Icon đổi nét sáng/tối theo theme AutoCAD; palette cũng dùng icon này. |
