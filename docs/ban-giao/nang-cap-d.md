# Bàn giao: Nâng cấp đợt D (3D)

*13/11/2026 · thread Nâng cấp đợt D · nhánh `full/nang-cap-d` (xuất phát `full/nang-cap-c-test` @ `f4f27fe` + tài liệu
luồng chính `claude/inspiring-cray-58xsmt`; `full/test-tong-the` @ `19944df` đã có sẵn). **Chưa thử trên AutoCAD thật.**
Thử tay: mục **Q** của [`checklist-thu-that.md`](../checklist-thu-that.md).*

## Đã làm (nhãn gói 3D / Full)

| # | Việc | Lệnh | Ở đâu | Ghi chú |
| --- | --- | --- | --- | --- |
| D1 | **Dầm** | `build_beam` (mới) | `Drawing/Solid3Tools.cs` (`BuildBeam`); `Solid3Logic.Beam`, `BeamTop`, `CheckBeam` | Theo `path_handles` (line, arc, polyline nhẹ có đoạn thẳng và cung; mỗi đường một dầm; chỉ đọc) hoặc `path_points` + `path_bulges` (+ `closed`). Tiết diện `width` × `height` (mặc định **220 × 400**, 50–3000 × 50–5000 mm), tâm dầm trên đường. Đỉnh dầm = `top_z` (mm, mặc định z của đường) − `slab_thickness` (dầm dưới sàn). Mỗi đoạn: chữ nhật (đoạn thẳng) hoặc hình quạt vành khăn (đoạn cung), đầu vuông; mỗi góc thêm miếng vát mép ngoài (góc ≤ 120°: miter; gắt hơn: vát phẳng, không nhọn), các mảnh cộng thành 1 khối. Đường không nằm ngang, cung bán kính < nửa bề rộng: từ chối có lý do. Layer `A-BEAM-3D` (màu 4) |
| D2 | **Tường cong** trong `build_3d_from_plan` | (nâng cấp) | `PlanTo3dTools.cs` (`ReadWallCurve`, `BuildWalls`, `CurvedReport`); `Solid3Logic.CurvedWalls`, `ArcPieces`, `AddGivenArcOpening`, `DropCurvedCaps`, `SectorOutlines` | Đọc được: hai **cung đồng tâm** cách nhau một bề dày (ARC, CIRCLE, đoạn cung của polyline) trên layer tường, polyline có bề rộng có đoạn cung, `centerline_handles` là cung + `thickness`. Chỉ ghép hai cung kề nhau (có cung thứ ba ở giữa thì không ghép cung ngoài với cung trong). Các đoạn cùng tâm / bán kính / dày gộp thành một tường; khe giữa các đoạn (300–6000 mm, dưới ¼ vòng) là chỗ cửa: có ký hiệu cửa → **khoét đúng** (cửa đi: lanh tô; cửa sổ: bậu + lanh tô), không ký hiệu → để hở cả chiều cao (báo `open_gaps`). Ký hiệu cửa sổ đè lên đoạn cong liền → lỗ theo góc phủ của ký hiệu. `openings` theo điểm trên tường cong (cần `width` khi tường liền). Đường bịt hai đầu tường cong tính là "cap". Cung đơn không có cặp → `skipped` có lý do. Ellipse / spline vẫn bỏ qua. Báo cáo: `curved_walls` [{tâm, `radius_mm`, `thickness_mm`, `length_m`, `parts_deg`, `openings`, `open_gaps`}] |
| D3 | **PRESSPULL** | `presspull` (mới) | `Solid3Tools.cs` (`Presspull`); `Solid3Logic.NormalizePresspullMode`, `ResolvePresspull`; trợ lý `confirmation.ts` | (1) `handle` + `faces` / `face_filter` (+ `topology_id`): đẩy các mặt đó theo pháp tuyến ngoài (`Solid3d.ExtrudeFaces`; dương = ra, khối lớn lên). (2) Hình kín (`profile_points` / `profile_handles`) kéo theo pháp tuyến: khối mới; có `target` thì thêm vào / khoét bớt khối đó (`mode` auto: phần kéo lấn vào khối → khoét, ra ngoài → thêm; `add`; `subtract`; `new`). Khối còn lại rỗng → từ chối, không đổi gì. **Thẻ xác nhận:** `presspull` thuộc `ALWAYS_CONFIRM` (như slice / fillet / shell): khối `handle` / `target` có trước lượt phải có nút Đồng ý trên thẻ; gõ "ok" không thay được; khối dựng trong lượt thì không hỏi. Đường biên xóa (`erase_profile`) theo luật đếm thường |
| D4 | **Xuất STL** | `export_stl` (mới) | `Solid3Tools.cs` (`ExportStl`); `Solid3Logic.ReadStl`, `WriteBinaryStl`, `WriteAsciiStl`, `DefaultStlName`, `PositiveShift` | `handles` hoặc mọi khối (lọc `layers`) → **một file** `.stl` (mặc định `<bản vẽ>-3d.stl` cạnh bản vẽ; tên tương đối cũng nằm cạnh bản vẽ). Đường dẫn kiểm như `plot_pdf`: phải đuôi `.stl`, tên dành riêng / ký tự cấm → lỗi, thư mục không có → lỗi, là thư mục → lỗi. File đã có → từ chối trừ `overwrite: true` (Claude hỏi trước). Đơn vị **mm** (bản vẽ mét được nhân tỉ lệ), dời vào góc phần tám dương khi có tọa độ âm (`moved_by_mm`): bản sao tạm trong một transaction bị hủy ngay, bản vẽ không đổi. Mỗi khối `StlOut` ra file tạm, gộp tam giác rồi ghi binary (hoặc `ascii: true`). Không thêm bước hoàn tác, chạy được khi "Chỉ xem" |
| D5 | **Bảng khối lượng theo layer** | `volume_schedule` (mới) | `Solid3Tools.cs` (`VolumeSchedule`); `Solid3Logic.Schedule`, `LayerRank`, `VolumeTable` | Mỗi layer: số khối, thể tích, phần giao đã trừ, thể tích thực, diện tích mặt (`Solid3d.Area`, tắt bằng `surface_area: false`); dòng tổng. **Cột lồng tường (lưu ý đợt A):** đo phần giao của mọi cặp khối chồng hộp bao (tối đa 2000 cặp), `overlap: "deduct"` (mặc định) trừ phần giao **một lần** khỏi khối xếp sau: cột → dầm → tường → sàn → còn lại (theo tên layer, có dấu / không dấu: `COT`, `DAM`, `TUONG`, `SAN`…), hoặc theo `priority`; không trừ quá thể tích của khối. `overlap: "none"`: không trừ, cảnh báo tính chồng. Cặp không đo được / quá giới hạn: cảnh báo. Chỉ đọc khi không chèn bảng |
| D6 | **Va chạm ra bảng** | `check_interference` (nâng cấp: `table_position`, `title`, `text_height`, `decimals`) | `Solid2Tools.cs` (`CheckInterferenceTool`); `Solid3Logic.ClashTable` | Mỗi cặp va chạm có thêm `layer_a` / `layer_b`. Có `table_position`: bảng STT / khối 1 / layer 1 / khối 2 / layer 2 / thể tích giao (m³) + tổng; cặp không đo được ghi "chưa đo được"; không có va chạm thì không chèn. Gọi cũ không đổi |

**Gói (phương án A):** 4 lệnh mới nhãn **ThreeD** (`Core/Plans.cs`). **Bảng trong bản vẽ là chữ → chỉ gói Full:**
`PlanLogic.TextParams` thêm `volume_schedule` / `check_interference` → `table_position`, nên plugin từ chối
`table_position` ở gói 3D (`NOT_IN_PLAN`, cả trong `batch_execute` và macro: "… (bảng trong bản vẽ)"), lời báo nói rõ gọi
lại không có `table_position` và đưa số vào khung chat. Trợ lý ẩn `table_position` / `title` / `text_height` của hai lệnh
này ở gói 3D (`PLAN_HIDDEN_PARAMS`), thêm câu "show the numbers in the chat".

Kèm theo:

- Thời gian chờ 120 giây cho `build_beam`, `volume_schedule`, `export_stl` (`agent/src/ipc/rpc.ts`): cộng / đo nhiều khối.
- Skill `ve-3d-kien-truc`: layer `A-BEAM-3D`, tường cong, dầm, khối lượng (vài dòng).
- System prompt không đổi.

## Giữ đúng luật đã chốt

- Thẻ xác nhận 3D: `presspull` trên khối có trước lượt chỉ mở bằng nút Đồng ý trên thẻ có handle đó (kể cả trong
  `batch_execute`). Các lệnh còn lại chỉ tạo mới (dầm, tường, bảng) hoặc chỉ đọc (STL, số liệu).
- Test bắt buộc nhãn (`Every_registered_tool_has_a_plan_label`, `Every_3d_tool_is_labelled_ThreeD…`, trợ lý
  `crossplan.test.ts`) qua với 4 lệnh mới; số đếm cũ sửa 31 → 35 nhãn 3D, 25 → 29 lệnh 3D Claude thấy.
- Hoàn tác: mọi lệnh ghi chạy trong transaction + nhóm undo của lượt; U một lần bỏ cả lượt. `export_stl` không đổi bản vẽ.
- Tên lệnh cũ giữ nguyên; gọi cũ của `check_interference` / `build_3d_from_plan` hợp lệ như trước.
- Hành vi 3D cũ: tường thẳng, cột, nhiều tầng, kiểm dựng lại (nay so cả hộp bao các mảnh cong), lan can không đổi; test
  cũ của đợt 3 / A vẫn qua (1 test kiểm mã nguồn cập nhật theo tên biến mới).

## Test

- Lõi: `dotnet test plugin/ClaudeCAD.Core.Tests` — **651** test (613 trước). `NangCapDTests.cs` (38): nhãn, bảng chỉ ở
  Full (3D bị chặn `table_position`, macro), đăng ký / `readsWhen`; cung từ bulge / 3 điểm; quạt vành khăn (diện tích,
  tách > nửa vòng, hộp bao); dầm thẳng, chữ L rẽ trái / phải (miếng góc 100 × 100), góc gắt vát phẳng, dầm cong, cung
  quá gắt, vòng kín, kiểm kích thước, cao độ; tường cong một cặp (thể tích), cung đơn, ba cung, khe cửa có / không ký hiệu
  (lanh tô 2200 → 3300), cửa sổ đè tường liền (bậu, lanh tô, thể tích), tường theo trục, tường tròn, cửa cho theo điểm (cả
  qua 0°), khe vòng còn lại không phải cửa, đường bịt đầu; PRESSPULL các chế độ; STL binary / ascii đọc lại, header bắt
  đầu "solid", tên file, dời góc; khối lượng (cột lồng tường trừ một lần, `none`, `priority`, không đo được, không trừ quá
  khối), thứ hạng layer 9 tên, bảng khối lượng / va chạm (dấu phẩy, tổng); kiểm mã nguồn các phần cần AutoCAD.
- Trợ lý: `cd agent && npm ci && npm test` — **279** test (270 trước). `test/nangcapd.test.ts` (9): nhãn, gói 3D / 2D thấy gì,
  `table_position` ẩn ở gói 3D (cả mô tả), schema, số mặc định khớp plugin, thời gian chờ, PRESSPULL qua thẻ (gõ ok không
  được, khối mới trong lượt không hỏi, trong batch), hình kín không target không hỏi, khối mới là "đã tạo".
- Build net48 / net8.0-windows / net10.0-windows (Linux, `-p:EnableWindowsTargeting=true`): xanh, 0 cảnh báo.
- CI: xem mục Commit.

## Token mô tả lệnh + system prompt (cùng cách đo với đợt A / C, ~3,5 ký tự / token)

Đo như plugin gửi (`plan_tools` theo gói, kể cả 5 lệnh thẻ, tên + mô tả + schema JSON + system prompt). Trước = `f4f27fe`.

| Gói | Số lệnh trước → sau | Ký tự trước | Ký tự sau | ≈ token |
| --- | --- | --- | --- | --- |
| 2D | 105 → 105 | 135 344 | 135 344 | ~38,7k (không đổi) |
| 3D | 87 → 91 | 127 011 | 133 560 | ~36,3k → ~38,2k (**+1,9k**) |
| Full | 130 → 134 | 189 046 | 196 259 | ~54,0k → ~56,1k (**+2,1k**) |

Phần thêm: mô tả + schema 4 lệnh mới (~6,0k ký tự), tham số bảng của `check_interference` (~0,6k ở Full; gói 3D bị ẩn).
(Số "trước" của 2D / Full chênh vài chục ký tự so với bảng đợt C: script đo viết lại, cùng một script cho hai bản.)

## Giới hạn / còn lưu ý

- Chưa thử trên AutoCAD thật. Phần cần AutoCAD: `ExtrudeFaces` (hướng dương = ra ngoài, theo tài liệu), `StlOut` (file
  tạm, bản sao trong transaction bị hủy khi phải dời / đổi tỉ lệ), `Solid3d.Area`, region từ polyline có bulge (quạt vành
  khăn), hợp khối các mảnh dầm / tường cong.
- Dầm: một tiết diện cho cả đường; không tự cắt ở cột / tường (dùng `volume_schedule` để trừ phần giao khi tính khối lượng).
- Tường cong: chỉ cung tròn (không ellipse / spline); khe cửa trên tường cong phải dưới ¼ vòng; tường cong và tường thẳng
  gặp nhau ở góc chỉ cộng khối (không tự cắt vát).
- Bảng khối lượng: ba khối chồng nhau ở một chỗ thì phần chung của cả ba có thể trừ hai lần (vẫn không trừ quá thể tích
  từng khối); giới hạn 2000 cặp chồng hộp bao mỗi lượt gọi (quá thì cảnh báo, chia theo layer / phần).
- STL: một file cho mọi khối (không tách file theo khối); không chọn độ mịn lưới (theo mặc định AutoCAD).
- `CLAUDECHATSELFTEST` chưa có bước của đợt D: để thread con cân nhắc (dầm chữ L, tường cong có cửa, PRESSPULL, STL).

## Việc cho thread con Test & sửa đợt D

Rà 6 việc trên, đặc biệt: dầm góc gắt / đoạn rất ngắn / đơn vị mét / polyline mặt -Z; tường cong nối tường thẳng, khe cửa
tại 0°, cung đảo chiều, symbol cửa vừa gần tường cong vừa gần tường thẳng, `dry_run`, kiểm dựng lại với tường cong; PRESSPULL
trong `batch_execute`, `target` = khối vừa dựng, `mode` "new" có `target`, khoét hết khối; STL tên lạ / ghi đè / bản vẽ
mét / tọa độ âm, transaction tạm khi "Chỉ xem"; khối lượng với tường nhiều tầng + cột, layer tiếng Việt, `priority`; bảng ở
gói 3D (plugin + trợ lý + macro); token (rút gọn mô tả nếu đáng).

## Commit

Xem `git log full/nang-cap-d`.
