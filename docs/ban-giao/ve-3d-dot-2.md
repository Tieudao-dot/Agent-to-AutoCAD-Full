# Bàn giao: 3D đợt 2 (thread "Vẽ 3D đợt 2")

*12/11/2026 · luồng chính giao. Nhánh `full/ve-3d-dot-2`, xuất phát `full/ve-3d-test-1` @ `9332985`
(đợt 1 đã test & sửa; lõi 236/236, trợ lý 159/159, CI run 43203265912 xanh). Bước 2 trong chuỗi chủ dự án đặt:
test & sửa đợt 1 (xong) → **viết đợt 2** → test & sửa đợt 2 (thread con) → viết đợt 3 → test & sửa đợt 3 → test tổng thể.*

Đọc trước: `CLAUDE.md`, [`../ke-hoach-3d-goi-dich-vu.md`](../ke-hoach-3d-goi-dich-vu.md) Phần 1,
[`ve-3d-dot-1.md`](ve-3d-dot-1.md), [`ve-3d-test-1.md`](ve-3d-test-1.md) (7 lỗi đợt 1, không lặp lại).

## Chủ dự án đã chốt (12/11/2026)

- Gói 2D / 3D / Full, gói mặc định 2D, phương án A. **Mọi lệnh 3D mới thuộc `ToolGroup.ThreeD`.**
- Cộng / trừ vào khối có trước lượt đó (kể cả khối Claude dựng ở lượt trước) luôn qua thẻ xác nhận. Luồng chính áp
  cùng luật cho `slice_solid`, `fillet_edges`, `chamfer_edges`, `shell_solid`. `rotate_3d`, `mirror_3d`, `align` theo
  luật sửa đối tượng có sẵn của move / rotate / mirror 2D.
- Đợt 2 giữ đúng phạm vi kế hoạch, không làm việc của đợt 3.

## Việc cần làm

| Nhóm | Lệnh | API AutoCAD .NET |
| --- | --- | --- |
| Biên dạng nâng cao | `sweep`, `loft` | `Solid3d.CreateSweptSolid` + `SweepOptions`, `CreateLoftedSolid` + `LoftOptions` |
| Sửa khối | `slice_solid`, `fillet_edges`, `chamfer_edges`, `shell_solid` | `Solid3d.Slice`, `FilletEdges`, `ChamferEdges`, `ShellBody` |
| Biến đổi 3D | `rotate_3d`, `mirror_3d`, `align` | `Matrix3d.Rotation / Mirroring / AlignCoordinateSystem`, `TransformBy` |
| Mặt phẳng làm việc | `set_ucs`, `reset_ucs` | `Editor.CurrentUserCoordinateSystem` |
| Kiểm tra | `check_interference` | `Solid3d.CheckInterference` |
| Hỏi khối | `get_solid_info` thêm danh sách cạnh / mặt (có giới hạn) | Brep |

Điểm khó:

- Chọn cạnh / mặt: số thứ tự từ `get_solid_info` (ổn định khi khối chưa đổi; khối đã đổi thì báo lỗi rõ) hoặc bộ lọc
  (song song trục, thuộc mặt trên / dưới / trước…, mọi cạnh).
- UCS: mọi lệnh hiện có vẫn hiểu tọa độ theo WCS. Cần đổi quy ước thì dừng, báo luồng chính.
- Macro: tham số handle mới phải được `Macros.IsHandleKey` ánh xạ; số thứ tự cạnh / mặt khi phát lại chọn cách an toàn.
- Bài học đợt 1: Dispose đối tượng tạm khi lỗi; AutoCAD từ chối hình → `INVALID_PARAMS` rõ, bản vẽ không đổi; giới hạn
  tham số chung giữa trợ lý và plugin; AutoCAD không phải tiếng Anh.

Kèm theo: trợ lý (định nghĩa lệnh, system prompt, cổng xác nhận), nhãn `ThreeD`, `CLAUDECHATSELFTEST` thêm bước đợt 2,
test lõi + trợ lý, build net48 / net8 / net10, tài liệu (checklist phần K, kế hoạch, `bo-cai/README.md`, file này).

"Viết xong" = đủ lệnh có nhãn; test lõi + trợ lý xanh; **1 lượt** CI Windows xanh (`build.yml`, workflow_dispatch,
`release: "false"`, nhánh `full/ve-3d-dot-2`); docs cập nhật; đã push. Xong thì tự mở thread con
"Vẽ 3D đợt 2 › Test & sửa" (nhánh `full/ve-3d-test-2`) với prompt luồng chính soạn sẵn.

## Giới hạn

- Chỉ 1 lượt CI; đỏ và cần chạy thêm thì báo luồng chính. Chỉ mở đúng 1 thread con.
- Không merge / push `main`, không PR, không CI `release: true`, không nâng phiên bản.
- Không sửa `activation/activation.json`, `License.cs`, `.claude/settings.json`; logic gói chỉ thêm nhãn.
- Không làm đợt 3 (`build_3d_from_plan`, mặt cắt / hình chiếu 2D, skill 3D kiến trúc).

## Kết quả (12/11/2026, nhánh `full/ve-3d-dot-2`)

### Plugin (`plugin/ClaudeCAD.Plugin`)

- `Drawing/Solid2Tools.cs` (mới): 12 lệnh, tất cả nhãn `ToolGroup.ThreeD` trong `Core/Plans.cs`.
  - `sweep`: biên dạng kín (`profile_points` / `profile_handles`, dùng chung `ProfileRegions` của đợt 1) theo
    `path_handle` hoặc `path_points` (polyline tạm; polyline 3D thêm tạm vào bản vẽ rồi xóa ngay vì đỉnh cần database).
    `align` (mặc định: đặt tâm tiết diện vào đầu đường dẫn, vuông góc), `twist`, `scale`, `bank`, `erase_profile`.
    `SweepOptionsBuilder.CheckIntersections = true`.
  - `loft`: `section_handles` (đường kín, theo thứ tự) hoặc `sections_points` (đa giác kín, mỗi tiết diện phẳng ngang);
    `ruled`, `closed`, `guide_handles` / `path_handle`, `erase_profile`. `NoTwist`, `AlignDirection`, `Simplify` bật.
  - `slice_solid`: mặt phẳng = `plane_points` | `point` + `normal` | `point` + `plane` (`xy`/`yz`/`zx`).
    `keep` = `both` (mặc định: handle giữ nửa dương, nửa âm thành khối mới cùng layer / thuộc tính, cùng không gian),
    `positive`, `negative`. Kiểm trước mặt phẳng có đi qua hộp bao; sau cắt kiểm hai nửa đều có thể tích.
  - `fillet_edges` (`radius`), `chamfer_edges` (`distance`, `distance2` + `base_face`), `shell_solid` (`thickness`,
    mặt mở theo `faces` hoặc `face_filter`; không mặt nào = vỏ kín). Chọn cạnh: `edges` (số thứ tự) + `topology_id`,
    hoặc `edge_filter` (mọi bộ lọc phải đúng: `all`, `straight`, `curved`, `parallel_x/y/z`, `top/bottom/front/back/left/right`
    = cạnh nằm trên mặt đó của hộp bao; front = Y nhỏ nhất như hướng nhìn Front).
    - `ChamferEdges` cần một mặt gốc mỗi lần gọi: plugin gom cạnh theo mặt (ít nhóm nhất); nhóm sau tìm lại cạnh / mặt
      bằng hình học (cạnh thẳng cùng đường, cùng hướng) vì AutoCAD đánh số lại; không tìm được thì báo lỗi, lùi cả lệnh.
    - *(Test đợt 2: tài liệu có ghi dấu, âm = vào trong; đã bỏ bước thử trên bản sao, xem `ve-3d-test-2.md`.)* `ShellBody` không có tài liệu về dấu độ dày: plugin thử trên **bản sao** (−, rồi +), chọn phía làm thể tích giảm
      mà hộp bao giữ nguyên, rồi mới làm trên khối thật.
  - `rotate_3d` (trục bất kỳ, `Matrix3d.Rotation`), `mirror_3d` (`Matrix3d.Mirroring(Plane)`, `keep_original` mặc định
    true như `mirror_entities`, giữ luật MIRRTEXT), `align` (1–3 cặp điểm, `scale`; ma trận tính trong
    `Solid2Logic.AlignMatrix`, có test). Dùng chung `Transform` / `CloneEntities` của lệnh 2D.
  - `set_ucs` (`preset` world/top/bottom/front/back/left/right, `origin` + `x_axis` + `y_axis`, `name` = UCS đã lưu,
    chỉ `origin` = dời UCS hiện tại; `save_as` lưu UCS tên mới, không ghi đè; `plan_view`), `reset_ucs` (về World).
    **Quy ước giữ nguyên:** mọi lệnh vẫn nhận / trả tọa độ WCS; UCS chỉ là mặt phẳng làm việc của người dùng. Không
    lệnh nào khác đọc UCS (có test nguồn). Hai lệnh này: không transaction (không chạy trong `batch_execute`), không ghi
    macro, **bị chặn khi "Chỉ xem"** (là thay đổi lưu trong bản vẽ), có mở nhóm U của lượt nên U hoàn tác.
  - `check_interference`: mọi cặp trong `handles`, hoặc `handles` × `other_handles` (≤ 100 khối mỗi danh sách); lọc
    hộp bao trước, `Solid3d.CheckInterference`, thể tích chồng = giao trên **bản sao** (Dispose). Phân biệt "chạm" và
    "chồng". Chỉ đọc (được dùng khi "Chỉ xem").
  - `get_solid_info`: `edges: true` / `faces: true` (≤ 10 khối khi liệt kê), `max_items` (mặc định 60, tối đa 300).
    Cạnh: `i`, `line`/`curve`, `start`, `end`, (`mid`), `faces`, `parallel`. Mặt: `i`, `plane`/`curved`, `center`,
    `normal` (hướng ra ngoài, kiểm bằng `GetPointContainment`), `area`, `side`. Kèm `topology_id`.
- **Số thứ tự ổn định:** `topology_id` = băm thứ tự Brep của mọi cạnh / mặt (tọa độ so với góc hộp bao, làm tròn
  0,01 mm). Lệnh sửa nhận số thứ tự **bắt buộc** kèm `topology_id`; khác → `INVALID_PARAMS` "solid has changed… call
  get_solid_info again", không đổi gì.
- **Macro:** tham số handle mới (`handle`, `handles`, `other_handles`, `path_handle`, `section_handles`,
  `guide_handles`, `profile_handles`) đều khớp `MacroLogic.IsHandleKey` (không phải sửa, có test). Số thứ tự cạnh / mặt
  được ghi kèm `topology_id`: phát lại trên khối giống hệt (macro dựng lại khối rồi bo, kể cả đặt ở điểm khác vì id
  không phụ thuộc vị trí) thì chạy đúng; khối khác thì lệnh đó báo lỗi rõ, không bo nhầm cạnh. Bộ lọc thì luôn phát lại
  được. `set_ucs` / `reset_ucs` không ghi macro.
- Bài học đợt 1: mọi bản sao / khối tạm Dispose khi lỗi (`using`, `finally` + `DropTemp`); AutoCAD từ chối hình →
  `INVALID_PARAMS` "Nothing was changed" + gợi ý (transaction của lượt lùi lại); giới hạn chung trong `Solid2Logic`
  (test so với `SOLID2_LIMITS` của trợ lý); không dùng tên tiếng Anh nào của AutoCAD.
- `Core/Solids2.cs` (mới, không cần AutoCAD): `Solid2Logic` (bộ lọc, `topology_id`, nhóm vát, tìm lại cạnh, mặt phẳng,
  align, trục xoay, UCS, tên UCS, giới hạn, thể tích hộp bo cạnh).
- `CLAUDECHATSELFTEST`: thêm 3 bước trên bản vẽ tạm: hộp 1000×800×600 bo 4 cạnh đứng R100 (0,474850 m³ ± 0,1 %), cắt
  đôi (0,24 m³ mỗi nửa), hai hộp 1 m³ chồng 500 mm (`CheckInterference` + 0,5 m³).

### Trợ lý (`agent/`)

- `src/tools/solids.ts`: 12 lệnh mới + `get_solid_info` mở rộng; `SOLID2_LIMITS`, `EDGE_FILTERS`, `FACE_SIDES`,
  `UCS_PRESETS` khớp plugin. Máy gói 2D không nạp (bộ lọc `plan_tools` có sẵn).
- `src/agent/confirmation.ts`: `slice_solid`, `fillet_edges`, `chamfer_edges`, `shell_solid` vào `ALWAYS_CONFIRM` (khối
  có trước lượt → thẻ xác nhận, kể cả trong batch). `rotate_3d`, `align`, `mirror_3d` (`keep_original: false`) tính như
  move / rotate / mirror (> 20 đối tượng). `sweep` / `loft` có `erase_profile` tính như xóa. `slice_solid` giữ cả hai
  nửa: chỉ nửa mới là "đã tạo trong lượt".
- `src/agent/systemPrompt.ts` (chỉ gói 3D / Full): gọi `get_solid_info` với `edges`/`faces` trước khi bo / vát / làm
  rỗng, không đoán số thứ tự; luật xác nhận; sửa khối xong kiểm thể tích / hộp bao và chụp iso Conceptual; UCS không đổi
  tọa độ WCS; đợt 3 (mặt cắt, dựng từ mặt bằng) chưa có.

### Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **255/255** (236 cũ + 19 mới trong `Solid2Tests.cs`).
- Trợ lý `npm test`: **169/169** (159 cũ + 10 mới trong `test/solids2.test.ts`).
- Build Linux (NuGet, .NET SDK 10): net48, net8.0-windows, net10.0-windows: 0 lỗi, 0 cảnh báo.
- CI Windows: run 43208694624 (`build.yml`, workflow_dispatch, `release: false`, commit `2b908a4`) **xanh** (~10 phút, gồm UI test WPF).

### Chỉ kiểm được trên AutoCAD thật (phần K của `checklist-thu-that.md`)

- Dấu độ dày `ShellBody` (đã xử lý bằng thử trên bản sao), `FilletEdges` với setback 0, căn tiết diện của `sweep`
  (`BasePoint` = tâm tiết diện), thứ tự Brep giữa khối và bản sao (shell dựa vào đó; khác thì báo lỗi, không đoán),
  U hoàn tác `set_ucs`, `CreateLoftedSolid` với tiết diện tạm chưa vào bản vẽ.

## Tiến độ

- [x] Lệnh plugin + nhãn 3D
- [x] Trợ lý: định nghĩa lệnh, system prompt, cổng xác nhận
- [x] Self-test
- [x] Test lõi + trợ lý
- [x] CI Windows (1 lượt, xanh: run 43208694624)
- [x] Tài liệu (checklist phần K, kế hoạch, `bo-cai/README.md`, file này)
- [ ] Mở thread con Test & sửa đợt 2
