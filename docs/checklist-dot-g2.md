# Thử tay đợt G2: soát bản vẽ trước khi in / gửi

*Đã gộp vào khối "Đợt G" của [`docs/checklist-tong-the.md`](checklist-tong-the.md) (test tổng thể đợt G), thử tay theo file đó.*

*Nhánh `full/nang-cap-g2`. Chi tiết: `docs/ban-giao/nang-cap-g2.md`. Máy gói 2D hoặc Full, bản vẽ đơn vị mm. Khoảng 25
phút. "Đạt" là thấy đúng như cột phải; khác thì chụp màn hình và ghi số thứ tự.*

## A. Tự kiểm (~3 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| A1 | Gõ `CLAUDECHATSELFTEST` | Dòng "Nâng cấp đợt G2: soát bản vẽ (hình trùng, đường dài 0, chữ rỗng), sửa chỉ sau thẻ Đồng ý" **đạt** |

## B. Bản vẽ dùng bộ chuẩn (~12 phút)

Chuẩn bị: bản vẽ mới, nhờ Claude "thiết lập style chuẩn 1:100 theo skill kiến trúc" và "vẽ phòng 4×6 m tường 220". Rồi tự
tay thêm (lệnh AutoCAD): một đường trên layer `0`; chép một đoạn tường đè lên chính nó (COPY, điểm gốc = điểm đích); một
đường có màu đỏ chọn tay (không ByLayer); một chữ TEXT cao 120 kiểu `KT-ARIAL`; một chữ kiểu `Standard`; một kích thước rồi
gõ đè số "9999" (Properties → Text override); một đường rất xa (vd tới điểm 5000000,5000000).

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| B1 | "Soát bản vẽ" | Claude báo theo mức, "nên sửa" trước: layer 0, hình trùng, kích thước gõ đè, hình lạc xa; "cân nhắc": màu không theo layer, kiểu chữ ngoài bộ, chữ lệch cỡ. Có câu đã quét N trên M đối tượng. **Bản vẽ không đổi gì** |
| B2 | "Sửa hình trùng" | Hiện **thẻ Đồng ý / Hủy** nói xóa mấy hình. Hủy: không đổi. Đồng ý: còn một đoạn tường (bấm vào chỉ còn một) |
| B3 | "Chuyển hình trên layer 0 sang A-WALL" | Thẻ nói chuyển sang A-WALL; Đồng ý thì đường đó sang layer A-WALL |
| B4 | "Đưa màu về theo layer" rồi "hiện lại số đo kích thước" | Mỗi việc một thẻ; sau Đồng ý đường đỏ theo màu layer, kích thước hiện lại số đo thật |
| B5 | Gõ `U` một lần | Lần sửa vừa rồi trở lại như cũ |
| B6 | Khóa layer A-WALL, "xóa hình trùng" (sau khi chép đè lần nữa) | Claude nói hình nằm trên layer khóa nên không sửa, hỏi có mở khóa không |
| B7 | "Soát bản vẽ và xuất ra Excel" | Có file `<tên bản vẽ>-soat-ban-ve.xlsx` cạnh bản vẽ, mỗi nhóm một dòng. Làm lại lần nữa: hiện thẻ ghi đè file |

## C. Bản vẽ không dùng bộ chuẩn (~5 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| C1 | Mở một bản vẽ cũ của cậu (layer tên riêng, kiểu chữ riêng), "soát bản vẽ" | Claude **không** coi layer / kiểu chữ của cậu là lỗi, nói rõ phần tên layer / kiểu / cỡ chữ đã bỏ qua vì bản vẽ không dùng bộ chuẩn; vẫn báo lỗi chung (layer 0, trùng, dài 0…) |
| C2 | Bản vẽ lớn (vài chục nghìn đối tượng), "soát bản vẽ" | AutoCAD không treo; Claude nói chỉ quét 20 000 trên M đối tượng nếu bản vẽ lớn hơn |

## D. Tờ in và chế độ (~5 phút)

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| D1 | Bấm tab "+" (layout mới tên `Layout2`), xóa hết rồi tự đặt một khung nhìn (MVIEW), không khung tên, không khóa; "soát bản vẽ" *(Sửa KFG-08: tên mặc định vẫn được kiểm khi có khung nhìn của cậu; layout mặc định trống hẳn thì bỏ qua)* | Báo "Tờ in không có khung tên" (tên layout) và "Khung nhìn chưa khóa"; "khóa khung nhìn" → thẻ → khóa được |
| D2 | Bật "Chỉ xem", "soát bản vẽ" rồi "sửa hình trùng" | Soát vẫn chạy; sửa bị chặn (Chỉ xem) |
| D3 | Máy gói 3D (nếu có) | Không có lệnh soát bản vẽ (thuộc gói 2D / Full) |

## E. Sau Test & sửa đợt G2 (~5 phút)

*Nhánh `full/nang-cap-g2-test`. Chi tiết: `docs/ban-giao/nang-cap-g2-test.md`.*

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| E1 | Bản vẽ nhỏ (khoảng 10–15 hình), kéo một đường ra rất xa (vd cách 50 km), "soát bản vẽ" | Báo "Hình lạc rất xa phần còn lại" đúng đường đó |
| E2 | Đặt một định nghĩa thuộc tính (lệnh `ATTDEF`, để trống giá trị mặc định) trong mô hình, "soát bản vẽ" rồi "sửa chữ rỗng" | Định nghĩa thuộc tính **không** nằm trong "Chữ rỗng", không bị xóa |
| E3 | Gắn một xref rồi tạm gỡ nó (`XREF` → Unload), "soát bản vẽ" | **Không** báo "Xref / ảnh mất file" cho xref đó. Đổi tên file xref trên ổ rồi Reload: lúc đó mới báo |
