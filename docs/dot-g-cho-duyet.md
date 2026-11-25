# Đợt G: con số và quyết định chờ chủ dự án duyệt

Chủ dự án chọn "chạy liền như đợt F" cho đợt G (25/11/2026 ~20h): các thread dùng số đề xuất, không dừng để hỏi. File này
gom các số và các điểm tự chọn để cậu duyệt **một lần** ở báo cáo cuối chuỗi. Số nào cậu muốn đổi thì chỉ sửa một chỗ
(cột "Ở đâu").

Luồng chính ghi file này theo báo cáo của từng đợt. **Thread test tổng thể đợt G đã soát từng dòng so với code (26/11)**: các
số khớp code; đã sửa mô tả G4 / G5 cho đúng sau các lần rà và thêm mục "Test tổng thể đợt G" ở cuối.

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
| Mỗi nhóm kể tối đa **30** tên layer / tờ in; kết quả sửa kể tối đa **50** hình đã sửa | độ dài danh sách Claude đọc | kết quả gọn (thread rà bổ sung dòng này 25/11, số đã có sẵn trong code) |

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
| Xref khách tạm gỡ, ảnh đã gỡ mà file vẫn còn | không coi là "mất file" | thread rà thêm 25/11; chỉ báo khi không tìm thấy file |

### Sau khi rà (25/11 22h13)

Thread rà sửa 4 lỗi, không đổi số nào: hình lạc xa bị bỏ sót ở bản vẽ 10–19 hình; định nghĩa thuộc tính của block để trống bị
coi là chữ rỗng và bị xóa; xref tạm gỡ bị báo mất file; sửa trên bản vẽ quá 20 000 hình không nói là chỉ sửa phần đã quét.
Nhánh `full/nang-cap-g2-test` @ `f49e717` (code `7b9be8f`), CI 45640452138 xanh, lõi 1255, trợ lý 373.

### Chưa kiểm được

- Chưa chạy trên AutoCAD thật (trạng thái xref / ảnh, khung nhìn, khung tên).
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
| Mỗi lần tối đa **8** đường cắt khách chỉ, **10** vùng mặt bằng 2D; mỗi hình dùng tối đa **500** khối 3D | nhiều hơn thì bị từ chối hoặc có ghi chú | thread rà bổ sung dòng này 25/11, số đã có sẵn trong code |

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
| Khách cho một tỉ lệ mà hình không vừa tờ | vẫn theo tỉ lệ của khách, có ghi chú gợi ý tỉ lệ / khổ lớn hơn | thread rà thêm 25/11 |
| Nhà quá 10 tầng | vẫn ra 10 tầng dưới cùng, có ghi chú | thread rà thêm 25/11 |

### Sau khi rà (25/11 22h23)

Thread rà sửa 4 lỗi, không đổi số nào: nhà quá 10 tầng bị bỏ bớt tầng mà không báo; hình không vừa tờ mà không báo; trên máy
gói 3D mã thẻ "thay bộ cũ" giống nhau ở mọi bản vẽ; ô khung tên lạ làm mất bộ cũ mà không ra bộ mới (nay từ chối trước khi
đổi gì). Nhánh `full/nang-cap-g1-test` @ `99aaab7` (code `9c7c95f`), CI 45642708237 xanh, lõi 1237, trợ lý 367.

### Chưa kiểm được

- Chưa chạy trên AutoCAD thật: hình mặt bằng / mặt đứng / mặt cắt / phối cảnh ra đúng, khung nhìn xoay theo UCS, in PDF cả
  bộ, thẻ chạy lại, macro.
- Chưa đo tốc độ với nhà lớn.
- Trường hợp lỗi lạ xảy ra giữa chừng, sau khi bộ cũ đã bị xóa (khi đó một lần U hoàn tác cả lượt).

### Token lúc mở chat sau G1 + G2 (ước tính)

Bật "Nạp gọn lệnh": mỗi gói thêm khoảng 0,1k (Full 20,7k → 20,8k). Tắt: thêm khoảng 1,1k cho mỗi đợt.

## G3: bảng dự toán

Khách nói "lập dự toán" hoặc "tính tiền theo khối lượng". Claude lấy khối lượng từ các bảng đã có (thể tích khối 3D theo
layer, số block, diện tích phòng / layer), nhân với **đơn giá do khách đưa**, ra thành tiền từng dòng, dòng Cộng và Tổng
cộng. Xuất được file Excel có công thức (khách sửa giá trong Excel thì tiền tự nhảy) và vẽ được bảng trong bản vẽ.

Claude không tự đặt giá và không lấy giá tham khảo. Dòng nào chưa có giá thì để trống, không tính vào tổng, và Claude kể tên
các dòng đó để hỏi khách.

Máy gói 3D dự toán từ khối lượng khối 3D (số liệu + Excel, không vẽ bảng); máy gói 2D từ số block và diện tích; máy Full đủ
cả.

Bàn giao đầy đủ: `docs/ban-giao/nang-cap-g3.md` trên nhánh `full/nang-cap-g3`.

### Con số

Mọi số ở `G3Numbers` (`Core/Estimate.cs`).

| Số / chữ | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- |
| Đơn vị tiền mặc định **"đồng"** (khách đổi được, vd USD) | tiêu đề cột "Đơn giá (đồng)", "Thành tiền (đồng)" | khách Việt Nam |
| Thành tiền làm tròn **đến đồng** (nửa đồng thì làm tròn lên) | số tiền | tiền đồng không có số lẻ |
| Trong Excel số tiền có dấu tách hàng nghìn (`#,##0`) | dấu chấm hay phẩy hiện theo cài đặt của máy khách | dễ đọc số lớn |
| Trong bảng vẽ: viết **1.234.567**, số lẻ dùng dấu phẩy **12,5** | số trong bảng | cách viết số Việt Nam, như các bảng đợt C / D |
| Khối lượng giữ **3** số lẻ | cột Khối lượng | như bảng khối lượng đợt D |
| Đơn vị: **m³** (khối), **m²** (diện tích), **cái** (block) | cột Đơn vị | đơn vị của bảng nguồn |
| Cột: STT, Hạng mục, Đơn vị, Khối lượng, Đơn giá, Thành tiền; dòng "Cộng", "Tổng cộng"; tiêu đề "BẢNG DỰ TOÁN"; tab Excel "Dự toán" | bảng và file | mẫu bảng dự toán quen dùng |
| Tên file **`<bản vẽ>-du-toan.xlsx`** | tên file | không dấu, như các file Excel đợt F4 |
| Tối đa **5** dòng phần trăm (thuế, chi phí khác) và **500** đơn giá một lần | nhiều hơn thì lệnh từ chối | chặn trường hợp lỗi |
| Thể tích mặc định là thể tích **thực** (phần hai khối chồng nhau chỉ tính một lần) | số m³ | đúng khối lượng thi công; khách muốn tính gộp thì nói |
| Trong bảng vẽ đơn giá hiện tối đa **2** số lẻ; kết quả Claude nhận về tối đa **200** dòng | số trong bảng; bảng rất dài thì Claude chỉ đọc 200 dòng đầu (file Excel vẫn đủ) | thread rà bổ sung dòng này 25/11, số đã có sẵn trong code |

### Quyết định tự chọn

| Tình huống | Đã chọn | Ghi chú |
| --- | --- | --- |
| Lưu bảng đơn giá để dùng lại | không lưu trong plugin; khách đưa giá mỗi lần, file Excel là nơi khách giữ và sửa giá | lưu hoặc đọc giá từ file Excel là việc thêm, làm sau được |
| Thuế / chi phí khác | không tự thêm; chỉ khi khách nói phần trăm | |
| Làm tròn | làm tròn từng dòng rồi mới cộng (Excel dùng cùng công thức) | muốn chỉ làm tròn tổng thì đổi công thức |
| Tên hạng mục và tên trong bảng giá lệch nhau | coi là một khi chỉ khác dấu, chữ hoa / thường, gạch nối; khách dùng được mẫu có `*`; không đoán "gần giống" | |
| Hai giá khác nhau cùng khớp một dòng | để trống dòng đó và hỏi lại, không chọn hộ | |
| Giá khách đưa là 0 | coi là một giá (thành tiền 0), không coi là thiếu | |
| Giá ghi đơn vị khác đơn vị của dòng (vd giá theo m² cho dòng m³) | vẫn tính, kèm ghi chú nhắc kiểm | muốn chặn hẳn thì đổi thành từ chối dòng đó |
| Giá khách đưa mà không khớp dòng nào | kể lại trong kết quả | |
| Chế độ "Chỉ xem" | tính và xuất Excel được; vẽ bảng vào bản vẽ bị chặn | file Excel nằm ngoài bản vẽ, như các bảng đợt F4 |
| Macro | không ghi lại file Excel khi phát lại | như đợt F4 |
| Khách gõ đơn vị "m3", "m2" | coi là m³, m² | thread rà thêm 25/11 |
| Nhiều dòng phần trăm | mỗi dòng đều tính trên dòng "Cộng", không tính chồng lên nhau | thread rà xác nhận 25/11 |

### Sau khi rà (25/11 23h00)

Thread rà sửa 3 lỗi, không đổi số nào: khách gõ đơn vị "m3" / "m2" bị ghi chú là sai đơn vị; hai giá khớp lẫn nhau còn bị kể
là "không khớp dòng nào"; tên giống số (phòng "101", mã "007") bị đổi thành số trong Excel (nay giữ là chữ). Thread rà cũng
quét và xác nhận không có giá mẫu nào ở chỗ Claude đọc (mô tả lệnh, hướng dẫn, skill). Nhánh `full/nang-cap-g3-test` @
`4061a6c` (code `d6b4a60`), CI 45649649749 xanh, lõi 1292, trợ lý 379.

### Chưa kiểm được

- Chưa chạy trên AutoCAD thật (bảng vẽ, thẻ ghi đè, macro).
- Chưa mở file bằng Excel thật, LibreOffice hay Google Sheets (công thức tính lại, dấu tách hàng nghìn theo máy).
- Chưa thử với Claude thật xem có giữ đúng luật "không tự đặt giá" khi khách ép không.

## G4: render thật

Khách nói "render" hoặc "ra ảnh đẹp" (gói 3D / Full). AutoCAD render góc nhìn đã lưu hoặc góc đang xem ra file PNG cạnh bản
vẽ. Trong lúc chờ, khung chat hiện tiến độ và AutoCAD không bị treo; bấm Dừng trong khung chat hoặc Esc trong AutoCAD là
dừng. Nền và ánh sáng chỉ đổi tạm trong lúc render rồi tự trả lại.

**Đường lui:** lệnh render lỗi, máy không có lệnh render hoặc chờ quá thời gian tối đa thì plugin lưu ảnh chụp như hiện nay
vào đúng file đó, và Claude nói rõ đây là ảnh chụp chứ không phải ảnh render.

**Vật liệu:** AutoCAD không cho chương trình tự lấy vật liệu từ thư viện Autodesk. Vì vậy plugin chỉ dùng vật liệu thư viện
khi bản vẽ đã có sẵn (khách kéo vào từ Materials Browser); không có thì dùng vật liệu màu đơn giản của đợt F3 và nói rõ.

**Điểm rủi ro nhất của cả đợt G:** cách plugin trả lời các câu hỏi của lệnh render được viết theo trí nhớ, chưa đối chiếu
được với tài liệu Autodesk và chưa chạy trên AutoCAD thật. Sau lần rà, nếu thứ tự câu hỏi khác đi thì phần lớn trường hợp
plugin nhận ra ngay và lưu ảnh chụp (kèm lý do); chỉ khi lệnh đứng chờ nhập thì khách mới phải chờ tới thời gian tối đa (hoặc
bấm Dừng / Esc). Mục này cần cậu thử tay trước tiên (`docs/checklist-tong-the.md`, mục G0-2, G0-3).

Bàn giao đầy đủ: `docs/ban-giao/nang-cap-g4.md` trên nhánh `full/nang-cap-g4`.

### Con số

Mọi số ở `G4Numbers` (`Core/Render.cs`).

| Số | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- |
| Cỡ ảnh mặc định **1920 × 1080** | cỡ file ảnh | Full HD, vừa màn hình và slide |
| Cỡ ảnh khách chọn: **64 – 4096** điểm mỗi cạnh; cho một cạnh thì cạnh kia theo tỉ lệ 16:9 | — | giới hạn an toàn cho bộ nhớ |
| Ba mức: nhanh, **vừa** (mặc định), đẹp | chất lượng và thời gian chờ | ba mức có sẵn của AutoCAD (Low / Medium / High) |
| Thời gian ước lượng ở cỡ Full HD: nhanh **20 s**, vừa **60 s**, đẹp **180 s** | dòng tiến độ trong khung chat | ước lượng thô, chưa đo trên máy thật |
| Thời gian chờ tối đa: nhanh **120 s**, vừa **300 s**, đẹp **900 s** | quá thì dừng và lưu ảnh chụp | không để khách chờ vô hạn; đủ cho máy văn phòng |
| Nền mặc định **trắng** | nền ảnh | an toàn, dễ in |
| Nền "trời": xanh nhạt chuyển dần xuống gần trắng | nền ảnh | có trời mà không cần đặt mặt trời |
| Ánh sáng mặc định: dùng đèn / mặt trời của bản vẽ nếu có, không thì đèn mặc định của AutoCAD | độ sáng ảnh | không đổi ánh sáng khách đã đặt |
| Giờ nắng: **chưa đặt** | — | đặt giờ cần vị trí địa lý; làm sau nếu cậu muốn |
| Ảnh chụp khi đi đường lui: cạnh dài tối đa **1568** điểm | cỡ ảnh chụp | như ảnh của đợt F3 |
| Bảng từ khóa vật liệu (bê tông, gạch, vữa / sơn, kính, gỗ, ngói, thép / kim loại, đá) khớp với tên vật liệu thư viện tiếng Anh | vật liệu nào được chọn | khớp nguyên từ trong tên; nhiều cái khớp thì lấy cái đầu theo tên |
| Tên file `<bản vẽ>-<góc nhìn>-render.png`; không nói góc nhìn: `<bản vẽ>-hien-tai-render.png` | tên file | theo cách đặt tên ảnh của đợt F3 |

### Quyết định tự chọn

| Tình huống | Đã chọn | Ghi chú |
| --- | --- | --- |
| Lệnh mới hay thêm vào lệnh cũ | thêm lựa chọn "render" vào lệnh góc nhìn sẵn có | không tăng phần mô tả nạp sẵn; tách thành lệnh riêng sau cũng được |
| Khách không nói góc nhìn nào | render cái khách đang nhìn | "ra ảnh đẹp" là ảnh của cái đang xem |
| Khách bấm Dừng | không tự lưu ảnh chụp thay thế | khách chủ động dừng |
| Đường lui ghi vào đâu | cùng file khách đã đồng ý ghi | |
| File ảnh đã có | hỏi qua thẻ Đồng ý / Hủy trước khi đổi bất cứ gì | như đợt F5 |
| Chế độ "Chỉ xem" | vẫn render được, dùng nền và ánh sáng của bản vẽ, không đổi gì | |
| AutoCAD bị tắt giữa lúc render | nền tạm có thể còn lại trong bản vẽ; khách đổi lại bằng tay | rủi ro đã biết, hiếm |
| Đang có lệnh AutoCAD chạy dở | từ chối, nhờ khách bấm Esc | |

### Sau khi rà (26/11 00h21)

Thread rà sửa 6 lỗi, không đổi số nào trong bảng trên:

- **Nặng:** máy có tên người dùng chứa dấu cách hoặc dấu tiếng Việt (vd `C:\Users\Nguyen Van A`) thì phần sau dấu cách của
  đường dẫn file tạm bị AutoCAD hiểu thành một lệnh khác. Nay file tạm chỉ đặt ở thư mục không dấu, không dấu cách; không có
  thư mục như vậy thì đi thẳng đường lui (ảnh chụp).
- **Đường lui tới sớm hơn:** lệnh render hỏi khác thứ tự thì câu trả lời của plugin bị AutoCAD báo "lệnh lạ"; plugin thấy vậy
  là dừng ngay và lưu ảnh chụp. Lệnh render kết thúc mà 20 giây sau chưa có file cũng chuyển sang ảnh chụp. Chỉ còn một
  trường hợp phải chờ tới thời gian tối đa: lệnh đứng im chờ nhập (khách bấm Dừng / Esc để thôi sớm).
- AutoCAD bận render làm trợ lý tưởng lỗi và bỏ cuộc, nền tạm không được trả lại: nay trợ lý hỏi tiếp trong giới hạn.
- File tạm được dọn ở mọi nhánh; góc nhìn luôn được trả lại; câu báo về vật liệu thư viện nói rõ là khớp theo tên.

Quyết định thêm: thứ tự câu hỏi khác đi thì bỏ render, không thử đoán thứ tự khác. Checklist thử tay có thêm mục F (chép lại
các câu hỏi thật của lệnh render, thử máy có tên người dùng chứa dấu cách).

Nhánh `full/nang-cap-g4-test` @ `29a2e6c` (code `fdeb306`), CI 45661671404 xanh, lõi 1299, trợ lý 387.

### Chưa kiểm được

- Chưa chạy trên AutoCAD thật: thứ tự câu hỏi thật của lệnh render trên AutoCAD 2022–2027 và tên các mức chất lượng; AutoCAD
  có trả lời plugin trong lúc đang render không; Dừng / Esc có ngắt được render đang chạy không; nền trắng / trời có vào ảnh
  không; lệnh render có ghi cài đặt vào bản vẽ không (kể cả khi "Chỉ xem"); thời gian render thật (để chỉnh số ước lượng).

## G5: vẽ lại từ ảnh / PDF scan

Khách kéo hoặc dán ảnh chụp, bản scan vào khung chat (hoặc nói đường dẫn ảnh / PDF) rồi gõ "vẽ lại bản này" (gói 2D / Full).
Các bước Claude làm:

1. gắn ảnh hoặc trang PDF làm nền trên một layer riêng (khóa, không in), nằm bên phải bản vẽ đang có;
2. tìm một kích thước ghi trên bản scan hoặc hỏi khách, rồi co ảnh cho đúng tỉ lệ thật. Không có kích thước thật thì Claude
   hỏi, không đoán;
3. ảnh chụp nghiêng thì xoay cho thẳng theo một bức tường;
4. xem ảnh có lưới tọa độ, vẽ tường, cửa, cửa sổ, tên phòng, kích thước chính đè lên ảnh, rồi xem lại để so.

Kết quả luôn ghi rõ là **bản nháp cần khách kiểm kích thước**, kể các chỗ đọc không chắc. Một lần U hoàn tác cả lượt.

**Hai điểm rủi ro, chỉ biết được khi thử thật:**

- Sau lần rà, ảnh Claude xem **dựng thẳng từ file ảnh gốc** (nét đã vẽ phủ lên), nên Claude thấy bản scan dù AutoCAD có đưa
  ảnh nền vào ảnh chụp hay không. Riêng **PDF scan** vẫn xem qua ảnh chụp của AutoCAD: nếu trắng trơn thì Claude nói không
  thấy và nhờ khách xuất trang PDF ra PNG / JPG, không vẽ mò. Bước tự kiểm G5 ghi AutoCAD có vẽ ảnh nền khi chụp không.
- Chưa biết Claude đọc tường, chữ, kích thước trên ảnh chính xác tới đâu.

Mỗi lần "vẽ lại" tốn thêm của khách khoảng 6–8 nghìn token cho phần xem ảnh (ước lượng), chưa kể các lệnh vẽ.

Bàn giao đầy đủ: `docs/ban-giao/nang-cap-g5.md` trên nhánh `full/nang-cap-g5`.

### Con số

Mọi số ở `G5Numbers` (`Core/Trace.cs`), trừ số lần xem ở `agent/src/tools/trace.ts`.

| Số | Khách thấy gì | Vì sao đề xuất |
| --- | --- | --- |
| Tối đa **8** lần xem ảnh mỗi yêu cầu | quá thì Claude dừng xem, hoàn tất bản nháp và nói phần chưa soát | 1 lần xem cả ảnh + khoảng 5 phần + 1–2 lần so; mỗi lần khoảng 1 nghìn token |
| Ảnh gửi Claude: cạnh dài **1024** điểm (Claude xin được tới 1568) | — (ảnh hưởng số token) | đủ đọc nét chính; chữ nhỏ thì xem phóng to từng phần |
| Bước lưới tự chọn số tròn (100 mm … 50 m), khoảng **10** đường trên cạnh dài | lưới trên ảnh Claude xem | nhãn tròn số, dễ đọc |
| File tối đa **50 MB** | lớn hơn bị từ chối, nhờ khách scan nhỏ hơn | bản scan 300 dpi khổ A1 khoảng 20–40 MB |
| Loại file: PNG, JPG, BMP, TIF, GIF, PDF | loại khác bị từ chối kèm hướng dẫn | các loại AutoCAD gắn được |
| Layer ảnh nền **`X-ANH-GOC`**, màu xám, khóa, không in | tên layer trong bản vẽ | đứng riêng khỏi các layer vẽ |
| Lúc mới gắn (chưa đặt tỉ lệ) ảnh rộng **10 m** | cỡ ảnh trước khi đặt tỉ lệ | cỡ một mặt bằng nhà, dễ nhìn |
| Ảnh đặt cách bản vẽ đang có **5 m** về bên phải | chỗ ảnh nằm | không đè lên hình có sẵn |
| Tường dày **200 mm** khi không đọc được độ dày trên bản scan (Claude nói rõ đã lấy số này) | độ dày tường trong bản nháp | tường gạch thường gặp |
| Xoay thẳng tối đa **45°** | xoay nhiều hơn bị từ chối | ảnh nghiêng thật hiếm khi quá vài độ; quá 45° thường là chọn sai cặp điểm |

### Quyết định tự chọn

| Tình huống | Đã chọn | Ghi chú |
| --- | --- | --- |
| Chưa có kích thước thật | không bao giờ tự đặt tỉ lệ theo điểm ảnh; kết quả nhắc đặt tỉ lệ | |
| Ảnh khách dán vào khung chat | lưu rồi chép cạnh bản vẽ (`<bản vẽ>-anh-goc-<mã>.png`) | thư mục tạm của khung chat có thể bị dọn |
| Ảnh khách kéo từ file | dùng chính file đó, không chép | khách tự giữ file |
| Khách chỉ hỏi về ảnh, chưa bảo vẽ | không gắn gì vào bản vẽ | |
| Ảnh chụp bị méo phối cảnh | chỉ xoay thẳng, không sửa méo; Claude nói rõ | |
| PDF xuất từ phần mềm CAD (không phải scan) | vẫn gắn được, kèm gợi ý dùng lệnh nhập PDF của AutoCAD | plugin không tự nhận ra loại PDF này |
| Hướng dẫn cách làm đặt ở đâu | trong mô tả lệnh (nạp khi cần) và một mục của skill vẽ kiến trúc; không tạo skill mới | máy đã cài skill cũ không nhận được file skill mới |
| Macro | không ghi lại | như lệnh sửa lỗi bản vẽ của G2 |
| UCS nghiêng khỏi mặt bằng | từ chối, nhờ khách đưa UCS về mặt bằng | |
| Chữ viết trong ảnh | coi là nội dung bản vẽ, không phải lệnh cho Claude | ghi rõ trong mô tả lệnh |
| Ảnh xem lấy từ đâu | luôn dựng từ file ảnh gốc, nét đã vẽ phủ lên | thread rà thêm 25/11 |
| Ảnh xem của PDF | giữ ảnh chụp của AutoCAD; trắng thì báo khách và dừng | dựng trang PDF cần thư viện đọc PDF, chưa có sẵn |
| Ảnh / PDF nào được gắn | mọi ảnh / PDF Claude biết đường dẫn (khách nói, hoặc ảnh đính kèm) | muốn chỉ cho file khách đưa trong cuộc chat thì cần thêm việc |
| Bản vẽ chưa lưu | ảnh dán chép vào thư mục Documents | muốn thì đổi thành hỏi khách lưu bản vẽ trước |

### Sau khi rà (26/11)

Thread rà sửa 5 lỗi, không đổi số nào khách thấy: điểm Claude đọc trên lưới (theo UCS) bị hiểu theo tọa độ thế giới khi UCS
xoay / dời (ảnh co / xoay / xem sai chỗ); ảnh xem phụ thuộc AutoCAD có vẽ ảnh nền hay không (nay dựng từ file gốc); loại file
chỉ xét theo đuôi tên (ảnh HEIC / WebP đổi đuôi `.png` lọt qua; nay đọc nội dung file); `check_drawing` (G2) báo nhầm layer
ảnh nền; `create_drawing_set` (G1) thu nhỏ tờ mặt bằng để chứa cả ảnh nền. Số kỹ thuật mới (khách không thấy): giải nén ảnh
tối đa 6 000 px chiều ngang; ảnh xem coi là trắng khi dưới 0,5 % điểm khác nền. Nhánh `full/nang-cap-g5-test` @ `464b6e5`.

### Chưa kiểm được

- Chưa chạy trên AutoCAD thật: gắn ảnh / PDF, ảnh nền có hiện trong ảnh Claude xem không, U hoàn tác ảnh nền, đường dẫn có dấu
  tiếng Việt.
- Chưa thử với Claude thật: đọc ảnh tốt tới đâu, số lần xem thực tế, kích thước lệch bao nhiêu.

## Test tổng thể đợt G (26/11)

*Nhánh `full/nang-cap-g-tong-the`. Chi tiết: `docs/ban-giao/test-tong-the-dot-g.md`. Thử tay cả đợt G: khối "Đợt G" cuối
`docs/checklist-tong-the.md` (~3 giờ; mục G0 làm trước: các điểm rủi ro nhất).*

Soát file này so với code: các số của G1 → G5 khớp code. Sửa mô tả G4 (đường lui tới sớm sau lần rà, tên file khi không nói
góc nhìn) và G5 (ảnh xem dựng từ file gốc, các quyết định và lỗi của lần rà). Không đổi số nào trong code.

### Lỗi chéo giữa các đợt đã sửa

- `check_drawing` (G2) báo khung nhìn của tờ **PHỐI CẢNH** (G1) là "tỉ lệ không chuẩn": khung nhìn 3D không có tỉ lệ in, nay
  không kiểm tỉ lệ cho khung nhìn không nhìn thẳng xuống.
- `area_schedule` (đợt C) và `cost_estimate` theo diện tích (G3), khi không chỉ layer, đếm cả **hình mặt cắt / mặt đứng** do
  bộ hồ sơ (G1) hoặc `section_view` vẽ ra (layer A-SECT, A-SECT-PAT, A-SECT-BACK, A-ELEV) thành diện tích phòng / layer: nay
  bỏ các layer đó (kết quả nói rõ đã bỏ; khách chỉ layer thì vẫn đếm).

### Quyết định tự chọn thêm

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Bảng diện tích không chỉ layer, bản vẽ có hình mặt cắt / mặt đứng vẽ từ mô hình 3D | bỏ các layer A-SECT* / A-ELEV, ghi rõ trong kết quả | khách nêu các layer đó trong `layers` |
| Khung nhìn 3D (phối cảnh) khi soát bản vẽ | không kiểm tỉ lệ | — |

### Token lúc mở chat (ước tính, cùng cách đo test tổng thể đợt F)

| Gói / chế độ | Cuối đợt F, nạp gọn | Cuối đợt G, nạp gọn | Cuối đợt F, tắt nạp gọn | Cuối đợt G, tắt nạp gọn |
| --- | --- | --- | --- | --- |
| Gói 2D | 13,4k | 13,7k | 41,1k | 45,2k |
| Gói 3D | 18,8k | 19,0k | 42,6k | 45,0k |
| Full · Cả hai | 20,7k | 21,0k | 62,0k | 66,5k |
| Full · chế độ 2D | 13,5k | 13,7k | 41,2k | 45,2k |
| Full · chế độ 3D | 18,8k | 19,0k | 42,7k | 45,1k |

Nạp gọn (mặc định): 5 lệnh mới chỉ thêm tên và vài dòng system prompt, khoảng 0,2–0,3k mỗi gói. Mô tả đủ của lệnh mới chỉ nạp
khi Claude cần (hoặc khi tắt "Nạp gọn lệnh").

## Sau lượt kiểm tra độc lập F + G (26/11)

*Một agent khác test lại toàn bộ đợt F và G, báo 27 lỗi (`docs/ban-giao/kiem-tra-doc-lap-dot-fg.md`). Ba thread sửa, thread giữ
nhánh tích hợp gộp lại trên `full/nang-cap-g-tong-the`. Chi tiết: `docs/ban-giao/test-tong-the-dot-g.md` mục "Sau kiểm tra độc
lập F + G", `docs/ban-giao/nang-cap-g-sua-kfg-a.md`, `docs/ban-giao/nang-cap-g-sua-kfg-b.md`. Thử tay: `docs/checklist-tong-the.md`
mục G2-D1 / D1b, G4 F, G5 H, G6-1 / G6-2, G7.*

### Khách sẽ thấy khác gì

**Render (G4):**
- "Render góc Phối cảnh 1" (góc đã lưu) nay chạy được. Trước đây có thể báo lỗi "could not prepare the render".
- Bấm U sau khi render không còn nhảy sang góc vừa render.
- Bản vẽ có đèn của khách thì ảnh sáng theo đèn đó: đèn mặc định được tắt trong lúc render rồi bật lại.
- Render rơi về ảnh chụp thì Claude không còn nói về nền / ánh sáng / mức đẹp của render.

**Vẽ lại từ ảnh (G5):**
- Ảnh nền nằm thẳng theo UCS đang dùng.
- Claude không co / xoay ảnh của khách (vd logo trong khung tên), chỉ đụng ảnh do lệnh vẽ lại gắn.
- Ảnh ghi bằng đường dẫn mạng chỉ được mở khi cùng máy chủ + thư mục chia sẻ với bản vẽ. Chỗ khác thì Claude nhờ **kéo file vào
  khung chat** hoặc chép về máy. File kéo vào chat từ ổ mạng: lúc gửi tin không chép gì; lúc Claude gắn ảnh mới chép về máy (tên `tep-…`,
  giữ 7 ngày từ lúc chép; file quá 50 MB, đã mất hay mất mạng giữa chừng thì Claude báo lý do). Các dạng đường dẫn đặc biệt
  (`\\.\…`, `\\?\…`) chỉ nhận khi trỏ về một ổ của máy; dạng khác bị từ chối. Bản vẽ mở từ ổ mạng ánh xạ (vd Z:) thì ghi
  đường dẫn ảnh theo ổ đó.
- Ảnh dán vào chat: chỉ lưu trên máy có phần 2D; ảnh cũ hơn 7 ngày tự dọn; bản vẽ chưa lưu thì Claude nhờ lưu trước.
- PDF nhiều trang đã sửa nhiều lần: số trang đúng.

**Soát bản vẽ (G2):**
- Hatch mặt cắt của bộ hồ sơ và hatch vẽ bằng điểm bấm không còn bị báo "mất liên kết".
- Tab `Layout2`… khách tự đặt khung nhìn và vẽ thêm thì được soát như tờ in.
- Tab chỉ có khung nhìn AutoCAD tự tạo thì không báo lỗi, nhưng Claude nêu tên tab. Muốn soát tab đó thì nói "soát cả Layout1".
- Kích thước bị gõ đè bằng một dấu cách (không in ra số) nay bị báo.
- Hai hình lệch nhau rất ít (≤ 0,1 mm) luôn bị tính là trùng, kể cả khi nằm hai bên ranh ô lưới.
- Nét của bộ hồ sơ không bị coi là trùng nữa, nên không bị xóa nhầm.
- "Sửa" không chuyển hình sang layer không in / đang tắt.
- "Sửa" chỉ đụng đúng phần đã soát (Model hay cả layout).

**Dự toán (G3):**
- Tên giá **giữ dấu**: "Sân" không còn ăn giá của "Sàn". Hoa thường / khoảng trắng vẫn không quan trọng.
- Gõ tên không dấu ("phong khach") vẫn ghép được nếu chỉ khớp một dòng. Khớp nhiều dòng (vd "san" khớp cả Sàn và Sân) thì để
  trống và Claude hỏi. Mẫu có dấu `*` cũng vậy: "San*" khi có cả "Sân phơi" và "Sàn gỗ" thì để trống cả hai và hỏi; "Sàn*"
  (gõ có dấu) chỉ khớp các tên bắt đầu bằng "Sàn". Phần nằm ở chỗ `*` thì khác dấu cũng được ("san*" cho "Sàn gỗ", "Sàn đá").
- `A-WALL` và `A_WALL` chỉ gộp khi bản vẽ không có cả hai.
- Dự toán theo diện tích gặp đường bao nhà chứa các phòng thì **không tính** (tránh tính tiền hai lần). Claude hỏi bỏ đường bao hay cứ tính.
- Lời cảnh báo của bảng nguồn (vd đã bỏ layer mặt cắt) được Claude kể lại.
- Đơn giá lẻ (12,5) hiện đủ trong Excel, không còn làm tròn thành 13.
- Máy gói 3D không được mời "dự toán theo diện tích / block". Máy gói 2D không được mời "theo khối lượng 3D".

**Bộ hồ sơ (G1):**
- Thẻ "thay bộ cũ" đếm đúng số nét / hatch của bộ cũ.
- Bộ cũ đã bị xóa tay hết thì không hỏi thẻ nữa.
- Bảng thống kê / dự toán cạnh mặt bằng không làm tờ mặt bằng lệch tâm hay thu nhỏ.
- Chân tường lệch nhau vài cm không đẻ thêm tầng.
- Tờ của bộ cũ được nhận theo mã riêng của tờ, không theo tên: layout mới khách đặt trùng tên không bị xóa (kể cả sau một lần
  AutoCAD lỗi giữa lúc ra bộ). Riêng bộ làm bằng bản cài trước bản sửa này (chưa lưu mã tờ) thì lần đầu vẫn nhận theo tên.
- AutoCAD lỗi giữa chừng thì lần sau vẫn biết bộ dở dang đó.
- Macro không ghi đè PDF đã có (in lại bằng chat, có thẻ hỏi).

**Khác:**
- Đồng ý "lưu đè trạng thái layer A" không còn cho xóa luôn A trong cùng lượt; xóa có thẻ riêng.
- Đồng ý "gạch cho cả layer tường" không cho vật liệu khác; đồng ý "màu đỏ" không cho màu khác.
- Claude biết rõ gói nào có bộ hồ sơ / dự toán / soát bản vẽ / vẽ lại từ ảnh. Chữ trong ảnh đính kèm là nội dung, không phải lệnh.

### Con số mới chờ cậu duyệt

| Số | Đề xuất | Khách thấy | Vì sao |
| --- | --- | --- | --- |
| Giữ ảnh dán trong thư mục khung chat | 7 ngày | ảnh dán cũ hơn bị xóa khỏi `%LOCALAPPDATA%\ClaudeCAD\anh-dinh-kem` (bản đã gắn vào bản vẽ được chép cạnh bản vẽ nên không mất) | đủ một tuần làm việc, không đầy đĩa |
| Chờ thêm sau khi lệnh RENDER xong mà chưa có ảnh | số lớn hơn giữa 20 giây và thời gian ước lượng của mức (nhanh ~20 s, vừa ~60 s, đẹp ~180 s cho Full HD) | lâu nhất phải chờ trước khi nhận ảnh chụp thay ảnh render | luồng chính đã chọn |
| Đơn giá lẻ trong Excel | 2 số lẻ (`#,##0.00`), chỉ khi có đơn giá lẻ | ô đơn giá hiện 12,50 | giữ đúng số khách gõ; giá chẵn vẫn hiện như cũ |

Không đổi: dung sai "trùng" vẫn 0,1 mm (nay đúng như tài liệu); gom tầng vẫn 50 mm (nay so với chân tường kề nhau).

### Quyết định tự chọn thêm

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Ghép đơn giá theo tên (luồng chính chọn) | giữ dấu; không dấu chỉ khi khách gõ không dấu và khớp một dòng; nhiều dòng thì để trống, hỏi | — |
| Dự toán diện tích có vùng lồng nhau (luồng chính chọn) | từ chối, hỏi khách; khách cho phép (`allow_nested`) thì tính | — |
| Tab mặc định chỉ có khung nhìn (luồng chính chọn) | không báo lỗi, nêu tên; "soát cả <tab>" để soát | coi luôn là tờ in (sẽ báo nhiễu ở hầu hết bản vẽ) |
| Đường dẫn mạng của ảnh (luồng chính chọn) | chỉ cùng thư mục chia sẻ với bản vẽ | — |
| Tờ của bộ cũ khách đã đổi tên | coi là của khách, giữ nguyên khi thay bộ | xóa theo mã tờ dù đã đổi tên |
| AutoCAD lỗi giữa lúc ra bộ | ghi lại các tờ / hình đã làm + phần còn của bộ cũ, báo lỗi | — |
| Macro + PDF đã có | giữ PDF cũ, vẫn vẽ các tờ, ghi chú in lại bằng chat | — |
| Ảnh dán: không lưu bản gốc | nói rõ ảnh ≤ 1568 px, nhờ kéo file gốc khi bản scan lớn | — |
| Ảnh dán vào bản vẽ chưa lưu | nhờ lưu bản vẽ trước | chép vào Documents như cũ |
| Dừng / quá giờ khi render | trả cảnh ngay; file tạm còn sót dọn ở lần sau (> 1 giờ) | — |
| PDF không đếm được trang | vẫn gắn trang 1, nói rõ | từ chối |
| Hatch / nét vẽ trước bản sửa | hatch cũ không có dấu vẫn bị báo nếu mất liên kết | — |

### Token lúc mở chat sau khi sửa

| Gói / chế độ | Nạp gọn (trước sửa → sau sửa) | Tắt nạp gọn |
| --- | --- | --- |
| Gói 2D | 13,7k → 13,7k | 45,2k → 45,5k |
| Gói 3D | 19,0k → 19,1k | 45,0k → 45,2k |
| Full · Cả hai | 21,0k → 21,0k | 66,5k → 66,8k |
| Full · chế độ 2D | 13,7k → 13,8k | 45,2k → 45,5k |
| Full · chế độ 3D | 19,0k → 19,1k | 45,1k → 45,2k |

Phạm vi gói trong system prompt thêm khoảng 0,1k; phần còn lại là mô tả lệnh dài thêm (chỉ nạp khi Claude cần).

### Lượt 2 (sau khi agent kiểm tra độc lập soát lại, 26/11)

Soát lại thấy 26 / 27 lỗi sửa đúng, KFG-07 sửa chưa đủ, và 11 lỗi mới nhỏ hơn (KFG-28 … 38, không lỗi nặng). Đã sửa hết:

- **Dự toán:** mẫu `*` gõ không dấu ("San*") gặp các tên chỉ khác dấu ("Sân phơi", "Sàn gỗ") thì để trống và hỏi, như tên thường.
- **Bộ hồ sơ:** sau một lần AutoCAD lỗi giữa chừng, layout cậu tự tạo trùng tên tờ cũ không còn bị nhận nhầm là của bộ.
- **Soát bản vẽ:** hình mặt cắt / hình chiếu đặt layer riêng cũng không bị báo hatch "mất liên kết" và không bị xóa nét vì "trùng";
  soát bản vẽ rất nhiều nét nhanh hơn; polyline vẽ ngược chiều nay được nhận là trùng đúng.
- **Vẽ lại từ ảnh:** đường dẫn đặc biệt kiểu `\\.\UNC\…` không lách được luật đường dẫn mạng; file kéo vào chat từ ổ mạng được chép
  về máy nên dùng được; ảnh đặt mặc định không đè lên bản vẽ khi UCS xoay; góc ảnh PDF báo đúng theo UCS; đếm trang PDF tối đa
  khoảng 2 giây.
- **Render:** "render lại góc đã lưu" rồi bấm Hủy nhiều lần không để lại đối tượng thừa.

Không có con số mới cho khách. Quyết định tự chọn thêm:

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| File khách kéo vào chat từ ổ mạng | khung chat chép về máy một lần, Claude dùng bản chép | mở thẳng file trên máy mạng (kém an toàn) |
| Bản vẽ ở ổ mạng ánh xạ, ảnh ghi theo tên máy chủ | từ chối, nhắc ghi theo ổ (vd Z:) | đổi ổ ra máy chủ (phải gọi thêm Windows) |
| Nét hình chiếu bị khách COPY đè lên chính nó | không báo trùng (bản chép mang cùng dấu) | — |
| Bộ hồ sơ làm bằng bản cài trước (chưa lưu mã tờ) | lần đầu vẫn nhận tờ theo tên | — |

Vẫn giữ, cần cậu thử thật rồi chốt: chờ thêm sau khi lệnh RENDER xong = số lớn hơn giữa 20 giây và thời gian ước lượng của mức
(một chỗ đổi: `RenderLogic.EndGrace`).

### Lượt 3 (sau lần soát lại thứ hai, 26/11)

Lần soát lại thứ hai thấy các lỗi lượt 2 đã sửa đúng, còn 4 lỗi nhỏ (KFG-39 … 42), đã sửa hết:

- **Soát bản vẽ:** đường gần thẳng đứng lệch rất ít nay được nhận là trùng dù vẽ theo chiều nào; bản vẽ có rất nhiều nét chung một
  điểm (kiểu nan quạt) soát vẫn nhanh.
- **Vẽ lại từ ảnh, file kéo từ ổ mạng:** gửi tin không còn phải chờ chép file. File chỉ được chép khi Claude gắn ảnh (đúng lúc cậu
  nhờ vẽ lại), nên file mạng lớn có thể làm AutoCAD chờ lúc đó. Bản chép giữ 7 ngày tính từ lúc chép; mất mạng giữa chừng không để
  lại file dở.

Không có con số mới cho khách (7 ngày, 50 MB như trước). Quyết định tự chọn thêm:

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Lúc nào chép file mạng khách kéo vào chat | lúc Claude gắn ảnh (không chép file khách không định vẽ lại) | chép nền ngay lúc gửi tin |
| Danh sách file khách đã kéo vào chat | chỉ nhớ trong một phiên AutoCAD; mở lại AutoCAD thì kéo lại file mạng | lưu ra đĩa |

### Kết luận sau ba lượt soát lại (26/11)

Agent kiểm tra độc lập soát lại ba lượt: tổng 43 lỗi đã ghi, 42 đã sửa đúng, không còn lỗi mức vừa trở lên.

**Lỗi nhẹ đã biết, chấp nhận (không sửa ở đợt này):** "soát bản vẽ" chậm khoảng 6 giây với bản vẽ có khoảng 20 000 điểm hoặc
block nằm chồng thẳng đứng (cùng vị trí trên mặt bằng, khác cao độ). Rất hiếm. Lệnh có giới hạn 20 000 hình mỗi lần quét nên
không treo máy. Sửa sau: tính thêm cao độ khi chia ô so trùng (`Cell` trong `Core/DrawingCheck.cs`). Luồng chính quyết không sửa
thêm code sau lượt soát cuối, vì phần sửa mới sẽ không còn được kiểm độc lập.

**Số mới của ba lượt sửa (chờ cậu duyệt):**

| Số | Đề xuất | Khách thấy |
| --- | --- | --- |
| Ảnh dán / file mạng chép về giữ bao lâu | 7 ngày | ảnh cũ hơn tự xóa khỏi thư mục ảnh đính kèm (bản đã gắn vào bản vẽ thì còn cạnh bản vẽ) |
| File mạng lớn nhất được chép khi gắn | 50 MB (như trước) | file lớn hơn: Claude báo, nhờ chép về máy |
| Chờ thêm sau khi lệnh RENDER xong mà chưa có ảnh | số lớn hơn giữa 20 giây và thời gian ước lượng của mức | lâu nhất phải chờ trước khi nhận ảnh chụp thay ảnh render |
| Đơn giá lẻ trong Excel | 2 số lẻ, chỉ khi có đơn giá lẻ | ô đơn giá hiện 12,50 |

**Bốn điểm luồng chính giữ nguyên, chờ cậu xem (thử thật rồi chốt):**

1. **Chờ sau khi lệnh RENDER kết thúc:** như bảng trên. Cần cậu render mức "đẹp" trên máy thật xem AutoCAD có còn chạy tiếp sau
   khi dòng lệnh đã về `Command:` không (checklist G4 mục F.3). Đổi ở một chỗ (`RenderLogic.EndGrace`).
2. **Nét hình chiếu bị chép vẫn mang dấu:** nếu cậu COPY một nét của hình mặt cắt / hình chiếu rồi đặt đè lên chính nó, lần soát
   không báo trùng (bản chép mang cùng dấu của lệnh vẽ hình chiếu). Chỉ xảy ra với nét do hai lệnh đó vẽ.
3. **AutoCAD chờ khi gắn file mạng lớn:** file kéo vào chat từ ổ mạng chỉ được chép lúc Claude gắn ảnh, nên file lớn / mạng chậm làm
   AutoCAD đứng chờ đúng lúc đó (tối đa 50 MB).
4. **Danh sách file cậu kéo vào chat chỉ nhớ trong một phiên AutoCAD:** mở lại AutoCAD thì cần kéo lại file nằm ở ổ mạng.

Quyết định tự chọn của cả ba lượt: các bảng "Quyết định tự chọn thêm" ở trên (lượt 1, lượt 2, lượt 3).
