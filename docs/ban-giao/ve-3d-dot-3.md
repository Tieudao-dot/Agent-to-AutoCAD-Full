# Bàn giao: 3D đợt 3 (thread "Vẽ 3D đợt 3")

*12/11/2026 · luồng chính giao. Nhánh `full/ve-3d-dot-3`, xuất phát `full/ve-3d-test-2` @ `a51cd63`
(đợt 2 đã test & sửa; lõi 261/261, trợ lý 171/171, CI run 43212088529 xanh). Bước 4 trong chuỗi chủ dự án đặt:
test & sửa đợt 1 (xong) → viết đợt 2 (xong) → test & sửa đợt 2 (xong) → **viết đợt 3** → test & sửa đợt 3
(thread con) → test tổng thể (luồng chính mở).*

Đọc trước: `CLAUDE.md`, [`../ke-hoach-3d-goi-dich-vu.md`](../ke-hoach-3d-goi-dich-vu.md) Phần 1,
[`ve-3d-dot-1.md`](ve-3d-dot-1.md), [`ve-3d-test-1.md`](ve-3d-test-1.md), [`ve-3d-dot-2.md`](ve-3d-dot-2.md),
[`ve-3d-test-2.md`](ve-3d-test-2.md) (13 lỗi đã sửa: không lặp lại).

## Chủ dự án đã chốt (12/11/2026)

- Gói 2D / 3D / Full, gói mặc định 2D, phương án A. **Mọi lệnh mới của đợt 3 thuộc `ToolGroup.ThreeD`.**
  Bên trong một lệnh 3D được tạo đường / hatch 2D làm kết quả (mặt cắt). Không mở lệnh nhóm 2D cho gói 3D.
- Lệnh làm đổi hình khối có trước lượt đó (kể cả khối Claude dựng lượt trước) luôn qua thẻ xác nhận.
- Mọi lệnh dùng tọa độ WCS; `set_ucs` chỉ đặt mặt phẳng làm việc.
- Đợt 3 đúng phạm vi kế hoạch, **không** làm skill "3D cơ khí".

## Việc cần làm

1. **`build_3d_from_plan`**: dựng 3D từ mặt bằng 2D ("dựng 3D mặt bằng này, tường cao 3,3 m").
   - Đọc tường, cửa đi / cửa sổ, sàn theo layer của skill `ve-kien-truc`; thêm chế độ chỉ định rõ (handle đường tim
     hoặc đường bao tường, chiều dày, vùng sàn).
   - Extrude tường, trừ lỗ cửa (cao cửa đi; cao bậu + cao cửa sổ), đổ sàn có độ dày; mỗi loại khối một layer 3D.
   - Mặt bằng lộn xộn: báo cáo rõ phần nhận / bỏ qua (handle, lý do), không đoán bừa, giới hạn số đối tượng, chạy lại
     không tạo trùng mà không báo. Chỉ đọc đối tượng 2D của người dùng.
   - Dựng xong: góc iso Conceptual, kiểm bằng số (thể tích, hộp bao).
2. **Mặt cắt / hình chiếu 2D** từ khối 3D: đường bao vết cắt + hatch (+ phần nhìn thấy phía sau), hình chiếu theo
   hướng nhìn; layer riêng, không đè mô hình. Ưu tiên API .NET (`Section`, `GenerateSectionGeometry`); lệnh AutoCAD
   (FLATSHOT / VIEWBASE) chỉ khi không có API, phải cô lập. Không làm được an toàn thì dừng, báo luồng chính.
3. **Skill mẫu "3D kiến trúc"** (`skills/`): chiều cao tầng, dày sàn, cao cửa đi, cao bậu / cửa sổ, layer khối 3D,
   quy trình dựng + tự kiểm. Máy gói 2D không nạp (nếu cơ chế cho phép).

Kèm theo: trợ lý (định nghĩa lệnh, system prompt gói 3D / Full), nhãn `ThreeD` + test nhãn, `CLAUDECHATSELFTEST`
(phòng chữ nhật + cửa đi → dựng → thể tích; cắt ngang → có đường bao), test lõi + trợ lý (đợt 1, 2 vẫn xanh), build
net48 / net8 / net10, tài liệu (checklist phần L, kế hoạch, `bo-cai/README.md`, `skills/README.md`, file này).

"Viết xong" = đủ 3 phần, gắn nhãn; test lõi + trợ lý xanh; **1 lượt** CI Windows xanh (`build.yml`,
workflow_dispatch, `release: "false"`, nhánh `full/ve-3d-dot-3`); docs cập nhật; đã push. Xong thì tự mở thread con
"Vẽ 3D đợt 3 › Test & sửa" (nhánh `full/ve-3d-test-3`) với prompt luồng chính soạn sẵn, báo luồng chính.

## Giới hạn

- Chỉ 1 lượt CI; đỏ và cần chạy thêm thì báo luồng chính. Chỉ mở đúng 1 thread con.
- Không merge / push `main`, không PR, không CI `release: true`, không nâng phiên bản.
- Không sửa `activation/activation.json`, `License.cs`, `.claude/settings.json`; logic gói chỉ thêm nhãn.
- Không sửa lệnh 3D đợt 1, 2 trừ khi cần dùng lại (ghi rõ ở đây, test cũ vẫn xanh).

## Kết quả (12/11/2026, nhánh `full/ve-3d-dot-3`)

### Plugin (`plugin/ClaudeCAD.Plugin`)

3 lệnh mới, nhãn `ToolGroup.ThreeD` (`Core/Plans.cs`). Không gọi lệnh AutoCAD nào (FLATSHOT / VIEWBASE): chỉ API .NET.

- **`build_3d_from_plan`** (`Drawing/PlanTo3dTools.cs`, phần nhận diện không cần AutoCAD ở `Core/FloorPlan.cs`,
  `FloorPlanLogic`):
  - **Đọc** (chỉ đọc, không mở ghi / xóa đối tượng 2D nào; có test nguồn): theo layer (`wall_layers` mặc định `A-WALL`,
    `door_layers` `A-DOOR`, `window_layers` `A-WIND`, `slab_layers` `A-SLAB` / `A-FLOR`; khớp không phân biệt hoa thường,
    có `*` `?`), block `CUA-DI-*` / `CUA-SO-*` của skill ở mọi layer, hoặc chỉ định: `wall_handles` (nét mặt tường),
    `centerline_handles` + `thickness` (đường tim), `door_handles`, `window_handles`, `slab_handles`. `area_min` /
    `area_max` giới hạn vùng (đối tượng nằm trọn trong vùng).
  - **Tường:** cặp nét song song cách nhau `min_thickness`–`max_thickness` (mặc định 50–600 mm), chồng nhau ít nhất bằng
    chiều dày, hoặc ngắn hơn khi có nét đóng đầu (trụ giữa hai cửa); chỉ cặp gần nhất (nét thứ ba ở giữa chặn cặp);
    phần nhô của nét dài (tới bằng chiều dày) được thêm ở đầu → góc L, nối T kín. Đường tim: đầu nối với đường tim khác
    được kéo thêm ½ chiều dày. Polyline có bề rộng đều = tường theo đường tim. Nét trùng, nét đứt khúc / hở ≤ 5 mm được
    gộp trước. Tường cùng đường tim + cùng dày gộp thành một tường; chỗ trống giữa hai đoạn = khe hở.
  - **Cửa:** ký hiệu (block, hoặc nét + cung chạm nhau gộp thành một) nằm ở khe hở → cửa rộng bằng khe; nằm đè tường
    liền → khoét phần tường dưới ký hiệu; chạm hai tường ngang nhau, xa tường, khe quá hẹp / rộng (300–6000) → bỏ qua có
    lý do. Khe hở không có ký hiệu → `open_gaps` (để trống cả chiều cao, hỏi người dùng); khe hẹp mà tường khác chạy qua
    (nút chữ thập vẽ đứt mọi nét) → lấp lại. `openings` thêm / sửa cửa theo điểm (khe → bề rộng khe; tường liền → cần
    `width`).
  - **Dựng:** mỗi tường tách thành hộp (`MakeBox` của đợt 1): đoạn liền cao `wall_height`; cửa đi chừa từ sàn tới
    `door_height` (lanh tô phía trên); cửa sổ: dưới bậu `window_sill` và trên `window_sill + window_height`. Cộng hết vào
    một khối trên `A-WALL-3D` (cộng không được thì để riêng, báo). Sàn: mỗi đường bao kín (đường nhỏ nằm trong đường lớn
    là lỗ, kiểm bằng trừ region trên bản sao), mặt trên ở `base_z`, dày `slab_thickness`, trên `A-SLAB-3D`; `slab: "bbox"`
    một sàn theo hộp bao tường; `"none"`. Layer kết quả tự tạo khi thiếu.
  - **Báo cáo:** `walls` (trục, dày, dài, nguồn), `thickness_summary`, `openings`, `open_gaps`, `skipped` (handle, loại,
    lý do; ≤ 100 dòng + `skipped_count`), `caps`, `junctions_filled`, `plan_min` / `plan_max`, `wall_solid`
    (`volume_m3`, `pieces_volume_m3` = tổng hộp trước khi cộng, `min` / `max`), `slabs` (`area_m2`, `volume_m3`).
    `dry_run: true` chỉ nhận diện và báo cáo.
  - **Không đoán / không trùng:** không nhận được tường nào → `INVALID_PARAMS` gợi ý layer / handle (kèm cảnh báo
    `INSUNITS` = 0); khối cũ trên `A-WALL-3D` / `A-SLAB-3D` chồng lên mặt bằng (cả cao độ) → `INVALID_PARAMS` "already
    built", không dựng gì, trừ khi `on_existing: "add"`. Giới hạn: 5000 đối tượng đọc, 3000 đoạn tường, 1000 ký hiệu,
    5000 hộp, 500 cửa chỉ định; vượt → báo, gợi ý `area_min` / `area_max`.
  - **Đơn vị:** `INSUNITS` (mm, cm, dm, m, inch, feet; 0 = mm). Chiều cao / dày nhập bằng mm, đổi sang đơn vị bản vẽ;
    tọa độ, `base_z` theo đơn vị bản vẽ như mọi lệnh.
  - Dựng xong: lệnh không tự đổi góc nhìn (đang trong transaction); kết quả có ghi chú và system prompt dặn Claude gọi
    `set_view_3d` sw_iso + `set_visual_style` conceptual + `capture_view` và so số.
- **`section_view`** (`Drawing/SectionTools.cs`): mặt phẳng = `line` (đường cắt trên mặt bằng, mặt cắt đứng; đứng bên trái
  start → end nhìn sang phải) hoặc như `slice_solid` (`plane_points`, `point` + `normal`, `point` + `plane`); `flip`.
  Vết cắt: `Solid3d.GetSection(Plane)` → region → đưa về khung 2D (`SectionLogic.Frame`: phải / lên của người nhìn) →
  `Explode` → nối thành vòng kín (`SectionLogic.ChainLoops`) → polyline kín (có bulge) trên `A-SECT`; hatch
  (`ANSI31` mặc định, `hatch_scale` 10, kiểu Normal để trống phần trong vòng) trên `A-SECT-PAT`. `show_behind`: nét nhìn
  thấy phía sau bằng `Section.GenerateSectionGeometry` (xem dưới) trên `A-SECT-BACK`. Đặt cạnh phải toàn bộ model (cách
  10 % bề rộng, ≥ 1 m) hoặc `position`; `position` đè lên model thì có ghi chú. Trả `cut_area_m2`, `size`, `frame` (quy
  tắc đổi điểm model → điểm trên hình).
- **`projection_view`**: `view` (front / back / left / right / top / bottom / iso) hoặc `direction`; `method: "auto"` =
  `Section` đặt ngoài hộp bao phía người nhìn, chỉ bật background, tắt nét khuất (AutoCAD bỏ nét khuất) →
  `hidden_lines_removed: true`; AutoCAD không trả gì / lỗi → chiếu mọi cạnh Brep (`GetOrthoProjectedCurve`, bỏ trùng),
  `hidden_lines_removed: false`. Layer `A-ELEV`.
- **Phần `Section` được cô lập** (`SectionBackground`): đối tượng `Section` tạm thêm vào bản vẽ chỉ để có
  `SectionSettings` và **xóa ngay trong cùng transaction**; mọi lỗi ở đây chỉ làm mất nét phía sau (ghi chú "the result
  has the cut only"), vết cắt vẫn có; không có hộp thoại, không chạy ngoài transaction, U hoàn tác cả lượt. Tên tham số
  `GenerateSectionGeometry` trong API .NET lệch với ObjectARX (`AcDbMgd.xml` 24.1 / 25.0 / 26.0 giống nhau) nên plugin
  chỉ bật background và lấy mọi mảng như nhau; kết quả không nằm trên mặt phẳng thì tự chiếu lên. Đã đối chiếu chữ ký
  `Section`, `SectionSettings`, `SectionType`, `SectionGeometry`, `Solid3d.GetSection`, `Curve.GetOrthoProjectedCurve`,
  `Curve.CreateFromGeCurve` bằng reflection trên `AcDbMgd.dll` của cả 3 đời.
- Mọi đối tượng tạm (hộp chưa cộng, region, đường nổ ra, nét phía sau) Dispose khi lỗi / không dùng; AutoCAD từ chối hình
  → `INVALID_PARAMS` "Nothing was built / drawn", transaction của lượt lùi lại.
- **Macro:** tham số handle mới (`wall_handles`, `centerline_handles`, `door_handles`, `window_handles`,
  `slab_handles`, `handles`) đều khớp `MacroLogic.IsHandleKey` (không phải sửa, có test). Cả 3 lệnh ghi macro; kết quả
  `handles` là khối / nét mới.
- **"Chỉ xem":** cả 3 lệnh là lệnh ghi → bị chặn (kể cả `dry_run`).
- `CLAUDECHATSELFTEST` (`SelfTest.cs`): 2 bước trên bản vẽ tạm: phòng 4×3 m tường 200 có khe cửa 900 + lá cửa + cung →
  `build_3d_from_plan` (gọi thẳng handler) → 4 tường, 1 cửa, thể tích 8,022 m³ ± 0,1 %; cắt ngang ở z = 1000 →
  có đường bao, 1 hatch, diện tích cắt 2,62 m².
- Skill mẫu `ve-3d-kien-truc` thêm vào `SkillStore.BuiltinNames`.
- **Sửa lệnh đợt 1, 2:** không. Dùng lại `MakeBox`, `AddSolid`, `BooleanInto`, `AddSolidNumbers`, `VolumeOf`,
  `PlaneOf`, `AcPlane`, `ViewOf`, `J` (không đổi).

### Skill `skills/ve-3d-kien-truc`

Chiều cao tầng 3300, tường 3300, sàn 120 / 150, cửa đi 2200 (chính 2400), bậu 900 / cửa sổ 1500 (WC 1500 / 600), dải
dày nhận diện; layer `A-WALL-3D`, `A-SLAB-3D`, `A-COLS-3D`, `A-STAIR-3D`, `A-ROOF-3D`, `A-SECT*`, `A-ELEV`; quy trình
chuẩn bị → chạy thử → hỏi → dựng → tầng trên (`base_z`) → tự kiểm (số + hình); mặt cắt / mặt đứng. Frontmatter có
`plan: 3d`: **máy gói 2D không nạp** (trợ lý `skillsForPlan` lọc khi tạo plugin skill, kể cả khi đoạn chat đã bấm +).
Phía plugin, danh sách skill của khung chat vẫn hiện skill này trong thư viện ở máy 2D (không sửa giao diện).

### Trợ lý (`agent/`)

- `src/tools/plan3d.ts` (mới): 3 lệnh, `PLAN3D_LIMITS` / `PLAN3D_LAYERS` khớp `Core/FloorPlan.cs` (test so nguồn);
  thêm vào `SOLID_TOOL_NAMES` → máy gói 2D không nạp.
- Cổng xác nhận: không phải sửa. 3 lệnh chỉ tạo mới (không trong `DESTRUCTIVE`), handle kết quả tính là "tạo trong lượt"
  (cắt / trừ khối vừa dựng không cần thẻ; khối của lượt trước vẫn cần).
- `src/agent/systemPrompt.ts` (gói 3D / Full): dùng `build_3d_from_plan` (không dựng từng hộp), `dry_run` trước, đọc
  `skipped` / `open_gaps` rồi hỏi, không đoán; kiểm `volume_m3` / hộp bao, chụp iso Conceptual; lỗi "already built" thì
  hỏi; `section_view` / `projection_view` và kiểm `cut_area_m2`. Câu "đợt sau mới có" đã bỏ. Phạm vi gói 3D ghi thêm
  mặt cắt có hatch riêng.
- `src/agent/skills.ts`: `skillPlan`, `skillsForPlan`; `prepareSkillsPlugin(…, plan)`; `session.ts` truyền gói.

### Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **290/290** (261 cũ + 20 trong `FloorPlanTests.cs`: phòng mẫu của skill
  (4 tường, 1 cửa, thể tích tính tay), nét trùng / đứt / hở, nối T tường mỏng, chữ thập vẽ đứt, hai mép lỗ cửa không
  thành tường, trụ ngắn, nét đơn / quá dày bị bỏ có handle, cửa sổ ở khe + block cửa đè tường liền, ký hiệu xa / ở góc
  không đoán, đường tim, cửa chỉ định, đơn vị / layer / chiều cao, mặt bằng đơn vị mét, khung mặt cắt, đường cắt + đảo,
  mặt phẳng hình chiếu ngoài mô hình, vị trí đặt không đè, nối vòng + bulge; + 9 trong `Plan3dToolTests.cs`: nhãn,
  đăng ký, macro, chỉ đọc mặt bằng, không gọi lệnh AutoCAD + `Section` tạm được xóa, Dispose, câu từ chối, self-test,
  skill mẫu). `SolidTests` cũ: đếm nhãn ThreeD cộng thêm 3 lệnh đợt 3.
- Trợ lý `npm test`: **184/184** (171 cũ + 13 trong `test/plan3d.test.ts`). `solids2.test.ts` cũ: kim "next phase" đổi
  thành `build_3d_from_plan` (câu "đợt sau mới có" đã bỏ có chủ đích; thêm kiểm câu đó không còn).
- Build Linux (.NET SDK 10, xóa `obj` / `bin` giữa các lần): net48, net8.0-windows, net10.0-windows: 0 lỗi, 0 cảnh báo.
- CI Windows: run 43220181366 (`build.yml`, workflow_dispatch, `release: false`, commit `5d737b6`) **xanh** (~10 phút, gồm UI test WPF, bộ cài).

### Chỉ kiểm được trên AutoCAD thật (phần L của `checklist-thu-that.md`, L1–L16)

`GenerateSectionGeometry` (thứ tự mảng, phía "sau", kết quả trên mặt phẳng hay trải phẳng, hình chiếu khi mặt phẳng ở
ngoài mô hình), `GetSection` + `Explode` với cung / spline, hatch kiểu Normal để trống trong vòng, thời gian cộng nhiều
hộp, nhận diện trên mặt bằng thật của cậu.

## Tiến độ

- [x] `build_3d_from_plan`
- [x] Mặt cắt / hình chiếu
- [x] Skill 3D kiến trúc
- [x] Trợ lý, nhãn, self-test, test
- [x] CI Windows (1 lượt, xanh: run 43220181366)
- [x] Tài liệu (checklist phần L, kế hoạch, `bo-cai/README.md`, `skills/README.md`, file này)
- [ ] Mở thread con Test & sửa đợt 3
