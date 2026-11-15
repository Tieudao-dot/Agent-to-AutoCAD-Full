# Bàn giao: Tối ưu khung chat (14/11/2026)

Thread "Tối ưu khung chat". Nhánh `full/toi-uu-khung-chat`, tách từ `full/nang-cap-tong-the` @ `b0da814`.
Chủ dự án báo khi thử bản thử nội bộ 2 (14/11): khung chat giật khi cuộn và khi gõ, rõ nhất lúc đoạn chat dài.

## Nguyên nhân

- Mỗi tin nằm trong một `StackPanel` thường bên trong `ScrollViewer` (`ChatControl.cs`, `_messages`). Mỗi câu trả lời
  là một `RichTextBox` + `FlowDocument` (bảng, khối code, danh sách), vốn là ô chữ nặng nhất của WPF. Không có ảo hóa,
  nên với 200–300 tin, mỗi khung hình (một bước cuộn, một phím gõ, con trỏ nháy, ô nhập cao thêm một dòng) WPF phải duyệt
  và vẽ lại cây hình của **toàn bộ** các tin, kể cả tin ở rất xa phía trên. Palette trong AutoCAD thường vẽ bằng phần mềm
  (không dùng card đồ họa), nên chi phí này lộ rõ.
- Lúc Claude đang viết, cứ 150 ms `MarkdownView.Markdown` được gán lại cả câu trả lời: dựng lại `FlowDocument` mới từ đầu,
  `RichTextBox` dựng lại toàn bộ phần hiển thị và layout lại cả câu (câu dài có bảng thì nặng dần theo độ dài).
- Ô chữ chỉ đọc vẫn bật bộ nhớ hoàn tác (undo), không dùng tới.
- Các xử lý mỗi lần gõ phím (popup `/`, `@`) chỉ chạy khi chữ bắt đầu bằng `/` hoặc có `@`; không phải nguồn giật.
- `TranscriptWindow` (60 tin khi mở lại đoạn chat) vẫn giữ; trong một phiên đang chạy tin vẫn dồn thêm, nhưng nhờ phần
  dưới, tin cũ ngoài màn hình không còn tốn công vẽ.

## Đã sửa (không đổi cách dùng)

1. **Tạm ngừng vẽ tin ở xa màn hình** (`ChatControl.UpdateOffscreen`, chạy mỗi lần `ScrollChanged`): tin cách vùng đang
   xem hơn một khung (phía trên hoặc dưới) được đặt `Visibility.Hidden`. `Hidden` giữ nguyên chỗ và chiều cao, nên thanh
   cuộn, vị trí cuộn, "tự cuộn theo khi Claude đang viết" không đổi; WPF chỉ bỏ không vẽ. Tin được hiện lại trước khi cuộn
   tới (vùng đệm một khung). Không ẩn tin đang có con trỏ bàn phím hay đang kéo chọn chữ; không đụng tin mà code tự ẩn /
   thu gọn. Các thẻ cũ được đưa lại vào tầm nhìn (thẻ kích hoạt, thẻ dán mã, thẻ khóa) được hiện ngay trước `BringIntoView`
   / `Focus` (`Reveal`). `ClearHistory` xóa danh sách ẩn tạm.
2. **Dựng lại từng phần khi Claude đang viết** (`MarkdownView.Render(full: false)`): mỗi khối markdown (đoạn, tiêu đề, bảng,
   khối code, cả một dãy mục danh sách) có một khóa nội dung; lần cập nhật sau giữ nguyên các khối đầu giống hệt, chỉ thay
   từ khối đầu tiên khác trở đi, và không thay `Document` của `RichTextBox`. `Render()` / `Rerender` (đổi cỡ chữ, giao diện)
   vẫn dựng lại toàn bộ; đổi font / cỡ chữ giữa chừng cũng tự dựng lại toàn bộ.
3. `MarkdownView.IsUndoEnabled = false`.

Hai cách tối ưu có công tắc tĩnh nội bộ (`ChatControl.HideOffscreen`, `MarkdownView.Incremental`, mặc định bật) chỉ để
UI test đo cả hai cách trong cùng một lượt chạy.

Không làm (rủi ro cao hơn, đổi thứ người dùng thấy): chuyển sang `ItemsControl` ảo hóa, gom tin cũ trong phiên thành nút
"Xem thêm", `BitmapCache` (chữ mờ hơn).

## Test

- `plugin/ClaudeCAD.UI.Tests/ChatPerfUiTests.cs` (chạy trên Windows CI):
  - `LongChatScrollsTypesAndStreamsFasterWithoutChangingWhatIsShown`: dựng 150 lượt hỏi + trả lời (~315 tin: bảng, khối
    code, danh sách, dòng công cụ, ghi chú) trong khung 420×800, đo cuộn (60 bước), gõ (80 phím, ô nhập cao dần), Claude
    đang viết (120 lần cập nhật một bảng dài), mỗi bước một khung hình (layout + vẽ cả palette). Đo khi tắt rồi khi bật tối
    ưu, in bảng số đo ra log CI và file `perf-khung-chat.txt` trong artifact `palette-screenshots`. Kiểm: số tin và tổng
    chiều cao như nhau, hơn nửa số tin được ẩn tạm khi bật, bật không chậm hơn tắt.
  - `HiddenMessagesComeBackBeforeTheyScrollIntoView`: ở cuối thì tin cuối hiện, tin xa phía trên ẩn; cuộn lên đầu thì mọi
    tin trong hai khung đầu hiện; tắt công tắc thì hiện hết.
  - `StreamedReplyEndsWithTheSameTextAsARebuild`: câu trả lời gõ dần từng 3 ký tự cho ra đúng chữ và số khối như dựng một
    lần; đổi cỡ chữ thì dựng lại ở cỡ mới.
- Bộ UI test chạy tuần tự (`DisableTestParallelization`) để số đo không bị test khác chen.
- CI: bước UI test ghi log chi tiết (`--logger "console;verbosity=detailed"`) để thấy số đo; artifact kèm file `.txt`.

## Số đo trước / sau

Đo trên máy Windows của CI (run 43668498796, 15/11/2026), test
`LongChatScrollsTypesAndStreamsFasterWithoutChangingWhatIsShown`: 315 tin (150 lượt hỏi + trả lời, bảng, khối code,
danh sách, dòng công cụ, ghi chú), khung 420×800, mỗi bước một khung hình (layout + vẽ cả palette).

| Việc | Trước (tắt tối ưu) | Sau (bật tối ưu) |
| --- | --- | --- |
| Dựng 300 tin + layout | 1408 ms | 1356 ms |
| Cuộn, mỗi bước | 58,3 ms | 52,7 ms (−10%) |
| Gõ, mỗi phím | 77,2 ms | 75,5 ms (−2%) |
| Claude đang viết, mỗi lần cập nhật | 86,1 ms | 62,9 ms (−27%) |
| Tin đang ẩn tạm (ngoài màn hình) | 0 | 304 |

Số tin và tổng chiều cao như nhau ở hai cách (người dùng không thấy khác). Máy CI không có card đồ họa và chụp cả
palette mỗi bước nên số tuyệt đối cao hơn máy thật; nên so tỉ lệ. Số đo lấy ở lượt CI 1: lượt 2 chỉ sửa 2 UI test
khác, không đổi mã khung chat; log lượt 2 quá dài (bước bộ cài) nên không lấy lại được bảng.

## Build, commit, CI

- Build trên Linux (`-p:EnableWindowsTargeting=true`), xóa `obj` / `bin` giữa các lần: net48, net8.0-windows,
  net10.0-windows đều xanh; `ClaudeCAD.UI.Tests` build xanh. `dotnet test plugin/ClaudeCAD.Core.Tests`: 910/910 xanh.
  Không đụng trợ lý (`agent/`).
- Commit / CI: nhánh `full/toi-uu-khung-chat` (tách từ `full/nang-cap-tong-the` @ `b0da814`).
  - `e2bea8d` bản sửa khung chat; CI run 43668498796 **đỏ**: 2 UI test cũ / mới chưa theo kịp bản sửa
    (`RendersAutoCadStyleShell` còn tìm nút ký tự thay vì icon nét; `HiddenMessagesComeBackBeforeTheyScrollIntoView`
    đo vị trí tin thiếu lề 6 px của danh sách tin).
  - `ea7bd02` sửa đúng 2 test đó (không nới ngưỡng, không đổi giao diện); CI run 43669231795 **xanh** (release=false):
    sidecar, lõi 910/910, UI test 33/33, build 3 bản plugin, bộ cài.

## Bộ icon và chế độ thu gọn (chủ dự án yêu cầu 14/11/2026, trong thread này)

- **Bỏ emoji / ký tự làm icon.** `UI/Icons.cs`: bộ icon nét đơn sắc 16 px kiểu palette AutoCAD 2022+ (29 hình: cài đặt,
  xuất, chat mới, dừng, thu gọn / mở rộng, thêm, skill, layer, macro, ghi chú, phiên bản, chọn đối tượng, lấy điểm, ảnh,
  chụp, khóa / mở khóa, lưu, xem, định vị, ghim, chờ, sao, cảnh báo, lỗi, xong, thông tin, đóng). Tự vẽ cho plugin, **không
  chép icon của Autodesk** (có bản quyền). Icon lấy màu chữ của nút nên vẫn đổi theo theme AutoCAD, màu nhấn khi có trạng
  thái (MarkIcon), mờ khi nút bị khóa. Dữ liệu hình là cú pháp path chung WPF / SVG.
  - 15 nút icon trên thanh công cụ và hàng dưới ô nhập; nút xóa trên chip; dấu cảnh báo / lỗi của thẻ, thông báo nổi.
  - Thẻ Kích hoạt / Tạm khóa: icon khóa trước tiêu đề. Nút "Chỉ xem / Sửa được": icon khóa / mở khóa + chữ.
  - Nút chữ bỏ emoji: "Lưu macro", "Chạy", "Tại điểm", "Lưu mẫu", "Xem trên bản vẽ — tô sáng …", dòng "Chờ gửi …",
    banner phiên bản; các ghi chú trong chat bỏ 🔒 🔓 ✎ ⏳ 📍 và viết "Cài đặt ▸ …" thay cho "⚙ ▸ …".
  - Giữ các dấu trạng thái dạng chữ ✓ ⚠ ✗ ● ○ ▸ trong dòng chữ (như AutoCAD ghi trong dòng lệnh / báo cáo).
- **Chế độ thu gọn cân đối:** nút Gửi nằm cùng hàng với ô nhập (trước đây đứng một mình ở hàng dưới); dòng gợi ý trống
  không còn chiếm chỗ giữa thanh công cụ và ô nhập; palette nổi tự co / giãn đúng bằng nội dung (thanh công cụ + dòng
  "Đã xong" + ô nhập + khung của AutoCAD, đo thật, kẹp 90–320 px) nên không còn dải trống dưới ô nhập; khi dòng "Đã xong"
  hiện / ẩn hay ô nhập cao thêm dòng thì tự chỉnh lại. Palette đang neo cạnh màn hình giữ kích thước AutoCAD đặt.
- Test: `IconCompactUiTests` (15 nút có icon thuộc bộ, không còn emoji trong chữ của khung chat lúc có đủ thẻ; thu gọn:
  Gửi cùng hàng ô nhập, dòng gợi ý trống ẩn, chiều cao nội dung 50–130 px); test thu gọn cũ đổi sang đọc tên icon
  (`chevron-up` / `chevron-down`).
- Chưa thấy trên AutoCAD thật: chiều cao palette nổi khi thu gọn tính từ khung của AutoCAD lúc chạy; cần thử tay (mục 8c).

## Thử tay

Mục 8b của `docs/checklist-tong-the.md`.
