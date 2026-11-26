# Bàn giao: Sửa KTP-A — 3 lỗi nặng ở H9 (kiểm tra độc lập phần 2, 26/11/2026)

- **Thread:** `session_01D56HmFAo4dZ7c8sbeNB9eh`.
- **Nhánh:** `full/nang-cap-h-sua-ktp-a`, tách từ `full/nang-cap-h-tong-the-2` @ `8c46a8e`.
- **CI:** chưa chạy. Thread đợt F2 gộp nhánh này rồi chạy CI.
- **Kiểm:** chưa thử trên AutoCAD thật.

## Lỗi đã sửa

| # | Lỗi | Khách thấy gì trước khi sửa | Sửa | Test |
| --- | --- | --- | --- | --- |
| 1 | Ô ⚙ **Ngôn ngữ / Language** không gắn `SelectionChanged`, nên đổi ô này không báo gì cho phần điều khiển. Test UI cũ gọi thẳng `Relabel`, nên không bắt được | Chọn English không có tác dụng gì, cho tới khi khách đổi một ô khác trong ⚙ | `ChatControl.cs`: `_language.SelectionChanged += (s, e) => OnSettingsUi();`, gắn cạnh các ô khác | UI test `Picking_english_in_the_list_reaches_the_controller` đổi ô thật, kiểm `SettingsChanged` mang `en` rồi `vi`; test lõi kiểm dòng gắn sự kiện |
| 2 | Xác nhận gõ tay nhận nhầm là đồng ý (lỗi có từ trước H9): "ko / k / kg đồng ý", "chẳng / chả đồng ý", "có đồng ý đâu" | Lệnh xóa / sửa hơn 20 đối tượng vẫn chạy khi khách gõ một câu từ chối | Đổi sang luật an toàn (`confirmation.ts`): chỉ tính là đồng ý khi **mọi chữ** trong câu là chữ đồng ý, chữ của cụm đồng ý ("xóa đi", "go ahead"…) hoặc chữ đệm (nhé, ạ, đi, luôn, thôi, cũng, please, it, them…). Có bất kỳ chữ nào khác thì coi là không chắc, tức là không đồng ý: Claude hỏi lại, hoặc khách bấm nút trên thẻ. "không sao" vẫn được tính là đồng ý | Test trợ lý: 14 câu "không", 8 câu "có" |
| 3 | Câu có điều kiện bị tính là đồng ý: "đồng ý nếu giữ tường", "yes if you keep the walls", "ok unless doors" | Như lỗi 2 | Cùng luật với lỗi 2 ("nếu", "if", "unless", "giữ", "tường"… không phải chữ đồng ý) | Cùng test với lỗi 2 |

Mọi test xác nhận cũ vẫn qua, kể cả các câu tự nhiên như "Vâng, tớ đồng ý", "Thôi được, xóa đi", "Không sao, xóa đi", "Cậu cứ xóa đi", "go ahead and erase them".

## Quyết định tự chọn

- Danh sách chữ đệm (`FILLERS` trong `confirmation.ts`) cố ý ngắn. Câu đồng ý dài hơn ("ok xóa hết mấy cái tường đó") giờ là **không chắc, tức là không đồng ý**: Claude hỏi lại hoặc hiện thẻ. Thêm một lượt hỏi, nhưng không bao giờ xóa nhầm. Thấy thiếu chữ đệm khi thử thật thì thêm vào danh sách là xong.

## Con số chờ duyệt

Không có.

## Test

- Lõi **1687 / 1687**.
- Trợ lý **466 / 466**.
- Build `net48`, `net8.0-windows`, `net10.0-windows` và project UI test: 0 lỗi.
- UI test WPF chỉ chạy trên CI Windows.
