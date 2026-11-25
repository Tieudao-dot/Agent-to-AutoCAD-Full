# Checklist thử tay đợt G5: vẽ lại từ ảnh / PDF scan

Máy gói 2D hoặc Full (chế độ "2D" hoặc "Cả hai"), AutoCAD 2022 – 2027. Mở một bản vẽ mới, lưu thành file (để ảnh nền được
chép cạnh bản vẽ). Chuẩn bị 5 file: (1) ảnh chụp điện thoại một mặt bằng vẽ tay, (2) bản scan mặt bằng có ghi kích thước,
(3) PDF scan 1 trang, (4) PDF nhiều trang, (5) ảnh chụp nghiêng. Khoảng 40 phút. Mỗi bước ghi lại số lần xem ảnh Claude
báo, và chi phí lượt (nếu khung chat hiện).

## A. Ảnh scan có kích thước (~10 phút, quan trọng nhất)

1. Kéo file (2) vào khung chat (hoặc bấm nút đính kèm ảnh), gõ "vẽ lại bản này".
2. Cần thấy:
   - Claude gắn ảnh làm nền: layer `X-ANH-GOC` màu xám, **khóa**, **không in**; ảnh nằm bên phải bản vẽ đang có (bản vẽ
     trống: ở gốc 0,0); file ảnh được chép cạnh bản vẽ (`<tên bản vẽ>-anh-goc-….png`) nếu cậu dán ảnh, còn kéo file thì
     dùng chính file đó;
   - Claude nói ảnh **chưa đúng tỉ lệ**, rồi tìm một kích thước ghi trên bản scan để đặt tỉ lệ (nói rõ đã dùng số nào);
   - Claude vẽ tường, cửa đi, cửa sổ, tên phòng, vài kích thước chính đè lên ảnh, đúng layer A-WALL / A-DOOR / A-WIND…;
   - cuối cùng Claude nói đây là **bản nháp cần kiểm kích thước**, kể các chỗ đọc không chắc, và số lần đã xem ảnh (≤ 8).
3. Đo bằng `DIST` một kích thước ghi trên bản scan (khác số Claude dùng để đặt tỉ lệ): lệch bao nhiêu? Ghi lại.
4. Bấm U một lần: mọi thứ của lượt (ảnh nền + nét vẽ) mất. Bấm Redo (hoặc gửi lại) để tiếp.
5. In thử (plot_pdf hoặc lệnh PLOT): ảnh nền **không** ra giấy.

## B. Ảnh chụp điện thoại, vẽ tay (~8 phút)

1. Dán ảnh (1) bằng Ctrl+V vào khung chat, gõ "vẽ lại bản này, phòng khách dài 4,5 m".
2. Claude dùng 4,5 m cậu cho để đặt tỉ lệ (không đoán). Không cho số nào: Claude phải **hỏi** cậu một kích thước thật.
3. Nét vẽ tay xiêu vẹo: tường Claude vẽ thẳng, vuông góc; chỗ không chắc được liệt kê.

## C. Ảnh nghiêng (~5 phút)

1. Gửi ảnh (5), "vẽ lại bản này". Claude nên gọi `straighten` (xoay ảnh theo một bức tường) trước khi đặt tỉ lệ.
2. Sau khi xoay, tường trên ảnh nền gần như ngang / dọc. Ảnh chụp xiên mạnh (méo phối cảnh): Claude nói không chỉnh được méo,
   chỉ xoay; ghi lại kết quả.

## D. PDF scan (~7 phút)

1. "Vẽ lại bản này: <đường dẫn file (3)>": PDF 1 trang gắn được, các bước như A.
2. File (4) nhiều trang, không nói trang: Claude **hỏi** trang nào. Trả lời "trang 2": gắn đúng trang 2.
3. (Nếu có) một PDF xuất từ phần mềm CAD (nét vector, không phải scan): Claude gợi ý lệnh `PDFIMPORT` của AutoCAD.

## E. Ảnh mờ và các lỗi (~5 phút)

1. Ảnh mờ / chữ nhỏ: Claude xem phóng to từng phần (mỗi lần tính một lượt xem); chữ không đọc được phải được kể ra, không bịa.
2. Đường dẫn sai: Claude báo không có file, hỏi lại. File `.webp` / `.heic` (ảnh iPhone): Claude nhờ lưu lại thành JPG.
   File trên 50 MB: Claude nhờ scan nhỏ hơn.
3. Bật "Chỉ xem" (🔒): "mô tả bản scan này" được; "vẽ lại" thì Claude nói không gắn / không vẽ khi đang Chỉ xem.
4. Đổi UCS xoay (`UCS` → Z → 30°), "vẽ lại bản này": lưới trên ảnh xem nghiêng theo UCS, nét Claude vẽ vẫn khớp ảnh.
5. Bản vẽ đơn vị mét (`INSUNITS` = 6): tỉ lệ vẫn đúng (cạnh 5 m dài 5 đơn vị).

## F. Tự kiểm

Bấm "Tự kiểm": bước "Nâng cấp đợt G5" phải đạt. Bước này cũng cho biết AutoCAD có vẽ ảnh nền trong ảnh xem (ảnh chụp ngoài
màn hình) hay không. **Nếu bước này báo "ảnh xem không thấy ảnh nền", gửi tớ ngay**: khi đó Claude không nhìn thấy bản scan
khi xem và cần đổi cách làm.
