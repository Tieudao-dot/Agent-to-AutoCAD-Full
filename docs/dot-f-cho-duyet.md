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
| Số lẻ của kích thước | Bản vẽ mm: không có số lẻ; cm: 1 số lẻ; mét: 2 số lẻ (vd 4.00) | Theo thông lệ |
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

*Theo báo cáo của thread viết (25/11 17h12); thread rà đang soát lại. Chi tiết: `docs/ban-giao/nang-cap-f3.md` trên nhánh
`full/nang-cap-f3`.*

### Con số chờ duyệt

Số mới của đợt F3:

| Nhóm | Số đề xuất | Nguồn |
| --- | --- | --- |
| Giới hạn mái dốc nhà góc vuông | Tối đa 40 góc tường, 12 mảnh mái; quá thì báo và gợi ý mái bằng | Giới hạn kỹ thuật do thread chọn |
| Vật liệu mẫu | 8 loại: bê tông, gạch, vữa trắng, kính, gỗ, ngói, thép, đá; mỗi loại một màu; kính trong 35 %; tên vật liệu tiếng Việt | Theo thông lệ |
| Ảnh xuất ra | File PNG, cạnh dài 1568 px; mặc định nhìn chéo từ góc Tây Nam; kiểu hiển thị Realistic; tên file `<bản vẽ>-<góc nhìn>.png`, lưu cạnh bản vẽ | Do thread chọn |

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

### Chưa kiểm được (cần AutoCAD thật)

Toàn bộ phần F3 mới qua test tự động. Bước tự kiểm `CLAUDECHATSELFTEST` mới (mái chữ L, vật liệu, góc nhìn, ảnh) chưa chạy.
Danh sách thử tay: `docs/checklist-dot-f3.md` (khoảng 30 phút).

## Đợt F4 (mở chat nhanh, xuất Excel)

*Chưa chạy.*
