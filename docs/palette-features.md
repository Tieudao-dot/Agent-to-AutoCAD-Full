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

## Kiểm chứng

- Lõi thuần (markdown, lịch sử, cài đặt, mẫu lệnh, bảng Excel) có unit test chạy mọi OS: `dotnet test plugin/ClaudeCAD.Core.Tests`.
- Phía sidecar (nội dung tin nhắn, giao thức, đổi model/effort, chuyển phiên, khôi phục phiên lỗi, usage, rate limit, thẻ xác nhận): `npm test`.
- **Giao diện WPF thật** dựng trên CI Windows (`plugin/ClaudeCAD.UI.Tests`): mô phỏng một cuộc chat đủ tính năng, kiểm tra không lỗi layout và chụp ảnh palette sáng/tối → artifact `palette-screenshots`.
- Phần dính tới AutoCAD (chọn đối tượng, lấy điểm, tô sáng, U, thông báo khi AutoCAD ở nền) cần thử trên AutoCAD thật — xem `docs/test-log.md`.
