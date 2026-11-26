# Thử tay đợt H5: làm nhiều file một lúc

*Nhánh `full/nang-cap-h5`. Chi tiết: `docs/ban-giao/nang-cap-h5.md`. Máy gói Full hoặc 2D. Chuẩn bị một thư mục **bản sao** gồm
5 – 10 bản vẽ (có một thư mục con), một file chỉ đọc (chuột phải › Properties › Read-only), một file đang mở trên máy khác (hoặc ở
AutoCAD thứ hai). Khoảng 30 phút. "Đạt" là thấy đúng như cột phải; khác thì chụp màn hình và ghi số thứ tự.*

## A. Soát cả thư mục (~8 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| A1 | Gõ `CLAUDECHATSELFTEST` | Dòng "Nâng cấp đợt H5: làm nhiều file…" **đạt** |
| A2 | "Soát lỗi cả thư mục `D:\Thu\H5`" | Claude kể số file sẽ soát, các file bỏ qua và lý do, rồi hiện **thẻ Đồng ý / Hủy**. Hủy: không làm gì |
| A3 | Đồng ý | Khung chat hiện "file 1 / N …"; cuối cùng mỗi file một dòng: số lỗi nên sửa / cân nhắc / gợi ý, 2 – 3 nhóm lỗi chính. Ngày sửa của các file **không đổi** |
| A4 | "Soát cả thư mục con nữa, xuất Excel" | Có file trong thư mục con; file `tong-hop-check-<ngày giờ>.xlsx` trong thư mục, mỗi file một dòng, dòng Tổng |
| A5 | "Soát theo chuẩn ABC cả thư mục" (bộ chuẩn đã học ở H4) | Lỗi layer / kiểu theo bộ chuẩn ABC |
| A6 | Đặt một file hỏng (đổi đuôi một file .txt thành .dwg) vào thư mục, soát lại | File đó **Lỗi** kèm lý do; các file khác vẫn soát xong |

## B. Áp chuẩn cả thư mục (~10 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| B1 | "Áp chuẩn 1:100 cho cả thư mục" | Thẻ nói rõ số file, mỗi file được **sao lưu trước** vào `_sao-luu-claudecad`; file chỉ đọc và file đang mở ở máy khác nằm trong danh sách bỏ qua |
| B2 | Đồng ý | Mỗi file: kiểu chữ / kích thước VN-… tạo hay giữ; thư mục `_sao-luu-claudecad\<ngày giờ>\` có bản gốc của từng file (cả thư mục con) |
| B3 | Mở một file vừa áp | Có VN-ARIAL, VN-100; bản vẽ vẫn mở được, cùng đời DWG như trước (xem Properties hoặc lệnh `DWGPROPS`) |
| B4 | Mở bản sao lưu của file đó | Là bản trước khi áp |
| B5 | Bản vẽ đang mở trong AutoCAD này nằm trong thư mục | Bị bỏ qua, Claude nhờ lưu và đóng rồi làm lại |
| B6 | Bật **Chỉ xem**, "áp chuẩn cả thư mục" | Claude nói Chỉ xem đang bật, không ghi file nào; soát (A2) vẫn chạy |
| B7 | "Áp chuẩn ABC cho cả thư mục" | Layer / kiểu ABC có trong các file |
| B8 | Giữa lúc áp (thư mục 10 file), bấm **Dừng** | Dừng sau file đang làm; Claude nói đã làm bao nhiêu file; không còn file `.claudecad-tam-…` nào trong thư mục; các file chưa tới không đổi |
| B9 | *(Test & sửa H5)* Lập kế hoạch áp chuẩn, **trước khi** bấm Đồng ý thì chép thêm một file `.dwg` vào thư mục | Claude báo thẻ không còn khớp danh sách, không ghi file nào; lập kế hoạch lại thì có file mới |
| B10 | *(Test & sửa H5)* Đang áp (thư mục 10 file), chép thêm một file `.dwg` vào thư mục | Lượt dừng ở file kế tiếp; Claude **vẫn kể** các file đã áp xong (kèm nơi sao lưu) và nói các file còn lại chưa đụng tới |

## C. In PDF hàng loạt (~7 phút, rủi ro cao)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| C1 | "In PDF cả thư mục" (**ít nhất 3 file**, các file có layout) | Mỗi file một PDF cùng tên cạnh file, đủ các layout; AutoCAD mở từng file (chỉ đọc) rồi đóng, trở lại bản vẽ của cậu. Khung chat **không** báo "Cậu đã chuyển sang bản vẽ khác giữa lượt" và không dừng sau file đầu (Test & sửa H5 sửa lỗi này); lịch sử chat vẫn là của bản vẽ của cậu |
| C2 | Làm lại C1 | Thẻ nói rõ số PDF có sẵn sẽ bị thay; Đồng ý thì thay |
| C3 | File chỉ có Model (không layout) | File đó **Lỗi** "the drawing has no paper layouts"; các file khác vẫn in |
| C4 | Một file đang mở ở đây có thay đổi chưa lưu | Bỏ qua, nhờ lưu trước |

## D. Đếm block, giới hạn, gói (~5 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| D1 | "Đếm block cả thư mục, xuất Excel" | Bảng tổng hợp + file `tong-hop-block-<ngày giờ>.xlsx`: mỗi dòng file / block / số lượng |
| D2 | Thư mục có hơn 200 bản vẽ | Claude nói chỉ làm 200 file đầu, nhờ chia thư mục |
| D3 | *(máy khác)* Máy gói 3D | Không có lệnh này; Claude nói việc này thuộc phần 2D |
| D4 | Thư mục mạng khác máy chủ với bản vẽ đang mở | Bị từ chối như luật đường dẫn G5 |
