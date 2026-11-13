# Bàn giao: Nâng cấp đợt A (3D)

*13/11/2026 · thread Nâng cấp đợt A · nhánh `full/nang-cap-a` (xuất phát `full/test-tong-the` @ `19944df` + tài liệu
luồng chính `claude/inspiring-cray-58xsmt`). Chưa thử trên AutoCAD thật. Thử tay: mục **N** của
[`checklist-thu-that.md`](../checklist-thu-that.md).*

## Đã làm (tất cả nhãn gói 3D / Full)

| # | Việc | Ở đâu | Ghi chú |
| --- | --- | --- | --- |
| A1 | **Gộp 6 lệnh dựng khối thành `draw_solid`** (`shape`: `box`, `cylinder`, `sphere`, `wedge`, `torus`, `pyramid` + tham số của hình đó) | `agent/src/tools/solids.ts`; `Drawing/SolidTools.cs` (`DrawSolid`); `Core/Solids.cs` (`SolidLogic.NormalizeShape`, `ShapeParams`, `CheckShapeParams`) | Plugin vẫn đăng ký 6 tên cũ (`draw_box`…) làm **bí danh cho macro cũ**, nhãn 3D; Claude chỉ thấy `draw_solid`. Tham số của hình khác (vd. `tube_radius` cho hộp) bị từ chối có lý do. Tên hình tiếng Việt / đồng nghĩa (`cube`, `trụ`, `nêm`…) được nhận ở plugin |
| A2 | **`build_3d_from_plan` nhận layer tên khác, hỏi lại trước khi dựng** | `Core/FloorPlan.cs` (`GuessLayerKind`, `Fold`); `Drawing/PlanTo3dTools.cs` (`LayerGuesses`, `layer_guess` trong báo cáo); `agent/src/agent/confirmation.ts` (`unconfirmedPlanLayers`) | Báo cáo (cả chạy thử) có `layer_guess` = layer chưa đọc mà tên giống tường / cửa / cửa sổ / sàn / cột / lan can (bỏ dấu tiếng Việt; bỏ qua layer kết quả `…3D`, chữ, kích thước, hatch). **Cổng ở trợ lý:** lệnh dựng (không phải `dry_run`) dùng `wall_layers` / `door_layers` / `window_layers` / `slab_layers` / `column_layers` khác mặc định mà câu của người dùng không nêu tên → bị từ chối ("Confirmation required"), Claude phải hỏi bằng `request_confirmation` rồi dừng lượt. Mở bằng nút Đồng ý, nút "Làm" của thẻ kế hoạch, hoặc gõ đồng ý trả lời câu hỏi (đây là dựng mới, không đổi khối cũ, nên không cần riêng nút thẻ) |
| A3 | **Cột** từ hình kín trên mặt bằng | `PlanTo3dTools.cs` (`ReadPlan` đọc `column_layers` mặc định `A-COLS`, `A-COL` / `column_handles`; `BuildColumns`) | Mỗi hình kín (polyline kín, tròn, region…) → 1 khối trên `A-COLS-3D` (màu 1), từ `base_z` cao bằng tường. Hình có cạnh > 3000 mm trên layer cột bị bỏ qua có lý do (trừ khi nêu trong `column_handles`). `columns: "none"` để tắt. Cột có từ lần trước trên `A-COLS-3D` vào kiểm tra dựng lại |
| A4 | **Nhiều tầng một lần** | `FloorPlanLogic.Storeys`; vòng lặp tầng trong `Build3dFromPlan` (`BuildWalls`, `BuildColumns`, `BuildSlabs`) | `storeys` (1–50), `storey_height` (mm), `roof_slab`. Tầng k ở `base_z + (k−1) × storey_height`; tường mặc định = `storey_height − slab_thickness` (L24); `wall_height` cho kèm mà cộng sàn vượt cao tầng thì báo lỗi. Kiểm tra dựng lại **mọi tầng trước khi dựng gì**; thông báo vẫn "Never erase other storeys". Cao độ là mm, chia theo INSUNITS (L27). Một tầng: báo cáo như cũ; nhiều tầng: thêm `levels` |
| A5 | **Lan can** `build_railing` (lệnh mới) | `Core/Railing.cs` (`RailingLogic`); `PlanTo3dTools.cs` (`BuildRailing`); `plan3d.ts` | Theo `path_handle` (line / polyline đoạn thẳng, chỉ đọc) hoặc `path_points` (+ `closed`). Mọi mảnh cộng thành 1 khối trên `A-RAIL-3D` (màu 3). Đoạn cung bị từ chối có lý do |
| A6 | **`array_rect` thêm số tầng** | `ModifyTools.cs` (`levels`, `level_spacing`); `Core/Plans.cs` (`PlanLogic.ThreeDParams`, `Blocked3dParam`); `agent/src/tools/index.ts` (`PLAN_HIDDEN_PARAMS`) | `array_rect` vẫn là lệnh chung; riêng `levels > 1` thuộc nhóm 3D: plugin từ chối ở gói 2D (cả macro: "array_rect (số tầng)"), trợ lý ẩn 2 tham số này với gói 2D |

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

Xem `git log full/nang-cap-a` (commit "Nâng cấp đợt A: …").

## Test

- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests` — thêm `NangCapATests.cs` (nhãn, bí danh macro, tên hình, tham số sai
  hình, đoán layer 18 tên đúng + 10 tên không đoán, cột ≤ 3 m, tầng / tường = tầng − sàn / sàn mái / lỗi chiều cao,
  lan can 3 m và vòng kín, khe song ≤ 100, `levels` theo gói, kiểm mã nguồn các luật cần AutoCAD). Sửa số đếm 29 → 31
  nhãn 3D ở test cũ.
- Trợ lý: `cd agent && npm ci && npm test` — thêm `test/nangcapa.test.ts` (draw_solid thay 6 lệnh, cổng layer đoán:
  từ chối / mở bằng thẻ / gõ đồng ý / người dùng tự nêu layer / chạy thử / layer mặc định / trong `batch_execute`,
  tham số tầng và cột, số mặc định lan can khớp plugin, `levels` ẩn ở gói 2D). Sửa test cũ: 25 lệnh 3D Claude thấy, 31
  nhãn (6 bí danh).
- Build net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`): xanh, 0 cảnh báo.

## Token mô tả lệnh + system prompt (cùng cách đo, ~3,5 ký tự / token)

Đo như plugin gửi (`plan_tools` theo gói, kể cả 5 lệnh thẻ, schema JSON + mô tả + system prompt). Trước = `19944df`.

| Gói | Số lệnh trước → sau | Ký tự trước | Ký tự sau | ≈ token |
| --- | --- | --- | --- | --- |
| 2D | 95 → 95 | 120 311 | 120 311 | ~34,4k (không đổi) |
| 3D | 89 → 85 | 124 940 | 124 796 | ~35,7k (−0,04k) |
| Full | 124 → 120 | 173 852 | 173 708 | ~49,6k (−0,04k) |

- Gộp 6 lệnh: 6 547 → 2 625 ký tự, **bớt ~3,9k ký tự ≈ 1,1k token** (ước cũ ~2k là cao: 6 lệnh vốn đã được rút gọn
  ngày 12/11).
- Phần thêm của đợt A dùng lại gần hết khoản đó: `build_railing` +1,4k ký tự, tham số tầng / cột / đoán layer của
  `build_3d_from_plan` +1,4k, `levels` của `array_rect` +0,3k, luật 3D +0,6k. Tổng gần như không đổi so với trước đợt A.
- Hai bản đo cùng một script nên so trước / sau là chuẩn; số tuyệt đối thấp hơn bảng `test-tong-the.md` vài % (cách
  đếm khác một chút), không phải do đợt A.

## Giữ đúng luật đã chốt

- Phương án A: lệnh mới đều nhãn 3D; gói 3D không thêm chữ / kích thước / hatch.
- Thẻ xác nhận 3D: không lệnh mới nào đổi / xóa khối có trước (cột, tầng, lan can chỉ dựng mới; nét mặt bằng chỉ đọc).
  Cổng layer đoán là cổng **riêng** cho việc dựng mới, không nới luật thẻ 3D.
- Lệnh chưa nhãn chỉ mở cho Full; test bắt buộc nhãn (`Every_registered_tool_has_a_plan_label`, trợ lý `tools.test.ts`)
  qua cho `draw_solid`, `build_railing` và 6 bí danh.
- Hành vi đã sửa (L24, L27, KT-14, KT-16, KT-20, KT-22, KT-23): một tầng giữ y nguyên đường đi cũ (cùng hàm kiểm tra
  dựng lại, cùng thông báo); nhiều tầng chỉ lặp lại nó theo `base_z` từng tầng.
- Macro: `column_handles`, `path_handle` được ánh xạ handle khi chạy lại macro (`MacroLogic.IsHandleKey` đã nhận mọi
  `*_handle(s)`).

## Giới hạn / còn lưu ý

- Chưa thử trên AutoCAD thật (cả 6 việc). Phần cần AutoCAD (đùn region cột, cộng hàng trăm hộp lan can, đọc polyline)
  chỉ kiểm bằng build + kiểm mã nguồn.
- Đoán layer chỉ theo tên; layer chứa lẫn nhiều loại (tường + cột trên cùng layer) Claude vẫn phải hỏi. Cổng trợ lý coi
  layer "đã được người dùng nêu" khi tên layer có trong câu của họ (không phân biệt hoa thường; "tường" không tính là
  layer `TUONG`).
- Cột: chỉ đứng thẳng, cao bằng tường của tầng; cột khác chiều cao dựng riêng bằng `draw_solid` / `extrude`.
- Nhiều tầng: mọi tầng cùng mặt bằng, cùng chiều cao; tầng khác mặt bằng gọi lại từng tầng với `base_z`.
- Lan can: chỉ đoạn thẳng, đặt giữa đường; không tự né cột / tường.
- `array_rect` `levels` nhân số bản sao: giới hạn chung `MaxArrayCopies` tính cả số tầng.

## Việc cho thread con Test & sửa đợt A

Rà 6 việc trên, đặc biệt: cổng layer đoán (có lách được không: `wall_handles` lấy từ layer đoán, wildcard), báo cáo
`layer_guess` khi đã truyền layer, nhiều tầng + `on_existing` / `dry_run`, cột + kiểm tra dựng lại, lan can vòng kín /
đoạn rất ngắn, `levels` trong `batch_execute` ở gói 2D, macro cũ 6 lệnh, token giảm thật của `draw_solid`.
