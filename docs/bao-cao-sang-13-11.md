# Báo cáo sáng 13/11/2026: Agent to AutoCAD Full

*Luồng chính. Việc làm từ tối 12/11 (21h45) tới khoảng 1h sáng 13/11, giờ Việt Nam.*

## 1. Tóm tắt

- Phần code của bản Full (gói dịch vụ 2D / 3D / Full + vẽ 3D cả 3 đợt) **đã xong và qua hết kiểm tra tự động**.
- Đêm qua:
  - làm bộ cài "thay bản gốc + cảnh báo" như cậu chọn;
  - một agent mới, chưa từng viết hay test phần nào, kiểm tra độc lập **4 lượt**;
  - tìm được **43 lỗi**, trong đó 3 nặng: **39 đã sửa đúng, 4 được chấp nhận, không còn lỗi nào mở**;
  - bản 2D vẫn chạy như cũ.
- Mọi thứ nằm ở nhánh cuối `full/test-tong-the` (commit `860101b`). Test lõi 369/369, trợ lý 215/215, build đủ 3 bản
  AutoCAD 2022–2027. CI Windows xanh ở lượt cuối.
- **Chưa thử trên AutoCAD thật**, và **chưa có bộ cài để cậu thử**: cần cậu dựng máy chủ kích hoạt riêng trước (mục 2).

## 2. Việc cần cậu làm (theo thứ tự)

1. **Xem lại các quyết định tớ tự đưa ra trong đêm** (mục 3). Đồng ý thì không cần làm gì; muốn đổi điểm nào thì nói tớ.
2. **Dựng máy chủ kích hoạt riêng** theo [`dung-may-chu-kich-hoat-full.md`](dung-may-chu-kich-hoat-full.md) (khoảng 15
   phút), rồi gửi tớ **URL web app + 2 dòng khóa công khai**. Không gửi khóa riêng hay SECRET.
3. Tớ giao gắn URL / khóa vào repo và **build bộ cài thử**. Cậu thử theo
   [`checklist-tong-the.md`](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/blob/full/test-tong-the/docs/checklist-tong-the.md):
   - lượt rút gọn khoảng 97 phút, cả lượt khoảng 157 phút;
   - quan trọng nhất: phần 1 (gói dịch vụ), L9 / L11 (mặt cắt, hình chiếu), phần M (những chỗ vừa sửa đêm qua).
   - Bản dùng thử AutoCAD hết khoảng **10/12**.
4. Một việc cũ còn treo: 2 commit đầu (ngày 12/11) có tên model trong dòng ghi công, sai quy tắc. Muốn xóa hẳn thì phải
   viết lại lịch sử của cả 11 nhánh. **Tớ đề xuất để nguyên**; từ commit thứ ba trở đi đã sạch.

## 3. Quyết định tớ tự đưa ra trong đêm (cậu xem lại)

Cậu dặn không hỏi tới 7h, nên chỗ nào phải quyết, tớ chọn cách **an toàn nhất, khớp với những gì cậu đã chốt**.

| # | Tình huống (ví dụ thực tế) | Tớ chọn | Vì sao |
| --- | --- | --- | --- |
| 1 | Hộp thoại cảnh báo khi cài đè bản gốc | Hai nút ghi **"Tiếp tục" / "Hủy"** | Đúng như phương án cậu chọn |
| 2 | Claude **xóa** một khối 3D có từ trước | **Luôn hiện thẻ xác nhận**, như cộng / trừ / cắt / phá khối | Cậu đã chốt luôn hỏi khi làm đổi khối cũ. Xóa còn nặng hơn. Đóng luôn đường lách "sao chép rồi xóa bản gốc" |
| 3 | Claude hỏi "Tường cao 3 m nhé?", cậu gõ "ok", rồi Claude khoét vào tường cũ | Gõ "ok" / "đồng ý" **không thay được thẻ**. Chỉ bấm **Đồng ý trên thẻ** mới cho sửa khối 3D cũ | Đúng nghĩa "luôn qua thẻ xác nhận". Luật cho đối tượng 2D giữ như cũ |
| 4 | Máy gói 3D tạo block có thuộc tính chữ "Phòng khách" | **Chặn** phần thuộc tính; block thường vẫn dùng được | Phương án A: gói 3D không có chữ |
| 5 | Máy không có mạng dán lại dòng gói Full cũ sau khi cậu hạ xuống 2D | **Chỉ nhận dòng gói mới hơn** | Để hạ gói có tác dụng |
| 6 | Gói 3D không có offset / nối / phá đường 2D | **Giữ** phương án A, không mở thêm | Đổi cách chia gói là việc của cậu |
| 7 | Máy offline cần chỗ dán mã cậu gửi | Ô key nhận nhiều dòng; thẻ 🔒 có ô **"Dùng mã"**; ⚙ ▸ Tài khoản AI có nút **"Kiểm tra lại"** và **"Dán mã của chủ plugin"** | Tài liệu đã hứa có chỗ dán, nhưng trước đó chưa có |
| 8 | Gói 3D chèn một block có sẵn, block đó có chữ mặc định | **Chấp nhận** chữ mặc định vẫn hiện | Đó là nội dung có sẵn trong block của người dùng; Claude vẫn không đặt hay sửa được chữ |
| 9 | Dán lại mã "miễn kiểm tra" cũ để mở máy cậu đã khóa | **Chặn**: máy không nhận mã cũ hơn trạng thái đang có | Để cậu luôn khóa được máy từ xa |

## 4. Giới hạn còn lại (đã chấp nhận, đã ghi trong tài liệu)

- **Hạ gói máy offline trong ngày:** máy offline đang Full, cậu hạ xuống 2D ngay trong ngày. Nếu khách khóa file gói
  trên máy, máy vẫn giữ Full tới hết ngày (giờ UTC, tức 7h sáng hôm sau giờ Việt Nam).
- **Khóa rồi cấp lại mã offline cùng ngày:** khóa máy rồi cấp lại mã không cần mạng trong cùng ngày (UTC) thì máy từ chối
  mã mới. Đợi sang ngày, hoặc khi máy có mạng thì đổi lại Active trong Sheet.
- **Macro cũ:** macro bo cạnh ghi trước bản sửa đêm qua phải ghi lại. Chưa có người dùng nên không ảnh hưởng ai.
- **Layer khi dựng khối:** khối đùn / xoay đặt trên layer hiện hành, giữ như cũ.
- **Chèn block ở gói 3D:** block có sẵn chữ mặc định thì chữ vẫn hiện (quyết định 8).
- **Ô dán mã chưa được thử dán thật:** UI test có thể chưa dán thật qua clipboard trên máy CI, nên mục M10–M12 cần
  cậu thử tay trên Windows.
- **Bộ cài CI hiện có:** vẫn trỏ máy chủ kích hoạt của dự án gốc. **Đừng cài thử bản đó**; chờ bộ cài thử ở mục 2.

## 5. Kiểm tra độc lập: diễn biến

| Lượt | Kết quả |
| --- | --- |
| 1 | 39 lỗi (3 nặng, 9 vừa, 27 nhẹ), chuyển về 5 thread phụ trách sửa |
| 2 | 35 sửa đúng, 1 sửa chưa đủ, 3 chấp nhận; thêm 3 lỗi mới nhẹ |
| 3 | 3 lỗi đó đã ổn; thêm 1 lỗi mới nhẹ (mã cũ mở lại máy đã khóa) |
| 4 | Lỗi đó sửa đúng, **không còn lỗi nào** |

**3 lỗi nặng đã sửa:**
- máy không có mạng dán mã 3 dòng vào bộ cài thì bị kẹt ở khóa 🔒;
- cửa sát góc phòng bị khoét nhầm vào bức tường bên cạnh;
- dựng tầng 2 bị chặn, và skill còn gợi ý xóa tầng 1.

Chi tiết:
- bảng 43 lỗi → thread sửa → commit: [`ban-giao/test-tong-the.md`](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/blob/full/test-tong-the/docs/ban-giao/test-tong-the.md);
- báo cáo 4 lượt: [`ban-giao/kiem-tra-doc-lap.md`](https://github.com/Tieudao-dot/Agent-to-AutoCAD-Full/blob/full/kiem-tra-doc-lap/docs/ban-giao/kiem-tra-doc-lap.md).

## 6. Thời gian và chi phí

- **Thời gian:** khoảng 3 giờ 15 phút (21h45 → 1h, giờ Việt Nam).
- **CI Windows:** 5 lượt trong đêm, đều xanh (43243835815, 43249502517, 43259984843, 43267347630, 43272295317), mỗi lượt
  khoảng 10 phút.
- **Tốn nhất:**
  - lượt kiểm tra độc lập đầu tiên: 4 tác vụ rà song song, thread đó tự ước khoảng 200k token mỗi tác vụ;
  - các lượt chờ CI.
- **Tớ không thấy tổng số token chính xác.**
- **Thread:** 11 thread đang mở (danh bạ trong `CLAUDE.md`), tất cả đang nghỉ và không tốn gì khi nghỉ. Muốn đóng bớt
  thì cậu bảo.
