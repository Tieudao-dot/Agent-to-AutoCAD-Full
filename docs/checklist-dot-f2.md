# Checklist thử tay đợt F2 — 2D dùng hằng ngày

> **Đã gộp vào [`docs/checklist-tong-the.md`](checklist-tong-the.md), khối "Đợt F" (25/11/2026).** Thử theo checklist tổng; file này giữ để tra mã mục cũ (F2-…).

Dành cho người thử trên AutoCAD thật (không cần biết code). Mỗi mục: **làm gì** → **thấy gì là đạt**. Ghi ✓ / ✗ và
chụp ảnh khi ✗. Cài bản thử của nhánh `full/nang-cap-f2` (hoặc `full/nang-cap-f2-test`), máy gói **Full**, chế độ
**Cả hai**, trừ khi mục ghi khác.

## 0. Tự kiểm nhanh

1. Gõ `CLAUDECHATSELFTEST` trên dòng lệnh AutoCAD.
   - Đạt: dòng **"Nâng cấp đợt F2: style chuẩn 1:100 …"** có dấu ✓ (style tạo được, chạy lại không trùng, cửa đi 900,
     trạng thái layer gọi lại được, trang A3 có khung tên). Dòng "Bản vẽ của cậu không bị đụng tới" vẫn ✓.

## A. Style chuẩn

Mở bản vẽ trống (đơn vị mm).

1. Gõ trong khung chat: **"thiết lập style chuẩn tỉ lệ 1:100"**.
   - Đạt: Claude báo đã tạo kiểu chữ `VN-ARIAL` và kiểu kích thước `VN-100`. Lệnh `STYLE` thấy `VN-ARIAL` (Arial),
     lệnh `DIMSTYLE` thấy `VN-100` là kiểu hiện hành. Vẽ thử một kích thước (nhờ Claude "ghi kích thước đoạn 4000"):
     chữ cao khoảng 250 trong bản vẽ, đầu kích thước là **gạch chéo**.
   - Lệnh `LINETYPE` có CENTER, DASHED, HIDDEN, PHANTOM, DASHDOT. Biến `LTSCALE` = 50.
2. Gõ lại đúng câu trên một lần nữa.
   - Đạt: Claude báo không có gì thay đổi; `STYLE` / `DIMSTYLE` **không** có kiểu trùng (không có `VN-100(1)` hay
     tương tự).
3. Ô **Tiêu chuẩn** của khung chat chọn **Kiến trúc**, gõ "thiết lập style chuẩn".
   - Đạt: tạo `KT-ARIAL` và `KT-100` (tên giống skill kiến trúc). Ô **Tỉ lệ** chọn 1:50 rồi gõ lại: thêm `KT-50`.
4. Sửa tay `VN-100` (vd đổi chiều cao chữ thành 3.5 trong `DIMSTYLE`), rồi nhờ "thiết lập style chuẩn 1:100".
   - Đạt: Claude **không** tự ghi đè: hiện thẻ xác nhận hỏi có ghi đè `VN-100` không, nêu chỗ khác (chiều cao chữ).
     Bấm **Hủy**: `VN-100` vẫn là 3.5. Làm lại và bấm **Đồng ý**: `VN-100` về 2.5.
   - Gõ tay "đồng ý" thay vì bấm nút: **không** được ghi đè.
5. Bản vẽ đơn vị **mét** (`INSUNITS` = 6): "thiết lập style chuẩn 1:100".
   - Đạt: tạo `VN-100-M`; ghi kích thước đoạn dài 4 (mét) hiện **4** (đoạn 4,5 m hiện **4.5**: số 0 thừa bị bỏ) và chữ cao khoảng 0,25.
6. Đặt `LTSCALE` = 20 trước, rồi chạy lại: Claude báo giữ LTSCALE của cậu và nói số nên dùng (không tự đổi).

## B. Trang in có khung tên

Mở một mặt bằng có sẵn (khoảng 12 × 9 m, đơn vị mm).

1. "**tạo trang in A3 tỉ lệ 1:100, công trình NHÀ PHỐ, tên bản vẽ MẶT BẰNG TẦNG 1, người vẽ <tên cậu>**".
   - Đạt: có tab layout mới (vd `BV-01`); mở tab thấy khung bản vẽ cách mép giấy 25 mm bên trái, 5 mm ba cạnh còn lại;
     khung tên góc dưới phải đủ ô: ĐƠN VỊ TK, CÔNG TRÌNH, HẠNG MỤC, TÊN BẢN VẼ, NGƯỜI VẼ, KIỂM TRA, NGÀY (hôm nay), TỈ
     LỆ (1:100), SỐ BẢN VẼ, KHỔ GIẤY (A3); chữ tiếng Việt có dấu đúng.
   - Khung nhìn thấy mặt bằng, đúng tỉ lệ 1:100 (chọn khung nhìn, xem Properties → Standard scale / Custom scale), đã
     khóa (zoom trong khung nhìn không đổi tỉ lệ). Viền khung nhìn không in ra.
2. Bấm đúp khung tên (lệnh `EATTEDIT`) sửa ô "KIỂM TRA": sửa được. Hoặc nhờ Claude "sửa ô kiểm tra thành …": đúng ô.
3. "**in trang BV-01 ra PDF**".
   - Đạt: file PDF khổ A3 ngang, khung và khung tên nằm trọn trong giấy, nét đen trắng.
4. Không nói tỉ lệ: "tạo trang A3 cho bản vẽ này". Đạt: Claude chọn tỉ lệ chuẩn vừa bản vẽ (vd 1:50) và báo lại.
5. Khổ khác: A1 ngang, A4 đứng ("tạo trang A4 đứng"). Đạt: khổ giấy đúng, khung tên vừa khung.
6. Tạo trùng tên layout có sẵn ("tạo trang tên BV-01"). Đạt: Claude báo tên đã có, **không** sửa layout cũ.
7. Bản vẽ có UCS xoay 30° (lệnh `UCS` → Z → 30) rồi tạo trang: mặt bằng trong khung nhìn xoay theo UCS (trục X của
   UCS nằm ngang trên giấy). *(Cần thử thật: chưa kiểm được ngoài AutoCAD.)*

## C. Block thư viện

1. "**liệt kê thư viện block**". Đạt: Claude kể 15 loại (cửa đi, cửa 2 cánh, cửa trượt, cửa sổ, bồn cầu, lavabo, bồn
   tắm, khay tắm, giường, sofa, bàn, ghế, tủ, bếp, cây) kèm kích thước mặc định.
2. "**chèn cửa đi 900 tại 0,0**". Đạt: có block `CUA-DI-900` trên layer `A-DOOR`: một cánh dài 900 vuông góc tường +
   cung mở 90°, gốc ở bản lề.
3. "chèn cửa đi 800, bản lề phía bên kia, xoay 90 độ". Đạt: block `CUA-DI-800`, lật và xoay đúng.
4. "chèn cửa sổ 1500 tường 110". Đạt: block `CUA-SO-1500x110` trên `A-WIND`, 3 nét trong bề dày tường.
5. "chèn bồn cầu, lavabo, giường đôi 1800, sofa 2400, cây đường kính 4000". Đạt: mỗi thứ đúng cỡ (đo bằng `DIST`),
   nằm trên layer A-FIXT / A-FURN / L-PLNT.
6. Chèn lại "cửa đi 900" lần nữa: dùng lại block `CUA-DI-900` (lệnh `BLOCK` / `INSERT` không thấy `CUA-DI-900(1)`).
7. Bản vẽ đã có block tự vẽ tên `CUA-DI-900`: chèn từ thư viện dùng đúng block của cậu, **không** vẽ đè định nghĩa.
8. Máy gói **3D** (hoặc chế độ **3D**): vẫn chèn được block thư viện.
9. Dựng 3D từ mặt bằng có cửa thư viện (máy 3D / Full: "dựng 3D mặt bằng này"): cửa đi / cửa sổ thư viện được nhận
   là lỗ cửa.
10. UCS xoay 30°: "chèn cửa đi tại 0,0". Đạt: cửa nằm ở gốc UCS, xoay theo UCS.

## D. Trạng thái layer

Bản vẽ có vài layer (A-WALL, A-DOOR, A-DIM, A-TEXT…).

1. "**lưu trạng thái layer tên IN-KIEN-TRUC**". Đạt: lệnh `LAYERSTATE` (hoặc nút Layer States trong bảng Layer) thấy
   `IN-KIEN-TRUC`.
2. Tắt A-DIM, khóa A-TEXT, đổi màu A-WALL. Rồi "**gọi lại trạng thái IN-KIEN-TRUC**". Đạt: A-DIM bật lại, A-TEXT
   mở khóa, A-WALL về màu cũ; Claude báo số layer đã đổi.
3. "liệt kê các trạng thái layer". Đạt: thấy tên vừa lưu.
4. "lưu trạng thái layer tên IN-KIEN-TRUC" lần nữa (trùng tên). Đạt: hiện thẻ hỏi ghi đè; Hủy → giữ bản cũ; Đồng ý →
   ghi bản mới.
5. "xóa trạng thái IN-KIEN-TRUC". Đạt: hiện thẻ; chỉ xóa sau khi bấm Đồng ý.
6. Nút **Hoàn tác lượt này** sau khi gọi lại trạng thái: layer về như trước khi gọi.

## E. Chéo

1. Máy gói **2D**: A, B, C, D đều dùng được.
2. Máy gói **3D**: nhờ "thiết lập style chuẩn" hoặc "tạo trang in" → Claude nói máy không có phần 2D (không gọi lệnh);
   C, D dùng được.
3. Máy **Full**, chế độ **2D**: A–D dùng được. Chế độ **3D**: A, B → Claude nhắc đổi chế độ (không mời mua gói); C, D
   dùng được.
4. Ghi macro trong lúc làm A + C ("Lưu thành macro"), mở bản vẽ trống, chạy macro: style và block ra đúng, không trùng.

## F. Sau Test & sửa đợt F2 (thêm 25/11/2026, ~8 phút)

*Các lỗi đã sửa ở `full/nang-cap-f2-test` (`docs/ban-giao/nang-cap-f2-test.md`). UCS = hệ trục khách tự đặt bằng lệnh
`UCS` (biểu tượng trục X / Y ở góc màn hình đổi theo).*

1. Gõ `UCS` → `Z` → 30 (xoay hệ trục 30°). Nhờ "liệt kê thư viện block". Đạt: Claude đọc ra danh sách 15 ký hiệu, không
   báo lỗi. Rồi "chèn cửa đi 900 tại gốc": cửa nằm ở gốc UCS, xoay theo UCS.
2. Vẫn UCS xoay 30°: vẽ một dải dài 40 m × 4 m dọc trục X của UCS, rồi "tạo trang in A3" (không nói tỉ lệ). Đạt: trang
   in có khung nhìn xoay theo UCS (dải nằm ngang trên giấy), tỉ lệ chọn vừa khít dải (không chọn tỉ lệ nhỏ quá khiến dải
   chỉ là một vệt nhỏ giữa trang). Gõ `UCS` → `W` khi xong.
3. Trong một bản vẽ đã có kiểu chữ `KT-ARIAL` tự tạo bằng lệnh `STYLE` (font Arial, rộng 0,85): nhờ "thiết lập style
   chuẩn kiến trúc 1:100". Đạt: kiểu chữ báo "không đổi" (không hiện thẻ hỏi ghi đè chỉ vì tên file font).
4. Bản vẽ đơn vị **dm** (`INSUNITS` = 14) hoặc **inch** (1): "thiết lập style chuẩn 1:100". Đạt: kiểu kích thước tên
   `VN-100-DM` (dm) / `VN-100-IN` (inch), không còn ghi nhầm `-CM`.
5. Đặt màu hiện hành của AutoCAD sang **đỏ** (thanh Properties → Color = Red). Nhờ "chèn lavabo" và "tạo trang in A3".
   Đạt: lavabo, khung bản vẽ, khung tên **không đỏ** (theo màu layer). Đặt lại màu hiện hành ByLayer khi xong.
6. Bật **Chỉ xem**, nhờ "liệt kê trạng thái layer". Đạt: Claude đọc ra danh sách, không báo bị chặn vì Chỉ xem.
