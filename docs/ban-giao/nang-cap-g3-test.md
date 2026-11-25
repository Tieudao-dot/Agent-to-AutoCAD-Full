# Bàn giao: Test & sửa đợt G3 — bảng dự toán (25/11/2026)

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh` (người rà; người viết G3 là thread Nâng cấp đợt F2). Nhánh
`full/nang-cap-g3-test`, tách từ `full/nang-cap-g3` @ `6c7c650` (code `ec03a77`).

## 1. Lỗi tìm thấy và đã sửa

| Mã | Lỗi | Sửa |
| --- | --- | --- |
| TG3-1 | Khách gõ đơn vị giá là "m3" / "m2" (bàn phím không có số mũ) trong khi dòng ghi "m³" / "m²": mọi dòng bị ghi chú "đơn vị khác nhau: check", dù đúng đơn vị. | `EstimateLogic.UnitKey` coi "m3", "m³", "m^3", "m 3" là một đơn vị (chuẩn hóa NFKD). Đơn vị thật sự khác (m² với m³) vẫn có ghi chú. |
| TG3-2 | Hai giá cùng khớp một dòng với hai số khác nhau: dòng để trống (đúng), nhưng hai giá đó còn bị kể là "không khớp dòng nào" (`unused_prices`), nên Claude báo khách sai lý do. | Giá đã khớp ít nhất một dòng (kể cả khi khớp lẫn nhau) không còn bị kể là "không dùng". Dòng đó vẫn có ghi chú "several price patterns fit". |
| TG3-3 | Trong file Excel, hạng mục có tên giống số (phòng "101", layer "2024", mã "007") bị đổi thành số. | Tên hạng mục, đơn vị và tên dòng % ghi là chữ (`XlsxCell` có chữ mà không có công thức thì luôn là chữ). Khối lượng / giá / tiền vẫn là số. Các bảng Excel của F4 giữ nguyên luật cũ. |

## 2. Đã rà 9 điểm, đúng, không phải sửa

1. **Đơn giá chỉ từ khách:** mô tả lệnh, system prompt và hai skill không có giá mẫu nào (test mới quét các file đó). Giá mẫu chỉ
   có trong `docs/checklist-dot-g3.md`, là chỗ để chủ dự án thử tay; Claude không đọc file này. Dòng thiếu giá để trống,
   không vào tổng, được kể tên (`missing_prices` + `warning`). Giá 0 khách đưa là một giá.
2. **Ghép tên:**
   - bỏ dấu (đ = d), hoa / thường, dấu `-` `_` `.`; mẫu `*`; ký tự lạ (ngoặc, dấu cộng) không làm hỏng mẫu;
   - không đoán "gần giống";
   - giá trùng khớp mà khác số thì để trống và báo; một giá khớp nhiều dòng thì dùng cho cả các dòng đó;
   - giá không khớp dòng nào được báo lại.
3. **Tính toán:**
   - thành tiền từng dòng = khối lượng như hiện (3 số lẻ) × giá, làm tròn đến đồng rồi mới cộng;
   - số tỉ / nghìn tỉ và số rất nhỏ đều đúng; khối lượng 0 cho 0;
   - mỗi dòng % tính trên "Cộng", không tính chồng lên dòng % trước;
   - tới giới hạn 5 dòng % / 500 giá thì từ chối.
4. **Excel:**
   - công thức trỏ đúng ô khi có dòng thiếu giá, có dòng %, hoặc chỉ một dòng;
   - số ghi sẵn trong file khớp số lệnh trả về;
   - có `fullCalcOnLoad`; `numFmts` và `calcPr` đúng chỗ theo cấu trúc OpenXML;
   - tên sheet "Dự toán", tên file `…-du-toan.xlsx`; các bảng F4 không đổi (chữ giống số: xem TG3-3).
5. **Gói nguồn:**
   - lệnh kiểm gói của bảng nguồn với gói hiệu lực theo chế độ (`ChatController.Plan`);
   - máy 3D: khối lượng khối 3D, chỉ số liệu + Excel (trợ lý ẩn `table_position`, plugin cũng chặn bảng vẽ);
   - máy 2D: block / diện tích; máy Full đủ;
   - xin nguồn ngoài gói: thông báo gói / chế độ như các lệnh khác.
6. **Thẻ, Chỉ xem, macro, UCS:**
   - ghi đè file Excel cần thẻ F5; Hủy thì không ghi file, và bảng vẽ cùng lượt cũng được hoàn lại (giao dịch bị hủy);
   - "Chỉ xem": tính và xuất Excel được, vẽ bảng bị chặn;
   - macro không ghi file;
   - `table_position` là điểm UCS (lệnh lớp Ref).
7. **Nạp gọn:** lệnh ngoài nhóm nạp sẵn (test G3).
8. **Tên tham số cũ `source`:** không còn trong mô tả, skill, test, tài liệu. Kết quả trả về vẫn có khóa `source` (tên bảng
   nguồn); đó là dữ liệu trả về, không phải tham số.
9. **Bảng số / quyết định:** từng dòng mục 3, 4 của `nang-cap-g3.md` khớp code. Thiếu ba số khách thấy, bổ sung ở mục 3 dưới
   đây.

## 3. Con số chờ chủ dự án duyệt (bổ sung)

| Số | Ở đâu | Khách thấy gì | Vì sao |
| --- | --- | --- | --- |
| Đơn giá trong bảng vẽ hiện tối đa **2** số lẻ | `EstimateLogic.DrawingTable` (`Vn(..., 2)`) | cột Đơn giá trong bản vẽ | có sẵn trong code G3, chưa ghi bảng |
| Độ rộng cột bảng vẽ **5 / 24 / 8 / 12 / 14 / 16** (theo cỡ chữ) | `EstimateTools.cs` (`InsertSchedule`) | độ rộng cột bảng trong bản vẽ | có sẵn, chưa ghi bảng |
| Kết quả trả về cho Claude tối đa **200** dòng (bảng vẽ / Excel đủ dòng) | `EstimateTools.cs` (`Take(200)`) | Claude nói "200 trên N dòng" | có sẵn, chưa ghi bảng |

Không đổi số nào của G3.

## 4. Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Đơn vị "m3" và "m³" | coi là một đơn vị | — |
| Dòng % tính trên gì | giữ như G3: mọi dòng % trên "Cộng" (không chồng) | tính VAT trên "Cộng + chi phí khác": đổi công thức dòng % |

## 5. Test

- Thêm `plugin/ClaudeCAD.Core.Tests/NangCapG3TestSuaTests.cs` (7 test, 10 trường hợp), nội dung:
  - 3 lỗi trên;
  - công thức Excel đọc lại từ file (dòng thiếu giá, 2 dòng %, chỉ một dòng);
  - làm tròn, số rất lớn / rất nhỏ / 0;
  - tên có ký tự lạ, giới hạn;
  - không có giá mẫu chỗ Claude đọc; tên tham số cũ.
- `docs/checklist-dot-g3.md`: thêm C4 (gõ m3, phòng tên số).
- Kết quả sau lần sửa cuối và CI: xem mục 6.

## 6. CI

Xem báo cáo gửi luồng chính.

## 7. Chưa kiểm được

- Chưa chạy trên AutoCAD thật.
- Chưa mở file bằng Excel thật: công thức tự tính lại khi mở, dấu tách hàng nghìn theo máy, gõ giá vào ô trống thì tiền tự
  tính. Thử tay theo `docs/checklist-dot-g3.md`.
- Bước tự kiểm G3 không chạy trên CI.
