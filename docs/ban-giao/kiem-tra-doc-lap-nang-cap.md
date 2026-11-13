# Bàn giao: Kiểm tra độc lập chuỗi nâng cấp A → E (lượt 1)

*13/11/2026 · thread "Kiểm tra độc lập nâng cấp" · nhánh `full/kiem-tra-doc-lap-2` (tách từ `full/nang-cap-tong-the` @
`f1e7123`). Chỉ ghi lỗi, **không sửa code sản phẩm**, không thêm test (các lỗi đều chỉ ra được bằng file / dòng + cách tái
hiện). Rà bằng đọc code end-to-end (4 tác vụ con song song: 3D A + E, 2D B + C, 3D D + chặn gói / macro / thẻ, tài liệu
↔ code), từng lỗi dưới đây đã được kiểm lại trực tiếp trong code. **Chưa thử trên AutoCAD thật.** Các lỗi đã ghi và sửa
trong bàn giao các đợt (NA-*, NB-*, NC-*, ND-*, NE-*, TT2-*) không ghi lại.*

## Kết quả chạy

| Việc | Kết quả |
| --- | --- |
| `dotnet test plugin/ClaudeCAD.Core.Tests` | **861/861** xanh |
| `cd agent && npm ci && npm test` | **291/291** xanh |
| Build plugin net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`, xóa `obj` / `bin` giữa các lần) | cả 3 xanh, 0 cảnh báo |
| CI run `43471931772` (workflow_dispatch, `release: "false"`, ở `f1e7123`) | **xanh** |

## Tổng hợp

| Phần phụ trách | Nặng | Vừa | Nhẹ | Mã |
| --- | --- | --- | --- | --- |
| Nâng cấp đợt A | 0 | 0 | 4 | KT2-10, 11, 21, 22 |
| Nâng cấp đợt B | 0 | 1 | 5 | KT2-03, 14, 15, 23, 24, 25 |
| Nâng cấp đợt C | 0 | 2 | 3 | KT2-02, 04, 16, 17, 28 |
| Nâng cấp đợt D | 0 | 2 | 4 | KT2-06, 07, 12, 13, 18, 19 |
| Nâng cấp đợt E | 0 | 1 | 2 | KT2-05, 08, 09 |
| Test tổng thể nâng cấp (lỗi chéo) | 0 | 1 | 2 | KT2-01, 20, 26 |
| 3D cũ / Gói dịch vụ (thread đã đóng → Test tổng thể) | 0 | 0 | 2 | KT2-27, 29 |
| Bộ cài / tài liệu (luồng chính) | 0 | 0 | 2 | KT2-30, 31 |
| **Cộng** | **0** | **7** | **24** | 31 lỗi |

Không thấy lỗi nặng (hỏng bản vẽ, mất dữ liệu, lọt gói ở đường thường). Chặn gói (lệnh lẻ, batch, macro), nhãn gói
theo phương án A, thẻ xác nhận PRESSPULL / tìm & thay, bí danh `draw_*` → `draw_solid`, ánh xạ handle macro, quy đổi
mét ở cầu thang / mái / lan can / dầm, cao độ tầng / sàn mái (TT2-1) đều khớp khi lần theo code.

## Lỗi mức vừa

| Mã | Mức | Lỗi (người dùng sẽ thấy gì) + cách tái hiện | File / dòng (`f1e7123`) | Phụ trách | Đề xuất sửa |
| --- | --- | --- | --- | --- | --- |
| KT2-01 | vừa | **Macro coi đối tượng "bị sửa" là "do macro tạo".** Kết quả `stretch_entities` (khung chọn), `match_properties`, `lengthen_entity` (và `move_entities` cũ) trả `handles` = các đối tượng **có sẵn** vừa bị sửa; macro ghi chúng vào `ResultHandles` và coi như tạo mới. Hệ quả: (a) luật KT-10 (macro không được sửa khối 3D có sẵn) bị lách: ghi macro "kéo giãn khung bao trọn khối S" rồi "cắt khối S" (đã duyệt thẻ lúc ghi) → chạy lại macro thì bước cắt chạy **không qua thẻ**; (b) khi chạy lại, `Learn` ghép handle theo thứ tự với đối tượng khung chọn trúng ở bản vẽ mới, nên bước sau nhắc tới handle đó có thể sửa nhầm đối tượng khác thay vì báo "không thấy" | `Core/Macros.cs:98-103` (`ResultHandles`), `:171`, `:215`; `ChatController.cs:1433, 1453, 1517, 1543`; `Drawing/Edit2DTools.cs:90`, `:409`; `Drawing/ModifyTools.cs:47` | Test tổng thể nâng cấp | Chỉ lệnh **tạo** mới ghi `ResultHandles` (danh sách lệnh tạo trong `MacroLogic`), hoặc lệnh sửa trả khóa khác (`moved` / `stretched` / `changed`), không dùng `handles`. Thêm test: macro stretch + `slice_solid` trên khối có sẵn phải bị chặn |
| KT2-02 | vừa | **Tìm & thay hỏng cả lượt khi gặp chữ trên layer khóa, lỗi không nói chữ nào.** Bấm Đồng ý trên thẻ 40 chữ, một chữ nằm trên layer khung tên đang khóa → không chữ nào được thay, lỗi chung "đối tượng trên layer khóa" không có handle / layer; Claude chỉ thử lại mù. Nguyên nhân: `GetObject(..., ForWrite)` chạy **trước** `EnsureLayerUnlocked` nên AutoCAD ném eOnLockedLayer trước, kiểm tra thân thiện không bao giờ chạy. Bước xem trước cũng không đánh dấu chữ trên layer khóa | `Drawing/Util2DTools.cs:482-484` | Nâng cấp đợt C | Mở ForRead → `EnsureLayerUnlocked` → `UpgradeOpen`; bước xem trước để chữ trên layer khóa ra danh sách `locked` (không đưa vào thẻ) |
| KT2-03 | vừa | **Kéo giãn theo khung chọn sửa cả đối tượng trên layer đang tắt.** Layer tắt (đường dựng hình, nội thất tắt) vẫn bị kéo / dời mà người dùng không thấy, và còn tính vào giới hạn 20. AutoCAD thật không chọn đối tượng layer tắt bằng khung | `Drawing/Edit2DTools.cs:54-56` (chỉ xét `IsFrozen`, `IsLocked`; so với `Util2DTools.cs` `Pick()` ~dòng 62 có xét `IsOff`) | Nâng cấp đợt B | Bỏ qua `ltr.IsOff` như layer đóng băng (hoặc liệt kê riêng như layer khóa) |
| KT2-04 | vừa | **Tiện ích 2D trong bản vẽ đơn vị mét (INSUNITS = m) dùng số mm.** `dim_floor_plan`: khoảng cách đường kích thước mặc định max(8 × DIMSCALE, 5 % mặt bằng) → nhà 10 m có các hàng kích thước ở 8 m, 16 m, 24 m ra ngoài. Bảng thống kê block / diện tích: chữ cao 2,5 × DIMSCALE = **2,5 m**. `area_schedule`: `min_area * 1e6` nên `min_area: 1` bỏ hết phòng ("không có vùng kín"); `area_m2` / `total_m2` nhỏ 10⁶ lần (phần m² đã ghi là giới hạn, phần khoảng cách / chữ chưa). Chỉ NC-5 (dung sai) đã sửa | `Drawing/Util2DTools.cs:79, 107-108, 270, 324`; `Core/Util2D.cs:180` (`DefaultDimOffset`) | Nâng cấp đợt C | Quy đổi theo `FloorPlanLogic.MmPerUnit` như `volume_schedule` (`Solid3Logic.M2(a, mmPerUnit)`); `Util2DLogic.M2` / `AreaTable` đã có tham số `unitToMm` nhưng chưa được truyền |
| KT2-05 | vừa | **Kiểm tra "cầu thang vừa ô" bỏ sót phần lồi vào ô, cầu thang dựng xuyên cột / tường.** Mỗi hình chữ nhật vế / chiếu nghỉ chỉ thử 16 điểm (k/4 trên mỗi cạnh), không xét đỉnh của ô cầu thang nằm trong hình chữ nhật. Tái hiện: ô 1200 × 5000 có chỗ lõm 200 × 200 ở x 600–800 trên cạnh y = 0; `build_stair` `start` [500,0], hướng 0, cao 3000 → điểm thử ở x 0 / 1250 / 2500 / 3750 nên không thấy chỗ lõm, báo "vừa" và dựng xuyên | `Core/StairRoof.cs:440-452` (`StairLogic.Outside`), `FitInWell` ~dòng 480 dùng chung | Nâng cấp đợt E | Thêm: đỉnh ô nằm hẳn trong hình chữ nhật → không vừa; cạnh ô cắt cạnh hình chữ nhật (như `RoofLogic.SegCross`) → không vừa |
| KT2-06 | vừa | **Xuất STL méo khi mô hình ở tọa độ lớn** (bản vẽ theo tọa độ khảo sát / VN-2000, mm: X ≈ 5,8·10⁸, Y ≈ 2,3·10⁹). STL lưu số thực 32 bit; `PositiveShift` chỉ dời trục có giá trị âm nên mô hình ở xa phía dương ghi nguyên tọa độ → đỉnh bị làm tròn theo lưới 64–256 mm, mô hình răng cưa / vỡ trong phần mềm in 3D | `Core/Solids3.cs:741-742`; `Drawing/Solid3Tools.cs:366-368` | Nâng cấp đợt D | Luôn dời góc nhỏ nhất của hộp bao về 0 (hoặc khi \|tọa độ\| > ~10⁶ mm), báo `moved_by_mm` |
| KT2-07 | vừa | **Hướng dẫn dầm dưới sàn mâu thuẫn với code.** Skill ghi "`top_z` = cao độ mặt sàn **+** `slab_thickness`" nhưng code lấy đỉnh dầm = `top_z` **−** `slab_thickness` (ví dụ trong skill thì đúng). Claude làm theo câu chữ (top_z 3450, slab 150) → dầm chồng vào sàn. Checklist V2 cũng ghi "dầm đỉnh ở 3300" cho dầm dưới sàn tầng 2 (sàn 150), đúng phải là đỉnh 3150, đáy 2750; người thử tay sẽ báo lỗi nhầm | `skills/ve-3d-kien-truc/SKILL.md:133`; `docs/checklist-tong-the.md:187` (V2), xem lại V3 `:188`; code `Core/Solids3.cs:296` | Nâng cấp đợt D | Skill: "`top_z` = cao độ mặt sàn tầng trên; đỉnh dầm = `top_z` − `slab_thickness`". V2: "dầm đỉnh 3150, đáy 2750, chạm đáy sàn" |

## Lỗi mức nhẹ

| Mã | Mức | Lỗi + cách tái hiện | File / dòng | Phụ trách | Đề xuất sửa |
| --- | --- | --- | --- | --- | --- |
| KT2-08 | nhẹ | Gợi ý cho nhà chữ L / T "chia thành hình chữ nhật, lợp từng phần" tạo **hai mái chồng nhau**: mỗi mái đua 600 mm qua cả đường chia và dốc xuống cánh kia; `roof_area_m2` và khối lượng tính hai lần | `Core/StairRoof.cs:749`; `skills/ve-3d-kien-truc/SKILL.md:149, 183`; `RoofLogic.Build` (`Offset(pts, oh)`) | Nâng cấp đợt E | Ít nhất: dặn Claude báo người dùng mái chồng ở chỗ nối. Tốt hơn: đua theo cạnh (0 ở cạnh chung). Xem điểm cần quyết 1 |
| KT2-09 | nhẹ | (a) Lỗi mái 4 dốc trên đường bao lõm gợi ý "dùng type gable", nhưng gable cũng đòi hình chữ nhật nên bị từ chối tiếp, tốn một lượt. (b) `landing` schema cho 500–10000 nhưng plugin đòi ≥ min(width, 600): với vế 1000, `landing` 500–599 qua trợ lý rồi bị plugin từ chối | (a) `Core/StairRoof.cs:749`, `:751`; (b) `agent/src/tools/solids3.ts:153`, `Core/StairRoof.cs:205` | Nâng cấp đợt E | (a) gợi ý "type flat"; (b) schema min 600 hoặc plugin nhận 500 |
| KT2-10 | nhẹ | `array_rect` `level_spacing` mô tả là mm nhưng plugin dùng thẳng đơn vị bản vẽ: bản vẽ mét, "3 tầng cột, cách 3300" → các bản sao cách nhau 3,3 km (các lệnh 3D khác cùng luồng thì có quy đổi) | `Drawing/ModifyTools.cs:149, 164`; `agent/src/tools/modify.ts:50` | Nâng cấp đợt A | Chia theo `MmPerUnit`, hoặc mô tả "drawing units" (cả `row_spacing` / `col_spacing`) |
| KT2-11 | nhẹ | Đường bao cột vẽ trùng hai lần → hai khối cột trùng nhau (khối lượng cột gấp đôi, nhấp nháy màn hình). Sàn có lọc trùng (`SameRegion`), cột thì không | `Drawing/PlanTo3dTools.cs:926-944` (`BuildColumns`) | Nâng cấp đợt A | Lọc trùng như `BuildSlabs`; cân nhắc bỏ đường bao nằm hẳn trong đường bao khác |
| KT2-12 | nhẹ | Dầm vòng theo polyline **mở** quay về điểm đầu (vẽ PLINE về điểm đầu, không "Close"): góc đầu thiếu một ô w/2 × w/2 (220 × 400: hụt 110 × 110 mm, 0,0048 m³). `path_points` đầu = cuối không có `closed` cũng vậy | `Drawing/Solid3Tools.cs:87, 150`; `Core/Solids3.cs:212-216` | Nâng cấp đợt D | Coi đầu = cuối (trong dung sai) là khép, như `StairRoofTools.cs:91`, `PlanTo3dTools.cs:309`, NC-6 |
| KT2-13 | nhẹ | PRESSPULL `distance` là đơn vị bản vẽ trong khi schema ghi mm; `build_beam` cùng đợt thì quy đổi. Bản vẽ mét: "kéo lên 200 mm" thành 200 m | `Drawing/Solid3Tools.cs:217, 228, 257`; `agent/src/tools/solids3.ts` (presspull `distance`) | Nâng cấp đợt D | Chia theo hệ số đơn vị, hoặc ghi "drawing units" trong schema |
| KT2-14 | nhẹ | Gói 3D: trợ lý vẫn chào `match_properties` `properties` "text" / "dimension" (plugin từ chối đúng) → Claude được mời gọi một lệnh chắc chắn bị chặn | `agent/src/tools/index.ts:58-67` (`PLAN_HIDDEN_PARAMS` / `trimForPlan`) | Nâng cấp đợt B | Gói không 2D: bỏ 2 giá trị enum và câu mô tả tương ứng |
| KT2-15 | nhẹ | Kéo giãn theo khung tính cả đối tượng **vừa tạo trong lượt** vào giới hạn 20: vẽ 25 đường rồi kéo giãn bằng khung → plugin từ chối "hơn 20"; Claude phải gọi lại theo handle (tốn lượt, có thể hỏi người dùng không cần thiết) | `Drawing/Edit2DTools.cs:73-79` | Nâng cấp đợt B | Trợ lý truyền danh sách handle đã tạo (hoặc ngân sách đã trừ chúng), plugin không tính chúng |
| KT2-16 | nhẹ | `plot_pdf` báo `exists: true` dù không ghi gì: file PDF cũ đang có sẵn (hoặc đang mở trong trình xem) thì kết quả vẫn "có file", Claude báo đã in xong | `Drawing/LayoutTools.cs:359` | Nâng cấp đợt C | Ghi thời điểm sửa file trước khi in rồi so; file đang bị khóa thì báo "file đang mở" |
| KT2-17 | nhẹ | In nhiều layout vào một PDF ép mọi trang cùng một khổ giấy mặc định (layout A1 và A3 trộn lẫn đều ra cùng khổ, thu nhỏ cho vừa) | `Drawing/LayoutTools.cs:294` (`media` mặc định đè thiết lập trang của từng layout) | Nâng cấp đợt C | Không có `media` thì giữ `CanonicalMediaName` của layout khi máy in PDF hỗ trợ |
| KT2-18 | nhẹ | `check_interference` với `handles: [A]`, `other_handles: [A]` (mọi cặp là tự so với mình) trả `interfere: false, pairs_checked: 0`, kèm `table_position` thì báo "không va chạm" → Claude nói "không có va chạm" dù chưa so cặp nào | `Drawing/Solid2Tools.cs:834`; `Core/Solids2.cs:583` | Nâng cấp đợt D (phần bảng); kiểm cũ của 3D đợt 2 | Từ chối khi số cặp = 0 ("cần hai khối khác nhau") |
| KT2-19 | nhẹ | `build_beam` không có giới hạn tổng: 500 đường × 2000 đoạn → ~10⁶ phép extrude + cộng khối trên luồng chính, AutoCAD treo rất lâu | `Drawing/Solid3Tools.cs:114-130, 157, 165` | Nâng cấp đợt D | Giới hạn tổng số đoạn + mối nối mỗi lần gọi (vd 5000), từ chối trước khi dựng |
| KT2-20 | nhẹ | Dòng trạng thái khung chat hiện tên lệnh tiếng Anh thô cho các lệnh mới đợt B, C, D: `stretch_entities`, `break_entity`, `lengthen_entity`, `divide_entity`, `group_entities`, `match_properties`, `dim_floor_plan`, `block_schedule`, `area_schedule`, `find_replace_text`, `build_beam`, `presspull`, `export_stl`, `volume_schedule` (A, E có nhãn). Một số lệnh 3D cũ (`check_interference`, `sweep`, `loft`, `section_view`…) cũng thiếu từ trước | `UI/ToolLabels.cs:8-117` | Test tổng thể nâng cấp | Thêm nhãn tiếng Việt |
| KT2-21 | nhẹ | 1 tầng + `storey_height` + không có đường bao sàn: tường thấp đi 150 mm (3300 − 150) mà không có sàn ở trên; lượt sau dựng tầng 2 `base_z` 3300 (cũng không sàn) để hở 150 mm. Luật "nhiều tầng không sàn thì hỏi trước" chỉ áp khi ≥ 2 tầng | `Core/FloorPlan.cs` ~354 (`Storeys` / `StoreysWithoutSlab`) | Nâng cấp đợt A | 1 tầng không dựng sàn thì tường = `storey_height` (như `slab: none`), hoặc ghi chú hỏi trước |
| KT2-22 | nhẹ | Layer **đoán** là lan can (LANCAN, RAIL…): ghi chú gợi ý "dùng build_railing path_handle" nhưng `build_railing` không qua cổng hỏi lại layer (cổng chỉ phủ `build_3d_from_plan`) → Claude có thể dựng lan can theo layer đoán mà không hỏi (chỉ thêm khối mới, U được) | `Drawing/PlanTo3dTools.cs:1079` (`LayerGuesses`); `agent/src/agent/confirmation.ts` (`PLAN_LAYER_PARAMS`) | Nâng cấp đợt A | Đưa lan can vào danh sách "không dựng theo layer đoán" của ghi chú, hoặc bỏ gợi ý "dùng" |
| KT2-23 | nhẹ | Bỏ nhóm theo handle xóa **mọi** nhóm chứa các đối tượng đó, không đếm, không hỏi | `Drawing/Edit2DTools.cs:317-336` | Nâng cấp đợt B | Trả tên các nhóm đã bỏ; > 1 nhóm thì cần `name` |
| KT2-24 | nhẹ | `lengthen_entity` trên LINE nghiêng / 3D chỉ tính trong mặt XY: đường nghiêng ra độ dài khác yêu cầu (kết quả có báo độ dài thật); đường thẳng đứng báo "đoạn cuối không có độ dài" | `Drawing/Edit2DTools.cs:174-180`; `Core/Edit2D.cs` `LengthenSegment` | Nâng cấp đợt B | Dùng hướng 3D cho LINE |
| KT2-25 | nhẹ | Schema `stretch_entities` `polygon` không có `.max(1000)` trong khi plugin giới hạn 1000 đỉnh | `agent/src/tools/modify.ts` (`points(3, …)`); `Drawing/Edit2DTools.cs:33` | Nâng cấp đợt B | Thêm `.max(1000)` |
| KT2-26 | nhẹ | System prompt 3D: tiêu đề `# 3D modelling (...)` và luật 3D chưa nhắc `build_beam`, `presspull`, `export_stl`, `volume_schedule`, `build_stair`, `build_roof`; dòng `:110` và skill (`SKILL.md:81`) vẫn bảo "tạo layer bằng `create_layer` trước khi dựng" trong khi cột / dầm / cầu thang / mái / lan can tự tạo layer (thừa lượt gọi) | `agent/src/agent/systemPrompt.ts:106, 110`; `skills/ve-3d-kien-truc/SKILL.md:81` | Test tổng thể nâng cấp | Thêm tên lệnh vào tiêu đề + 1 dòng luật; chỉ giữ `create_layer` cho cột dựng tay bằng `draw_solid` / `extrude` |
| KT2-27 | nhẹ | Câu "gói 3D không gồm" trong system prompt thiếu wipeout / revision cloud (nhãn 2D trong `Plans.cs`) | `agent/src/agent/systemPrompt.ts:100`; `Core/Plans.cs` (`draw_wipeout`, `draw_revcloud`) | Gói dịch vụ (đã có từ trước) → Test tổng thể nâng cấp | Thêm vào danh sách "không gồm" |
| KT2-28 | nhẹ | Mô tả `dim_floor_plan` `spacing` ghi "mặc định 8 mm × DIMSCALE" nhưng code lấy `spacing = offset` (truyền `offset: 500` thì khoảng hàng là 500) | `agent/src/tools/utility2d.ts:22`; `Drawing/Util2DTools.cs:79-80` | Nâng cấp đợt C | Sửa mô tả: "mặc định = offset" |
| KT2-29 | nhẹ | Mô tả `draw_solid` và tổng quan ghi gộp 6 lệnh "bớt ~2k token", số đo thật ~1,1k (`nang-cap-a.md:93`, `bo-cai/README.md:114`); tổng quan `:85` còn ghi như việc sắp làm | `agent/src/tools/solids.ts:22`; `docs/tong-quan-du-an.md:85` | Test tổng thể nâng cấp (mô tả lệnh) + luồng chính (tổng quan) | Đổi thành ~1,1k, ghi "đã làm" |
| KT2-30 | nhẹ | Tài liệu tình hình cũ: `tong-quan-du-an.md:11, 14` còn nhánh tích hợp `full/test-tong-the` @ `19944df`, test 369 / 215, CI 43272295317; `:31` "Đợt E đang chạy"; `:38-40`, `:102` thiếu thread chuỗi nâng cấp và S3 / V1–V8. `ke-hoach-nang-cap-lenh.md:81-82`: dòng E "Đang test & sửa", dòng kiểm tra độc lập / test tổng thể trống | `docs/tong-quan-du-an.md`, `docs/ke-hoach-nang-cap-lenh.md` | Bộ cài / tài liệu (luồng chính) | Cập nhật nhánh `full/nang-cap-tong-the` @ `f1e7123`, 861 / 291, CI 43468951145; đánh dấu E, test tổng thể xong; kiểm tra độc lập đang làm |
| KT2-31 | nhẹ | Checklist tổng: đầu file (`:3-4`) và tổng quan `:22` chỉ nêu thời gian phần I–M, X (~97 / ~157 phút), không nêu khối "chuỗi nâng cấp A → E" (~95 phút / ~3 giờ). Danh sách ★ của phần 11–15 (N–R) là tập con (thiếu N1, N2, N17, O4, O6, O11–O13, P5, P6, P14, P17, Q4, Q6, Q9, Q10, Q13, R2, R6, R8, R10, R13, R15) mà câu "rút gọn = các mục ★" dễ hiểu là tất cả | `docs/checklist-tong-the.md:3-4`, phần 11–16; `docs/tong-quan-du-an.md:22` | Bộ cài / tài liệu (luồng chính; checklist do Test tổng thể nâng cấp viết) | Nêu thêm khối A → E và thời gian; ghi "các mục ★ chọn lọc" hoặc đưa đủ ★ vào phần rút gọn |

## Cần thử trên AutoCAD thật (đọc code không kết luận được)

- Tìm & thay trên TEXT / MTEXT / ô bảng có **field** (diện tích, ngày, tên file) hoặc công thức: ghi lại chữ đã tính có
  thể làm mất liên kết field (`Util2DTools.cs` `WriteText` ~509). Nếu mất: xem trước nên bỏ qua / đánh dấu (`HasFields`).
- `break_entity` trên polyline **khép** khi đoạn bỏ đi vắt qua đỉnh đầu: tùy `GetSplitCurves` trả 2 hay 3 mảnh, có thể
  còn sót một phần đoạn cần bỏ.
- `export_stl` khi khối nằm trên layer khóa: bản sao được thêm vào model space rồi mới `TransformBy`, có thể bị
  eOnLockedLayer; việc thêm rồi hủy có thể làm bản vẽ bị đánh dấu "đã sửa" (hỏi lưu khi đóng) dù chỉ xuất file
  (`Solid3Tools.cs:379-386`).

## Điểm cần chủ dự án quyết (lời thường)

1. **Mái nhà chữ L / chữ T.** Hiện lệnh lợp mái dốc chỉ làm được nhà hình chữ nhật; nhà chữ L thì Claude chia thành hai
   hình chữ nhật và lợp từng phần, nhưng hai mái sẽ **chồng lên nhau ở chỗ nối** (nhìn thấy mái này đâm vào mái kia,
   bảng khối lượng tính mái hai lần).
   - Cách 1 (đề xuất, rẻ, làm ngay): Claude báo trước cho người dùng "chỗ nối hai mái sẽ chồng, cần sửa tay" — chỉ đổi
     lời hướng dẫn, gần như không tốn.
   - Cách 2: thêm lựa chọn "không đua mái ở cạnh chung" để hai mái khớp nhau hơn — ước thêm một ít việc trong đợt sửa;
     chỗ nối vẫn chưa thành mái liền đẹp như vẽ tay.
   - Cách 3: làm mái liền cho nhà chữ L / T đúng nghĩa — việc lớn, nên để sau phát hành (đợt F).
2. **Bản vẽ đơn vị mét.** Lệnh 3D đã tự đổi mm ↔ m, nhưng nhiều lệnh 2D mới (ghi kích thước mặt bằng, bảng diện tích,
   bảng thống kê) và vài số của lệnh 3D (khoảng cách tầng khi nhân bản, PRESSPULL) vẫn hiểu là mm. Khách vẽ bằng mét
   sẽ thấy kích thước đặt rất xa, chữ trong bảng cao 2,5 m, diện tích sai.
   - Cách 1 (đề xuất): sửa cho các lệnh này tự đổi đơn vị như lệnh 3D (KT2-04, 10, 13) — thuộc đợt sửa lỗi, tốn vừa phải.
   - Cách 2: ghi rõ "bản vẽ phải dùng mm" trong hướng dẫn, Claude nhắc khách khi thấy bản vẽ mét — rẻ hơn nhưng khách
     vẽ mét phải tự đổi.

## CI

Run `43471931772` (workflow_dispatch, `release: "false"`, ở `f1e7123` = nhánh lúc tách, chưa có file này): **xanh** — trợ lý, test lõi, UI test WPF, build 3 bản, gói + Setup.exe; bước phát hành bỏ qua đúng (không cài secret).

## Lượt 2: soát lại các lỗi đã sửa (13/11/2026)

Gộp `origin/full/nang-cap-tong-the` @ `4487514` (code ở `0153a1f`, bản sửa của 6 thread) vào `full/kiem-tra-doc-lap-2`.
Soát bằng đọc diff `f1e7123..4487514` (23 file code / skill) theo từng mã, đối chiếu bảng lỗi → thread → commit trong
`test-tong-the-nang-cap.md`. Theo 2 hướng luồng chính đã chọn: bản vẽ mét → các lệnh tự quy đổi; mái chữ L / T → Claude
báo trước chỗ chồng, mái liền để đợt F. Không chạy CI (nhánh tích hợp vừa xanh, run 43475785808).

| Việc | Kết quả |
| --- | --- |
| `dotnet test plugin/ClaudeCAD.Core.Tests` | **908/908** xanh |
| `cd agent && npm ci && npm test` | **298/298** xanh |
| Build net48 / net8.0-windows / net10.0-windows (xóa `obj` / `bin` giữa các lần) | cả 3 xanh, 0 cảnh báo |

**Tổng: 29 sửa đúng, 2 chấp nhận (KT2-11 phần "cân nhắc", KT2-22), 0 chưa đủ; 1 lỗi mới nhẹ (KT2-32), 1 điểm cần thử
trên AutoCAD thật.**

| Mã | Kết luận | Ghi chú soát |
| --- | --- | --- |
| KT2-01 | sửa đúng | `MacroLogic.InPlaceTools` + `NewHandles` (trừ lệnh sửa tại chỗ và handle chính lệnh nhận) dùng cho cả `ExternalSolidEdits`, `ExternalHandles`, `CreatedHandles`, `Learn`; macro "kéo giãn trọn khối S rồi cắt S" giờ bị chặn. Đã rà các lệnh mới trả `handles`: tìm & thay, PRESSPULL không trả handle có sẵn; cộng / trừ / cắt khối trả handle khối gốc (là handle đã nhận → bị trừ đúng) |
| KT2-02 | sửa đúng (trừ KT2-32) | Xem trước để chữ trên layer khóa ra `locked`, không vào thẻ; bước thay đọc hết trước, từ chối có handle + layer trước khi đổi gì |
| KT2-03 | sửa đúng | Layer tắt / đóng băng không bị khung chọn, đếm `hidden_layer_skipped` |
| KT2-04 | sửa đúng | Khoảng cách kích thước, cỡ chữ bảng, `min_area`, `area_m2` / `total_m2`, bảng diện tích đều quy đổi theo INSUNITS |
| KT2-05 | sửa đúng | `WellCutsInto`: đỉnh ô nằm hẳn trong phần thang, hoặc cạnh ô cắt cạnh phần thang (thu vào `tol`) → không vừa; ví dụ chỗ lõm 200 × 200 của lượt 1 bị bắt |
| KT2-06 | sửa đúng | `StlShift`: tọa độ > 10⁶ mm hoặc âm → dời góc nhỏ nhất về 0, đi đường bản sao; báo `moved_by_mm` |
| KT2-07 | sửa đúng | Skill `:133-135` (đỉnh dầm = `top_z` − `slab_thickness`), checklist V2 "đỉnh 3150, đáy 2750" |
| KT2-08 | sửa đúng (theo hướng đã chọn) | Lỗi mái 4 dốc / 2 dốc và mô tả `build_roof` dặn Claude báo trước chỗ chồng và bảng khối lượng tính hai lần |
| KT2-09 | sửa đúng | (a) gợi ý "type flat"; (b) `MinLanding` 600 chung cho plugin và schema. Hệ quả nhỏ: vế rộng 500–599 mà Claude truyền `landing` = vế thì bị từ chối (trước đây nhận); không truyền `landing` thì vẫn mặc định = vế, nên chấp nhận được |
| KT2-10 | sửa đúng | `level_spacing` quy đổi theo INSUNITS; `row_spacing` / `col_spacing` giữ đơn vị bản vẽ như mọi lệnh 2D, mô tả ghi rõ |
| KT2-11 | sửa đúng + **chấp nhận** phần "cân nhắc" | Hai đường bao trùng → một cột (cùng hộp bao + cùng diện tích). Đường bao nằm trong đường bao khác chưa lọc: hiếm (đường hoàn thiện quanh cột kết cấu), dễ nhầm cột thật nhỏ đặt sát; để nguyên là hợp lý |
| KT2-12 | sửa đúng | Đường mở có điểm cuối trùng điểm đầu (dung sai tương đối) thành vòng khép, có ô góc |
| KT2-13 | sửa đúng | PRESSPULL `distance` chia hệ số mm / đơn vị |
| KT2-14 | sửa đúng | Gói không 2D: enum `properties` chỉ còn thuộc tính chung, mô tả bỏ câu text / dimension |
| KT2-15 | sửa đúng | Plugin không tính đối tượng tạo trong lượt (`ctx.CreatedThisTurn`) vào giới hạn 20 của khung chọn |
| KT2-16 | sửa đúng | Kiểm file đang mở (khóa ghi) trước khi in, so thời điểm sửa sau khi in; `exists` chỉ true khi file mới được ghi, kèm `warning` |
| KT2-17 | sửa đúng, **cần thử thật** | Mỗi trang giữ khổ giấy của layout khi máy in PDF có khổ đó. Cần thử: `SetPlotConfigurationName(ps, device, null)` (khổ trống) có được AutoCAD nhận không (chỉ bắt lỗi AutoCAD, lỗi tham số trống sẽ làm hỏng cả lệnh in). Hệ quả: in **Model** không chỉ định khổ giờ theo khổ đang đặt cho Model (bản vẽ từ mẫu có thể là Letter / A4) thay vì A3 như trước; mô tả lệnh đã ghi |
| KT2-18 | sửa đúng | Không có cặp nào → từ chối "cần hai khối khác nhau" |
| KT2-19 | sửa đúng | Tổng ≤ 5000 đoạn mỗi lần gọi, kiểm trước khi dựng |
| KT2-20 | sửa đúng | Nhãn tiếng Việt cho mọi lệnh có nhãn gói; test so `Plans.cs` ↔ `ToolLabels.cs` |
| KT2-21 | sửa đúng | `StoreyWithoutSlab`: 1 tầng, có `storey_height`, không `wall_height`, sàn "auto" mà không có đường bao sàn → hỏi trước. Bước tự kiểm tra A (2 tầng có sàn) không bị ảnh hưởng |
| KT2-22 | **chấp nhận** | Chỉ là lời dặn trong báo cáo / system prompt ("hỏi trước"), không có cổng chặn ở trợ lý như layer tường / cửa. Chấp nhận: lan can chỉ thêm khối mới, U bỏ được; checklist V12 kiểm tay |
| KT2-23 | sửa đúng | Đối tượng thuộc > 1 nhóm → từ chối, nêu tên nhóm, cần `name` |
| KT2-24 | sửa đúng | Kéo dài theo hướng 3D; polyline (z = 0 hai đầu) không đổi |
| KT2-25 | sửa đúng | `.max(1000)` |
| KT2-26 | sửa đúng | Tiêu đề luật 3D đủ lệnh; câu layer + skill chỉ bảo `create_layer` cho khối tự dựng |
| KT2-27 | sửa đúng | Thêm "wipeouts and revision clouds" |
| KT2-28 | sửa đúng | "spacing … default = offset" |
| KT2-29 | sửa đúng | ~1,1k ở `solids.ts` và tổng quan |
| KT2-30 | sửa đúng | `tong-quan-du-an.md` / `ke-hoach-nang-cap-lenh.md` trên `claude/inspiring-cray-58xsmt` @ `69bf71c` khớp lúc `f1e7123`. Sau khi gộp KT2 cần đẩy tiếp lên `4487514`, lõi 908 / trợ lý 298, CI 43475785808 (việc thường lệ của luồng chính, không tính lỗi) |
| KT2-31 | sửa đúng | Đầu checklist có bảng 2 khối + thời gian; ghi "một số mục ★ chọn lọc", phần 16 liệt kê phần còn lại; thêm 10b (V9–V12) cho các sửa KT2 |

### Lỗi mới

| Mã | Mức | Lỗi + cách tái hiện | File / dòng (`4487514`) | Phụ trách | Đề xuất sửa |
| --- | --- | --- | --- | --- | --- |
| KT2-32 | nhẹ | Bản sửa KT2-02 chỉ kiểm layer khóa của **chữ đầu tiên** trong một đối tượng. Block có nhiều thuộc tính nằm trên các layer khác nhau (thuộc tính 1 trên layer mở, thuộc tính 2 trên layer khóa, block trên layer mở): bước thay không thấy layer khóa, `UpgradeOpen` của thuộc tính 2 ném eOnLockedLayer → lại ra lỗi chung không nói chữ nào (đúng cái KT2-02 muốn bỏ), và vì lỗi ném giữa chừng nên cả lượt hoàn tác. Bước xem trước thì kiểm từng thuộc tính nên không mắc | `Drawing/Util2DTools.cs` (bước thay: `LockedLayerOf(ctx, hits[0].Ent)`) | Nâng cấp đợt C | Kiểm `hits.Select(x => LockedLayerOf(ctx, x.Ent))`, lấy layer khóa đầu tiên khác null |

Không thấy bản sửa nào làm hỏng luật cũ (chặn gói, thẻ xác nhận, macro, bước tự kiểm tra) khi lần theo code.

## Lượt 3: soát KT2-32 (13/11/2026)

Gộp `origin/full/nang-cap-tong-the` @ `f7f32ad` (code `6b6c911`, bản sửa đợt C `5c244af`). Test lõi **909/909**, trợ lý
**298/298** xanh; không chạy CI (nhánh tích hợp vừa xanh, run 43478782014).

| Mã | Kết luận | Ghi chú soát |
| --- | --- | --- |
| KT2-32 | sửa đúng | Bước thay kiểm layer khóa của **mọi** chữ trong đối tượng (`Util2DLogic.LockedHits` trên toàn bộ `hits`), không chỉ chữ đầu; thuộc tính ghi rõ "handle (attribute TAG of block …)", mỗi chữ một lần; từ chối trước khi đổi gì, nên không còn lỗi chung giữa chừng. Block trên layer khóa thì mọi thuộc tính đều báo layer của block (đúng, vì AutoCAD cũng chặn). Không thấy lỗi mới ở phần tìm & thay / block |

**Không còn lỗi mở từ kiểm tra độc lập nâng cấp.** Điểm còn chờ thử trên AutoCAD thật: KT2-17 (checklist V13) và 3 mục
"Cần thử trên AutoCAD thật" của lượt 1.
