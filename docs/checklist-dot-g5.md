# Checklist thử tay đợt G5: vẽ lại từ ảnh / PDF scan

*Đã gộp vào khối "Đợt G" của [`docs/checklist-tong-the.md`](checklist-tong-the.md) (test tổng thể đợt G), thử tay theo file đó.*

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

## H. Sau sửa lỗi kiểm tra độc lập (KFG, ~12 phút)

1. **KFG-05:** đặt UCS xoay 30° (`UCS` → `Z` → 30), gửi một bản scan, "vẽ lại bản này": ảnh nền nằm **thẳng theo UCS** (cạnh ảnh song
   song lưới UCS); `straighten` theo một tường nằm dọc trục x của UCS thì gần như không xoay.
2. **KFG-06:** bản vẽ có một ảnh của cậu (logo trong khung tên). Nhờ Claude "co ảnh logo này" bằng trace: Claude phải báo ảnh đó
   không do lệnh vẽ lại gắn, không co / xoay nó.
3. **Đường dẫn mạng:** "vẽ lại `\\máy-khác\thư-mục\scan.png`" (máy khác với chỗ lưu bản vẽ): Claude từ chối, nhờ kéo file vào
   khung chat hoặc chép về máy. File cùng thư mục mạng với bản vẽ đang mở: được.
4. **KFG-11:** một PDF nhiều trang đã sửa / lưu lại nhiều lần (vd ký, ghi chú bằng Acrobat): Claude hỏi trang, số trang đúng.
   PDF nén (Claude báo "không đọc được số trang"): Claude nói đã gắn trang 1 và hỏi lại nếu cần.
5. **KFG-13:** máy gói 3D (hoặc chế độ "3D"): dán ảnh vào chat, không có file mới trong `%LOCALAPPDATA%\ClaudeCAD\anh-dinh-kem`.
   Gói 2D: ảnh dán lưu ở đó; file cũ hơn 7 ngày bị dọn khi dán ảnh mới. Ảnh dán là bản ≤ 1568 px: Claude nói nên kéo file gốc
   khi bản scan lớn, chữ nhỏ.
6. Bản vẽ **chưa lưu** + ảnh dán: Claude nhờ lưu bản vẽ trước (không còn chép ảnh vào Documents).
7. Phóng to vào giữa một trang PDF (`view` với `p1` / `p2` nằm trong trang): không còn cảnh báo nhầm "ảnh xem không có ảnh".

## I. Sau sửa lỗi lượt 2 (KFG-28 … 37, ~10 phút)

1. **KFG-29:** kéo một ảnh từ thư mục mạng (khác thư mục của bản vẽ, vd `\\nas\anh\scan.jpg`) vào khung chat, gõ "vẽ lại bản này":
   phải gắn được. File được chép một lần vào `%LOCALAPPDATA%\ClaudeCAD\anh-dinh-kem` (tên `tep-…`) rồi chép cạnh bản vẽ; Claude
   không tự mở file trên máy mạng. Làm theo lời khuyên khi bị từ chối (kéo vào khung chat) thì qua được.
2. **KFG-28:** nhờ Claude "vẽ lại `\\.\UNC\máy-khác\x\a.png`", "`\\?\GLOBALROOT\Device\Mup\máy-khác\x\a.png`", hoặc
   "`\\?\Z:\a.png`" (Z: là ổ mạng, bản vẽ ở C:): đều bị từ chối. "`\\?\C:\…\a.png`" (ổ của máy): được.
3. Đường dẫn không đầy đủ ("vẽ lại `scan.png`"): Claude nhờ đường dẫn đầy đủ hoặc kéo file vào chat (không đọc thư mục hiện tại).
4. Bản vẽ mở từ ổ mạng Z: (= `\\nas\duan`) và ảnh ghi `\\nas\duan\…`: bị từ chối kèm lời nhắc ghi đường dẫn theo ổ Z:.
5. **KFG-36:** UCS xoay 60° hoặc 90°, bản vẽ có sẵn hình, gắn ảnh không chỉ chỗ: ảnh nằm bên phải bản vẽ theo UCS, không đè lên hình.
6. **KFG-37:** UCS xoay 30°, gắn một trang PDF, `scale` rồi `info`: `min` / `max` là hai góc của trang theo UCS (khớp lưới UCS).
7. **KFG-31:** "render lại góc Phối cảnh 1" khi PNG đã có → thẻ ghi đè → Hủy, làm vài lần: AutoCAD không báo lỗi lạ.
8. **KFG-35:** gắn một PDF rất lớn (vài chục MB): AutoCAD không đứng quá ~2 giây ở bước đếm trang; nếu không đếm được, Claude nói
   đã gắn trang 1.

