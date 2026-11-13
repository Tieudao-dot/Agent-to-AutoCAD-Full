# Bàn giao: Test & sửa Nâng cấp đợt D (3D)

*13/11/2026 · thread Test & sửa đợt D (con của Nâng cấp đợt D) · nhánh `full/nang-cap-d-test` (xuất phát `full/nang-cap-d`
@ `809ad78`). **Chưa thử trên AutoCAD thật.** Thử tay: mục **Q** của [`checklist-thu-that.md`](../checklist-thu-that.md)
(Q13–Q17 là các lỗi dưới đây).*

## Lỗi tìm thấy → sửa

| # | Lỗi (người dùng sẽ thấy gì) | Sửa | Ở đâu |
| --- | --- | --- | --- |
| ND-1 | **Dầm theo đường quay ngược lại không dựng được.** Đường đi tới rồi quay lại trên cùng một nét (góc quay 180°): miếng khép góc thành tam giác dẹt, AutoCAD không tạo được vùng nên cả lệnh `build_beam` báo lỗi | Góc quay ≥ 179,5° (`MaxFillerDeg`) không thêm miếng khép góc: hai đầu vuông của hai đoạn đã chồng lên nhau, không có khe | `Core/Solids3.cs` (`Beam`) |
| ND-2 | **PRESSPULL `mode` "subtract" / "add" mà không có `target` lặng lẽ dựng khối mới** thay vì khoét / thêm. `mode` "new" có `target` vẫn mở khối đích để ghi (dù không đổi, trợ lý không hỏi thẻ) | Không có `target` mà `mode` add / subtract: từ chối có lý do **trước khi** dựng gì. `mode` "new": không mở khối đích, kết quả ghi "<handle> was not changed". Mô tả lệnh trợ lý thêm "add / subtract need target" | `Drawing/Solid3Tools.cs` (`Presspull`), `agent/src/tools/solids3.ts` |
| ND-3 | **Macro có PRESSPULL trên khối có từ trước chạy mà không qua thẻ xác nhận** (luật 3D: đổi hình khối có sẵn chỉ mở bằng nút Đồng ý). Macro không có thẻ, nhưng `presspull` chưa nằm trong danh sách lệnh macro phải chặn | Thêm `presspull` vào `MacroLogic.AlwaysConfirmTools`; với `presspull` chỉ tính khối `handle` và `target` (trừ `mode` "new"), đường biên `profile_handles` chỉ được đọc nên không chặn (`SolidEditHandles`). Macro tự dựng khối rồi kéo vẫn chạy | `Core/Macros.cs` |
| ND-4 | **Thể tích va chạm sai ở bản vẽ đơn vị mét** (nhỏ đi 10⁹ lần): `check_interference` (cả `volume_m3`, `total_overlap_m3` và bảng va chạm mới) đổi m³ như bản vẽ mm | Đổi theo đơn vị bản vẽ (`Solid3Logic.M3(v, mmPerUnit)`, như `volume_schedule`); bản vẽ mm không đổi | `Drawing/Solid2Tools.cs`, `Core/Solids3.cs` (`ClashTable`) |
| ND-5 | **Bảng khối lượng nhận nhầm layer**: khóa 3 chữ khớp theo đầu từ nên "SANH" (sảnh) thành sàn, "COLOR" thành cột, "DAMPER" thành dầm → thứ tự trừ phần giao sai | Khóa ngắn (`COL`, `COT`, `DAM`, `SAN`) chỉ khớp cả từ, từ + số (`COT1`) hoặc + S (`COLS`); khóa dài (`COLUMN`, `BEAM`, `WALL`, `SLAB`, `FLOOR`, `TUONG`) vẫn khớp đầu từ | `Core/Solids3.cs` (`LayerRank`) |
| ND-6 | **`export_stl` nhận tên ".stl"** (file không có tên, dễ bị ẩn / ghi đè lẫn) | Báo lỗi "the file has no name before .stl" | `Solid3Tools.cs` (`ExportStl`) |

Thêm: **`CLAUDECHATSELFTEST` có bước đợt D** (gọi thẳng lệnh của plugin): dầm chữ L 220 × 400 (kiểm thể tích 0,616 m³),
¼ tường cong R3000–3200 (thể tích), kéo mặt trên hộp 1000 lên +200 (kiểm **hướng** của `ExtrudeFaces`: dương = ra ngoài,
thể tích 1,2 m³), xuất STL dầm (đếm tam giác trong file = số lệnh báo). `Presspull` / `ExportStl` đổi `private` → `internal`
để bước này gọi được.

Đã rà, không phải lỗi: thẻ xác nhận PRESSPULL (trợ lý; trong `batch_execute`; khối vừa dựng không hỏi; `mode` "new" không hỏi);
nhãn 4 lệnh mới ThreeD, `table_position` chặn ở gói 3D (plugin + macro + trợ lý ẩn tham số); hoàn tác (mọi lệnh ghi trong
transaction của lượt; `export_stl` bản sao tạm trong transaction bị hủy, không ghi bản vẽ); đường dẫn STL (tương đối → cạnh
bản vẽ, tên dành riêng / ký tự cấm, thư mục không có, là thư mục, ghi đè cần `overwrite`); dầm polyline mặt -Z (đảo dấu
bulge), cung ARC pháp tuyến bất kỳ (đọc 3 điểm), dầm đơn vị mét (bề rộng / cao / `top_z` chia hệ số); tường cong: cặp cung
có cung thứ ba ở giữa, khe qua 0°, đường bịt đầu, cửa cho theo điểm, kiểm dựng lại (hộp bao mảnh cong); bảng khối lượng:
không trừ quá thể tích khối, cặp không đo được cảnh báo. Đơn vị `distance` của PRESSPULL theo quy ước cũ của mọi lệnh 3D
(đơn vị bản vẽ = mm), không đổi.

## Test

- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests` — **675** test (651 trước). `NangCapDTestSuaTests.cs` (24): dầm quay 180°
  không có miếng dẹt, 170° vẫn khép góc, 179,9° bỏ miếng, vòng kín đi–về; PRESSPULL add / subtract không target bị từ chối
  trước khi dựng, "new" không mở target; macro: kéo mặt / khoét khối có sẵn bị chặn, khối mới từ đường biên có sẵn, `mode`
  "new", khối macro tự dựng thì chạy; bảng va chạm bản vẽ mét / mm, nguồn không còn `ToCubicMetres`; 15 tên layer
  (SANH, COLOR, DAMPER, COTE, COT1, COLS, COLUMNS, BEAMS, DAM2, TUONGXAY, SÀN 150…); tên STL; bước tự kiểm tra đợt D. Sửa 2 test cũ
  theo thay đổi có chủ đích: danh sách `AlwaysConfirmTools` thêm `presspull`, nguồn `ClashTable(…, mmPerUnit)`.
- Trợ lý: `cd agent && npm ci && npm test` — **281** test (279 trước). `test/nangcapdtest.test.ts` (2): mô tả nói add /
  subtract cần target; `mode` "new" có target không hỏi thẻ, add / subtract / auto có hỏi.
- Build net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`): xanh, 0 cảnh báo.
- CI: run **43454839792 xanh** ở `37f590c` (`release: "false"`, Windows: trợ lý, test lõi, UI test WPF, build 3 bản, gói + Setup.exe; bước phát hành bỏ qua đúng).

Token: chỉ thêm ~25 ký tự vào mô tả `presspull` (gói 3D / Full); system prompt không đổi.

## Còn phải thử tay (chỉ thấy trên AutoCAD thật)

Q1–Q12 của đợt D và Q13–Q17. Đặc biệt: hướng `ExtrudeFaces` (Q13 / Q5), `StlOut` (file tạm, bản sao dời / đổi tỉ lệ, Q8,
Q13), `Solid3d.Area`, region từ polyline có bulge, hợp khối các mảnh dầm / tường cong.

## Giới hạn còn lại (không sửa ở bước này)

- Như bàn giao đợt D: dầm một tiết diện, không tự cắt ở cột; tường cong chỉ cung tròn, khe cửa dưới ¼ vòng; ba khối chồng
  nhau ở một chỗ có thể trừ hai lần; STL một file, không chọn độ mịn.
- Tường cong một cung lớn (vd 300°) không có cặp ở phần còn lại: khe còn lại dưới ¼ vòng được báo `open_gaps` (để hở), có ký
  hiệu cửa ở đó thì thành cửa.
- `export_stl` có cả `handles` và `layers`: chỉ dùng `handles`.

## Commit

`37f590c` (sửa ND-1…ND-6 + bước tự kiểm tra + test + tài liệu, CI 43454839792 xanh), sau đó một commit ghi kết quả CI vào file này (chỉ tài liệu).
