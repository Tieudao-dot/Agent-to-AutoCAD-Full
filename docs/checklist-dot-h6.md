# Thử tay đợt H6: hạ tầng (điểm khảo sát, đồng mức, trắc dọc, trắc ngang)

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
