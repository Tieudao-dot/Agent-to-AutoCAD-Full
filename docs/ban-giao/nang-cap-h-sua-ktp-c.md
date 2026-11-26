# Bàn giao: Sửa KTP phần C — H6 + tài liệu + gộp (26/11/2026)

Thread `session_01D56HmFcYrzsme7GoBm8tNC` (giữ nhánh tích hợp phần 2). Nhánh `full/nang-cap-h-tong-the-2`. Báo cáo kiểm tra độc
lập: `docs/ban-giao/kiem-tra-doc-lap-dot-h-phan-2.md` (luồng chính). Chưa chạy trên AutoCAD thật.

## 1. H6 đã sửa

| Mã | Lỗi | Đã sửa (`Core/Terrain.cs`, `Drawing/TerrainTools.cs`) |
| --- | --- | --- |
| KTP-08 | 4 cột không tiêu đề "tên số, X, Y, Z", tên nhảy bước (1, 4, 9, 15): tên bị coi là X | `FirstColumnIsName`: số nguyên nhỏ (< `RealCoordinateM`) trước hai cột tọa độ thật (≥ 80 % dòng), hoặc số thứ tự → cột tên |
| KTP-09 | 3 cột "tên số, X, Y": X = tên | Cùng phép thử, đặt trước nhánh "X, Y, Z" (3 cột chỉ dùng phép tọa độ thật, không dùng số thứ tự: "100 200 1.5" vẫn là X, Y, Z) |
| KTP-10 | Dấu nghìn kiểu Việt `1.234.012,500`, khoảng trắng / U+00A0 / U+202F giữa nhóm: 0 điểm | `NumberText`: bỏ khoảng trắng các loại; bỏ dấu chấm nhóm 3 số trước phẩy thập phân (hoặc ≥ 2 nhóm); phẩy thập phân → chấm. Một dấu chấm đơn vẫn là dấu thập phân |
| KTP-11 | Ô `.xlsx` số lưu dạng chữ "1234012,5": 0 điểm | Cùng `NumberText` (mọi ô đi qua `TryNumber`) |
| KTP-12 | `.txt` cách bằng dấu cách, số phẩy thập phân: chọn nhầm `,` làm dấu tách | `CommasAreDecimal`: mọi dòng có ≥ 2 chữ cách bằng khoảng trắng và mọi dấu phẩy nằm trong một số → tách theo khoảng trắng (tab nếu có) |
| KTP-22 | Tiêu đề "STT \| Tên điểm \| …": tên lấy theo STT | "stt / tt / số tt / id / no" chỉ là tên khi không có cột tên |
| KTP-23 | Số dòng trong `skipped_rows` lệch file (bỏ dòng trống trước khi đánh số; `.xlsx` bỏ thuộc tính `r`) | Giữ dòng trống làm dòng rỗng; `.xlsx` đặt dòng theo `r`; dòng rỗng bỏ qua im lặng |
| KTP-24 | `TerrainTools.cs`, `terrain.ts`, skill `ve-ha-tang` ghi "m × 1000", `position` "WCS, mm" | Ghi theo đơn vị bản vẽ (kết quả `axes` in hệ số thật; mô tả "× 1000 khi bản vẽ mm") |
| KTP-25 | `Breakpoints` trắc ngang: sai số cộng dồn vượt 1 cm | Bỏ mẫu chỉ khi **mọi** mẫu từ điểm giữ cuối tới mẫu kế đều nằm trong 1 cm so với dây cung |
| KTP-26 | Đỉnh tuyến trùng cọc đầu / cuối: "C43/Đ2" | "Đầu/Đ…", "Cuối/Đ…" |

**Nghi vấn (đã sửa, rẻ):**

- Delaunay hụt tam giác rìa khi tọa độ rất lớn và `max_edge: 0`: tam giác bao lớn hơn (1 000 lần, trước 20 lần); test lưới tọa độ
  VN-2000 mm đủ 50 tam giác.
- Trắc ngang ở cọc đỉnh lấy hướng đoạn sau: nay vuông góc với **phân giác** hai đoạn.
- Lọc `stakes` "Đ1" không khớp "C3/Đ1": so từng phần tên.
- `PlaceBelow` dùng `Extmin` / `Extmax` cũ: gọi `UpdateExt(true)` trước.

**Lỗi thêm tìm thấy khi sửa (nặng, của H6 gốc):** `Alignment.At` kẹp khoảng cách vào chiều dài tuyến tính bằng **mét**, trong
khi khoảng cách truyền vào là **đơn vị bản vẽ**: bản vẽ mm có mọi cọc dồn trong mét đầu tuyến (lý trình, trắc dọc, trắc ngang sai
hẳn; test lõi không bắt vì cần AutoCAD, SelfTest sẽ báo). Nay kẹp theo chiều dài đơn vị bản vẽ.

Test: `plugin/ClaudeCAD.Core.Tests/SuaKtpCTests.cs` (10 test).

## 2. Gộp và tài liệu

*(ghi sau khi gộp `full/nang-cap-h-sua-ktp-a`, `full/nang-cap-h-sua-ktp-b`)*

## 3. Test / CI

*(ghi sau)*
