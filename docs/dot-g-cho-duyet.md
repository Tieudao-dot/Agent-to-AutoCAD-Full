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

Khách nói "ra bộ hồ sơ". Claude gọi một lệnh, ra các tờ có khung tên (bộ chuẩn đợt F2), mỗi hình một tờ, đánh số `KT-01`,
`KT-02`…: mặt bằng từng tầng, 4 mặt đứng, 2 mặt cắt, phối cảnh; muốn thì in cả bộ ra một file PDF. Chạy lại thì bộ cũ chỉ
bị thay sau khi khách bấm Đồng ý trên thẻ; tờ của khách không bị đụng tới.

Máy gói 2D ra tờ mặt bằng 2D và PDF; máy gói 3D ra các hình trong Model (không có tờ in / PDF); máy Full có đủ.

Bàn giao đầy đủ: `docs/ban-giao/nang-cap-g1.md` trên nhánh `full/nang-cap-g1`.

### Con số

Mọi số ở `G1Numbers` (`Core/DrawingSet.cs`).

| Số / chữ | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- |
| Số bản vẽ **`KT-01`, `KT-02`…** (cũng là tên tờ) | số bản vẽ trong khung tên, tên tab | KT = kiến trúc, cách đánh số hay gặp ở hồ sơ kiến trúc Việt Nam |
| Thứ tự tờ: mặt bằng → mặt đứng → mặt cắt → phối cảnh | thứ tự KT-01… và trang PDF | thứ tự đọc hồ sơ thường gặp |
| Tên bản vẽ: "MẶT BẰNG TẦNG n", "MẶT ĐỨNG TRƯỚC / SAU / BÊN TRÁI / BÊN PHẢI", "MẶT CẮT A-A", "PHỐI CẢNH" | ô tên bản vẽ | chữ quen dùng; mặt đứng gọi theo hướng nhìn vì chưa biết hướng bắc |
| Tỉ lệ mặc định **1:100**; hình không vừa thì tự lấy tỉ lệ nhỏ hơn (1:125, 1:150, 1:200…); các mặt bằng chung một tỉ lệ, mặt đứng + mặt cắt chung một tỉ lệ | tỉ lệ trên tờ | 1:100 phổ biến cho nhà ở; chung tỉ lệ để so các hình |
| Phối cảnh không tỉ lệ (ô tỉ lệ ghi **"—"**), chiếm **85 %** khung | cỡ phối cảnh | phối cảnh không đo được; chừa lề |
| Phối cảnh nhìn từ **góc tây nam**, kiểu hiển thị **Realistic** | góc nhìn, màu khi in | hướng nhìn quen thuộc; Realistic hiện vật liệu của đợt F3 |
| Mặt bằng cắt ngang ở độ cao **1200 mm** trên sàn mỗi tầng | cửa sổ (bậu 900) hiện là khoảng hở | thông lệ cắt mặt bằng 1–1,5 m |
| Mặc định **2** mặt cắt (A-A dọc theo cạnh dài, B-B ngang) qua giữa nhà, đường cắt dài hơn nhà **5 %** mỗi đầu | vị trí mặt cắt | một cắt dọc và một cắt ngang qua giữa nhà |
| Mặc định **4** mặt đứng | số tờ mặt đứng | đủ bốn phía |
| Các hình vẽ trong Model cách nhau **30 mm** trên giấy | hàng hình bên phải mô hình | tờ này không thấy hình của tờ kia |
| Tối đa **30** tờ một bộ, **10** tầng | bộ quá lớn bị từ chối | chặn trường hợp lỗi; nhà ở thường dưới 15 tờ |
| Tên file PDF **`<bản vẽ>-bo-ho-so.pdf`**, nằm cạnh bản vẽ | tên file | không dấu, chạy được trên mọi máy / ổ mạng |
| Khổ giấy mặc định A3 ngang | khổ tờ | giữ đúng luật đợt F2 |

### Quyết định tự chọn

| Tình huống | Đã chọn | Ghi chú |
| --- | --- | --- |
| Mỗi tờ mấy hình | một hình một tờ | nhiều hình một tờ là việc vừa, làm sau được |
| Mặt bằng khi bản vẽ có khối 3D | cắt từ khối 3D, mỗi tầng một hình | khách muốn dùng mặt bằng 2D đã có kích thước, chữ thì nói rõ |
| Tầng đọc từ đâu | đáy các khối tường; không có khối tường thì coi là một tầng | |
| Phối cảnh | trục đo (các cạnh song song), không có điểm tụ | giữ hình vừa khung chắc chắn |
| Tên mặt đứng | theo hướng nhìn (trước / sau / trái / phải) | có lưới trục thì đổi sang gọi theo trục được |
| Chạy lại | thay cả bộ sau một thẻ; chỗ khách sửa tay trên tờ / hình cũ sẽ mất, thẻ nói rõ | cập nhật từng tờ, giữ chỗ khách sửa là việc lớn |
| Tờ của khách trùng số bản vẽ | không đụng, từ chối và gợi ý tiền tố khác | |
| Bản vẽ chỉ có các tờ của bộ cũ | tạo lại `Layout1` trước khi xóa tờ cũ | AutoCAD buộc giữ ít nhất một tờ |
| Macro chạy trên bản vẽ đã có bộ hồ sơ | từ chối | macro không bấm thẻ được |
| Máy gói 3D | vẽ các hình trong Model, không có tờ in / PDF | tờ và PDF thuộc nhóm 2D |

### Chưa kiểm được

- Chưa chạy trên AutoCAD thật: hình mặt bằng / mặt đứng / mặt cắt / phối cảnh ra đúng, khung nhìn xoay theo UCS, in PDF cả
  bộ, thẻ chạy lại, macro.
- Chưa đo tốc độ với nhà lớn.

### Token lúc mở chat sau G1 + G2 (ước tính)

Bật "Nạp gọn lệnh": mỗi gói thêm khoảng 0,1k (Full 20,7k → 20,8k). Tắt: thêm khoảng 1,1k cho mỗi đợt.
