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

Bấm "Tự kiểm": bước "Nâng cấp đợt G5" phải đạt. *(Test & sửa G5)* Ảnh xem nay dựng thẳng từ file ảnh gốc, nét Claude vẽ phủ
lên trên, nên Claude thấy bản scan dù AutoCAD có vẽ ảnh nền trong ảnh chụp ngoài màn hình hay không. Dòng kết quả của bước
này ghi một trong hai câu: "AutoCAD có vẽ ảnh nền khi chụp", hoặc "AutoCAD KHÔNG vẽ ảnh nền khi chụp (ảnh xem lấy từ file)".
**Ghi lại câu đó gửi tớ.** Nếu là "KHÔNG" thì ảnh vẫn xem được, nhưng **PDF scan** sẽ không thấy (xem G3 dưới).

## G. Sau Test & sửa đợt G5 (~10 phút)

*Nhánh `full/nang-cap-g5-test`. Chi tiết: `docs/ban-giao/nang-cap-g5-test.md`.*

| # | Làm gì | Thấy gì là đạt |
| --- | --- | --- |
| G1 | Xoay UCS 30° (`UCS` → `Z` → 30), "vẽ lại bản này" với bản scan (2), rồi "xem kỹ góc trên bên trái" | Ảnh nền nằm đúng chỗ Claude nói; vùng xem phóng to đúng góc đó (không lệch sang chỗ khác); sau khi đặt tỉ lệ, ảnh không bị trôi đi |
| G2 | Đổi tên một ảnh HEIC / WebP thành `.png`, gửi "vẽ lại bản này: <đường dẫn>" | Claude nói file thật ra là HEIC / WebP, nhờ lưu lại JPG (không báo lỗi khó hiểu của AutoCAD) |
| G3 | PDF scan (3): sau khi gắn, Claude xem ảnh | Nếu ảnh xem trắng trơn: Claude **nói không thấy ảnh** và nhờ xuất trang PDF ra PNG / JPG để gửi lại, **không vẽ mò**. Ghi lại Claude thấy hay không thấy trang PDF |
| G4 | Sau khi vẽ lại, "soát bản vẽ" | **Không** báo layer `X-ANH-GOC` là "layer không in còn chứa hình" hay "layer không thuộc bảng chuẩn" |
| G5 | Sau khi vẽ lại, "ra bộ hồ sơ" (bản vẽ chỉ có mặt bằng 2D) | Tờ mặt bằng chỉ có phần đã vẽ, không thu nhỏ để chứa cả ảnh nền |
| G6 | Đường dẫn có dấu tiếng Việt và dấu cách (vd `D:\Bản vẽ\nhà phố.jpg`) | Gắn được, không có lệnh lạ chạy ở dòng lệnh |
