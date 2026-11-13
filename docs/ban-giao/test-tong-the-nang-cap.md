# Bàn giao: Test tổng thể chuỗi nâng cấp A → E

*13/11/2026 · thread Test tổng thể nâng cấp · nhánh `full/nang-cap-tong-the` (xuất phát `full/nang-cap-e-test` @ `cdaa29b`
+ tài liệu luồng chính `claude/inspiring-cray-58xsmt`; `full/test-tong-the` vẫn ở `19944df`, đã có sẵn). **Chưa thử trên
AutoCAD thật.** Thử tay: phần "chuỗi nâng cấp A → E" cuối [`checklist-tong-the.md`](../checklist-tong-the.md). Thread
này **giữ nhánh tích hợp** cho các bản sửa sau kiểm tra độc lập.*

## Lỗi tìm thấy → sửa

| Mã | Lỗi (người dùng sẽ thấy gì) | Sửa | Commit |
| --- | --- | --- | --- |
| TT2-1 | **Mái dốc cắt vào sàn mái.** Nhà dựng nhiều tầng có `roof_slab` (đợt A) rồi "lợp mái 4 dốc" (đợt E): mái tự lấy đỉnh tường làm chân mái, nhưng sàn mái nằm ngay trên đỉnh tường nên phần mái gần mép đua chồng lên sàn mái (bảng khối lượng đếm hai lần, mặt cắt thấy mái xuyên sàn). Mái bằng thì thành hai lớp sàn trùng nhau | Không có `wall_top_z`: tìm khối sàn trên `A-SLAB-3D` dưới đường bao có **mặt dưới nằm ở đỉnh tường (±1 mm)** → chân mái đặt lên mặt sàn mái, báo `wall_top_from` "top of the roof slab…". Mái bằng trên sàn mái: ghi chú cho Claude "lớp sàn thứ hai". Sàn giữa các tầng (dưới tường tầng trên) không bị nhận nhầm. Mô tả `build_roof` + skill `ve-3d-kien-truc` ghi luật này | `9f9910f` (`Core/StairRoof.cs` `RoofLogic.RoofBase`, `Drawing/StairRoofTools.cs`) |
| TT2-2 | **Câu "gói gồm / không gồm" trong system prompt chưa có lệnh của 5 đợt.** Máy gói 3D được nhờ "kéo giãn", "tìm & thay chữ", "ghi kích thước mặt bằng" thì Claude không biết đó là việc của gói 2D (lệnh bị ẩn, câu phạm vi không nhắc) nên dễ tìm cách làm vòng thay vì nói câu "Máy này dùng gói 3D…"; gói 2D tương tự với dầm / cầu thang / mái | Câu phạm vi gói 2D thêm nhóm / chép thuộc tính / bảng / tìm & thay / STRETCH, BREAK, LENGTHEN, DIVIDE; "không gồm" thêm dầm, cầu thang, mái, lan can, STL. Gói 3D thêm cột, dầm, cầu thang, mái, lan can, PRESSPULL, STL, số liệu khối lượng / va chạm (không bảng), nhóm / chép thuộc tính (không chữ); "không gồm" thêm 4 lệnh sửa hình mới, kích thước mặt bằng, bảng block / diện tích, tìm & thay | `9f9910f` (`agent/src/agent/systemPrompt.ts`) |
| TT2-3 | **`CLAUDECHATSELFTEST` không có bước đợt C** (A, B, D, E đều có): phần cần AutoCAD của tìm & thay (ghi TEXT / MTEXT) và bảng diện tích phòng chỉ được kiểm khi thử tay | Bước mới "Nâng cấp đợt C: tìm & thay chữ (TEXT + MTEXT, chỉ đổi khi đã duyệt), bảng diện tích phòng" ở x 32000 (không chồng chỗ bước khác): xem trước 2 đối tượng / 3 chỗ, thay khi chưa duyệt bị từ chối, thay sau duyệt đúng chữ, phòng 4 × 3 m = 12 m² có tên lấy từ chữ trong phòng. `FindReplaceText`, `AreaSchedule` đổi `private` → `internal` | `9f9910f` (`SelfTest.cs`, `Drawing/Util2DTools.cs`) |

## Đã rà theo chiều ngang, không phải lỗi

- **Nhãn gói (phương án A):** GROUP / MATCHPROP nhãn Common; STRETCH, BREAK, LENGTHEN, DIVIDE, `dim_floor_plan`,
  `block_schedule`, `area_schedule`, `find_replace_text` nhãn 2D; `draw_solid` + 6 bí danh, `build_railing`, `build_beam`,
  `presspull`, `export_stl`, `volume_schedule`, `build_stair`, `build_roof` nhãn 3D; `table_position` của
  `volume_schedule` / `check_interference` là tham số chữ (gói 3D: plugin chặn, trợ lý ẩn, chỉ trả số); MATCHPROP
  `text` / `dimension` chặn ở gói không 2D; `array_rect` `levels > 1` chặn ở gói 2D (cả batch, macro).
- **Thẻ xác nhận / giới hạn 20:** `stretch_entities` (handle và khung chọn: plugin nhận ngân sách còn lại của lượt),
  `break_entity`, `lengthen_entity`, `match_properties` tính vào 20; `find_replace_text` luôn qua thẻ (plugin chỉ thay
  handle trong `user_confirmed`, gõ "ok" không thay thẻ); `presspull` trên khối có trước luôn qua thẻ; lệnh dựng mới
  (cột, tầng, lan can, dầm, cầu thang, mái, bảng) không hỏi và khối mới tính là "đã tạo trong lượt".
- **Macro:** mọi tham số handle của lệnh plugin (`p.Handle` / `p.Handles`) đều có tên được `MacroLogic.IsHandleKey` ánh
  xạ (kiểm bằng grep toàn bộ `Drawing/*.cs`); `user_confirmed` và ngân sách khung chọn không bao giờ được ghi vào macro;
  `presspull` trên khối có sẵn chặn cả macro; `find_replace_text` bước thay trong macro bị plugin từ chối (không có
  duyệt), các bước khác vẫn chạy như luật xóa / phá khối cũ.
- **Bước tự kiểm tra:** 6 bước nâng cấp chạy nối tiếp trên cùng bản vẽ thử ở các vùng x tách nhau (đợt A 20000–29000,
  C 32000–36000, B 40000–40300, D 50000–71000, E 89500–110600; test khóa vị trí); bước A / D dựng 3D với
  `on_existing: add`.
- **Kịch bản chéo (đọc code, cần thử thật V1–V8):** nhà nhiều tầng + cột + dầm + cầu thang + mái → bảng khối lượng:
  thứ tự giữ thể tích cột → dầm → tường → sàn → còn lại (cầu thang, mái, lan can), mép bậc trên cùng gác lên sàn bị trừ
  vào cầu thang, mái không chồng tường (mặt dưới chạm đỉnh tường đúng ở mép ngoài), sau TT2-1 không chồng sàn mái;
  cầu thang `height` = cao tầng khớp sàn tầng trên của `build_3d_from_plan` (sàn nằm trên tường, đỉnh sàn ở bội số cao
  tầng). Mặt cắt / hình chiếu nhận mọi khối theo handle (tối đa 500). 2D: kích thước mặt bằng, bảng diện tích đọc hình
  hiện tại nên chạy sau kéo giãn / bẻ gãy là đúng hình mới.

## Token mô tả lệnh + system prompt (cùng cách đo với đợt A, ~3,5 ký tự / token)

Đo như plugin gửi: lệnh của gói (`plan_tools` theo nhãn `Core/Plans.cs`) + 5 lệnh thẻ, tên + mô tả + schema JSON
(`z.toJSONSchema`) + system prompt theo gói. Script chạy lại đủ ở cả hai đầu (số "trước" khớp từng ký tự với bảng của
`nang-cap-a.md`).

| Gói | Số lệnh trước → sau | Ký tự trước (`19944df`) | Ký tự sau (`9f9910f`) | ≈ token trước → sau | Tăng |
| --- | --- | --- | --- | --- | --- |
| 2D | 95 → 105 | 120 311 | 135 511 | 34,4k → 38,7k | **+4,3k** (+12,6 %) |
| 3D | 89 → 93 | 124 940 | 138 908 | 35,7k → 39,7k | **+4,0k** (+11,2 %) |
| Full | 124 → 136 | 173 852 | 201 310 | 49,7k → 57,5k | **+7,8k** (+15,8 %) |

- Riêng lượt này: system prompt 2D +167 ký tự, 3D +297 (TT2-2), mô tả `build_roof` +49 (TT2-1); ≈ +0,05k / +0,1k token.
- Theo đợt (từ các bàn giao): A ~0, B +2,2k (2D / Full) / +0,5k (3D), C +2,1k (2D / Full), D +1,9k (3D) / +2,1k (Full),
  E +1,4k (3D / Full).

## Test

- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests` — **861** test (856 trước). `TongTheNangCapTests.cs` (5): chân mái
  trên sàn mái / không sàn mái / sàn lệch 5 mm / lệch 0,5 mm; `build_roof` chỉ tìm sàn mái khi không có `wall_top_z`;
  bước tự kiểm tra có đủ đợt A–E, bước C đi qua duyệt; vị trí các bước không chồng nhau.
- Trợ lý: `cd agent && npm ci && npm test` — **291** test (287 trước). `test/tongthenangcap.test.ts` (4): câu phạm vi
  gói 3D / 2D nêu lệnh mới đúng phía có / không; Full không có danh sách; mô tả `build_roof` nêu sàn mái.
- Build net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`, xóa `obj` / `bin` giữa các
  lần): xanh, 0 cảnh báo.
- CI: run **43468951145 xanh** ở `4893283` (`release: "false"`).

## Giới hạn còn lại

- Chưa thử trên AutoCAD thật (cả chuỗi A → E). Phần chỉ AutoCAD kiểm được: S3 + V1–V8 của checklist tổng.
- TT2-1 chỉ nhận sàn mái trên `A-SLAB-3D` (layer mặc định); sàn mái dựng với `slab_layer_3d` khác thì Claude đưa
  `wall_top_z`. Nhà lệch tầng vẫn cần `wall_top_z` như trước.
- Macro có bước tìm & thay: bước thay luôn bị từ chối khi chạy lại (không có thẻ), các bước khác vẫn chạy (như xóa /
  phá khối có sẵn).
- `break_entity` không ghi các mảnh mới là "đã tạo trong lượt": sửa tiếp các mảnh đó trong cùng lượt bị tính như đối
  tượng có sẵn (hỏi sớm hơn, không phải lỏng hơn).
- Các giới hạn của từng đợt: xem `nang-cap-{a…e}.md` và `nang-cap-{a…e}-test.md`.

## Chờ chủ dự án

- Thử tay theo phần "chuỗi nâng cấp" của `checklist-tong-the.md` (rút gọn ~95 phút, đủ ~3 giờ).
- Xem 3 số đợt E tự chọn: bản thang dày **150**, khe giữa 2 vế chữ U **100**, bản mái dốc dày **150** (mm).

## CI

Run **43468951145 xanh** ở `4893283` (`release: "false"`, Windows: trợ lý, test lõi, UI test WPF, build 3 bản, gói +
Setup.exe; bước phát hành bỏ qua đúng). Sau đó một commit ghi kết quả CI vào file này (chỉ tài liệu).
