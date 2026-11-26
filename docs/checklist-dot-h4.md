# Thử tay đợt H4: học chuẩn công ty từ bản vẽ mẫu

*Đã gộp vào khối "Đợt H phần 1" của [`docs/checklist-tong-the.md`](checklist-tong-the.md) (test tổng thể đợt H phần 1), thử tay theo file đó.*

*Nhánh `full/nang-cap-h4`. Chi tiết: `docs/ban-giao/nang-cap-h4.md`. Máy gói Full hoặc 2D (bộ cài thử nội bộ). Cần một **file mẫu
của công ty** (.dwg hoặc .dwt) có: vài layer riêng (tên, màu, kiểu nét, độ dày nét khác nhau), một kiểu chữ, một kiểu kích thước,
một layout A3 có khung tên là block có thuộc tính, và một khung nhìn. Khoảng 30 phút. "Đạt" là thấy đúng như cột phải; khác thì
chụp màn hình và ghi số thứ tự.*

## A. Học chuẩn (~8 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| A1 | Gõ `CLAUDECHATSELFTEST` | Dòng "Nâng cấp đợt H4: học chuẩn công ty…" **đạt** |
| A2 | Kéo file mẫu vào khung chat (hoặc gõ đường dẫn), gõ "học chuẩn công ty từ file này, đặt tên ABC" | Claude báo số layer, các kiểu chữ / kích thước / ghi chú mũi tên, tên khung tên kèm các ô (thuộc tính), khổ giấy. Số layer khớp với file mẫu |
| A3 | Mở thư mục file mẫu: ngày sửa của file mẫu | **Không đổi** (file mẫu chỉ được đọc). File mẫu đang mở ở AutoCAD khác vẫn học được |
| A4 | Mở `%LOCALAPPDATA%\ClaudeCAD\chuan-cong-ty\abc` | Có `standard.json` và `mau.dwg` |
| A5 | "Có những bộ chuẩn công ty nào?" rồi "xem chuẩn ABC" | Liệt kê ABC; xem: danh sách layer kèm màu, kiểu nét, độ dày |
| A6 | Học lại cùng tên ABC | Hiện **thẻ Đồng ý / Hủy** thay bộ cũ. Hủy: giữ bộ cũ |
| A7 | Học từ file `.dwl` / file không có | Claude báo rõ (file khóa / không có), không tạo bộ chuẩn |

## B. Áp chuẩn (~6 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| B1 | Bản vẽ mới trống: "áp chuẩn ABC" | Các layer, kiểu chữ, kiểu kích thước, kiểu ghi chú của ABC có trong bản vẽ (LAYER, STYLE, DIMSTYLE); kiểu đang dùng giống file mẫu |
| B2 | Đổi màu một layer của ABC trong bản vẽ, "áp chuẩn ABC" lần nữa | Claude nói layer đó khác chuẩn, hiện **thẻ**; Đồng ý: layer trở lại màu chuẩn; Hủy: giữ màu cậu đổi |
| B3 | Gõ U một lần | Bỏ hết phần vừa áp |

## C. Soát theo chuẩn (~5 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| C1 | Vẽ vài nét trên layer lạ (vd "TAM"), một chữ kiểu Standard, "soát bản vẽ theo chuẩn ABC" | Báo "Layer không thuộc bảng layer chuẩn" (TAM), "kiểu ngoài bộ chuẩn" cho chữ Standard; không báo layer của ABC |
| C2 | Chữ cỡ khác hẳn các cỡ chữ trong file mẫu | Báo "Chữ lệch cỡ chuẩn", kèm các cỡ của ABC |
| C3 | Như C1 nhưng "soát bản vẽ" (không nói chuẩn) | Như trước H4 (theo chuẩn Việt Nam nếu bản vẽ dùng) |
| C4 | Hỏi "sửa kiểu chữ" sau C1 | Claude **không** đề nghị sửa tự động nhóm kiểu chữ theo ABC; gợi ý áp chuẩn ABC rồi đổi kiểu cho đối tượng |

## D. Tờ in có khung tên công ty (~7 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| D1 | "Tạo tờ in theo chuẩn ABC, tên dự án Nhà phố Q7" | Layout mới, khổ giấy như file mẫu, **khung tên của công ty** (đúng chỗ như file mẫu), khung nhìn đúng chỗ khung nhìn của mẫu; không có khung tên VN |
| D2 | Claude điền ô | Claude hỏi / dùng đúng tên ô (thuộc tính) của khung tên ABC; ô điền đúng chữ |
| D3 | Xóa thư mục `mau.dwg` trong `chuan-cong-ty\abc` (giữ `standard.json`), làm lại D1 | Tờ vẫn ra, dùng khung tên VN, Claude nói rõ khung tên công ty không còn (nhờ học lại) |
| D4 | Đổi tên / xóa file mẫu gốc, "áp chuẩn ABC" | Vẫn áp được (dùng bản chép trong `chuan-cong-ty`) |
| D5 | In D1 ra PDF (`plot_pdf`) | Khung tên công ty in đúng |

## E. Gói, xóa (~4 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| E1 | *(máy khác)* Máy gói 3D: "học chuẩn công ty từ file này" | Claude nói việc này thuộc phần 2D |
| E2 | Full chế độ 3D | Claude bảo đổi chế độ 2D / Cả hai |
| E3 | "Xóa chuẩn ABC" | Hiện **thẻ**; Đồng ý: hết trong danh sách, thư mục `abc` mất; file mẫu gốc không bị đụng |
| E4 | Cài đè bộ cài (bản mới) rồi "có những bộ chuẩn nào" | Bộ chuẩn học trước vẫn còn |

## F. Thêm ở Test & sửa H4 (~8 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| F1 | Mở file mẫu `ABC.dwt` trong một AutoCAD khác (hoặc trên máy đồng nghiệp qua thư mục mạng), rồi "học chuẩn công ty từ file này, tên ABC2" | Học được **kèm bản chép** (`chuan-cong-ty\abc2\mau.dwg` có), Claude không báo "could not be copied" |
| F2 | Trong bản vẽ đang mở, đổi màu layer 0 và layer hiện hành (layer đang vẽ) khác chuẩn ABC, rồi "áp chuẩn ABC", Đồng ý thẻ | Hai layer đổi đúng màu / nét / độ dày như chuẩn; đối tượng trên đó vẫn còn và vẫn ở layer cũ; không báo lỗi |
| F3 | Mở `chuan-cong-ty\abc\standard.json` bằng Notepad, xóa nửa sau rồi lưu; hỏi "có những bộ chuẩn nào", rồi "soát theo chuẩn ABC" | Danh sách ghi `abc` **hỏng**; lệnh soát báo bộ chuẩn hỏng, nhờ học lại hoặc xóa (không báo "plugin bug"); "xóa chuẩn ABC" vẫn làm được (qua thẻ) |
| F4 | Đặt tên bộ chuẩn là "CON" | Học được; thư mục là `con-chuan` |
| F5 | Đặt layer hiện hành là A-WALL rồi "tạo tờ in theo chuẩn ABC" | Khung tên công ty nằm trên layer **KHUNG**, không trên A-WALL |
| F6 | "Tạo tờ in A1 theo chuẩn ABC" khi file mẫu là A3 | Claude nói khung tên công ty đặt theo tờ A3 của mẫu nên có thể lệch khỏi khung, gợi ý dùng A3 |
| F7 | Sau C1 (soát theo chuẩn ABC), nói "sửa các kiểu chữ sai" | Claude không sửa bằng kiểu VN; nếu vẫn gọi sửa thì lệnh từ chối, "Nothing was changed" |
| F8 | File mẫu đơn vị **mét**, bản vẽ đang mở mm: "áp chuẩn" | Claude báo kiểu kích thước / ghi chú của mẫu theo mét (cỡ chữ cần xem lại), cỡ chữ chuẩn đổi đúng ra mm |
