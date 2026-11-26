# Bàn giao: Sửa KTP-A — lỗi phần H9 (kiểm tra độc lập phần 2, 26/11/2026)

- **Thread:** `session_01D56HmFAo4dZ7c8sbeNB9eh`.
- **Nhánh:** `full/nang-cap-h-sua-ktp-a`, tách từ `full/nang-cap-h-tong-the-2` @ `8c46a8e`. Lượt 2 gộp thêm nhánh tích hợp @ `6bcd5b5` và báo cáo kiểm tra độc lập (`claude/inspiring-cray-58xsmt`).
- **CI:** chưa chạy. Thread đợt F2 gộp nhánh này rồi chạy CI.
- **Kiểm:** chưa thử trên AutoCAD thật.

## Lỗi đã sửa

| # | Lỗi | Khách thấy gì trước khi sửa | Sửa | Test |
| --- | --- | --- | --- | --- |
| 1 | Ô ⚙ **Ngôn ngữ / Language** không gắn `SelectionChanged`, nên đổi ô này không báo gì cho phần điều khiển. Test UI cũ gọi thẳng `Relabel`, nên không bắt được | Chọn English không có tác dụng gì, cho tới khi khách đổi một ô khác trong ⚙ | `ChatControl.cs`: `_language.SelectionChanged += (s, e) => OnSettingsUi();`, gắn cạnh các ô khác | UI test `Picking_english_in_the_list_reaches_the_controller` đổi ô thật, kiểm `SettingsChanged` mang `en` rồi `vi`; test lõi kiểm dòng gắn sự kiện |
| 2 | Xác nhận gõ tay nhận nhầm là đồng ý (lỗi có từ trước H9): "ko / k / kg đồng ý", "chẳng / chả đồng ý", "có đồng ý đâu" | Lệnh xóa / sửa hơn 20 đối tượng vẫn chạy khi khách gõ một câu từ chối | Đổi sang luật an toàn (`confirmation.ts`): chỉ tính là đồng ý khi **mọi chữ** trong câu là chữ đồng ý, chữ của cụm đồng ý ("xóa đi", "go ahead"…) hoặc chữ đệm (nhé, ạ, đi, luôn, cũng, please, it, them…). Có bất kỳ chữ nào khác thì coi là không chắc, tức là không đồng ý: Claude hỏi lại, hoặc khách bấm nút trên thẻ. "không sao" vẫn được tính là đồng ý | Test trợ lý: 14 câu "không", 8 câu "có" |
| 3 | Câu có điều kiện bị tính là đồng ý: "đồng ý nếu giữ tường", "yes if you keep the walls", "ok unless doors" | Như lỗi 2 | Cùng luật với lỗi 2 ("nếu", "if", "unless", "giữ", "tường"… không phải chữ đồng ý) | Cùng test với lỗi 2 |

## Lượt 2: các lỗi H9 còn lại (KTP-01 → 03 là ba dòng ở bảng trên)

| Mã | Đã sửa thế nào | Test |
| --- | --- | --- |
| KTP-02 / 03 (thêm) | Cùng luật an toàn: "ai đồng ý", "đồng ý đâu mà", "miễn là… thì ok" là không | Test trợ lý: các câu ở bảng §3 của báo cáo (khoảng 70 câu "không" và "có") |
| KTP-04 | "thôi" đứng một mình là từ chối ("dạ thôi", "ừ thôi"); chỉ "thôi được" mới là đồng ý. Mỉa mai, "later / hold / chờ / đợi / sau / hả / ko / ❌" đều ra không, vì không phải chữ đồng ý | Test trợ lý: 12 câu của KTP-04 |
| KTP-05 | `PluginApp.Initialize` đặt `L.Lang` theo cài đặt (máy chưa có cài đặt: ngôn ngữ chọn ở bộ cài) **trước** dòng "đã nạp…" và icon thanh trạng thái | Test lõi kiểm thứ tự trong `PluginApp.cs` |
| KTP-06 | Bọc `tr()`: lỗi "Trợ lý không đọc được tin nhắn…" (`index.ts`), tiến độ "file i / n" (`batchFiles.ts`), "ảnh W×H" (`capture.ts`), "bắt đầu render…" / "AutoCAD đang bận render…" (`render.ts`). "sidecar sẵn sàng" được gửi trước khi trợ lý biết ngôn ngữ, nên plugin dịch nó lúc hiện (`Tr(info)`, `Tr(_readyInfo)`) | Test trợ lý: dòng `summary / info / error` có chữ tiếng Việt phải qua `tr()` (hoặc ghi `// vi`) |
| KTP-07 | Máy chủ **không đổi**. `L.ServerTexts` có 8 câu lỗi cố định của `Code.gs.template`, 2 câu có giá trị chèn. `L.TrServer` dịch ở `Activation.cs` (3 chỗ); bộ cài có hàm `TrServer` với `H9SrvKeyNN` / `H9SrvNN` (vi + en) ở `ClaudeCAD.iss:903, 1032`. Câu lạ thì hiện nguyên văn | Test lõi: dịch đủ 8 câu, câu có giá trị, câu lạ; các câu vẫn có trong mẫu máy chủ; bộ cài có đủ thông điệp |
| KTP-32 | `interaction.ts`: "short Vietnamese steps", "(Vietnamese, imperative" → "in the language of the user" | Test trợ lý |
| KTP-33 | Bảng `LangEn.cs` dựng lại chỉ từ khóa code đang dùng; bỏ 23 mảnh câu cũ. Nhãn lệnh không dấu (Trim, Offset…) vẫn giữ | Test lõi: mọi khóa đều có trong code |
| KTP-34 | Bỏ các bản tiếng Anh dùng chung: "Gói: " → "Service plan: "; " — dừng giữa chừng" → " — stopped partway"; "Đã thêm" → "Already added" | Test lõi: không có hai câu Việt chung một câu Anh |
| KTP-35 | Câu dặn Claude nói "Đang ở chế độ…, đổi sang … hoặc Cả hai (nút chế độ…)" ghi tên nút theo ngôn ngữ đang chọn (`BothButton`: "Cả hai" / "Both"). 2D / 3D giống nhau ở hai thứ tiếng. Các câu khác gửi Claude có "Cả hai" (vd `DrawingSet.cs`) dựa vào bảng đổi tên nút trong `ENGLISH_SECTION` | Test lõi: English ra "hoặc Both (nút chế độ", không còn "Cả hai (nút" |
| Nghi vấn 1 | Lời dặn "Cậu có đồng ý không?": `ENGLISH_SECTION` thêm "write "Do you agree?" instead" | Test trợ lý |
| Nghi vấn 2 | `Relabel` thay chữ trong ô chọn lúc đang tắt sự kiện: lựa chọn được giữ, không báo đổi cài đặt. Đúng như mong muốn, không phải lỗi | UI test: ô Giao diện đang "Sáng" → "Light", vẫn mục 2, `SettingsChanged` không bắn |

Mọi test xác nhận cũ vẫn qua, kể cả các câu tự nhiên như "Vâng, tớ đồng ý", "Thôi được, xóa đi", "Không sao, xóa đi", "Cậu cứ xóa đi", "go ahead and erase them".

## Quyết định tự chọn

- Danh sách chữ đệm (`FILLERS` trong `confirmation.ts`) cố ý ngắn. Câu đồng ý dài hơn ("ok xóa hết mấy cái tường đó") giờ là **không chắc, tức là không đồng ý**: Claude hỏi lại hoặc hiện thẻ. Thêm một lượt hỏi, nhưng không bao giờ xóa nhầm. Thấy thiếu chữ đệm khi thử thật thì thêm vào danh sách là xong.

## Con số chờ duyệt

Không có.

## Test

- Lượt 1 (`e6ea0c1`): lõi 1687 / 1687, trợ lý 466 / 466.
- Lượt 2 (sau lần sửa cuối): lõi **1700 / 1700**, trợ lý **467 / 467**.
- Build `net48`, `net8.0-windows`, `net10.0-windows` và project UI test: 0 lỗi.
- UI test WPF chỉ chạy trên CI Windows.
