# Đợt G: con số và quyết định chờ chủ dự án duyệt

Chủ dự án chọn "chạy liền như đợt F" cho đợt G (25/11/2026 ~20h): các thread dùng số đề xuất, không dừng để hỏi. File này
gom các số và các điểm tự chọn để cậu duyệt **một lần** ở báo cáo cuối chuỗi. Số nào cậu muốn đổi thì chỉ sửa một chỗ
(cột "Ở đâu").

Luồng chính ghi file này theo báo cáo của từng đợt; thread test tổng thể đợt G sẽ soát lại từng dòng so với code.

## G2: soát bản vẽ trước khi in / gửi

Khách nói "soát bản vẽ". Claude chạy lệnh chỉ đọc, kể các lỗi theo ba mức **nên sửa / cân nhắc / gợi ý**, mỗi nhóm có số
lượng và vài hình ví dụ. Nhóm nào sửa tự động được thì Claude hỏi, khách bấm Đồng ý trên thẻ mới sửa; U một lần là hoàn tác.
Danh sách xuất được ra Excel. Gói 2D và Full.

Bàn giao đầy đủ (22 nhóm lỗi, 9 nhóm sửa được): `docs/ban-giao/nang-cap-g2.md` trên nhánh `full/nang-cap-g2`.

### Con số

Mọi số ở `G2Numbers` (`Core/DrawingCheck.cs`).

| Số | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- |
| Hai hình coi là **trùng** khi lệch ≤ **0,1 mm** (cùng loại, cùng layer) | số "hình trùng nhau" | nhỏ hơn mọi khoảng vẽ có chủ ý, đủ cho sai số làm tròn khi chép |
| Đường coi là **dài 0** khi ngắn hơn **0,01 mm** | số "đường dài 0" | chỉ bắt đường thật sự không có chiều dài |
| Hình **lạc xa**: cách phần chính quá **20 lần** cỡ phần chính (phần chính tính ít nhất **10 m**); bản vẽ dưới **10** hình thì không kiểm | số "hình lạc rất xa" | chữ tên bản vẽ, khung ngoài không bị báo; chỉ bắt hình lạc xa làm "zoom hết" nhỏ xíu |
| Chữ **lệch cỡ** khi khác mọi cỡ chuẩn (5 / 3,5 / 2,5 / 1,8 mm trên giấy) quá **10 %** | số "chữ lệch cỡ chuẩn" | cỡ gần đúng (vd 2,7 mm) không bị báo |
| Mỗi nhóm đưa **5** hình ví dụ (xin thêm được, tối đa 50) | số hình Claude chỉ cho xem | đủ để chỉ, kết quả gọn |
| Mỗi lần quét tối đa **20 000** hình | dòng "đã quét N trên M" | AutoCAD không đứng lâu |
| Mỗi lần sửa tối đa **2 000** hình | nhiều hơn thì phải gọi lại | một thẻ không quá lớn, hoàn tác nhẹ |
| Tỉ lệ khung nhìn coi là chuẩn khi lệch ≤ **0,5 %** so với 1:n của bộ chuẩn | số "khung nhìn có tỉ lệ không chuẩn" | khung chỉnh tay hơi lệch (1:99,7) vẫn tính là 1:100 |
| Tên 22 nhóm lỗi và ba mức "nên sửa / cân nhắc / gợi ý" | chữ Claude đọc cho khách | ngắn, nói thẳng việc |
| Tên file Excel `<bản vẽ>-soat-ban-ve.xlsx`, sheet "Soát bản vẽ" | tên file | theo cách đặt tên của đợt F4 |

### Quyết định tự chọn

| Tình huống | Đã chọn | Ghi chú |
| --- | --- | --- |
| Tên layer, kiểu chữ, cỡ chữ | chỉ kiểm khi bản vẽ dùng bộ chuẩn của plugin (có kiểu chữ `KT-ARIAL` hoặc `VN-ARIAL`); không thì ghi rõ đã bỏ qua | layer và kiểu chữ riêng của khách không bị coi là lỗi |
| Hai hình trùng nhưng khác layer | không coi là trùng | xóa một bản có thể đổi cái được in |
| "Kích thước rời khỏi hình" | không kiểm | kích thước do lệnh vẽ ra vốn không gắn với hình, kiểm sẽ báo nhầm gần hết |
| Layer đang khóa | không coi là lỗi; lần sửa bỏ qua hình trên layer khóa | layer khóa vẫn in ra bình thường |
| Tờ in mặc định chưa dùng (`Layout1`…) | không kiểm, có ghi số tờ đã bỏ qua | tránh báo "không có khung tên" cho tờ trống |
| Hatch mất liên kết với biên | báo mức "cân nhắc" | |
| Khối 3D | không kiểm | lệnh thuộc gói 2D / Full; khối 3D đã có lệnh kiểm va chạm |
| Tên layer sai, cỡ chữ lệch, hình lạc xa | chỉ báo và gợi ý, không sửa tự động | chọn tên / cỡ là việc của khách |
| Sửa lỗi có ghi vào macro không | không ghi | sửa theo từng bản vẽ, phát lại ở bản vẽ khác dễ sai |
| Xuất Excel khi không có lỗi | không ghi file, có ghi chú | |
| Polyline kín vẽ trùng nhưng bắt đầu ở đỉnh khác | không coi là trùng | bỏ sót hiếm, đổi lại không báo nhầm |

### Chưa kiểm được

- Chưa chạy trên AutoCAD thật.
- Chưa đo tốc độ khi quét 20 000 hình.

## G1: ra bộ hồ sơ một lệnh

Chờ báo cáo của thread viết.
