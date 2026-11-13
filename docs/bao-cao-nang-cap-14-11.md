# Báo cáo: chuỗi nâng cấp lệnh vẽ A → E (13–14/11/2026)

*Luồng chính. Chạy từ 14h28 ngày 13/11 tới khoảng 1h20 ngày 14/11 (giờ Việt Nam), theo yêu cầu "chạy liền A → E, chỉ
dừng trước phát hành".*

## 1. Tóm tắt

- **Xong cả 5 đợt nâng cấp** (A → E), test tổng thể, và **kiểm tra độc lập 3 lượt** bằng agent mới.
- Nhánh tích hợp cuối: **`full/nang-cap-tong-the` @ `f7f32ad`**. Test lõi **909/909**, trợ lý **298/298**, build đủ 3 bản
  AutoCAD 2022–2027, **CI Windows xanh** (run 43478782014).
- Lỗi: các bước Test & sửa từng đợt sửa 21 lỗi, test tổng thể 3 lỗi chéo, kiểm tra độc lập **32 lỗi: 30 sửa đúng,
  2 chấp nhận, 0 còn mở**. Không có lỗi nặng.
- **Chưa thử trên AutoCAD thật. Chưa phát hành:** `main` vẫn là v0.9.3 của bản gốc.
- Bộ cài thử nội bộ mới (có đủ các lệnh nâng cấp): xem mục 5.

## 2. Plugin có thêm gì (nói gọn)

| Đợt | Thêm gì | Gói |
| --- | --- | --- |
| A (3D) | Một lệnh dựng khối thay 6 lệnh cũ (macro cũ vẫn chạy); dựng 3D từ mặt bằng hiểu layer đặt tên khác (hỏi lại trước khi dựng); cột; dựng nhiều tầng một lần; lan can; sao chép theo lưới có số tầng | 3D / Full |
| B (sửa hình 2D) | Kéo giãn, bẻ gãy, kéo dài, chia đều / đặt mốc, nhóm, chép thuộc tính; bo / vát góc cho polyline và cung; offset hai phía; nối đường có khe hở | Nhóm, chép thuộc tính: mọi gói; còn lại 2D / Full |
| C (tiện ích 2D) | Hatch bấm một điểm; tự ghi kích thước cả mặt bằng; thống kê block ra bảng; bảng diện tích phòng; tìm & thay chữ (luôn hỏi bằng thẻ); in nhiều layout vào một PDF | 2D / Full |
| D (3D) | Dầm; tường cong; kéo / đẩy mặt khối; xuất file STL; bảng khối lượng (trừ phần cột lồng tường); bảng va chạm | 3D / Full (gói 3D chỉ hiện bảng trong khung chat, không vẽ bảng vào bản vẽ) |
| E (3D) | Cầu thang 1 vế / chữ L / chữ U; mái bằng / 2 dốc / 4 dốc | 3D / Full |

**Token mỗi lần khách mở chat** (mô tả lệnh + hướng dẫn cho Claude, trước → sau chuỗi): 2D 34,4k → 38,7k; 3D 35,7k →
39,7k; Full 49,7k → 57,5k. Phần này được lưu đệm: lượt hỏi sau trong 5 phút chỉ tốn khoảng 10%.

## 3. Quyết định đưa ra trong lúc chạy (cậu xem lại)

Cậu đã cho chạy liền, không dừng hỏi, nên các chỗ này tớ / thread chọn cách **rẻ, an toàn, đổi lại được**:

| # | Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- | --- |
| 1 | Khách vẽ bằng đơn vị **mét** | Các lệnh mới tự đổi đơn vị như lệnh 3D (kích thước mặt bằng, bảng, số tầng khi sao chép, kéo / đẩy mặt khối) | Cách khác: chỉ nhắc khách "bản vẽ phải dùng mm" |
| 2 | Nhà chữ **L / T** lợp mái dốc | Claude báo trước: chỗ nối hai mái sẽ chồng, cần sửa tay; mái liền khối để đợt F | Làm mái liền ngay (việc lớn) |
| 3 | 3 số đợt E tự chọn | Bản thang dày **150 mm**, khe giữa 2 vế cầu thang chữ U **100 mm**, bản mái dốc dày **150 mm** | Cho số khác |
| 4 | Số mặc định lan can (đợt A) | Cao **1100 mm**, khe giữa các song ≤ **100 mm**, trụ cách nhau ≤ **1500 mm** | Cho số khác |
| 5 | Layer đoán là lan can | Claude được dặn hỏi trước khi dựng, nhưng **chưa chặn cứng** (lan can chỉ thêm khối mới, gõ U là bỏ) | Chặn cứng như dựng 3D từ mặt bằng |
| 6 | Đường bao cột nằm lọt trong đường bao khác | Vẫn dựng cả hai (hiếm, dễ nhầm cột thật) | Bỏ cột nằm trong |

## 4. Kiểm tra

| Bước | Kết quả |
| --- | --- |
| Test & sửa từng đợt | A 4, B 4, C 6, D 6, E 3 lỗi sửa; mỗi đợt CI xanh |
| Test tổng thể nâng cấp | 3 lỗi chéo (mái cắt vào sàn mái nhà nhiều tầng, câu phạm vi gói, bước tự kiểm tra thiếu đợt C) |
| Kiểm tra độc lập lượt 1 | 31 lỗi (0 nặng, 7 vừa, 24 nhẹ), chia về 6 thread sửa |
| Lượt 2 | 29 sửa đúng, 2 chấp nhận (mục 3 dòng 5, 6), 1 lỗi mới nhẹ |
| Lượt 3 | Lỗi mới đã sửa đúng; **không còn lỗi mở** |

3 lỗi đáng kể nhất đã sửa: chạy lại macro có thể sửa khối 3D có sẵn mà không qua thẻ; tìm & thay gặp chữ trên layer khóa
thì hỏng cả lượt; cầu thang dựng xuyên cột / tường lồi vào ô.

Chi tiết: `docs/ban-giao/test-tong-the-nang-cap.md` (bảng lỗi → thread → commit) và
`docs/ban-giao/kiem-tra-doc-lap-nang-cap.md` (nhánh `full/kiem-tra-doc-lap-2`).

## 5. Việc cần cậu làm

1. **Thử trên AutoCAD thật** bằng bộ cài thử nội bộ mới (có đủ nâng cấp): BO_CAI_THU. Theo
   `docs/checklist-tong-the.md` trên nhánh tích hợp: khối "chuỗi nâng cấp A → E" rút gọn ~105 phút (cả hai khối ~3 giờ 25
   phút / đủ ~5 giờ 50 phút). Ưu tiên: bước tự kiểm tra `CLAUDECHATSELFTEST`, các mục chéo V1–V13, ★ của N–R. Bản dùng thử
   AutoCAD và bộ cài thử đều hết khoảng **10/12/2026**.
2. **Xem 6 quyết định ở mục 3**; đồng ý thì không cần làm gì.
3. **Dựng máy chủ kích hoạt riêng** (vẫn đang chờ) rồi gửi tớ URL + 2 dòng khóa công khai, để làm bộ cài thật có gói dịch
   vụ.
4. **Trước khi phát hành:** số phiên bản, secret CI, đồng ý gộp `main` (gộp gọn một commit như đã chốt).

## 6. Thời gian và chi phí

- **Thời gian:** khoảng 11 giờ (14h28 → ~1h20), gồm ~2 giờ đứng / nghỉ: chạm giới hạn dùng 5 giờ (16h41 → 17h05) và vài
  lần thread không tự dậy sau khi chờ CI (đã gỡ bằng lần tự kiểm tra; từ đợt D đổi cách chờ thì hết bị).
- **Chi phí** (hệ thống ghi theo từng thread, quy ra giá API, có thể chưa đủ phần tác vụ con):

| Phần | Chi phí |
| --- | --- |
| Đợt A (viết + test + sửa KT2) | ~$49 (lượt tự rà chạy ở mức suy nghĩ cao nhất, riêng ~$19) |
| Đợt B | ~$27 |
| Đợt C | ~$21 |
| Đợt D | ~$18 |
| Đợt E | ~$14 |
| Test tổng thể nâng cấp | ~$11 |
| Kiểm tra độc lập (3 lượt) | ~$16 |
| Luồng chính (điều phối trong chuỗi) | ~$35 |
| **Cộng** | **~$190** (ước ban đầu $150–230) |

- Tốn nhất: đợt A (chạy ở mức suy nghĩ cao nhất) và luồng chính (ngữ cảnh dài). Tớ không thấy số token chính xác.
- **Lưu ý:** tài khoản đang ở mức cảnh báo giới hạn dùng 7 ngày (đặt lại 0h ngày 19/11).
