# Thử tay đợt H6: hạ tầng (điểm khảo sát, đồng mức, trắc dọc, trắc ngang)

*Đã gộp vào khối "Đợt H phần 2" của [`docs/checklist-tong-the.md`](checklist-tong-the.md) (test tổng thể đợt H phần 2), thử tay theo file đó.*

*Nhánh `full/nang-cap-h6`. Chi tiết: `docs/ban-giao/nang-cap-h6.md`. Máy gói Full hoặc 2D, bản vẽ mới (đơn vị mm). Chuẩn bị
một file điểm đo thật (CSV hoặc Excel, khoảng 100 – 2 000 điểm, có cột Tên / X / Y / Cao độ theo VN-2000). Khoảng 30 phút.
"Đạt" là thấy đúng như cột phải; khác thì chụp màn hình và ghi số thứ tự.*

**Số mặc định** (cậu duyệt 26/11): trắc dọc 1:1000 / 1:100, trắc ngang 1:200 rộng 15 m mỗi bên, cọc chi tiết 20 m, vạch lý trình
mặt bằng 50 m, khoảng cao đều 0,5 m (1:500) / 1 m (1:1000, 1:2000), cứ 5 đường có 1 đường cái, MSS mét chẵn thấp hơn ≥ 2 m.
Khoảng cao đều và cọc 20 m: kỹ sư đối chiếu TCVN hiện hành trước khi dùng.

## A. Nhập điểm (~8 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| A1 | Gõ `CLAUDECHATSELFTEST` | Dòng "Nâng cấp đợt H6: hạ tầng…" **đạt** |
| A2 | Kéo file điểm vào chat: "nhập điểm khảo sát từ file này" | Claude hỏi một câu: vẽ theo tọa độ thật hay từ gốc cục bộ. Chưa vẽ gì |
| A3 | Trả lời "gốc cục bộ" | Mỗi điểm một dấu chấm (layer `C-SURVEY-PT`) kèm cao độ `+12.350` (`C-LEVEL`) và tên điểm (`C-COORD`); dòng "Gốc tọa độ: X = …, Y = … (VN-2000)" dưới đám điểm. Claude nói rõ đã đổi trục (X là hướng Bắc) |
| A4 | Đo khoảng cách hai điểm (lệnh `DIST`) so với tính tay từ tọa độ | Khớp (đơn vị mm: 1 m = 1 000) |
| A5 | File có cột X / Y đảo (X là hướng Đông) | Claude cảnh báo cột có vẻ bị đảo, hỏi lại |
| A6 | File Excel (.xlsx) cùng nội dung | Nhập được như CSV |
| A7 | File có vài dòng hỏng (chữ trong cột X) | Các dòng đó bị bỏ qua, Claude kể số dòng bỏ qua |

## B. Đồng mức (~6 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| B1 | "Vẽ đường đồng mức" | Đường con `C-TOPO-MINR`, đường cái nét đậm `C-TOPO-MAJR` có ghi số cao độ; Claude nói khoảng cao đều đã dùng |
| B2 | Chọn một đường đồng mức, xem Properties | Elevation = cao độ của đường (mm) |
| B3 | So vài đường với cao độ điểm đo bên cạnh | Đường nằm giữa hai điểm có cao độ hai bên đúng |
| B4 | "Vẽ lại đồng mức khoảng cao đều 0,5 m" | Đường mới thêm (Claude nhắc xóa đường cũ trước nếu không muốn trùng) |
| B5 | Khu đo hình chữ L (có vùng trống) | Không có đường đồng mức cắt ngang vùng trống |

## C. Tuyến, trắc dọc, trắc ngang (~12 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| C1 | Vẽ một polyline tim tuyến qua khu đo, "ghi lý trình cho tuyến này" | Vạch vuông góc tuyến kèm chữ `Km0+050.00`… (`C-STATION`), có cọc ở đỉnh tuyến (Đ1…) và cuối tuyến |
| C2 | "Vẽ trắc dọc tuyến này" | Khung trắc dọc đặt dưới bản vẽ: đường tự nhiên, đường MSS, bảng các dòng Cao độ tự nhiên / Khoảng cách lẻ / Khoảng cách cộng dồn / Tên cọc; tiêu đề có tỉ lệ ngang / đứng |
| C3 | So cao độ tự nhiên một cọc với cao độ điểm đo gần đó | Hợp lý (nội suy trên lưới tam giác) |
| C4 | "Thêm đường thiết kế từ +10.50 ở đầu tuyến đến +11.00 ở cuối" | Có thêm đường thiết kế (đỏ), dòng Cao độ thiết kế, Chênh cao TK − TN, độ dốc `i = …%` |
| C5 | "Vẽ trắc ngang tại các cọc" | Các trắc ngang xếp thành cột, mỗi cái có tim tuyến, đường tự nhiên, MSS, bảng cao độ / khoảng cách, tiêu đề "Cọc C3  Km0+060.00" |
| C6 | "Chỉ vẽ trắc ngang ở cọc H1 và H2" | Chỉ hai trắc ngang |
| C7 | Tuyến chạy ra ngoài khu đo | Cọc ngoài khu đo ghi "-" ở trắc dọc; Claude kể các cọc ngoài khu đo |
| C8 | Gõ `U` một lần | Mất cả phần vừa vẽ của lượt đó |

## D. Giới hạn, gói (~4 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| D1 | "Tính khối lượng đào đắp" | Claude nói lệnh này không tính khối lượng; bản vẽ là hỗ trợ, kỹ sư kiểm |
| D2 | *(máy khác)* Máy gói 3D | Không có lệnh này; Claude nói việc này thuộc phần 2D |
| D3 | Bật **Chỉ xem**, "nhập điểm" | Claude nói Chỉ xem đang bật, không vẽ gì |

## E. Thêm ở Test & sửa H6 (~10 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| E1 | *(H6T-3)* Bản vẽ mới đặt đơn vị **mét** (`INSUNITS` = 6), nhập cùng file điểm, vẽ đồng mức, trắc dọc | Điểm, đồng mức, trắc đúng kích thước (đo `DIST` hai điểm cách 10 m ra 10, không ra 10 000); chữ cao độ đọc được như ở bản vẽ mm |
| E2 | *(H6T-1)* File CSV lưu từ Excel tiếng Việt cũ (mã Windows-1258) có tiêu đề "Tên điểm; Cao độ" | Tên điểm có dấu đúng; Claude nhận cột cao độ theo tiêu đề |
| E3 | *(H6T-1)* Trong Excel: File › Save As › **Unicode Text (.txt)**, rồi nhập file .txt đó | Đọc đủ điểm như file .xlsx |
| E4 | *(H6T-2)* File không có dòng tiêu đề, 4 cột: X, Y, Z, mã (vd `1234012.5,567045.25,12.35,TN`) | X / Y / Z đúng cột (không lấy X làm tên điểm) |
| E5 | *(H6T-4)* Bản vẽ đã chạy "thiết lập style chuẩn", nhập điểm + đồng mức, rồi "soát bản vẽ" | Không báo các layer `C-…` của hạ tầng là layer ngoài chuẩn |
| E6 | *(H6T-5)* Ghi lý trình mặt bằng (50 m) rồi vẽ trắc dọc | Trên mặt bằng vạch 50 m chỉ ghi lý trình (vd `Km0+050.00`), không có tên `C…`; tên `C1, C2…` chỉ ở trắc dọc / trắc ngang (cọc 20 m); cọc H, Km, đỉnh vẫn có tên |
| E7 | *(Sửa KTP-10 / 11)* File Excel có số dạng `1.234.012,500` (dấu nghìn) và ô chữ `567 045,25` | Đọc đúng 1234012,5 / 567045,25 |
| E8 | *(Sửa KTP-12 / 39)* File `.txt` cách bằng dấu cách, số phẩy thập phân (`P1 1234012,5 567045,25 12,35`), dòng đầu chỉ một chữ (`SODIEM`) | Đủ điểm, X / Y / Z đúng (không lấy phẩy làm dấu tách) |
| E9 | *(Sửa KTP-08 / 09 / 48)* File không tiêu đề "số thứ tự, X, Y, Z" (`1;1234012.5;567045.25;12.35`); rồi file `.txt` có tiêu đề `Tên điểm  X  Y  Z` | Cột 1 là tên điểm; file `.txt` đọc đủ điểm theo tiêu đề |
| E10 | *(Sửa KTP-37)* Excel có tên bảng ở hàng 1, vài hàng trống, tiêu đề `STT; Tên; X; Y; Z` ở hàng 12 | Claude nhận cột theo tiêu đề, tên điểm lấy cột "Tên" (không lấy STT) |
| E11 | *(Sửa KTP-26)* Tuyến có đỉnh trùng điểm cuối (trong 1 cm) | Cọc cuối ghi `Cuối/Đ…`, không phải `C…/Đ…` |
| E12 | *(Sửa lỗi `Alignment.At`)* Bản vẽ **mm**, tuyến dài vài trăm mét, ghi lý trình rồi vẽ trắc dọc / trắc ngang | Cọc rải đủ dọc tuyến (không dồn ở mét đầu); cọc cuối đúng điểm cuối polyline |
| E13 | *(Sửa KTP-24)* Bản vẽ **mét**, vẽ trắc dọc | Claude báo trục "metres × 1" (bản vẽ mm: "× 1000"); khung trắc đặt đúng chỗ |
| E14 | *(Sửa KTP-50 / 52 / 53)* File `.txt` dòng đầu "DANH SACH DIEM" rồi các dòng `P1 X Y Z`; rồi file tiêu đề `So hieu diem X Y Z`; rồi file tiêu đề `Tên điểm X Y Z Mã` mà dòng đầu không có mã | Cả ba đọc đủ điểm, có cao độ, đúng cột; dòng tên bảng được kể là bỏ qua |
