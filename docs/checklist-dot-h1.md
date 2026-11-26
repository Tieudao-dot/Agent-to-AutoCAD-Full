# Checklist thử tay đợt H1: skill trên máy nâng cấp, trace_image đọc thẳng PDF

AutoCAD 2022–2027, khoảng 30 phút. Cần một máy đã cài bản cũ (bản trước H1) có thư mục skill
`C:\Program Files\ClaudeCAD\Contents\skills` (hoặc đường dẫn cài của cậu).

## A. Skill khi cài đè (~12 phút, phần quan trọng nhất)

1. Trên máy đang chạy bản cũ, mở `skills\ve-kien-truc\SKILL.md` bằng Notepad, thêm một dòng cuối (vd "Ghi chú của tớ")
   rồi lưu. Không sửa các skill khác.
2. Cài bản H1 đè lên (chạy bộ cài mới), mở AutoCAD, mở khung chat. Cần thấy:
   - khung chat hiện **một dòng**: "Skill ve-kien-truc có bản mới. Bản cậu đã sửa được giữ nguyên; bản mới nằm cạnh nó…";
   - `ve-kien-truc\SKILL.md` **vẫn có** dòng cậu thêm;
   - cạnh nó có `SKILL.ban-moi.md` (bản mới);
   - các skill khác cậu không sửa đã là bản mới. Mở một file so với `Contents\skills-ban-moi`: giống nhau;
   - trong `skills` có file `.ban-da-cai.json` (file ẩn tên bắt đầu bằng dấu chấm, plugin dùng để nhớ).
3. Đóng, mở lại khung chat (hoặc AutoCAD): dòng báo **không** hiện lại.
4. Thêm một file của cậu vào `skills` (vd `skills\cua-toi\SKILL.md`), cài lại bộ cài một lần nữa: file của cậu còn nguyên.
5. Gỡ cài (Control Panel): thư mục `skills` vẫn còn, kể cả file của cậu.
6. *(Nếu có bản cập nhật trong app)* Bấm "Kiểm tra cập nhật" để lên bản sau H1: kết quả giống bước 2–3. Skill đã sửa
   được giữ, có file `.ban-moi`.
7. Máy mới chưa từng cài: cài bản H1, mở khung chat. **Không** có dòng báo skill; skill đủ như bộ cài.

## B. trace_image với PDF scan (~12 phút)

Máy gói 2D hoặc Full, Windows 10 / 11, bản vẽ đã lưu thành file.

1. Gõ `CLAUDECAD_SELFTEST`. Trong kết quả tìm dòng **"PDF: Windows đọc thẳng trang PDF được"**. Nếu thấy "KHÔNG đọc thẳng
   được (…)", chép nguyên dòng đó gửi tớ.
2. Kéo một PDF scan 1 trang có ghi kích thước vào khung chat (hoặc nói đường dẫn), gõ "vẽ lại bản này". Cần thấy:
   - Claude gắn PDF làm nền, đặt tỉ lệ, vẽ đè như đợt G5;
   - Claude **không** báo "ảnh xem trống / nền trắng". Trước H1, PDF có lúc hiện trắng trong ảnh Claude xem.
3. Đo bằng `DIST` một kích thước ghi trên bản scan, so với nét Claude vẽ: lệch bao nhiêu mm? Lệch đều về một phía (vd
   toàn bộ nét lệch sang phải vài mm) thì ghi lại. Đó là lề trang PDF, tớ cần biết để chỉnh.
4. Xem thư mục `%TEMP%\ClaudeCAD\pdf-trang`: có file `pdf-….png`. Mở ra là đúng trang scan.
5. PDF nhiều trang, nói "vẽ lại trang 2": ảnh Claude xem là trang 2.
6. *(Nếu được)* PDF có mật khẩu: Claude vẫn làm được như trước (ảnh xem lấy từ AutoCAD) hoặc báo rõ không đọc được. Không
   treo quá khoảng 30 giây.

## C. Soát bản vẽ nhiều tầng (~3 phút, KFG-43)

1. Bản vẽ 3D nhiều tầng có hàng nghìn block / điểm chồng theo chiều cao (vd cột lặp mỗi tầng), gõ "soát bản vẽ". Kết quả
   ra trong vài giây (trước đây có thể lâu), không báo trùng các block khác tầng.

## D. Thư viện block dưới UCS xoay (~3 phút, bản vá F2)

1. Lệnh `UCS` → Z → 30, gõ "liệt kê thư viện block": Claude kể được danh sách (không báo lỗi "plugin bug").
