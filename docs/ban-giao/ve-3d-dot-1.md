# Bàn giao: 3D đợt 1 (sub-thread "Vẽ 3D")

*12/11/2026 · luồng chính giao cho sub-thread "Vẽ 3D". Nhánh `full/ve-3d`, xuất phát `full/goi-dich-vu` @ `b1e9419`.*

Đọc trước: `CLAUDE.md`, [`../ke-hoach-3d-goi-dich-vu.md`](../ke-hoach-3d-goi-dich-vu.md) (Phần 1),
[`goi-dich-vu-plugin.md`](goi-dich-vu-plugin.md), `plugin/ClaudeCAD.Plugin/Core/Plans.cs`.

## Chủ dự án đã chốt (12/11/2026)

- 3D đợt 1 giữ đúng phạm vi kế hoạch, không thêm `build_3d_from_plan` (để đợt 3).
- Gói: 2D / 3D / Full. Gói mặc định cho máy mới: 2D.
- Chia gói phương án A: gói 3D có đường cơ bản, không có chữ / kích thước / hatch / in 2D.
- Mọi lệnh 3D mới gắn nhãn `ToolGroup.ThreeD` trong `PlanLogic`: chỉ máy gói 3D và Full dùng được; máy gói 2D không
  nạp các lệnh này.
- Cộng / trừ / giao vào mọi khối có trước lượt đó, kể cả khối Claude dựng ở lượt trước, luôn qua thẻ xác nhận.

## Việc cần làm

| Nhóm | Lệnh | API AutoCAD .NET |
| --- | --- | --- |
| Khối cơ bản | `draw_box`, `draw_cylinder` (cả hình nón), `draw_sphere`, `draw_wedge`, `draw_torus`, `draw_pyramid` | `Solid3d.CreateBox / CreateFrustum / CreateSphere / CreateWedge / CreateTorus / CreatePyramid` |
| Từ biên dạng 2D | `extrude` (theo chiều cao, có góc vát), `revolve` (quanh trục) | `CreateExtrudedSolid`, `CreateRevolvedSolid`, `Region.CreateFromCurves` |
| Cộng trừ khối | `solid_union`, `solid_subtract`, `solid_intersect` | `Solid3d.BooleanOperation` |
| Góc nhìn | `set_view_3d` (trên, trước, bên, 4 góc iso), `set_visual_style` (2D Wireframe / Conceptual / Shaded / Realistic) | `ViewTableRecord.ViewDirection`, `VisualStyleId` |
| Hỏi khối | `get_solid_info`: thể tích, trọng tâm, hộp bao, số mặt / cạnh | `MassProperties`, `GeometricExtents`, Brep |
| Tự kiểm tra | `capture_view` thêm tùy chọn hướng nhìn + kiểu hiển thị; vẫn thuộc nhóm Chung | Lệnh chụp hiện có |

Kèm theo:

- **Trợ lý:** định nghĩa lệnh 3D (qua bộ lọc theo gói). System prompt thêm quy ước 3D: Z hướng lên, mm; biên dạng trên
  XY rồi extrude; mỗi loại khối một layer; dựng xong đặt góc iso, chụp Conceptual, kiểm bằng số (thể tích, hộp bao).
- **An toàn:** cộng / trừ vào khối có sẵn của người dùng là sửa đối tượng có sẵn → qua thẻ xác nhận
  (`agent/src/agent/confirmation.ts`). Hoàn tác cả lượt bằng một lần U giữ như cũ.
- **`CLAUDECHATSELFTEST`:** thêm bước dựng hộp, trừ trụ, kiểm thể tích.
- **Test:** phần tính toán không cần AutoCAD (hướng nhìn, trục quay, kiểm tham số) trong `plugin/ClaudeCAD.Core.Tests`;
  lệnh trợ lý trong `agent/test`. Test bắt buộc nhãn lệnh phải vẫn xanh.
- **Tương thích:** build net48 / net8.0-windows / net10.0-windows; kiểm trên CI.
- **Tài liệu:** `docs/checklist-thu-that.md` (phần "3D"), `docs/ke-hoach-3d-goi-dich-vu.md` (đánh dấu đợt 1),
  `bo-cai/README.md`, file này.

"Xong" = đủ lệnh đợt 1 có nhãn 3D; test lõi + trợ lý xanh; **1 lượt** CI Windows xanh (`build.yml`,
workflow_dispatch, `release: "false"`, nhánh `full/ve-3d`); docs cập nhật; đã push.

## Giới hạn

- Chỉ 1 lượt CI; đỏ và cần chạy thêm thì báo luồng chính hỏi chủ dự án.
- Không merge / push `main`, không PR, không CI `release: true`, không nâng phiên bản.
- Không sửa `activation/activation.json`, khóa công khai (`License.cs`), logic gói (`PlanLogic`, `Activation`,
  `ToolDispatcher` phần gói) ngoài việc thêm nhãn lệnh 3D.
- Không làm đợt 2 / 3 (sweep, loft, slice, fillet 3D, UCS, va chạm, `build_3d_from_plan`).
- Mức gấp: khá gấp; chủ dự án muốn thử trên AutoCAD trước khi bản dùng thử hết hạn (~10/12/2026).

## Kết quả (12/11/2026, nhánh `full/ve-3d`)

### Plugin (`plugin/ClaudeCAD.Plugin`)

- `Drawing/SolidTools.cs` (mới): 14 lệnh, tất cả nhãn `ToolGroup.ThreeD` trong `Core/Plans.cs`.
  - Khối: `draw_box` (corner hoặc center, rotation quanh Z, height âm dựng xuống), `draw_cylinder` (top_radius 0 = nón;
    trục theo `height` hoặc `top_center`), `draw_sphere`, `draw_wedge` (mặt đứng cao ở phía corner, dốc xuống theo +X;
    hướng của `CreateWedge` không có tài liệu nên plugin đọc trọng tâm rồi xoay cho đúng), `draw_torus`,
    `draw_pyramid` (3–32 cạnh, `radius` = bán kính đường tròn **nội tiếp** đáy, như API).
  - `extrude` / `revolve`: biên dạng `profile_points` (đa giác kín, không thêm vào bản vẽ) hoặc `profile_handles`
    (đường kín, region, hay line / arc khép vòng → `Region.CreateFromCurves`); `taper_angle`; `erase_profile`
    (mặc định giữ biên dạng). `revolve` kiểm biên dạng không cắt trục.
  - `solid_union` / `solid_intersect` (gộp vào khối đầu, xóa các khối kia), `solid_subtract` (`target`, `subtract`,
    `keep_tools`). Kết quả rỗng → báo lỗi, transaction lùi lại, bản vẽ không đổi.
  - `get_solid_info`: thể tích (mm³, m³), trọng tâm, hộp bao, kích thước, số mặt / cạnh / đỉnh (Brep).
  - `set_view_3d` (10 hướng + `direction` tùy ý, khung theo cả mô hình hoặc `handles`), `set_visual_style`
    (2d_wireframe, wireframe, hidden, conceptual, shaded, shaded_with_edges, realistic, xray). Hai lệnh này chỉ đổi
    hiển thị như zoom: không thêm bước U, được phép khi "Chỉ xem" (`ToolDispatcher.ReadOnlyAllowed`), không ghi macro.
  - Mọi lệnh dựng / cộng trừ trả `volume`, `volume_m3`, `min`, `max` để Claude tự soát bằng số.
- `capture_view` (vẫn nhóm Chung): thêm `view`, `direction`, `visual_style`; ảnh 3D song song, mặc định khung cả mô hình.
- `Core/Solids.cs` (mới, không cần AutoCAD): hướng nhìn, trục màn hình, kích thước chiếu, tên kiểu hiển thị, kiểm tham
  số, trục xoay, thể tích kỳ vọng. Test: `ClaudeCAD.Core.Tests/SolidTests.cs`.
- `CLAUDECHATSELFTEST`: thêm "Khối 3D: dựng hộp, trừ trụ, kiểm thể tích" (hộp 1000×800×600 trừ trụ R200, cần
  0,404602 m³ ± 0,1 %) và "Góc nhìn 3D (SW iso) + kiểu Conceptual + ảnh 3D" (`capture-3d.png`). Chạy trên bản vẽ tạm.
- `ClaudeCAD.Plugin.csproj`: chế độ Install tham chiếu thêm `acdbmgdbrep.dll` (Brep); chế độ NuGet đã có sẵn.

### Trợ lý (`agent/`)

- `src/tools/solids.ts` (mới): khai báo 14 lệnh; đi qua bộ lọc `plan_tools` có sẵn, nên máy gói 2D không nạp.
- `src/agent/systemPrompt.ts`: `THREE_D_RULES` chỉ thêm cho gói `3d` / `2d3d` (máy 2D không tốn token): Z lên, mm;
  biên dạng trên XY rồi extrude; mỗi loại khối một layer; lỗ cửa = dựng khối rồi trừ; dựng xong `set_view_3d` sw_iso,
  `capture_view` conceptual, đối chiếu `get_solid_info`.
- `src/agent/confirmation.ts`: cộng / trừ / giao vào khối **có trước lượt này** (kể cả khối Claude dựng ở lượt trước)
  luôn cần xác nhận, dù chỉ 1 khối; khối dựng trong cùng lượt thì không. **Đã chốt 12/11/2026:** giữ thẻ xác nhận
  cả với khối Claude dựng ở lượt trước. Kiểm cả trong `batch_execute`.
  `extrude` / `revolve` có `erase_profile` tính như xóa đối tượng có sẵn.
- Test: `test/solids.test.ts`. `test/tools.test.ts`: regex tên lệnh nhận cả chữ số (`set_view_3d`), giống test C#.

### Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **225/225** (185 cũ + 40 mới). Test nhãn gói vẫn xanh.
- Trợ lý `npm test`: **157/157** (144 cũ + 13 mới).
- Build Linux (NuGet): net48, net8.0-windows, net10.0-windows: 0 lỗi, 0 cảnh báo.
- CI Windows: run 43192673511 (`build.yml`, workflow_dispatch, `release: false`, commit `9dcf4a0`) **xanh** (~8 phút).

### Chỉ kiểm được trên AutoCAD thật (phần J của `checklist-thu-that.md`)

- Hướng mặt dốc của `draw_wedge` và góc vát dương của `extrude` (thu nhỏ lên trên) dựa trên tài liệu API; cần nhìn tận mắt.
- `set_visual_style` đổi kiểu qua `ViewportTableRecord.VisualStyleId` + `UpdateTiledViewportsFromDatabase`; ảnh 3D dùng
  `GsView.VisualStyleId`. Nếu ảnh 3D không có màu Conceptual, đó là chỗ cần sửa đầu tiên.

## Tiến độ

- [x] Lệnh plugin + nhãn 3D
- [x] Trợ lý: định nghĩa lệnh, system prompt, cổng xác nhận
- [x] Self-test
- [x] Test lõi + trợ lý
- [x] CI Windows (1 lượt, xanh)
- [x] Tài liệu (checklist phần J, kế hoạch, `bo-cai/README.md`, file này)

## Sửa sau kiểm tra độc lập (12/11/2026)

Nguồn: bảng "Nhẹ" của `docs/ban-giao/kiem-tra-doc-lap.md` (nhánh `full/kiem-tra-doc-lap`). Nhánh `full/ve-3d` đã
fast-forward lên `full/test-tong-the` @ `3f05da7` trước khi sửa. Code: commit `bf38ab3`.

| Mã | Lỗi | Cách sửa |
| --- | --- | --- |
| KT-28 | `solid_intersect` từ 3 khối: hai khối đầu không giao thì bước sau lỗi với câu báo sai ("mặt chỉ chạm…") | Kiểm kết quả rỗng sau **mỗi** lần giao; rỗng thì báo "the solids do not overlap: <handle…> have no common volume", transaction lùi, bản vẽ không đổi (`SolidLogic.EmptyIntersectionMessage`) |
| KT-29 | `set_view_3d` với `handles` không handle nào tồn tại vẫn trả ok, không phóng | Không handle nào tồn tại → `ENTITY_NOT_FOUND` (cùng câu với `capture_view`); có handle nhưng không có hộp bao → `INVALID_PARAMS` |
| KT-30 | Chú thích nói khối đùn lấy layer / màu của biên dạng, code đặt layer hiện hành | **Giữ hành vi, sửa chú thích:** khối mới theo `layer` / `color` truyền vào, không có thì layer hiện hành, như mọi đối tượng mới. Lý do giữ: biên dạng thường là đường dựng trên layer khác; quy ước 3D dặn Claude luôn truyền layer riêng cho từng loại khối. Revolve cùng quy tắc. Sweep thuộc đợt 2, không sửa ở đây |
| KT-30 (trục revolve) | Kiểm trục chỉ xét đỉnh trên mặt XY: cung bulge, đường tròn, biên dạng mặt đứng cắt trục không bắt được | Lấy điểm dọc theo từng đường (96 điểm / đường, cả cung bulge, đường tròn, spline) và xét phía của điểm trên **mặt phẳng của region** (pháp tuyến của region). Trục không nằm trên mặt phẳng biên dạng (vd. trục Z với biên dạng XY) → lỗi rõ thay cho lỗi chung của AutoCAD. Biên dạng là region có sẵn: không lấy được điểm, AutoCAD tự kiểm như trước |

Test: lõi 337/337 (thêm `SolidReviewFixTests.cs`, 6 test; test cũ "trục Z không kiểm" đổi thành "trục Z báo lỗi mặt
phẳng"), trợ lý 201/201, build net48 / net8.0-windows / net10.0-windows 0 lỗi 0 cảnh báo. Không chạy CI (CI chung chạy
sau khi thread Test tổng thể gộp).

Thử tay thêm (phần J): `revolve` với biên dạng có cung bulge vắt qua trục → báo "crosses the axis"; biên dạng vẽ trên
mặt đứng quanh trục Z → dựng được; `solid_intersect` 3 khối mà 2 khối đầu tách rời → báo "do not overlap", không đổi gì.
