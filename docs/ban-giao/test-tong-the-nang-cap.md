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

## Sau kiểm tra độc lập nâng cấp (KT2, `docs/ban-giao/kiem-tra-doc-lap-nang-cap.md` trên `full/kiem-tra-doc-lap-2`)

### Phần 1: lỗi giao thread này, sửa trên `full/nang-cap-tong-the`

| Mã | Lỗi | Sửa | Commit |
| --- | --- | --- | --- |
| KT2-01 (vừa) | Macro coi đối tượng **bị sửa** (kéo giãn theo khung, chép thuộc tính, kéo dài, dời…) là **do macro tạo**: lách được luật "macro không sửa khối 3D có sẵn" (kéo giãn khung trọn khối S rồi cắt S), và khi chạy lại, handle ghép nhầm với đối tượng khung chọn trúng ở bản vẽ mới | `MacroLogic.InPlaceTools` (lệnh sửa tại chỗ) + `NewHandles(call)` = kết quả trừ lệnh sửa tại chỗ và trừ handle chính lệnh đó nhận. Dùng cho `ExternalSolidEdits`, `ExternalHandles`, `CreatedHandles`; `Learn(map, call, replayed)` chỉ ghép handle do lệnh tạo (`ChatController` gọi bản mới) | `b9f7136` |
| KT2-20 | Dòng trạng thái hiện tên lệnh tiếng Anh thô cho 29 lệnh (B, C, D và 3D đợt 2–3) | Thêm nhãn tiếng Việt cho mọi lệnh có nhãn gói; test so `Plans.cs` với `ToolLabels.cs` | `b9f7136` |
| KT2-26 | Luật 3D chưa nêu `build_beam`, `build_stair`, `build_roof`, `presspull`, `volume_schedule`, `export_stl`; luật và skill bảo luôn `create_layer` trước dù các lệnh dựng tự tạo layer | Tiêu đề luật 3D nêu đủ; câu layer: lệnh dựng / mặt cắt tự tạo layer, `create_layer` chỉ cho khối tự dựng (`draw_solid`, `extrude`…); skill `ve-3d-kien-truc` như vậy | `b9f7136` |
| KT2-27 | Câu "gói 3D không gồm" thiếu wipeout / mây sửa đổi | Thêm "wipeouts and revision clouds" | `b9f7136` |
| KT2-29 | Ghi chú `draw_solid` ghi bớt "~2k token" | Đổi thành ~1,1k (số đo trong `nang-cap-a.md`) | `b9f7136` |
| KT2-31 | Đầu checklist tổng chỉ nêu khối cũ (~97 / ~157 phút); "các mục ★" của lượt rút gọn N–R là tập chọn lọc | Đầu file có bảng 2 khối + thời gian (cộng ~3 giờ 15 / ~5 giờ 40); ghi rõ "một số mục ★ chọn lọc", phần 16 liệt kê các ★ còn lại | `b9f7136` |

Test: lõi 866 (+5: `TongTheNangCapTests` KT2-01 ×3, KT2-20, KT2-29), trợ lý 293 (+2: KT2-26, KT2-27); build 3 bản xanh.

### Phần 2: gộp bản sửa của 5 đợt

Gộp vào `full/nang-cap-tong-the` (không xung đột) theo thứ tự A `75344e0`, B `7cf2873`, C `d14e8fc`, D `345c9ad`, E
`4a43452`. Sau gộp: lõi **908**, trợ lý **298** xanh; build 3 bản xanh 0 cảnh báo; CI run **43475785808 xanh** ở
`0153a1f` (`release: "false"`). Cả 31 lỗi + KT2-32 của lượt 2:

| Mã | Mức | Lỗi (tóm tắt) | Thread sửa | Commit |
| --- | --- | --- | --- | --- |
| KT2-01 | vừa | Macro coi đối tượng "bị sửa" là "do macro tạo". | Test tổng thể nâng cấp | `b9f7136` |
| KT2-02 | vừa | Tìm & thay hỏng cả lượt khi gặp chữ trên layer khóa, lỗi không nói chữ nào. | Đợt C | `d14e8fc` |
| KT2-03 | vừa | Kéo giãn theo khung chọn sửa cả đối tượng trên layer đang tắt. | Đợt B | `7cf2873` |
| KT2-04 | vừa | Tiện ích 2D trong bản vẽ đơn vị mét (INSUNITS = m) dùng số mm. | Đợt C | `d14e8fc` |
| KT2-05 | vừa | Kiểm tra "cầu thang vừa ô" bỏ sót phần lồi vào ô, cầu thang dựng xuyên cột / tường. | Đợt E | `4a43452` |
| KT2-06 | vừa | Xuất STL méo khi mô hình ở tọa độ lớn | Đợt D | `345c9ad` |
| KT2-07 | vừa | Hướng dẫn dầm dưới sàn mâu thuẫn với code. | Đợt D | `345c9ad` |
| KT2-08 | nhẹ | hai mái chồng nhau | Đợt E | `4a43452` |
| KT2-09 | nhẹ | (a) Lỗi mái 4 dốc trên đường bao lõm gợi ý "dùng type gable", nhưng gable cũng đòi hình chữ nhật nên bị từ chố… | Đợt E | `4a43452` |
| KT2-10 | nhẹ | `array_rect` `level_spacing` mô tả là mm nhưng plugin dùng thẳng đơn vị bản vẽ: bản vẽ mét, "3 tầng cột, cách … | Đợt A | `75344e0` |
| KT2-11 | nhẹ | Đường bao cột vẽ trùng hai lần → hai khối cột trùng nhau (khối lượng cột gấp đôi, nhấp nháy màn hình). Sàn có … (**chấp nhận**: phần "cân nhắc" — cột lồng trong cột khác — chưa làm, xem `nang-cap-a.md`) | Đợt A | `75344e0` |
| KT2-12 | nhẹ | mở | Đợt D | `345c9ad` |
| KT2-13 | nhẹ | PRESSPULL `distance` là đơn vị bản vẽ trong khi schema ghi mm; `build_beam` cùng đợt thì quy đổi. Bản vẽ mét: … | Đợt D | `345c9ad` |
| KT2-14 | nhẹ | Gói 3D: trợ lý vẫn chào `match_properties` `properties` "text" / "dimension" (plugin từ chối đúng) → Claude đư… | Đợt B | `7cf2873` |
| KT2-15 | nhẹ | vừa tạo trong lượt | Đợt B | `7cf2873` |
| KT2-16 | nhẹ | `plot_pdf` báo `exists: true` dù không ghi gì: file PDF cũ đang có sẵn (hoặc đang mở trong trình xem) thì kết … | Đợt C | `d14e8fc` |
| KT2-17 | nhẹ | In nhiều layout vào một PDF ép mọi trang cùng một khổ giấy mặc định (layout A1 và A3 trộn lẫn đều ra cùng khổ,… | Đợt C | `d14e8fc` |
| KT2-18 | nhẹ | `check_interference` với `handles: [A]`, `other_handles: [A]` (mọi cặp là tự so với mình) trả `interfere: fals… | Đợt D | `345c9ad` |
| KT2-19 | nhẹ | `build_beam` không có giới hạn tổng: 500 đường × 2000 đoạn → ~10⁶ phép extrude + cộng khối trên luồng chính, A… | Đợt D | `345c9ad` |
| KT2-20 | nhẹ | Dòng trạng thái khung chat hiện tên lệnh tiếng Anh thô cho các lệnh mới đợt B, C, D: `stretch_entities`, `brea… | Test tổng thể nâng cấp | `b9f7136` |
| KT2-21 | nhẹ | 1 tầng + `storey_height` + không có đường bao sàn: tường thấp đi 150 mm (3300 − 150) mà không có sàn ở trên; l… | Đợt A | `75344e0` |
| KT2-22 | nhẹ | Layer đoán là lan can: gợi ý dựng lan can ngay thay vì hỏi người dùng trước (**chấp nhận**: sửa ở mức lời dặn, trợ lý không chặn được bằng code) | Đợt A | `75344e0` |
| KT2-23 | nhẹ | mọi | Đợt B | `7cf2873` |
| KT2-24 | nhẹ | `lengthen_entity` trên LINE nghiêng / 3D chỉ tính trong mặt XY: đường nghiêng ra độ dài khác yêu cầu (kết quả … | Đợt B | `7cf2873` |
| KT2-25 | nhẹ | Schema `stretch_entities` `polygon` không có `.max(1000)` trong khi plugin giới hạn 1000 đỉnh | Đợt B | `7cf2873` |
| KT2-26 | nhẹ | System prompt 3D: tiêu đề `# 3D modelling (...)` và luật 3D chưa nhắc `build_beam`, `presspull`, `export_stl`,… | Test tổng thể nâng cấp | `b9f7136` |
| KT2-27 | nhẹ | Câu "gói 3D không gồm" trong system prompt thiếu wipeout / revision cloud (nhãn 2D trong `Plans.cs`) | Test tổng thể nâng cấp | `b9f7136` |
| KT2-28 | nhẹ | Mô tả `dim_floor_plan` `spacing` ghi "mặc định 8 mm × DIMSCALE" nhưng code lấy `spacing = offset` (truyền `off… | Đợt C | `d14e8fc` |
| KT2-29 | nhẹ | Mô tả `draw_solid` và tổng quan ghi gộp 6 lệnh "bớt ~2k token", số đo thật ~1,1k (`nang-cap-a.md:93`, `bo-cai/… | Test tổng thể nâng cấp | `b9f7136` |
| KT2-30 | nhẹ | Tài liệu tình hình cũ: `tong-quan-du-an.md:11, 14` còn nhánh tích hợp `full/test-tong-the` @ `19944df`, test 3… | Luồng chính | `69bf71c` (claude/inspiring-cray-58xsmt) |
| KT2-31 | nhẹ | Checklist tổng: đầu file (`:3-4`) và tổng quan `:22` chỉ nêu thời gian phần I–M, X (~97 / ~157 phút), không nê… | Test tổng thể nâng cấp (checklist) + luồng chính (tổng quan) | `b9f7136` + 69bf71c |
| KT2-32 | nhẹ | *(lượt 2 kiểm tra độc lập)* Tìm & thay chỉ kiểm layer khóa ở thuộc tính đầu của block; báo lỗi thiếu tên thuộc tính / layer | Đợt C | `5c244af` |

### Lượt 2 kiểm tra độc lập

29 lỗi sửa đúng, 2 chấp nhận (KT2-11 phần "cân nhắc", KT2-22 chỉ lời dặn), 1 lỗi mới nhẹ KT2-32 (đợt C, `5c244af`). Gộp
`full/nang-cap-c` @ `5c244af` vào `full/nang-cap-tong-the`: lõi **909**, trợ lý **298** xanh, build 3 bản xanh 0 cảnh báo, CI run **43478782014 xanh** ở `6b6c911` (`release: "false"`).

## Lỗi khi thử thật (chủ dự án, bộ cài thử nội bộ 0.9.4)

| Mã | Lỗi (người dùng thấy gì) | Sửa | Commit |
| --- | --- | --- | --- |
| TT-01 (14/11) | Chạy bộ cài khi AutoCAD đang mở (DLL plugin đang nạp) hoặc tiến trình phụ của plugin còn chạy: bước "Closing applications…" hiện hộp tiếng Anh của Inno "Setup was unable to automatically close all applications…" với 3 nút Try again / Ignore / Cancel, không biết phải làm gì | `CloseApplications=no` (bỏ Restart Manager). `PrepareToInstall` (sau xử lý KT-07): thấy `acad.exe` của người dùng này đang chạy → hộp tiếng Việt "AutoCAD đang mở. Hãy lưu bản vẽ và đóng hết AutoCAD, rồi bấm Thử lại." (Thử lại / Hủy, lặp tới khi đóng; không tự tắt AutoCAD; cài ngầm thì dừng có lý do); tắt `node.exe` / `claude.exe` **có đường dẫn trong `{app}`** (WMI `Win32_Process` + `taskkill /PID`); kiểm từng `.dll` / `.exe` của bundle mở ghi được (chờ tối đa 5 giây), còn bị giữ → dừng trước khi thay gì: "… Hãy khởi động lại máy rồi chạy lại bộ cài, trước khi mở AutoCAD." Hộp thử lại khi chép file (nếu vẫn gặp) đổi chữ tiếng Việt (`[Messages]` `AbortRetryIgnore*`). Luồng thay bản gốc + "Tiếp tục / Hủy" (KT-07) giữ nguyên. Test nguồn `Installer_handles_files_in_use_in_vietnamese`; thử tay V14 | `a4e8dd0` |

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
