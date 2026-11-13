# Bàn giao: Nâng cấp đợt A (3D)

*13/11/2026 · thread Nâng cấp đợt A · nhánh `full/nang-cap-a` (xuất phát `full/test-tong-the` @ `19944df` + tài liệu
luồng chính `claude/inspiring-cray-58xsmt`). **Chưa thử trên AutoCAD thật.** Thử tay: mục **N** của
[`checklist-thu-that.md`](../checklist-thu-that.md) (N0 = `CLAUDECHATSELFTEST`, có 2 bước mới của đợt A).*

## Đã làm (tất cả nhãn gói 3D / Full)

| # | Việc | Ở đâu | Ghi chú |
| --- | --- | --- | --- |
| A1 | **Gộp 6 lệnh dựng khối thành `draw_solid`** (`shape`: `box`, `cylinder`, `sphere`, `wedge`, `torus`, `pyramid` + tham số của hình đó) | `agent/src/tools/solids.ts`; `Drawing/SolidTools.cs` (`DrawSolid`); `Core/Solids.cs` (`SolidLogic.NormalizeShape`, `ShapeParams`, `CheckShapeParams`) | Plugin vẫn đăng ký 6 tên cũ (`draw_box`…) làm **bí danh cho macro cũ**, nhãn 3D; Claude chỉ thấy `draw_solid`. Tham số của hình khác (vd. `tube_radius` cho hộp) bị từ chối có lý do. Tên hình tiếng Việt / đồng nghĩa (`cube`, `trụ`, `nêm`…) được nhận ở plugin. Thông báo lỗi ghi đúng tên lệnh đang gọi |
| A2 | **`build_3d_from_plan` nhận layer tên khác, hỏi lại trước khi dựng** | `Core/FloorPlan.cs` (`GuessLayerKind`, `Fold`); `Drawing/PlanTo3dTools.cs` (`LayerGuesses`, `layer_guess` trong báo cáo); `agent/src/agent/confirmation.ts` (`unconfirmedPlanLayers`, `layerNameRegex`, `planLayersOf`); `agent/src/agent/session.ts` | Xem mục "Cổng layer" bên dưới |
| A3 | **Cột** từ hình kín trên mặt bằng | `PlanTo3dTools.cs` (`ReadPlan` đọc `column_layers` mặc định `A-COLS`, `A-COL` / `column_handles`; `BuildColumns`); `FloorPlanLogic.ColumnSkipReason`, `ColumnRerun`, `ColumnSizeSummary` | Mỗi hình kín (polyline kín, tròn, region…) → 1 khối trên `A-COLS-3D` (màu 1), từ `base_z` cao bằng tường. Hình có cạnh > 3000 mm trên layer cột bị bỏ qua có lý do (trừ khi nêu trong `column_handles`). `columns: "none"` để tắt. Báo cáo: `columns` {`count`, `volume_m3`, `sizes_mm`, `handles`} |
| A4 | **Nhiều tầng một lần** | `FloorPlanLogic.Storeys`; `Build3dFromPlan` (`BuildWalls` / `BuildColumns` / `BuildSlabs` cho tầng đầu, `CopyLevel` cho các tầng trên) | `storeys` (1–50), `storey_height` (mm), `roof_slab`. Tầng k ở `base_z + (k−1) × storey_height`; tường mặc định = `storey_height − slab_thickness` (L24); `wall_height` cho kèm mà cộng sàn vượt cao tầng thì báo lỗi. **Tầng đầu dựng từ mặt bằng, các tầng trên là bản sao dời lên** (không cộng khối lại từng tầng: nhanh hơn nhiều). Kiểm tra dựng lại **mọi tầng trước khi dựng gì**; báo rõ tầng nào đã có. Cao độ là mm, chia theo INSUNITS (L27). Một tầng: báo cáo như cũ; nhiều tầng: `levels` (mỗi tầng: `base_z_mm`, `wall_solid`, `columns`, `slabs`) + `total_volume_m3` |
| A5 | **Lan can** `build_railing` (lệnh mới) | `Core/Railing.cs` (`RailingLogic`); `PlanTo3dTools.cs` (`BuildRailing`); `plan3d.ts` | Theo `path_handle` (line / polyline nhẹ, đoạn thẳng, chỉ đọc) hoặc `path_points` (+ `closed`). Mọi mảnh cộng thành 1 khối trên `A-RAIL-3D` (màu 3). Đoạn cung, đường không nằm ngang (các điểm khác cao độ) bị từ chối có lý do. Tối đa 2000 mảnh (~220 m lan can song đứng) |
| A6 | **`array_rect` thêm số tầng** | `ModifyTools.cs` (`levels`, `level_spacing`); `Core/Plans.cs` (`PlanLogic.ThreeDParams`, `Blocked3dParam`); `agent/src/tools/index.ts` (`PLAN_HIDDEN_PARAMS`) | `array_rect` vẫn là lệnh chung; riêng `levels > 1` thuộc nhóm 3D: plugin từ chối ở gói 2D (cả trong `batch_execute` và macro: "array_rect (số tầng)"), trợ lý ẩn 2 tham số này với gói 2D |

Kèm theo:

- **Thời gian chờ:** `build_3d_from_plan` (không phải chạy thử) và `build_railing` được chờ 120 giây thay vì 30 giây
  (`agent/src/ipc/rpc.ts`), để AutoCAD cộng xong nhiều khối mà trợ lý không báo "hết giờ" rồi dựng lại.
- **`CLAUDECHATSELFTEST` thêm 2 bước** (`SelfTest.cs`): "Nâng cấp đợt A: dựng 2 tầng + sàn mái + 2 cột…" (kiểm cao độ
  tầng 2 và thể tích tường / cột / sàn) và "Nâng cấp đợt A: lan can thẳng 3 m…" (3 trụ, thể tích 0,03139 m³). Đây là
  cách nhanh nhất để biết phần cần AutoCAD (sao chép khối lên tầng trên, đùn cột, cộng mảnh lan can) có chạy thật không.

### Cổng layer (A2): hỏi trước khi dựng theo layer đoán

1. Plugin chỉ **đoán và báo**: báo cáo (cả chạy thử, cả thông báo "no wall recognised") có `layer_guess` = các layer
   chưa đọc mà **tên** giống tường / cửa đi / cửa sổ / sàn / cột / lan can (bỏ dấu tiếng Việt: `TUONG`, `KT_TƯỜNG`,
   `CUA SO`, `COT`, `SAN`, `A-WALL-NEW`…). Không đoán: layer kết quả (`…3D`), chữ, kích thước, hatch, đường trục / tim
   (`TRUC`, `TIM`, `AXIS`), tên chỉ chứa lẫn chữ (`C-SIDEWALK`, `A-OUTDOOR`, `SANH`). Loại đã truyền bằng handle hoặc đã
   tắt thì không đoán.
2. **Trợ lý chặn** (`confirmationError`): lệnh dựng (không phải `dry_run`, kể cả trong `batch_execute`) có layer khác
   mặc định trong `wall_layers` / `door_layers` / `window_layers` / `slab_layers` / `column_layers` bị từ chối
   ("Confirmation required…") trừ khi:
   - người dùng **tự viết tên layer đó** trong một tin của cuộc chat có nói về layer ("tường ở layer TUONG",
     "lớp CUA là cửa"); chữ "tường" / "tuong" trong câu thường không tính là layer `TUONG`; hoặc
   - lượt này trả lời **thẻ xác nhận / thẻ kế hoạch có ghi đúng tên layer đó** (nút Đồng ý / Làm, hoặc gõ "ok" trả
     lời thẻ). Đồng ý cho thẻ khác (vd. "Xóa 30 đối tượng?") không mở layer nào; gõ "ok" khi Claude chỉ hỏi bằng chữ
     (không có thẻ) cũng không mở: câu hỏi luôn đi qua thẻ của plugin; hoặc
   - layer đó **đã được đồng ý trước đó trong cùng cuộc chat** (dựng tầng 2, dựng lại không hỏi lại). Chuyển bản vẽ /
     chat mới thì hỏi lại.
3. Tên layer phải khớp **cả tên** (`CUA` không khớp `CUA-SO`), không phân biệt hoa thường. Mẫu có `*` (`KT_TUONG*`)
   khớp khi thẻ / câu ghi chính mẫu đó hoặc một tên nó bao; `*`, `T*` (dưới 3 ký tự cố định) không bao giờ được coi là
   đã nêu.
4. Đây là cổng **riêng cho việc dựng mới**, không nới luật thẻ 3D (đổi / xóa khối có trước vẫn chỉ nút Đồng ý trên thẻ
   có đúng handle mới mở).

### Số mặc định của lan can (mm)

| Thông số | Giá trị |
| --- | --- |
| Cao (đỉnh tay vịn so với sàn) | **1100** (cho phép 300–3000) |
| Tay vịn trên / thanh dưới | 50 × 50; thanh dưới cách sàn 100 |
| Trụ | 50 × 50, ở mọi góc và cách nhau tối đa **1500** (`post_spacing` 200–6000) |
| Song (mặc định `infill: "bars"`) | 20 × 20, khe hở thông thủy ≤ **100** (ít song nhất thỏa điều kiện) |
| Tấm (`infill: "panel"`) | dày 20, từ 100 tới đáy tay vịn |
| `infill: "none"` | chỉ trụ + tay vịn |
| Layer | `A-RAIL-3D` |

## Commit

`37209c4` (viết 6 việc, CI run 43390204331 xanh) và commit "Nâng cấp đợt A: tự rà…" sau đó (xem
`git log full/nang-cap-a`). Commit sau chỉ chạy test + build trên máy cloud; CI của thread con Test & sửa phủ nó.

## Test

- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests` — 439 test (369 trước đợt A). `NangCapATests.cs`: nhãn, bí danh macro,
  tên hình, tham số sai hình, đoán layer (18 tên đúng, 16 tên không đoán), cột ≤ 3 m, cột dựng lại chỉ so với cột (cột
  người dùng dựng trong tường không bị coi là dựng lại tường), tầng / tường = tầng − sàn / sàn mái / lỗi chiều cao, lan
  can 3 m (các mảnh chỉ chạm nhau, tổng 31 390 000 mm³) và vòng kín, khe song ≤ 100, giới hạn 2000 mảnh, `levels` theo
  gói, kiểm mã nguồn các luật cần AutoCAD. Sửa số đếm 29 → 31 nhãn 3D ở test cũ.
- Trợ lý: `cd agent && npm ci && npm test` — 232 test (215 trước đợt A). `test/nangcapa.test.ts` + 1 test luồng trong
  `session.test.ts`: `draw_solid` thay 6 lệnh; cổng layer (từ chối, thẻ có tên layer, Hủy, thẻ khác, gõ ok có / không
  có thẻ, người dùng tự nêu layer, câu không dấu "tuong", "mai lop ton", nhớ trong cuộc chat, quên khi chuyển bản vẽ,
  thẻ kế hoạch, `batch_execute`, mẫu `*`); tham số tầng và cột; số mặc định lan can khớp plugin; `levels` ẩn ở gói 2D;
  thời gian chờ 120 s. Sửa test cũ: 25 lệnh 3D Claude thấy, 31 nhãn (6 bí danh).
- Build net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`): xanh, 0 cảnh báo.
- CI: run 43390204331 xanh ở `37209c4` (`release: "false"`).
- Máy cloud chưa có .NET: `apt-get update && apt-get install -y dotnet-sdk-10.0 dotnet-runtime-8.0` (script cài của
  dot.net bị proxy chặn; test lõi chạy trên runtime 8, build cần SDK 10).

## Token mô tả lệnh + system prompt (cùng cách đo, ~3,5 ký tự / token)

Đo như plugin gửi (`plan_tools` theo gói, kể cả 5 lệnh thẻ, schema JSON + mô tả + system prompt). Trước = `19944df`.

| Gói | Số lệnh trước → sau | Ký tự trước | Ký tự sau | ≈ token |
| --- | --- | --- | --- | --- |
| 2D | 95 → 95 | 120 311 | 120 311 | ~34,4k (không đổi) |
| 3D | 89 → 85 | 124 940 | 124 993 | ~35,7k (không đổi) |
| Full | 124 → 120 | 173 852 | 173 905 | ~49,7k (không đổi) |

- Gộp 6 lệnh: 6 547 → 2 625 ký tự, **bớt ~3,9k ký tự ≈ 1,1k token** (ước cũ ~2k là cao: 6 lệnh vốn đã được rút gọn
  ngày 12/11).
- Phần thêm của đợt A dùng lại đúng khoản đó: `build_railing` +1,4k ký tự, tham số tầng / cột / đoán layer của
  `build_3d_from_plan` +1,5k, `levels` của `array_rect` +0,3k, luật 3D +0,7k. Tổng không đổi so với trước đợt A.
- Hai bản đo cùng một script nên so trước / sau là chuẩn; số tuyệt đối thấp hơn bảng `test-tong-the.md` vài % (cách
  đếm khác một chút), không phải do đợt A.

## Giữ đúng luật đã chốt

- Phương án A: lệnh mới đều nhãn 3D; gói 3D không thêm chữ / kích thước / hatch.
- Thẻ xác nhận 3D: không lệnh mới nào đổi / xóa khối có trước (cột, tầng, lan can chỉ dựng mới; nét mặt bằng và đường
  lan can chỉ đọc).
- Lệnh chưa nhãn chỉ mở cho Full; test bắt buộc nhãn (`Every_registered_tool_has_a_plan_label`, trợ lý `tools.test.ts`)
  qua cho `draw_solid`, `build_railing` và 6 bí danh.
- Hành vi đã sửa (L24, L27, KT-14, KT-16, KT-20, KT-22, KT-23): một tầng đi đúng đường cũ (cùng hàm kiểm tra dựng
  lại, cùng thông báo, cùng báo cáo); nhiều tầng chỉ lặp kiểm tra theo `base_z` từng tầng rồi sao chép tầng đầu.
- Macro: `column_handles`, `path_handle` được ánh xạ handle khi chạy lại macro (`MacroLogic.IsHandleKey` đã nhận mọi
  `*_handle(s)`); macro cũ có `draw_box`… vẫn chạy.

## Giới hạn / còn lưu ý

- Chưa thử trên AutoCAD thật (cả 6 việc). Phần cần AutoCAD (sao chép khối lên tầng trên bằng `Clone` + dời, đùn region
  cột, cộng hàng trăm hộp lan can, đọc polyline) chỉ kiểm bằng build + kiểm mã nguồn; N0 (self-test) thử đúng các phần
  này.
- Đoán layer chỉ theo tên; layer chứa lẫn nhiều loại (tường + cột trên cùng layer) Claude vẫn phải hỏi. Layer chuẩn
  ghi sẵn trong skill công ty hay ghi chú dự án vẫn bị hỏi một lần mỗi cuộc chat (cổng chỉ biết thẻ và câu của người
  dùng). Cổng không chặn được việc Claude tự gom handle trên layer đoán rồi truyền `wall_handles` (chỉ có luật trong
  prompt và thông báo từ chối nói rõ không làm vậy).
- Cột: đứng thẳng, cao bằng tường của tầng, chồng thể tích với tường nếu vẽ lồng trong tường (không trừ). Tường đã
  dựng từ trước mà muốn thêm cột: `extrude` các hình cột (lệnh dựng lại cả mặt bằng sẽ báo tường đã có).
- Nhiều tầng: mọi tầng cùng mặt bằng, cùng chiều cao; tầng khác mặt bằng gọi lại từng tầng với `base_z`.
- Lan can: chỉ đoạn thẳng nằm ngang, đặt giữa đường; không tự né cột / tường; polyline kiểu cũ (2D / 3D polyline) phải
  truyền bằng `path_points`.
- `array_rect` `levels` nhân số bản sao: giới hạn chung `MaxArrayCopies` tính cả số tầng.

## Việc cho thread con Test & sửa đợt A

Rà 6 việc trên, đặc biệt: cổng layer (có lách được không: `wall_handles` lấy từ layer đoán, mẫu `*`, tên layer có ký
tự lạ), `layer_guess` khi đã truyền layer, nhiều tầng + `on_existing` / `dry_run` / tầng đã có một phần, cột + kiểm tra
dựng lại, `CopyLevel` (thứ tự handle, số liệu từng tầng), lan can vòng kín / đoạn rất ngắn / đơn vị mét, `levels` trong
`batch_execute` ở gói 2D, macro cũ 6 lệnh, token của `draw_solid`, 2 bước self-test mới (số mong đợi có đúng không).
