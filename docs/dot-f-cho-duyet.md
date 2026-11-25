# Đợt F: con số và quyết định chờ chủ dự án duyệt

*Luồng chính gom từ báo cáo của các thread đợt F (25/11/2026). Chủ dự án dặn chạy liền, không hỏi lại, nên các thread dùng
số đề xuất và tự chọn cách làm rẻ, an toàn, đổi lại được. File này để chủ dự án duyệt một lần ở báo cáo cuối chuỗi. Số
nào muốn đổi thì nói, sửa nhanh vì mỗi đợt để số ở một chỗ.*

"Theo thông lệ" nghĩa là cách vẽ phổ biến, thread không chắc đó là số của tiêu chuẩn nào.

## Đã chốt trong ngày 25/11 (không cần duyệt lại)

| Nhóm | Số (mm) |
| --- | --- |
| Cầu thang, mái dốc | Bản thang + chiếu nghỉ 120 (trước 150); bản mái dốc 120 (trước 150); khe giữa 2 vế thang chữ U 100. Sửa trong đợt F3 |
| Lan can | Cao 1100; khe giữa các song ≤ 100; trụ cách nhau ≤ 1500; tay vịn và trụ 50 × 50; song 20 × 20; thanh dưới cách sàn 100; tấm kín dày 20 |

## Đợt F1 (lệnh vẽ hiểu UCS, nút chế độ)

Không có con số nào cần duyệt. Cỡ nút chế độ (cao 22 px, rộng tối thiểu 24 px, cách nút bên cạnh 4 px) lấy theo các nút
đang có trên thanh trên cùng.

Quyết định tự chọn:

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| Macro ghi lúc hệ tọa độ đang xoay | Macro nhớ hệ tọa độ đó, chạy lại ra đúng chỗ cũ; macro cũ chạy theo hệ gốc | Cho macro chạy theo hệ tọa độ đang dùng lúc chạy lại |
| Đổi chế độ "Cả hai / 2D / 3D" giữa chừng | Có hiệu lực từ tin nhắn kế tiếp, có dòng báo cho khách | Bắt mở đoạn chat mới |
| Khách ở chế độ 2D nhờ vẽ 3D (và ngược lại) | Claude nhắc đổi chế độ, không mời mua gói. Macro không bị giới hạn theo chế độ | — |

## Đợt F2 (2D dùng hằng ngày)

Mọi số nằm ở một chỗ: `F2Numbers` trong `plugin/ClaudeCAD.Plugin/Core/Standards2D.cs` (nhánh `full/nang-cap-f2`).

### Con số chờ duyệt

| Nhóm | Số đề xuất | Nguồn |
| --- | --- | --- |
| Chữ kích thước | Cao 2,5 mm khi in (tỉ lệ 1:100 → cao 250 trong bản vẽ) | Skill kiến trúc sẵn có |
| Đầu kích thước | Gạch chéo 1,2 mm | Skill kiến trúc |
| Đường gióng, đường kích thước | Đường gióng cách vật 2 mm, thò qua đường kích thước 1,5 mm; đường kích thước thò qua đường gióng 1 mm; chữ cách đường 0,8 mm | Skill kiến trúc |
| Số lẻ của kích thước | Bản vẽ mm: không có số lẻ; cm: tối đa 1 số lẻ; mét: tối đa 2 số lẻ, số 0 thừa bị bỏ (vd đoạn 4 m hiện "4", 4,5 m hiện "4.5", 4,25 m hiện "4.25") — sửa sau kiểm tra độc lập (KTF-07): trước ghi nhầm "4.00" | Theo thông lệ |
| Font chữ | Arial, bề rộng chữ 0,85 | Skill kiến trúc |
| Chiều cao chữ khi in | Tiêu đề 5 mm; tên phòng 3,5; ghi chú 2,5; chữ nhỏ 1,8 | 3,5 và 2,5 từ skill; còn lại theo thông lệ |
| Tỉ lệ nét đứt (LTSCALE) | Mẫu số tỉ lệ × 0,5 (1:100 → 50); chỉ đặt khi bản vẽ còn để mặc định 1 | Theo thông lệ |
| Tỉ lệ mặc định | 1:100 | Theo thông lệ |
| Khổ giấy | A0–A4 theo ISO; mặc định A3 ngang, riêng A4 đứng | ISO |
| Khung bản vẽ | Cách mép giấy: trái 25 mm (chừa đóng gáy), ba cạnh còn lại 5 mm; nét khung 0,5 mm; nét trong khung tên 0,25 mm | Theo thông lệ |
| Khung tên | Rộng 180 mm, cao 52 mm, 6 dòng | Theo thông lệ |
| Chữ trong khung tên | Nhãn 2 mm; nội dung 2,5 mm; tên bản vẽ 3,5 mm | Theo thông lệ |
| Ô trong khung tên | 10 ô: Đơn vị TK, Công trình, Hạng mục, Tên bản vẽ, Người vẽ, Kiểm tra, Ngày (dd/MM/yyyy, tự điền hôm nay), Tỉ lệ, Số bản vẽ, Khổ giấy | — |
| Khung nhìn | Cách khung bản vẽ / khung tên 5 mm; khóa sẵn; viền không in ra | Theo thông lệ |
| Chọn tỉ lệ khi khách không nói | Tỉ lệ chuẩn chi tiết nhất mà bản vẽ vẫn vừa giấy, trong dãy 1:1 … 1:5000 | — |
| Cỡ block mặc định (mm) | Cửa đi 900; cửa 2 cánh 1400; cửa trượt 1800; cửa sổ 1200 (cửa trượt và cửa sổ tính tường dày 220); bồn cầu 400 × 700; lavabo 500 × 420; bồn tắm 1700 × 750; khay tắm 900 × 900; giường 1600 × 2000; sofa 2000 × 850; bàn 1200 × 800; ghế 450 × 450; tủ 1200 × 600; bếp 700 × 450; cây đường kính 3000. Mỗi loại có giới hạn nhỏ nhất / lớn nhất | Theo thông lệ |
| Layer mới | `KHUNG` (khung và khung tên), `KHUNG-VP` (viền khung nhìn, không in); block chèn vào `A-DOOR`, `A-WIND`, `A-FIXT`, `A-FURN`, `L-PLNT`; tên block dạng `CUA-DI-900`, `CUA-SO-1200` | — |
| Màu layer mới (chỉ khi layer chưa có) *(Test tổng thể đợt F bổ sung: có trong code, chưa có trong bảng)* | `KHUNG` màu 7 (trắng / đen), `KHUNG-VP` 8 (xám); `A-DOOR` 4 (xanh lơ), `A-WIND` 5 (xanh dương), `A-FIXT` 6 (tím), `A-FURN` 8 (xám), `L-PLNT` 3 (xanh lá) | Theo thông lệ |

### Ghi chú của người rà về nguồn các con số (thread rà F2, 25/11)

Người rà đã soát bảng trên: không đổi số nào, bảng đủ sau khi thêm 4 dòng dưới đây.

- **Lề khung bản vẽ (chủ dự án chọn):** số đề xuất là trái 25 mm, ba cạnh còn lại 5 mm, theo thông lệ quen dùng ở Việt
  Nam; không nên ghi là số của TCVN. Người rà nhớ ISO 5457 / TCVN 7285 dùng trái 20 mm, ba cạnh còn lại 10 mm nhưng chưa
  tra lại văn bản. Cần tra trước khi ghi tên tiêu chuẩn vào tài liệu cho khách.
- Chiều cao chữ 5 / 3,5 / 2,5 / 1,8 mm nằm trong dãy cỡ chữ của ISO 3098.
- Khung tên rộng 180 mm trùng với ISO 7200.
- Bốn dòng thêm vào bảng: tên trang in mặc định `BV-01`; chữ kích thước nằm trên đường kích thước và bỏ số 0 thừa; 5 kiểu
  nét nạp sẵn; layer do khách đặt tên mới thì màu 7 (trắng / đen).
- Cỡ block mặc định nằm ở mục của từng block trong `LibraryBlocks.Items`, không nằm ở `F2Numbers`. Các số khác ở
  `F2Numbers`. Bảng đã soát: `docs/ban-giao/nang-cap-f2-test.md` trên nhánh `full/nang-cap-f2-test`.

### Quyết định tự chọn

| Tình huống | Đã chọn |
| --- | --- |
| Số lệnh mới | Gộp còn 4 lệnh để đỡ token. Xem danh sách block = gọi lệnh chèn mà không nêu tên; lưu / gọi / liệt kê / xóa trạng thái layer chung một lệnh |
| Tên style | Chọn tiêu chuẩn "Kiến trúc" thì style tên `KT-ARIAL` / `KT-100` (giống skill); lựa chọn khác dùng tên `VN-…`, cùng bộ số |
| Bản vẽ đã có style trùng tên nhưng khác số | Giữ nguyên, không đặt làm hiện hành; chỉ ghi đè sau khi khách bấm Đồng ý trên thẻ (gõ chữ "đồng ý" không tính) |
| Khách đã tự đặt tỉ lệ nét | Giữ nguyên, chỉ báo số nên dùng |
| Bản vẽ đơn vị mét / cm | Có kiểu kích thước riêng (`VN-100-M`, `VN-100-CM`) |
| Ghi chú có mũi tên chỉ dẫn (multileader) | Chưa làm |
| Khung tên | Là block có ô điền, sửa được bằng lệnh sửa thuộc tính của AutoCAD. Khung nhìn nằm phía trên khung tên, không chồng lên |
| Hệ tọa độ đang xoay khi tạo trang in | Khung nhìn xoay theo |
| Máy in không có giấy đúng chiều | Xoay giấy 90°. Tạo trang lỗi giữa chừng thì xóa layout vừa tạo |
| Block thư viện trùng tên block khách đã có | Dùng block của khách, không vẽ lại |
| Gọi lại trạng thái layer | Không cần thẻ (như sửa layer thường), hoàn tác được. Lưu đè hoặc xóa trạng thái phải qua thẻ |
| Macro chạy lại một lệnh ghi đè | Hỏi thẻ lại |
| Hình mới do F2 tạo (block thư viện, khung, khung tên, khung nhìn) | Màu, kiểu nét, độ dày nét theo layer, không ăn theo màu / nét đang chọn (người rà sửa) |
| Liệt kê block thư viện khi hệ tọa độ đang xoay | Sửa ở bộ điều phối chung: mọi lệnh vẽ được gọi chỉ để đọc thì không đòi quyền ghi (người rà chọn) |
| Tên kiểu kích thước theo đơn vị | mm / cm / m giữ nguyên tên cũ; dm, inch, feet có hậu tố riêng (`-DM`, `-IN`, `-FT`) |

### Chưa kiểm được (cần AutoCAD thật)

- Lưu / gọi / xóa trạng thái layer.
- Đọc lại tên font khi so style: nếu AutoCAD trả tên khác "Arial" thì lần chạy lại sẽ báo "giữ nguyên" thay vì "không đổi".
- Khổ giấy khi in PDF và việc xoay giấy.
- Khung nhìn xoay theo hệ tọa độ.
- Bỏ khung nhìn mặc định AutoCAD tự tạo khi khởi tạo layout.
- Căn chữ trong khung tên.
- Block thư viện khi hệ tọa độ đang xoay.

Bước tự kiểm `CLAUDECHATSELFTEST` của F2 kiểm phần lớn các điểm này. Danh sách thử tay: `docs/checklist-dot-f2.md` trên
nhánh `full/nang-cap-f2-test` (mục A–F; mục F do người rà thêm, khoảng 10 phút).

Lỗi người rà đã sửa (7): liệt kê block thư viện báo lỗi khi hệ tọa độ xoay; kiểu chữ Arial bị coi là khác font nên hỏi
ghi đè thừa (2 lỗi); trang in tự chọn sai tỉ lệ khi hệ tọa độ xoay; block / khung ăn theo màu đang chọn; bản vẽ dm / inch /
feet bị gắn tên kiểu kích thước của cm; "liệt kê trạng thái layer" bị coi là lệnh ghi.

## Đợt F3 (3D đẹp và đúng hơn)

*Theo báo cáo của thread viết (25/11 17h12) và thread rà (17h35; người rà không đổi số nào). Chi tiết:
`docs/ban-giao/nang-cap-f3.md` và `docs/ban-giao/nang-cap-f3-test.md` trên nhánh `full/nang-cap-f3-test`.*

### Con số chờ duyệt

Số mới của đợt F3:

| Nhóm | Số đề xuất | Nguồn |
| --- | --- | --- |
| Giới hạn mái dốc nhà góc vuông | Tối đa 40 góc tường, 12 mảnh mái; quá thì báo và gợi ý mái bằng | Giới hạn kỹ thuật do thread chọn |
| Vật liệu mẫu | 8 loại: bê tông, gạch, vữa trắng, kính, gỗ, ngói, thép, đá; mỗi loại một màu (RGB: bê tông 165,165,160; gạch 170,74,50; vữa trắng 236,232,222; kính 150,200,220; gỗ 150,100,60; ngói 180,80,45; thép 120,130,140; đá 140,135,125 — Test tổng thể đợt F ghi thêm số); kính trong 35 %; tên vật liệu tiếng Việt | Theo thông lệ |
| Ảnh xuất ra | File PNG, cạnh dài 1568 px (khách / Claude chọn được 384–1568); mặc định nhìn chéo từ góc Tây Nam; kiểu hiển thị Realistic; tên file `<bản vẽ>-<góc nhìn>.png`, lưu cạnh bản vẽ; file trùng tên: Claude hỏi rồi mới ghi đè | Do thread chọn |
| Màu layer mái | Layer mái `A-ROOF-3D` tự tạo có màu cam (mã màu 30 của AutoCAD) | Do thread chọn; người rà bổ sung vào bảng |

Người rà xác nhận: các số F3 đều theo thông lệ, không viện dẫn tiêu chuẩn nào.

Số có từ đợt E, chưa được duyệt, **không đổi** trong F3:

| Nhóm | Số (mm) |
| --- | --- |
| Tay vịn cầu thang | Cao 900; tay vịn và trụ 50 × 50; trụ cách nhau ≤ 1500 |
| Cầu thang | Bậc trên cùng chạy lên sàn 100; gợi ý tách vế khi quá 18 bậc |
| Khoảng cho phép | Vd độ dốc mái 5–60°, mái đua ≤ 3000; các khoảng khác xem bàn giao F3 |

Lưu ý: tay vịn cầu thang cao 900, trong khi lan can (đợt A, đã duyệt) cao 1100.

### Quyết định tự chọn

| Tình huống | Đã chọn |
| --- | --- |
| Mái 2 dốc nhà chữ L / T / U | Cánh rộng nhất làm mái chính; các cánh khác có nóc chạy vào mái chính |
| Đường bao có tường xiên lõm vào | Báo lỗi kèm gợi ý dùng mái bằng |
| Cách làm vật liệu và ảnh | Dùng vật liệu thường của bản vẽ (màu + độ trong) và bộ chụp ảnh sẵn có; không dùng lệnh RENDER hay thư viện vật liệu của Autodesk (ít rủi ro nhất trên AutoCAD 2022–2027) |
| Gói và chế độ | Hai lệnh mới thuộc nhóm 3D: gói 3D / Full, chế độ "3D" và "Cả hai" |
| Chế độ "Chỉ xem" | Gọi lại góc nhìn bị chặn như lệnh ghi; liệt kê và xuất ảnh vẫn chạy |
| Đối tượng trên layer khóa khi gán vật liệu | Bỏ qua và báo lại, không tự mở khóa |
| Gán vật liệu cho đối tượng có sẵn | Hỏi bằng thẻ khi sửa quá 20 đối tượng (luật chung đang có) |
| Lưu góc nhìn trùng tên | Phải nói rõ là ghi đè |
| Mái 2 dốc nhà chữ U (người rà) | Phần mái hẹp hơn dừng ở nóc của mái rộng hơn mà nó chạy vào; hai mái rộng bằng nhau thì mái chính thắng |
| Vật liệu "Global" (người rà) | Không cho đổi màu hay độ trong (đây là vật liệu mặc định của mọi layer, đổi là cả bản vẽ đổi màu); vẫn gán cho khối / layer được |

### Điểm để cậu xem lại

Người rà nêu, giữ nguyên như F3 đã chọn:

- **Gán vật liệu cho cả layer không hiện thẻ xác nhận**, dù layer có rất nhiều khối. Luật "hỏi bằng thẻ khi sửa quá 20 đối
  tượng" chỉ tính khối được chọn từng cái. Claude vẫn hỏi trước trong chat và hoàn tác được.
- **Xóa hoặc ghi đè góc nhìn đã lưu không hiện thẻ xác nhận.** Claude hỏi trước trong chat, hoàn tác được.

Muốn chặt hơn thì thêm thẻ xác nhận cho hai trường hợp này (việc nhỏ).

### Lỗi người rà đã sửa (2)

- Mái 2 dốc nhà chữ U: tam giác đầu hồi của phần nối nhô ra trên sườn mái cánh bên kia (đỉnh 4004 mm thay vì 3139 mm ở ví
  dụ thử), quay hướng nào cũng bị.
- Lệnh gán vật liệu đổi được màu của vật liệu "Global", làm đổi màu cả bản vẽ.

### Chưa kiểm được (cần AutoCAD thật)

Toàn bộ phần F3 mới qua test tự động. Bước tự kiểm `CLAUDECHATSELFTEST` mới (mái chữ L, vật liệu, góc nhìn, ảnh) chưa chạy.
Danh sách thử tay: `docs/checklist-dot-f3.md` trên nhánh `full/nang-cap-f3-test` (khoảng 30 phút; người rà thêm mục B8 mái
chữ U và D6 vật liệu Global).

## Đợt F4 (mở chat nhanh, xuất Excel)

*Theo báo cáo của thread viết (25/11 18h20) và thread rà (19h17; người rà không đổi, không thêm số nào). Chi tiết:
`docs/ban-giao/nang-cap-f4.md` và `docs/ban-giao/nang-cap-f4-test.md` trên nhánh `full/nang-cap-f4-test`.*

### Con số chờ duyệt

Xuất Excel (khách thấy trong file):

| Nhóm | Số đề xuất | Vì sao |
| --- | --- | --- |
| Tên file mặc định | `<tên bản vẽ>-thong-ke-block.xlsx`, `-dien-tich.xlsx`, `-khoi-luong.xlsx`, `-va-cham.xlsx`, lưu cạnh bản vẽ | Không dấu để mở được ở mọi máy / ổ mạng; giống kiểu tên file PDF, STL |
| Tên tab trong file | "Thống kê block", "Diện tích", "Khối lượng 3D", "Va chạm khối 3D" | Theo tiêu đề bảng trong bản vẽ |
| Độ rộng cột | Bằng chữ dài nhất + 2 ký tự, trong khoảng 6–60 ký tự | Đọc được ngay, không phải kéo cột |
| Kiểu chữ | Calibri 11, hàng tiêu đề in đậm | Mặc định của Excel |
| Số lẻ | Giữ đúng giá trị của bảng, Excel hiện gọn (bảng ghi 12.50 thì Excel hiện 12.5) | Để cộng được; khách tự chỉnh số lẻ trong Excel |
| Giới hạn | Tối đa 100 000 dòng | Chặn trường hợp lỗi chạy mãi; bảng thật ít hơn nhiều |

Mở chat nhanh (khách không thấy trực tiếp, nhưng ảnh hưởng tốc độ):

| Nhóm | Số đề xuất | Vì sao |
| --- | --- | --- |
| Khởi động sẵn | Đợi 3 giây sau khi trợ lý sẵn sàng | Để AutoCAD mở xong trước, không bị chậm thêm |
| Tự dựng lại | Sau 5 giây, tối đa 3 lần liền khi chưa có lượt chat nào | Máy cài hỏng thì không lặp mãi |
| Lệnh nạp sẵn | 25 lệnh hay dùng (+ 5 lệnh thẻ xác nhận); lệnh khác Claude tự tìm khi cần | Giảm token mở chat khoảng 55–67 % (ước tính) |

Token mở chat theo ước tính của thread viết (số thật phải đo trên máy thật):

| Gói / chế độ | Trước F4 | Sau F4 |
| --- | --- | --- |
| Gói 2D | 40,5k | 13,4k |
| Gói 3D | 41,8k | 18,8k |
| Full · Cả hai | 60,5k | 20,6k |
| Full · chế độ 2D | 40,6k | 13,5k |
| Full · chế độ 3D | 41,8k | 18,8k |

### Quyết định tự chọn

| Tình huống | Đã chọn |
| --- | --- |
| Cách giảm mô tả lệnh | Dùng tính năng tìm lệnh có sẵn của Claude Code: lệnh ít dùng chỉ hiện tên, Claude nạp mô tả khi cần. Đổi lại: lần đầu dùng một lệnh ít gặp có thêm một bước tìm (một lần mỗi cuộc chat) |
| Bộ lệnh nạp sẵn | Một bộ 25 lệnh chung cho mọi gói (lệnh ngoài gói tự bỏ) |
| Khởi động sẵn khi nào | Khi mở AutoCAD, đổi chế độ, đổi gói, bấm "Chat mới", chuyển bản vẽ. Chỉ trên máy đã kích hoạt. Không gửi gì cho Claude nên không tốn token trước tin đầu |
| Xuất Excel | Là tùy chọn thêm của 4 bảng có sẵn (thống kê block, diện tích, khối lượng 3D, va chạm), không thêm lệnh mới |
| File Excel đã có | Không ghi đè ngay: Claude hỏi khách trong chat rồi mới ghi đè (như xuất STL), không hiện thẻ xác nhận |
| Số viết dạng chữ trong bảng | Thành số khi là số thường ("12", "12.50"); giữ là chữ khi có số 0 đầu ("007"), dấu phẩy hay đơn vị |
| Bản vẽ chưa lưu | File Excel vào thư mục Documents (như STL) |
| Máy gói 3D | Không vẽ bảng trong bản vẽ được (như cũ) nhưng xuất Excel bảng 3D được |

Người rà chọn thêm:

| Tình huống | Đã chọn |
| --- | --- |
| Công tắc tắt nạp gọn (luồng chính dặn thêm, làm đường lui nếu thử thật thấy Claude tìm sót lệnh) | Đặt biến môi trường `CLAUDECAD_DEFER_TOOLS=0`, hoặc tạo file trống `%LocalAppData%\ClaudeCAD\tool-search-off`, rồi mở lại AutoCAD: Claude nhận đủ mô tả mọi lệnh như trước F4. Mặc định vẫn nạp gọn. Chưa làm thành ô trong phần cài đặt ⚙ |
| Macro có bảng kèm xuất Excel | Chạy lại macro chỉ vẽ lại bảng, không ghi file Excel (tránh ghi đè file khách đã sửa) |
| Máy bị khóa giữa chừng | Có thể còn một tiến trình Claude chờ sẵn tới khi đóng AutoCAD; tiến trình này không gửi gì, không tốn token |

### Lỗi người rà đã sửa (3)

- Chưa có cách tắt nạp gọn mà không build lại: đã thêm công tắc ở trên.
- Chạy lại macro có bảng + xuất Excel thì báo "file đã có", cả lệnh thất bại và mất luôn bảng trên bản vẽ.
- Vài ký tự hiếm trong tên block / layer làm Excel báo file hỏng: đã lọc bỏ (tiếng Việt và emoji giữ nguyên).

Người rà cũng đã đối chiếu: cách hoãn lệnh khớp với bộ thư viện đang dùng; lệnh bị hoãn vẫn qua đủ lớp chặn (gói, chế độ,
"Chỉ xem", thẻ xác nhận); việc tìm lệnh không mở thêm công cụ nào khác; ô bắt đầu bằng `=` `+` `-` `@` vẫn là chữ.

### Chưa kiểm được (cần AutoCAD / Claude thật)

- Claude thật có tìm đúng lệnh chưa nạp không, và số token thật của tin đầu.
- Thời gian tới câu trả lời đầu; AutoCAD mở có chậm hơn không.
- Trợ lý khởi động sẵn có thật sự không gọi Claude trước tin đầu (mới theo thiết kế của bộ thư viện).
- File `.xlsx` mở bằng Excel thật (test mới đọc lại cấu trúc file).

- Công tắc tắt nạp gọn; báo lỗi khi file Excel đang mở (khóa file chỉ có trên Windows).

Danh sách thử tay: `docs/checklist-dot-f4.md` trên nhánh `full/nang-cap-f4-test` (người rà thêm mục C).

## Test tổng thể đợt F (25/11/2026)

*Nhánh `full/nang-cap-f-tong-the`. Chi tiết: `docs/ban-giao/test-tong-the-dot-f.md`.*

Soát file này so với code: các số đã ghi khớp code; bổ sung 3 chỗ ở trên (màu layer mới của F2, mã màu RGB vật liệu F3,
cỡ ảnh 384–1568 và luật ghi đè ảnh). Không đổi số nào trong code.

### Lỗi đã sửa

- Bước tự kiểm F4 (xuất Excel) phụ thuộc bước tự kiểm F2 (cần block cửa do bước F2 chèn): bước F2 hỏng thì bước F4 báo
  sai. Bước F4 giờ tự tạo block riêng.
- Checklist tổng (phần đợt E) còn ghi bản thang / bản mái dốc **150 mm**: sửa thành 120 (đợt F3 đã đổi).
- Xuất ảnh PNG chỉ nhận `replace` để ghi đè file, các lệnh xuất khác (STL, Excel) dùng `overwrite`: giờ nhận cả hai.

### Điểm để cậu xem lại (giữ nguyên, chưa đổi)

- **Ghi đè có hai kiểu**: style / trạng thái layer (F2) phải bấm Đồng ý trên thẻ; góc nhìn trùng tên, file STL / Excel /
  PNG trùng tên (F3, F4, đợt D) thì Claude hỏi trong chat rồi mới ghi đè. Muốn một kiểu cho tất cả thì nói (việc nhỏ).
- **Kích thước bản vẽ mét hiện "4", không phải "4.00"** (KTF-07): kiểu kích thước bỏ số 0 thừa (4 m hiện "4", 4,5 m hiện
  "4.5"). Muốn luôn hiện đủ 2 số lẻ ("4.00") thì phải đổi cài đặt bỏ số 0 thừa của kiểu kích thước (việc nhỏ, đổi trong
  code tạo kiểu kích thước).
- **Block thư viện trùng tên block có sẵn nhưng khác cỡ** (sau kiểm tra độc lập): không dùng lại block cũ (sai cỡ) mà tạo
  block thư viện tên thêm đuôi `-TV` (vd `CUA-DI-900-TV`; dựng 3D từ mặt bằng vẫn nhận là cửa). Cùng cỡ (lệch ≤ 2 %) thì
  dùng block có sẵn như trước.
- **In PDF ghi đè file PDF trùng tên không hỏi** (cách làm có từ trước chuỗi nâng cấp), khác STL / Excel / PNG. Muốn hỏi
  trước thì nói.

### Token mở chat (ước tính, cùng cách đo các đợt trước)

| Gói / chế độ | Trước đợt F | Sau đợt F, nạp gọn (mặc định) | Sau đợt F, tắt nạp gọn |
| --- | --- | --- | --- |
| Gói 2D | 38,8k | 13,4k | 40,8k |
| Gói 3D | 39,8k | 18,8k | 42,0k |
| Full · Cả hai | 57,7k | 20,6k | 61,0k |
| Full · chế độ 2D | (chưa có chế độ) | 13,5k | 40,8k |
| Full · chế độ 3D | (chưa có chế độ) | 18,8k | 42,1k |

Thử tay cả đợt F: khối "Đợt F" cuối `docs/checklist-tong-the.md` (~2 giờ, xếp dễ trước khó sau).
