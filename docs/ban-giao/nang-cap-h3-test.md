# Bàn giao: Test & sửa đợt H3: so sánh hai bản vẽ (26/11/2026)

Thread `session_01D56HmFrL65iF9oJokvqXw5` (người rà). Người viết H3 là thread đợt F1.

- Nhánh `full/nang-cap-h3-test`, tách từ `full/nang-cap-h3` @ `738612f` (code `d5b74a0`).
- **Không sửa `OtherDrawing.cs`** (cả hai file). Bước gộp H5 không phải gỡ gì với H4.
- Chưa thử trên AutoCAD thật.

## 1. Đã soát

| Điểm | Kết quả |
| --- | --- |
| 1. `OtherDrawing.Open` | Ổn. Đọc đầu file bằng `FileShare.ReadWrite \| Delete`. Đọc DWG bằng `OpenForReadAndAllShare` rồi `CloseInput(true)`. DXF dùng `DxfIn` vào database trống. Không có `Save` hay `ForWrite`. Lỗi giữa chừng thì `Dispose` database và báo lỗi rõ. File đời mới hơn bị chặn trước khi AutoCAD đọc. DXF hỏng thì báo "có thể hỏng". Đường dẫn mạng theo luật G5. File > 200 MB bị chặn. So với chính bản vẽ đang mở: từ chối. Chưa sửa: `OpenHereModified` chỉ biết "có thay đổi chưa lưu" khi file kia là bản vẽ đang làm. Nếu nó mở ở tab khác, ghi chú chỉ nói "đọc bản đã lưu", vẫn đúng. |
| 2. Khớp | **2 lỗi** (H3T-01, H3T-02). Dời 0,5 mm: coi như cùng một đối tượng. Dời 1,5 mm: một bớt + một thêm (test mới). Block lồng nhau: so ở mức block. Thuộc tính, chữ nhiều dòng, kích thước bị ghi đè chữ (`DimensionText`) đều có trong phần so. **1 lỗi** báo đổi giả (H3T-03). UCS: so trong WCS, đúng. |
| 3. Đám mây | Vẽ trong WCS nên đúng chỗ cả khi UCS xoay. Ngưỡng thẻ 50 / 500 khớp code. Layer `X-SO-SANH` có sẵn: giữ thuộc tính của khách. **1 lỗi** (H3T-04). |
| 4. Excel, gói, "Chỉ xem", hiệu năng | Excel qua `ExportExcel` chung (thẻ ghi đè như G2 / G3). Gói 2D bỏ khối 3D, gói 3D bỏ đối tượng 2D, có ghi chú. Không `mark` thì là lệnh đọc (`readsWhen`), chạy được ở "Chỉ xem". 20 000 đối tượng mỗi bên: test có sẵn dưới 3 giây. Kiểu tự chọn sau khi sửa H3T-01 so hai lần, vẫn nhanh. |
| 5. Mô tả lệnh / system prompt | Một dòng trong prompt ("so với bản cũ"). Mô tả nói rõ file kia chỉ đọc, cách khớp, đám mây, thẻ, Excel. Không hứa so layout hay so bên trong định nghĩa block. |
| 6. Bảng con số | Khớp code (`H3Numbers`, `OtherDrawingNumbers`). Sau khi sửa không thêm số mới cho khách. |

## 2. Lỗi đã sửa

| Mã | Mức | Lỗi | Sửa | Test (`Core.Tests/NangCapH3TestSuaTests.cs`) |
| --- | --- | --- | --- | --- |
| H3T-01 | **Nặng** | Hai bản vẽ khác nhau nhưng tạo từ **cùng một file mẫu** có mã đối tượng (handle) trùng nhau, cùng loại. Hơn 50 % trùng nên bị khớp theo handle: báo "đổi hình" tràn lan, mây vàng khắp bản vẽ | `DrawingCompareLogic.Run`: ở chế độ tự chọn, khi handle trùng ≥ 50 % thì so cả hai cách. Giữ "handle" chỉ khi số đối tượng không đổi không ít hơn cách "hình học". Bản sửa thật của cùng một file vẫn khớp theo handle | `Two_drawings_from_one_template_…`, `A_real_revision_stays_matched_by_handle`, `Moved_half_a_millimetre_…` |
| H3T-02 | Vừa | Một bên không đặt đơn vị (INSUNITS = 0, hay gặp ở file cũ) bị coi là mm rồi đổi. Vd file cũ vẽ bằng mét không đặt đơn vị, so với bản mới đặt mét: mọi số lệch 1000 lần, mọi thứ thành "bớt + thêm" | `DrawingCompareLogic.UnitFactor`: chỉ đổi đơn vị khi cả hai bên có đơn vị biết rõ (in, ft, mm, cm, m, dm). Nếu không thì không đổi và ghi chú để Claude hỏi khách | `A_drawing_without_a_unit_is_never_converted` |
| H3T-03 | Vừa | Giá trị kích thước, độ dày polyline, diện tích hatch được so như chữ, làm tròn 6 số lẻ. Sai số làm tròn, hoặc đổi đơn vị m → mm, ra "value 3000 → 3000.0001" | So bằng số: giá trị kích thước dài và độ dày theo dung sai (1 mm). Diện tích theo tỉ lệ như thể tích (0,1 %). Kích thước góc vẫn so như chữ | `Dimension_values_widths_and_areas_…` |
| H3T-04 | Nhẹ | Khoanh lần hai vẽ chồng mây cũ mà kết quả không nói. Layer `X-SO-SANH` tắt thì mây vẽ ra nhưng không thấy. Layer khóa thì thẻ xác nhận hiện **trước**, khách bấm đồng ý rồi mới bị báo lỗi | Kiểm layer trước thẻ. Ghi chú khi còn mây cũ (số đối tượng) và khi layer tắt | `Earlier_clouds_and_an_off_layer_…` |

- File sửa: `Core/DrawingCompare.cs`, `Drawing/CompareTools.cs`.
- Test mới: `Core.Tests/NangCapH3TestSuaTests.cs` (6).
- Tài liệu: `docs/checklist-dot-h3.md` (B.5–B.7), bàn giao này.

## 3. Nhận xét, không sửa

- Khớp hình học tìm theo điểm đầu. Hàng chục nghìn đường cùng điểm đầu ở cả hai file thì so từng cặp (test có sẵn: 2 000
  đường dưới 3 giây). Bản vẽ thật hiếm khi như vậy.
- Gộp đám mây mỗi lượt so từng cặp (tối đa 5 000 chỗ khác, vì quá `MaxClouds × 10` thì không vẽ). Chậm nhất khoảng 1 giây.
- Với `other_is: new`, mây của đối tượng chỉ có ở file kia được vẽ đúng chỗ nó nằm trong file kia. Đúng ý, chỉ ghi lại.
- Mô tả lệnh có nhắc "3D solids … on 3D / Full" ở cả gói 2D, giống `export_drawing` (FBX). Không lộ lệnh 3D nào.

## 4. Con số chờ chủ dự án duyệt

Không thêm số mới cho khách:

- H3T-01 không cần ngưỡng mới: so hai cách, cách nào giữ được nhiều đối tượng không đổi hơn thì dùng.
- H3T-03 dùng lại dung sai 1 mm và 0,1 % đã có trong bảng của H3.

## 5. Quyết định tự chọn

- H3T-01: hòa (hai cách giữ cùng số đối tượng không đổi) thì giữ "handle". Lý do: bản sửa thật cho "đổi" gọn hơn "bớt + thêm".
- H3T-02: một bên không có đơn vị thì không đổi. Lý do: đoán sai còn tệ hơn không đổi; ghi chú để Claude hỏi khách.
- H3T-04: không tự xóa mây cũ (giữ quyết định của H3), chỉ báo.

## 6. Test

- Lõi: 1536 / 1536 qua (thêm 6).
- Trợ lý: 423 / 423 qua.
- Build 3 target (net48, net8.0-windows, net10.0-windows): qua.

## 7. CI

(điền sau khi chạy)
