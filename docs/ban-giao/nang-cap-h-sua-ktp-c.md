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
hẳn; test lõi không bắt vì cần AutoCAD; SelfTest lúc đó cũng không bắt vì chỉ đếm số cọc). Nay kẹp theo chiều dài đơn vị bản vẽ;
lượt 2 kẹp theo chiều dài đo trên chính đường cong và SelfTest H6 kiểm cọc cuối cách điểm cuối polyline < 1 mm (bản vẽ mm).

Test: `plugin/ClaudeCAD.Core.Tests/SuaKtpCTests.cs` (10 test).

## 1b. Lượt 2 (sau soát lại của kiểm tra độc lập)

| Mã | Lỗi | Sửa | Test |
| --- | --- | --- | --- |
| KTP-37 | `FindHeader` chỉ dò 10 hàng đầu, hàng trống (giữ từ KTP-23) chiếm chỗ | Dò 10 hàng **không rỗng** | Tên bảng hàng 1, tiêu đề "STT;Tên;X;Y;Z" hàng 12 |
| KTP-39 | `CommasAreDecimal` trả false khi có một dòng một chữ ("SODIEM") | Bỏ qua dòng một chữ không có dấu phẩy; phải có ít nhất một dòng nhiều chữ có phẩy | "SODIEM" + 2 dòng phẩy thập phân |
| KTP-48 | `.txt` tiêu đề "Tên điểm  X  Y  Z": tên hai chữ làm lệch cột, 0 điểm | Khi hàng tiêu đề nhiều ô hơn hàng số liệu kế tiếp, ghép các chữ liền nhau thành từ khóa đã biết ("ten diem", "cao do", "toa do x"…); chỉ dùng khi ghép xong đúng số ô | Tiêu đề "Tên điểm X Y Cao độ"; tiêu đề `;` giữ nguyên |
| KTP-24 (nốt) | `SKILL.md` còn "nhân 1000", "x_cad = Y × 1000" | "đổi theo đơn vị bản vẽ", "x_cad = Y × k (k = 1000 bản vẽ mm, 1 bản vẽ m)" | Test KTP-24 bắt thêm "nhân 1000" và mọi "×1000" trừ "× 1000 khi bản vẽ mm" |
| Cọc cuối | `Alignment.At` kẹp theo mét × đơn vị, có thể vượt chiều dài thật 1 ulp | Kẹp theo `LengthUnits` = chiều dài đo trên đường cong | Test nguồn; SelfTest H6 kiểm cọc cuối < 1 mm |
| KTP-47 b / c / d | Bàn giao ghi sai "SelfTest sẽ báo"; thiếu mục thử tay; thiếu quyết định KTP-12, 22, 26 | Sửa chữ ở §1; thêm E7 → E13 vào P2-H6 (`checklist-tong-the.md`) và `checklist-dot-h6.md`; thêm 3 dòng vào `dot-h-cho-duyet.md` | |

Sửa luôn câu của tớ ở `dot-h-cho-duyet.md` (KTP-47a, theo quyết định luồng chính): "không sao" gõ riêng là **không** đồng ý.

## 1c. Lượt 3

| Mã | Lỗi | Sửa | Test |
| --- | --- | --- | --- |
| KTP-50 | Không tiêu đề: dòng mẫu là dòng đầu có ≥ 2 ô, kể cả dòng chữ ("DANH SACH DIEM" → mất cao độ; "Toa do", "Ten diem", "Sodiem,2" → 0 điểm) | Dòng mẫu là dòng đầu có ≥ 2 ô đọc được số | 4 ca (Theory) |
| KTP-52 | "So hieu diem X Y Z" → 0 điểm | Thêm "so hieu diem"; ghép chữ bằng cách thử mọi cách ghép (từ khóa 2–3 chữ) và lấy cách ra đúng số ô | `KTP52_…` |
| KTP-53 | So số ô với một dòng số liệu: "Tên điểm X Y Z Mã" mà dòng đầu thiếu mã → lệch cột | So với dòng rộng nhất trong 5 dòng số liệu đầu | `KTP53_…` |
| KTP-54 (b) | `dot-h-cho-duyet.md` dòng KTP-12 ghi chưa đúng | Ghi rõ chỉ bỏ dòng một chữ không có dấu phẩy | |
| Ghi chú | `SKILL.md` §3–4 số ví dụ chỉ đúng bản vẽ mm | Thêm "(bản vẽ mm; bản vẽ m chia 1000)" | |

Checklist: thêm E14 vào P2-H6 và `checklist-dot-h6.md`; `dot-h-cho-duyet.md` thêm dòng KTP-50, KTP-52 / 53.

## 2. Gộp và tài liệu

- Gộp `full/nang-cap-h-sua-ktp-b` @ `bca2b36` (H7 / H8) rồi `full/nang-cap-h-sua-ktp-a` @ `06c3fb6` (H9, kèm báo cáo
  kiểm tra độc lập và tài liệu luồng chính): cả hai gộp sạch, không chồng nhau.
- `docs/checklist-tong-the.md`: chép lại phần H7 / H8 (bản nhánh B) vào khối "Đợt H phần 2"; thêm P2-0-6b và C3c (xác nhận gõ
  tay theo danh sách đóng: "ko đồng ý", "nếu", "thôi", "đợi" ra không; "Vâng, tớ đồng ý", "Thôi được, xóa đi" ra có).
- `docs/dot-h-cho-duyet.md` phần "Sau kiểm tra độc lập đợt H phần 2": quyết định tự chọn mới của A, B, C (KTP-21 móc 6,25d,
  bán kính co ống gió, nhịp tối thiểu, dòng Cộng theo loại thép, khe hở, layer `S-`, `under_beams`, H6 KTP-08 / 09 / 10,
  xác nhận gõ tay KTP-02 → 04, câu lỗi máy chủ KTP-07, ngôn ngữ lúc nạp KTP-05) và bảng **Con số mới chờ duyệt** (6,25d,
  100 mm, 1 × cạnh).
- KTP-36: dòng H9 ở §1 `test-tong-the-dot-h-phan-2.md` sửa lại (câu "nếu / if", "ko đồng ý" lúc đó chưa chặn, đã sửa ở nhánh A).

- **Lượt 2:** gộp `full/nang-cap-h-sua-ktp-b` @ `49701c1` (KTP-38, 40, 45, 46, 17, 29, KTP-20 25 / 30 mm) và
  `full/nang-cap-h-sua-ktp-a` @ `997bfd8` (KTP-04, 41 → 44, 47a). Chồng một chỗ ở `dot-h-cho-duyet.md` (dòng xác nhận gõ tay):
  lấy câu của A. Chép lại P2-H7 / P2-H8 từ `checklist-dot-h7.md` / `h8.md` (D10 → D12, "Sửa KTP"); thêm C3d (H9 lượt 3) và
  sửa P2-0-6b; `dot-h-cho-duyet.md` thêm KTP-44 và bảng "Con số đã duyệt sau kiểm tra độc lập" với KTP-20 **đã duyệt 27/11**.

## 3. Test / CI

- Sau khi gộp A + B: lõi **1708 / 1708**, trợ lý **467 / 467** (`# fail 0`). Build `net48`, `net8.0-windows`,
  `net10.0-windows`: 0 lỗi.
- CI 45863381169 trên `0d5da4c`: xanh, log trợ lý `# fail 0` (467), lõi `Failed: 0` (1708).
- Chưa thử trên AutoCAD thật.
- Lượt 2 sau khi gộp A + B: lõi **1716 / 1716**, trợ lý **467 / 467**; build 3 target 0 lỗi. CI: xem tin báo cáo.
