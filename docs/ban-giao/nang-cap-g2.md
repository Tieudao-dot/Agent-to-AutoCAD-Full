# Bàn giao: Nâng cấp đợt G2 — soát bản vẽ trước khi in / gửi (25/11/2026)

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh` (người viết). Nhánh `full/nang-cap-g2`, tách từ `full/nang-cap-f-tong-the` @
`a6da118`. Chạy cùng lúc với G1 (ra bộ hồ sơ): file dùng chung chỉ sửa vài dòng (xem mục 5).

## 1. Khách thấy gì

- Khách nói "soát bản vẽ" (hoặc "kiểm tra trước khi in"). Claude gọi `check_drawing`, lệnh này chỉ đọc. Claude tóm kết quả
  theo mức: **nên sửa**, **cân nhắc**, **gợi ý**. Mỗi nhóm có tên tiếng Việt, số lượng và vài đối tượng ví dụ; khách muốn
  xem thì Claude phóng tới đối tượng đó.
- Nhóm sửa được tự động: Claude hỏi khách có sửa không, rồi gọi `fix_drawing`. Lệnh từ chối lần đầu và đưa một mã cho thẻ
  Đồng ý / Hủy. Khách bấm Đồng ý thì mới sửa. U một lần là hoàn tác.
- Có thể xuất danh sách ra Excel: `<bản vẽ>-soat-ban-ve.xlsx`, sheet "Soát bản vẽ". File đã có thì hiện thẻ ghi đè như F5.

## 2. Các nhóm

| Nhóm (id) | Khách thấy | Mức | Sửa tự động |
| --- | --- | --- | --- |
| `layer_zero` | Hình nằm trên layer 0 | nên sửa | chuyển sang layer khách chọn |
| `defpoints` | Hình nằm trên layer Defpoints (không in ra) | nên sửa | chuyển sang layer khách chọn |
| `hidden_layers` | Layer tắt / đóng băng / không in còn chứa hình (sẽ in thiếu) | cân nhắc | — (chỉ báo, kèm tên layer) |
| `nonstandard_layers` * | Layer không thuộc bảng layer chuẩn | cân nhắc | — |
| `not_bylayer` | Màu / kiểu nét / độ dày nét không theo layer | cân nhắc | đưa về ByLayer |
| `nonstandard_styles` * | Chữ / kích thước / ghi chú mũi tên dùng kiểu ngoài bộ chuẩn | cân nhắc | đổi sang kiểu của bộ (nếu kiểu đó có trong bản vẽ) |
| `text_height` * | Chữ lệch cỡ chuẩn theo tỉ lệ | cân nhắc | — (kèm các cỡ chuẩn để Claude gợi ý) |
| `empty_text` | Chữ rỗng | nên sửa | xóa |
| `dim_override` | Kích thước bị gõ đè số (không còn `<>`) | nên sửa | hiện lại số đo |
| `no_dimensions` | Có tường mà chưa có kích thước nào | gợi ý | — (gợi ý `dim_floor_plan`) |
| `duplicates` | Hình trùng nhau | nên sửa | xóa bản thừa, giữ bản cũ nhất |
| `zero_length` | Đường dài 0 | nên sửa | xóa |
| `far_away` | Hình lạc rất xa phần còn lại | nên sửa | — (chỉ cho khách xem) |
| `hatch_broken` | Hatch hỏng (không có biên) | nên sửa | — |
| `hatch_unassociated` | Hatch mất liên kết với biên | cân nhắc | — |
| `unnamed_blocks` | Block chưa đặt tên (dán dạng block, tên `A$C…`) | cân nhắc | — |
| `empty_attributes` | Thuộc tính block để trống (trong mô hình) | cân nhắc | — |
| `missing_refs` | Xref / ảnh mất file | nên sửa | — |
| `no_title_block` | Tờ in không có khung tên | cân nhắc | — |
| `viewport_unlocked` | Khung nhìn chưa khóa | nên sửa | khóa |
| `viewport_scale` | Khung nhìn có tỉ lệ không chuẩn | cân nhắc | — |
| `title_empty` | Ô khung tên còn trống (thuộc tính block trên tờ in) | cân nhắc | — |

\* Chỉ kiểm khi bản vẽ dùng bộ chuẩn của `setup_standards`, tức là có kiểu chữ `KT-ARIAL` hoặc `VN-ARIAL`. Cỡ chữ cần thêm
kiểu kích thước `KT-<tỉ lệ>` để biết tỉ lệ. Bản vẽ không dùng bộ thì kết quả có ghi chú nói rõ phần nào đã bỏ qua; layer và
kiểu chữ của khách không bị coi là lỗi.

## 3. Đúng với

- **UCS xoay:** lệnh chỉ trả handle, không trả tọa độ. Phần "lạc xa" so trong WCS, không đổi khi xoay UCS.
- **mm / cm / m:** dung sai và cỡ chữ tính từ mm rồi đổi theo đơn vị bản vẽ. Test chạy cùng bản vẽ đổi sang mét.
- **"Chỉ xem":** `check_drawing` là lệnh đọc. `fix_drawing` là lệnh ghi nên bị chặn như mọi lệnh ghi.
- **Macro:** `fix_drawing` không được ghi vào macro (danh sách `NotRecorded`), vì lần sửa dành cho đúng các đối tượng khách
  đã thấy trên thẻ. `check_drawing` là lệnh đọc nên không được ghi.
- **Gói:** cả hai lệnh thuộc nhóm 2D (`Core/Plans.cs`): có ở gói 2D và Full, không có ở gói 3D. Không kiểm khối 3D (xem
  "Quyết định tự chọn").
- **Bản vẽ lớn:** đọc tối đa 20 000 đối tượng; kết quả có `scanned` / `total` / `complete` và ghi chú khi dừng sớm. Mỗi lần sửa
  tối đa 2 000 đối tượng; còn thừa thì có dòng "gọi lại", kèm thẻ mới.
- **Layer khóa:** lần sửa bỏ qua đối tượng trên layer khóa và liệt kê các layer đó. Không có gì để sửa thì không hỏi thẻ.
- **Nạp gọn lệnh:** hai lệnh không nằm trong nhóm nạp sẵn; Claude tìm bằng ToolSearch khi cần.

## 4. Thẻ xác nhận

Mỗi lần sửa có một mã 16 hex, tính từ: tên việc sửa, layer đích (nếu có) và đúng danh sách đối tượng sẽ đổi
(`DrawingCheckLogic.FixToken`). Vì vậy:

- đồng ý một việc sửa không dùng được cho việc sửa khác;
- bản vẽ đổi giữa lúc hiện thẻ và lúc sửa (thêm hay bớt một hình) thì mã đổi, và phải hỏi lại.

Trợ lý chỉ chuyển mã sang plugin khi mã có trên thẻ khách đã bấm Đồng ý (`F5_CARD_TOOLS`), kể cả khi gọi lô.

## 5. File

- Mới:
  - `plugin/ClaudeCAD.Plugin/Core/DrawingCheck.cs`: số, nhóm, logic, mã thẻ.
  - `plugin/ClaudeCAD.Plugin/Drawing/DrawingCheckTools.cs`: quét và sửa.
  - `agent/src/tools/drawingCheck.ts`: khai báo lệnh.
  - Test: `NangCapG2Tests.cs` (23 test) và `agent/test/nangcapg2.test.ts` (5 test).
  - `docs/checklist-dot-g2.md`, tài liệu này.
- Sửa vài dòng ở file dùng chung (dễ gộp với G1):
  - `DrawingService.cs`: 1 dòng đăng ký.
  - `Plans.cs`: thêm 2 tên lệnh vào nhóm 2D.
  - `ToolLabels.cs`: 2 nhãn "Soát bản vẽ", "Sửa lỗi bản vẽ".
  - `Xlsx.cs`: tên file / sheet.
  - `Macros.cs`: `fix_drawing` không ghi macro.
  - `SelfTest.cs`: thêm 1 bước.
  - `ClaudeCAD.Core.Tests.csproj`: 1 dòng.
  - `agent/src/tools/index.ts`: 2 dòng.
  - `agent/src/agent/confirmation.ts`: thêm 2 lệnh vào thẻ F5.
  - `agent/src/agent/systemPrompt.ts`: 1 dòng trong phần 2D.
  - `skills/ve-kien-truc/SKILL.md`: 1 dòng ở mục 8.
  - `bo-cai/README.md`: 1 dòng.

## 6. Con số chờ chủ dự án duyệt

Mọi số ở `G2Numbers` (`Core/DrawingCheck.cs`).

| Số | Ở đâu (để đổi) | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- | --- |
| Coi là **trùng** khi lệch ≤ **0,1 mm** (cùng loại hình, cùng layer) | `DuplicateTolMm` | số "hình trùng nhau" | nhỏ hơn mọi sai số vẽ tay có chủ ý, đủ lớn cho sai số làm tròn khi chép |
| Coi là **dài 0** khi < **0,01 mm** | `ZeroLengthMm` | số "đường dài 0" | chỉ bắt đường thật sự không có chiều dài |
| **Lạc xa**: cách phần chính quá **20 lần** cỡ phần chính, phần chính lấy ít nhất **10 m**; dưới **10** đối tượng thì bỏ qua | `FarFactor`, `FarMinSizeMm`, `FarMinObjects` | số "hình lạc rất xa" | chữ tên bản vẽ, khung ngoài không bị báo; chỉ bắt hình lạc ra rất xa làm "zoom hết" nhỏ xíu |
| Chữ **lệch cỡ** khi khác mọi cỡ chuẩn (5 / 3,5 / 2,5 / 1,8 mm trên giấy × tỉ lệ) quá **10 %** | `TextHeightTolerance` | số "chữ lệch cỡ chuẩn" | cho phép cỡ gần đúng (vd 2,7 mm) mà không báo |
| **5** ví dụ mỗi nhóm (Claude xin thêm được, tối đa 50) | `ExamplesPerGroup` | số đối tượng Claude chỉ cho xem | đủ để chỉ, kết quả gọn |
| Quét tối đa **20 000** đối tượng mỗi lần | `MaxScan` | "đã quét N trên M" | đọc nhanh, AutoCAD không đứng lâu |
| Sửa tối đa **2 000** đối tượng mỗi lần | `MaxFix` | phải gọi lại nếu nhiều hơn | một thẻ không quá lớn, hoàn tác nhẹ |
| Tỉ lệ khung nhìn coi là chuẩn khi lệch ≤ **0,5 %** so với 1:n của bộ (1, 2, 5, 10, 20, 25, 50, 75, 100, 125, 150, 200, 250, 500, 1000, 2000, 5000) | `ViewportScaleTolerance`, `F2Numbers.StandardScales` | số "khung nhìn có tỉ lệ không chuẩn" | khung nhìn chỉnh tay hơi lệch (vd 1:99,7) vẫn tính là 1:100 |
| Tên các nhóm và 3 mức "nên sửa / cân nhắc / gợi ý" (bảng mục 2) | `DrawingCheckLogic.Groups`, `G2Numbers.Level…` | chữ Claude đọc cho khách | ngắn, nói thẳng việc |
| Tên file Excel `…-soat-ban-ve.xlsx`, sheet "Soát bản vẽ" | `F4Numbers.DefaultSuffix` / `SheetName` | tên file | theo cách đặt tên của F4 |

## 7. Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Một lệnh hay hai | `check_drawing` (đọc) + `fix_drawing` (ghi): "Chỉ xem" chặn đúng phần sửa | — |
| Mã thẻ | một mã cho "việc sửa X trên đúng các đối tượng này (sang layer Y)" | — |
| Trùng khác layer | không coi là trùng (xóa một bản có thể đổi cái được in) | bỏ layer khỏi khóa so sánh trong `DupKey` |
| Đường polyline kín bắt đầu ở đỉnh khác | không coi là trùng (bỏ sót hiếm, không báo nhầm) | so thêm các cách xoay vòng đỉnh |
| "Kích thước rời khỏi hình" | không kiểm: kích thước do lệnh vẽ ra vốn không liên kết, kiểm sẽ báo nhầm gần hết | kiểm điểm gốc có nằm trên hình nào không (chậm, dễ sai) |
| Layer khóa | không coi là lỗi in (layer khóa vẫn in ra); lần sửa bỏ qua đối tượng trên đó | — |
| Khung tên chuẩn | đường khung trên layer `KHUNG…` tự đặt độ dày nét, nên không báo "không theo layer" | — |
| Layout mặc định chưa dùng (`Layout1`… chỉ có khung nhìn AutoCAD tự tạo) | không kiểm như tờ in, ghi chú số layout đã bỏ qua | bỏ điều kiện `IsDefaultLayoutName` |
| Hatch không liên kết | báo "cân nhắc" (hatch vẽ không giữ biên cũng nằm trong nhóm này) | bỏ nhóm |
| Khối 3D | không kiểm (G2 thuộc 2D / Full, khối 3D đã có `check_interference`) | thêm nhóm khối 3D cho gói Full |
| Sửa tên layer / cỡ chữ / hình lạc xa | chỉ báo và gợi ý, không sửa tự động (chọn tên / cỡ là việc của khách) | — |
| Macro | `fix_drawing` không ghi vào macro | bỏ khỏi `NotRecorded` |
| Excel khi không có lỗi | không ghi file, có ghi chú | — |

## 8. Token lúc mở chat

- System prompt gói 2D: 11 877 → 12 023 ký tự (+146, khoảng 40 token); Full: 19 099 → 19 245 (+146). Gói 3D không đổi.
- Mô tả 2 lệnh mới: 4 003 ký tự (khoảng 1,1k token), chỉ khi nạp đủ (ô "Nạp gọn lệnh" tắt). Khi nạp gọn (mặc định), hai lệnh
  hoãn lại nên tin đầu chỉ thêm tên lệnh.
- Đo bằng số ký tự (khoảng 3,5 ký tự / token), không phải số token chính xác.

## 9. Test

- Lõi `NangCapG2Tests.cs`: nhóm và nhãn, khớp danh sách bên trợ lý, nhận bộ chuẩn và tỉ lệ, tên layer chuẩn, cỡ chữ, khóa
  trùng (đường vẽ ngược, polyline ngược), lạc xa (cả bản vẽ đơn vị m), tỉ lệ khung nhìn, mã thẻ, layer đích, Excel, nối lệnh
  (đọc / ghi, thẻ trước khi ghi, layer khóa, giới hạn quét, macro, gói).
- Trợ lý `nangcapg2.test.ts`: khai báo, nhóm sửa nằm trong nhóm kiểm, không nạp sẵn, mã thẻ chỉ qua khi có trên thẻ (kể cả gọi lô,
  mã đoán bừa và `user_confirmed` giả), 1 dòng system prompt chỉ ở 2D / Full.
- SelfTest: bước "Nâng cấp đợt G2" (hình trùng, đường dài 0, chữ rỗng; sửa chỉ sau mã thẻ).
- Kết quả và CI: xem mục 10.

## 10. CI

Xem báo cáo gửi luồng chính.

## 11. Chưa kiểm được

- Chưa chạy trên AutoCAD thật: các nhóm đọc bằng API AutoCAD (khung nhìn, xref, ảnh, hatch, thuộc tính) mới qua build và
  SelfTest viết sẵn, chưa thấy trên bản vẽ thật. Thử tay theo `docs/checklist-dot-g2.md`.
- Tốc độ quét bản vẽ 20 000 đối tượng chưa đo.
