# Thử tay đợt G3: bảng dự toán

*Đã gộp vào khối "Đợt G" của [`docs/checklist-tong-the.md`](checklist-tong-the.md) (test tổng thể đợt G), thử tay theo file đó.*

*Nhánh `full/nang-cap-g3`. Chi tiết: `docs/ban-giao/nang-cap-g3.md`. Máy gói Full (bộ cài thử nội bộ), bản vẽ đã lưu, đơn vị
mm, có nhà đã dựng 3D (tường A-WALL-3D, sàn A-SLAB-3D…), mặt bằng có block cửa và chữ tên phòng. Khoảng 25 phút. Cần Excel /
LibreOffice / Google Sheets để mở file. "Đạt" là thấy đúng như cột phải; khác thì chụp màn hình và ghi số thứ tự.*

## A. Tự kiểm (~3 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| A1 | Gõ `CLAUDECHATSELFTEST` | Dòng "Nâng cấp đợt G3: lập dự toán (dòng thiếu đơn giá để trống, Excel có công thức)" **đạt** |

## B. Dự toán theo khối lượng 3D (~10 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| B1 | "Lập dự toán theo khối lượng: tường 1.200.000 đồng/m³, sàn 2.500.000 đồng/m³" (không nói giá cột) | Thẻ lệnh "Lập dự toán"; Claude báo thành tiền tường, sàn, tổng; nói rõ **dòng cột chưa có đơn giá, không tính vào tổng**, hỏi giá cột. Claude **không** tự nêu giá cột |
| B2 | "Lập dự toán, đơn giá cậu tự lấy giá thị trường" | Claude từ chối đặt giá, xin đơn giá |
| B3 | Như B1, thêm "xuất Excel" | File `<tên bản vẽ>-du-toan.xlsx` cạnh bản vẽ; cột Thành tiền và tổng có dấu tách hàng nghìn; ô đơn giá của cột để trống |
| B4 | Mở file B3, gõ đơn giá cột vào ô trống | Thành tiền dòng cột và **Cộng** tự tính lại |
| B5 | Làm lại B3 | Hiện **thẻ Đồng ý / Hủy** ghi đè file. Hủy: file giữ nguyên. Đồng ý: file mới |
| B6 | "Thêm thuế VAT 10%" | Dòng Thuế VAT = 10 % của Cộng, dòng Tổng cộng; Excel: dòng VAT là công thức |
| B7 | "Vẽ bảng dự toán vào bản vẽ ở góc phải" | Bảng BẢNG DỰ TOÁN trong bản vẽ, số kiểu 1.234.567; dòng thiếu giá để trống |

## C. Dự toán theo diện tích / block, ghép tên (~7 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| C1 | "Dự toán lát sàn theo diện tích phòng: phòng khách 450.000, phòng ngủ 400.000 đồng/m²" | Giá ghép đúng phòng dù gõ không dấu / khác hoa thường ("phong khach"); phòng khác báo thiếu giá |
| C2 | "Dự toán cửa: CUA-DI-* 3.500.000 đồng/cái" | Mọi block CUA-DI-… có giá theo mẫu `*` |
| C3 | Cho hai mẫu cùng khớp một dòng với hai giá khác nhau (vd "CUA*" 1.000.000 và "CUA-DI*" 2.000.000) | Dòng đó không có giá, ghi "several price patterns fit"; Claude hỏi lại, không chọn hộ |
| C4 | *(Test & sửa G3)* "Dự toán tường 1.200.000 đồng/m3" (gõ m3, không phải m³), xuất Excel; bản vẽ có phòng tên số (vd "101") | Không có ghi chú "check" đơn vị; trong Excel tên phòng "101" vẫn là chữ (canh trái), khối lượng / tiền là số |

## D. Gói, chế độ, Chỉ xem, macro (~5 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| D1 | Bật **Chỉ xem**, làm B3 | Tính và xuất Excel được; "vẽ bảng vào bản vẽ" bị chặn (Chỉ xem) |
| D2 | Chế độ **3D**: B1 + "vẽ bảng vào bản vẽ" | Có số và Excel; Claude nói không vẽ bảng ở chế độ này. "Dự toán theo diện tích phòng": bảo đổi chế độ 2D / Cả hai |
| D3 | Chế độ **2D**: "dự toán theo khối lượng 3D" | Claude bảo đổi chế độ 3D / Cả hai; dự toán theo diện tích / block làm được |
| D4 | Ghi macro một lượt B3 + B7, chạy lại | Bảng vẽ lại; **không** ghi lại file Excel |
| D5 | *(máy khác)* Máy gói 2D / 3D: như D2 / D3 | Câu báo nói về gói (không nói đổi chế độ) |
