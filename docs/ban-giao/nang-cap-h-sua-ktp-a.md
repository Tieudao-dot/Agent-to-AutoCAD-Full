# Bàn giao: Sửa KTP-A — lỗi phần H9 (kiểm tra độc lập phần 2, 26/11/2026)

- **Thread:** `session_01D56HmFAo4dZ7c8sbeNB9eh`.
- **Nhánh:** `full/nang-cap-h-sua-ktp-a`, tách từ `full/nang-cap-h-tong-the-2` @ `8c46a8e`. Lượt 2 gộp thêm nhánh tích hợp @ `6bcd5b5` và báo cáo kiểm tra độc lập (`claude/inspiring-cray-58xsmt`).
- **CI:** chưa chạy. Thread đợt F2 gộp nhánh này rồi chạy CI.
- **Kiểm:** chưa thử trên AutoCAD thật.

## Lỗi đã sửa

| # | Lỗi | Khách thấy gì trước khi sửa | Sửa | Test |
| --- | --- | --- | --- | --- |
| 1 | Ô ⚙ **Ngôn ngữ / Language** không gắn `SelectionChanged`, nên đổi ô này không báo gì cho phần điều khiển. Test UI cũ gọi thẳng `Relabel`, nên không bắt được | Chọn English không có tác dụng gì, cho tới khi khách đổi một ô khác trong ⚙ | `ChatControl.cs`: `_language.SelectionChanged += (s, e) => OnSettingsUi();`, gắn cạnh các ô khác | UI test `Picking_english_in_the_list_reaches_the_controller` đổi ô thật, kiểm `SettingsChanged` mang `en` rồi `vi`; test lõi kiểm dòng gắn sự kiện |
| 2 | Xác nhận gõ tay nhận nhầm là đồng ý (lỗi có từ trước H9): "ko / k / kg đồng ý", "chẳng / chả đồng ý", "có đồng ý đâu" | Lệnh xóa / sửa hơn 20 đối tượng vẫn chạy khi khách gõ một câu từ chối | Đổi sang luật an toàn (`confirmation.ts`): chỉ tính là đồng ý khi **mọi chữ** trong câu là chữ đồng ý, chữ của cụm đồng ý ("xóa đi", "go ahead"…) hoặc chữ đệm (nhé, ạ, đi, luôn, cũng, please, it, them…). Có bất kỳ chữ nào khác thì coi là không chắc, tức là không đồng ý: Claude hỏi lại, hoặc khách bấm nút trên thẻ. *(Lượt 3, KTP-47: "không sao" cũng là không đồng ý.)* | Test trợ lý: 14 câu "không", 8 câu "có" |
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

## Lượt 3: soát lại của kiểm tra độc lập (gộp `5611f3f`)

| Mã | Đã sửa thế nào | Test |
| --- | --- | --- |
| KTP-04 (chưa đủ) | Bỏ `à` khỏi `FILLERS`: "ok à", "đồng ý à", "đồng ý rồi à", "ok xóa hết rồi à" là câu hỏi lại, ra không | Test trợ lý |
| KTP-41 | Thêm `LOCKED_MSG` ("Máy này đã bị chủ plugin tạm khóa.") vào `L.ServerTexts` và bộ cài (`H9SrvKey10` / `H9Srv10`, `TrServer` xét cả mục 10). Test mới đọc **mọi** lời lỗi trong `Code.gs.template` (chuỗi trực tiếp, hằng `*_MSG`, câu ghép `'A' + giá trị + 'B'`) và đòi mỗi câu có trong `ServerTexts`, có bản tiếng Anh, có trong bộ cài. Chỉ đọc template | Test lõi `Every_error_text_of_the_activation_server_template_has_its_english` |
| KTP-42 | `PluginApp.cs`: thêm `!settings.LanguageSaved` như `ChatController.cs` (cài đặt cũ chưa có ngôn ngữ thì lấy ngôn ngữ bộ cài) | Test lõi kiểm dòng |
| KTP-43 | "Đã thêm" → "Added on" | Test lõi |
| KTP-44 | Thêm "xoá" (kiểu dấu cũ), "xoá đi", "xoá luôn", "ghi đè", "thay thế", "overwrite", "replace", "ơi", "yeah", "alright", "👍" (chữ đệm, đứng riêng không tính); "được thôi", "ok thôi" là đồng ý; "thôi" đứng riêng vẫn là từ chối; giữ danh sách đóng | Test trợ lý: 14 câu "có" |
| KTP-47 (a) | Gõ riêng "thôi được" là đồng ý (trước đây ra không). "không sao" giờ là **không đồng ý** vì có chữ "không" (trước đây là đồng ý); sửa test cũ "Không sao, xóa đi" sang "không". Sửa chữ ở `docs/dot-h-cho-duyet.md`, `docs/ban-giao/nang-cap-h9.md` và bàn giao này | Test trợ lý |

## Quyết định tự chọn

- **KTP-44 (luồng chính chọn 26/11):** thêm "xoá / ghi đè / thay thế / overwrite / replace / ơi / yeah / alright / 👍"; "được thôi", "ok thôi" là đồng ý; "thôi" đứng riêng là từ chối; giữ luật danh sách đóng.
- **KTP-47 (a) (luồng chính chọn 26/11):** "thôi được" gõ riêng là đồng ý; "không sao" là không đồng ý (có chữ "không"), an toàn hơn: Claude hỏi lại hoặc khách bấm nút trên thẻ.

- Danh sách chữ đệm (`FILLERS` trong `confirmation.ts`) cố ý ngắn. Câu đồng ý dài hơn ("ok xóa hết mấy cái tường đó") giờ là **không chắc, tức là không đồng ý**: Claude hỏi lại hoặc hiện thẻ. Thêm một lượt hỏi, nhưng không bao giờ xóa nhầm. Thấy thiếu chữ đệm khi thử thật thì thêm vào danh sách là xong.

## Con số chờ duyệt

Không có.

## Test

- Lượt 1 (`e6ea0c1`): lõi 1687 / 1687, trợ lý 466 / 466.
- Lượt 2: lõi 1700 / 1700, trợ lý 467 / 467.
- Lượt 3 (sau lần sửa cuối): lõi **1709 / 1709**, trợ lý **467 / 467** (`# fail 0`).
- Build `net48`, `net8.0-windows`, `net10.0-windows` và project UI test: 0 lỗi.
- UI test WPF chỉ chạy trên CI Windows.
