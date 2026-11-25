# Thử tay đợt F5: ô "Nạp gọn lệnh", thẻ xác nhận ghi đè, ghi chú có mũi tên

*Nhánh `full/nang-cap-f5`. Chi tiết: `docs/ban-giao/nang-cap-f5.md`. Máy gói Full (bộ cài thử nội bộ), bản vẽ đã lưu (có
tên file), đơn vị mm. Khoảng 25 phút. "Đạt" là thấy đúng như cột phải; khác thì chụp màn hình và ghi số thứ tự.*

## A. Tự kiểm (~3 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| A1 | Gõ `CLAUDECHATSELFTEST` | Dòng "Nâng cấp đợt F5: kiểu ghi chú mũi tên của bộ chuẩn, thẻ xác nhận ghi đè góc nhìn" **đạt** |

## B. Ô "Nạp gọn lệnh" (~5 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| B1 | Mở ⚙ → phần "Cách Claude làm việc" | Có ô **Nạp gọn lệnh** đang bật, rê chuột thấy dòng giải thích bật / tắt |
| B2 | Đang chat dở, tắt ô, đóng ⚙, gửi "kéo giãn hình chữ nhật vừa vẽ" | Claude làm được; vẫn nhớ đoạn chat trước (vd hỏi "lúc nãy mình vẽ gì?") |
| B3 | Đóng hẳn AutoCAD, mở lại, mở ⚙ | Ô vẫn **tắt** (nhớ theo máy). Bật lại cho các bước sau |

## C. Thẻ xác nhận ghi đè (~12 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| C1 | "In layout ra PDF" hai lần liền | Lần 2 hiện **thẻ Đồng ý / Hủy** nói file PDF đã có. Hủy: file cũ giữ nguyên. Đồng ý: file mới thay file cũ |
| C2 | (máy 3D / Full) "Xuất STL" hai lần; "xuất ảnh phối cảnh" hai lần | Lần 2 mỗi việc hiện thẻ; Đồng ý mới ghi đè |
| C3 | "Thống kê block ra Excel" hai lần | Lần 2 hiện thẻ; Đồng ý mới ghi đè |
| C4 | "Lưu góc nhìn tên Phối cảnh 1" hai lần, rồi "xóa góc nhìn Phối cảnh 1" | Lưu lần 2 và xóa đều hiện thẻ; Hủy thì gõ `VIEW` vẫn thấy góc nhìn cũ |
| C5 | "Gán vật liệu bê tông cho cả layer A-WALL-3D" | Hiện thẻ trước khi gán. Gán cho vài khối chọn sẵn (≤ 20): không hiện thẻ |
| C6 | Xuất PDF / Excel ra **tên mới** (file chưa có) | Không hiện thẻ, ghi luôn |
| C7 | Lưu macro cho một lượt có "lưu góc nhìn tên A" (lúc A chưa có), chạy macro | Macro chạy xong, **không** có thẻ nào treo |

## D. Ghi chú có mũi tên (~5 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| D1 | "Thiết lập bộ style kiến trúc tỷ lệ 1:100" | Claude báo có kiểu ghi chú mũi tên `KT-ML-100`. Gõ `MLEADERSTYLE`: thấy `KT-ML-100` là kiểu hiện hành |
| D2 | "Ghi chú 'Ống D110' có mũi tên chỉ vào góc phòng, chữ đặt lệch lên bên phải" | Mũi tên đặc ở góc phòng, chữ kiểu `KT-ARIAL`, in ra ~2,5 mm ở 1:100 (cao 250 trong bản vẽ mm) |
| D3 | Xoay UCS 30° (`UCS` → `Z` → 30), ghi chú lại | Chữ và đường gãy ngang xoay theo UCS |
| D4 | Máy gói **3D** (hoặc chế độ 3D): "ghi chú có mũi tên" | Claude nói không có ở gói / chế độ này (chế độ: bảo đổi sang 2D hoặc Cả hai) |
