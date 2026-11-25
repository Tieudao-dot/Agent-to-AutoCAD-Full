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
được với tài liệu Autodesk và chưa chạy trên AutoCAD thật. Nếu thứ tự câu hỏi khác đi thì khách phải chờ hết thời gian tối
đa rồi mới nhận ảnh chụp. Mục này cần cậu thử tay trước tiên (`docs/checklist-dot-g4.md`).

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
| Tên file `<bản vẽ>-<góc nhìn>-render.png` | tên file | theo cách đặt tên ảnh của đợt F3 |

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

### Chưa kiểm được

- Chưa chạy trên AutoCAD thật: thứ tự câu hỏi của lệnh render trên AutoCAD 2022–2027; Dừng / Esc có ngắt được render đang
  chạy không; nền trắng / trời có vào ảnh không; thời gian render thật (để chỉnh số ước lượng).
