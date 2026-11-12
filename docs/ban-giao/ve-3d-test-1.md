# Bàn giao: Test & sửa 3D đợt 1 (sub-thread "Vẽ 3D đợt 1 > Test & sửa")

*12/11/2026 · thread cha "Vẽ 3D đợt 1" giao. Nhánh `full/ve-3d-test-1`, xuất phát `full/ve-3d` @ `d44aa26`.
Bước 1 trong chuỗi chủ dự án đặt (test & sửa đợt 1 → viết đợt 2 → …). Đợt 2 xuất phát từ commit cuối nhánh này.*

Máy cloud không có AutoCAD: "test" = rà code, đối chiếu tài liệu API AutoCAD .NET (`AcDbMgd.xml` trong gói NuGet
`AutoCAD.NET.Model` 24.1 / 25.0 / 26.0, tức AutoCAD 2022–2027), viết test tự động, sửa lỗi. Chỗ chỉ thấy được trên
AutoCAD thật: viết code an toàn nhất có thể và đưa vào checklist phần J (J10–J15).

## Đã rà

| Phần | File | Kết quả |
| --- | --- | --- |
| 14 lệnh 3D | `plugin/ClaudeCAD.Plugin/Drawing/SolidTools.cs` | 7 lỗi, đã sửa (bảng dưới) |
| Tham số API | `CreateBox / CreateFrustum / CreateSphere / CreateWedge / CreateTorus / CreatePyramid`, `Extrude`, `Revolve`, `BooleanOperation`, `Region.CreateFromCurves` | Khớp tài liệu: `CreateFrustum(h, rx, ry, rTop)` và `CreatePyramid(h, sides, r, rTop)` đặt gốc ở **giữa chiều cao** (plugin nâng h/2), `radius` của chóp là bán kính **nội tiếp** đáy (đúng như mô tả lệnh). `CreateWedge` chỉ ghi "tâm ở gốc", không nói hướng dốc → giữ cách đọc trọng tâm, làm chặt hơn. `Revolve` chiếu trục lên mặt phẳng biên dạng |
| Hướng nêm, góc vát | `DrawWedge`, `Extrude` | Nêm: xem lỗi 4. Góc vát dương = thu nhỏ lên trên theo lệnh EXTRUDE; tài liệu API không nói dấu → thử tay J11 |
| Khối "công cụ" sau `BooleanOperation` | `SolidBoolean`, `SolidSubtract` | Đúng: khối công cụ rỗng sau phép toán và bị xóa (trừ `keep_tools`, khi đó trừ bằng bản sao rồi Dispose) |
| Kiểu hiển thị | `VisualStyleId`, `ApplyVisualStyle`, `capture_view` | Lỗi 3 (AutoCAD không phải tiếng Anh). Tên enum `VisualStyleType` đã đối chiếu bằng reflection trên `AcDbMgd.dll` |
| Đơn vị | cả file | Giống 2D: coi đơn vị bản vẽ = mm (INSUNITS khác 4 thì `get_drawing_info` đã cảnh báo). Không đổi |
| Transaction, hoàn tác U | `ToolDispatcher` | Lệnh dựng / cộng trừ chạy trong transaction của lượt, lỗi thì lùi lại; trong batch mỗi lệnh có transaction lồng. `set_view_3d` / `set_visual_style` không mở nhóm U, không sao lưu phiên bản. Đúng |
| "Chỉ xem" | `ToolDispatcher.ReadOnlyAllowed` | Chỉ 2 lệnh đổi hiển thị được phép; mọi lệnh dựng bị từ chối. Đúng |
| Chặn theo gói | `PlanLogic`, `RequireInPlan` | Kiểm ở `HandleAsync` (một lệnh, macro một lệnh) và từng lệnh trong `batch_execute` (macro nhiều lệnh chạy qua batch). Máy gói 2D bị từ chối mọi lệnh 3D. Đúng, không sửa logic gói |
| Batch / macro | `ToolDispatcher`, `Core/Macros.cs` | `set_view_3d` / `set_visual_style` bị chặn trong batch (chạy riêng) và không ghi macro. **Lỗi 1** ở macro |
| Tham số sai / hình suy biến | cả file | Lỗi 2, 5, 6, 7. Mọi ngoại lệ vẫn được `ToolDispatcher.Classify` đổi thành lỗi trả Claude, không thoát vào AutoCAD |
| `capture_view` 3D | `Drawing/CaptureTools.cs` | Không thấy lỗi; kiểu hiển thị dùng chung hàm tìm kiểu đã sửa |
| Self-test | `SelfTest.cs` | Không thấy lỗi; dùng chung `MakeBox` / `MakeFrustum` / `ApplyVisualStyle` đã sửa |
| Nhãn `PlanLogic` | `Core/Plans.cs` | Đủ 14 lệnh `ToolGroup.ThreeD`; test nhãn xanh |
| Trợ lý | `agent/src/tools/solids.ts`, `confirmation.ts`, `systemPrompt.ts`, `capture.ts` | Cổng xác nhận đúng (kể cả trong batch, kể cả khi đích mới dựng mà khối công cụ là của lượt trước). Sửa mô tả `height` của `extrude` cho khớp lỗi 5 |

## Lỗi tìm được và cách sửa (commit `16de01c`)

| # | Mức | Lỗi | Sửa |
| --- | --- | --- | --- |
| 1 | **Nặng** | Macro: `solid_subtract` dùng tham số `target` / `subtract`, không thuộc danh sách tham số handle của macro. Phát lại macro "dựng tường + cửa + trừ" sẽ gửi **handle cũ đã ghi**: trong bản vẽ cũ nó khoét tiếp vào tường cũ của người dùng (macro không qua thẻ xác nhận), trong bản vẽ khác thì lỗi; lệnh trừ còn bị gộp chung batch với lệnh dựng khối nên không thể dùng handle mới | `MacroLogic.IsHandleKey` nhận thêm `target`, `subtract` → ánh xạ handle, tách batch, bỏ qua khi khối chưa dựng được |
| 2 | Vừa | Khối / region tạm (chưa vào bản vẽ) không được Dispose khi lỗi (CreateXxx lỗi, layer khóa, biên dạng sai). Đối tượng ngoài database để GC giải phóng trên luồng khác có thể làm sập AutoCAD | `Build(...)` và `AddSolid(...)` Dispose khi lỗi; `ProfileRegions` dọn mọi region / polyline tạm |
| 3 | Vừa | Kiểu hiển thị chỉ tìm theo tên tiếng Anh trong từ điển (`Conceptual`…). AutoCAD / mẫu bản vẽ ngôn ngữ khác đặt tên khác → `set_visual_style`, `capture_view` có `visual_style` và self-test báo "không có kiểu" | Tìm theo tên, tên cũ (`3dWireframe`, `3dHidden`), rồi theo `DBVisualStyle.Type` (`Conceptual`, `Wireframe2D`…), bỏ kiểu nội bộ; không thấy thì liệt kê các kiểu có trong bản vẽ |
| 4 | Vừa | `draw_wedge` chỉ sửa trường hợp mặt cao nằm ở +X. Nếu AutoCAD dựng nêm úp ngược hoặc dốc theo Y thì vẽ sai mà không báo | Lật quanh X nếu úp ngược, xoay quanh Z nếu cần, rồi kiểm trọng tâm phải là (−l/6, 0, −h/6); sai thì báo lỗi, không vẽ |
| 5 | Vừa | `extrude` đùn theo pháp tuyến region: biên dạng nằm trên XY nhưng pháp tuyến −Z (LINE / ARC nối theo chiều kim đồng hồ, đường bị mirror) sẽ đùn **xuống**, trái với mô tả "dương = lên" | Biên dạng phẳng trên XY: chiều cao dương luôn đi +Z (`SolidLogic.ExtrudeHeightAlongNormal`) |
| 6 | Nhẹ | Cộng / trừ / giao bị AutoCAD từ chối (mặt chạm, gần trùng) trả lỗi chung `ACAD_ERROR` | Trả `INVALID_PARAMS` nói rõ khối nào, bản vẽ không đổi, gợi ý làm khối cắt to hơn 10 mm |
| 7 | Nhẹ | `taper_angle` = ±89.9 trợ lý cho, plugin từ chối; thiếu cả `radius` lẫn `diameter`, hoặc thiếu biên dạng → câu lỗi khó hiểu | Biên chung `SolidLogic.MaxTaper` = 89.9 (test so với schema trợ lý); câu lỗi rõ |

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **236/236** (225 + 11 mới trong `SolidFixTests.cs`: ánh xạ macro, tách batch,
  extrude pháp tuyến, biên vát, trọng tâm nêm, chọn kiểu hiển thị theo tên / loại / bản vẽ tiếng Đức, kiểm nguồn
  Dispose và lỗi boolean).
- Trợ lý `npm test`: **159/159** (157 + 2: khối công cụ có sẵn vẫn cần xác nhận khi đích mới dựng; mô tả `extrude`).
- Build Linux (NuGet, .NET SDK 10): net48, net8.0-windows, net10.0-windows: 0 lỗi, 0 cảnh báo.
- CI Windows: run 43203265912 (`build.yml`, workflow_dispatch, `release: false`, commit `87b8c3a`) **xanh** (~9 phút, gồm UI test WPF).

## Cần chủ dự án thử tay trên AutoCAD (checklist phần J)

| Mã | Bước thử | Kết quả đúng |
| --- | --- | --- |
| J10 | "dựng nêm dài 3000, rộng 1200, cao 600 tại gốc", xem SW iso | Mặt cao 600 ở x = 0, dốc xuống 0 tại x = 3000; 1,08 m³; hộp bao [0,0,0]–[3000,1200,600]. Nếu Claude báo "unexpected orientation": gửi câu lỗi |
| J11 | Hình chữ nhật 1000×500, đùn cao 300, vát 10° | Thu nhỏ dần lên trên; z 0 → 300 |
| J12 | 4 LINE kín vẽ theo chiều kim đồng hồ, đùn cao 500 | Khối đi lên (z 0 → 500) |
| J13 | Lưu lượt J2 thành macro, chạy ở bản vẽ mới rồi chạy lại ở bản vẽ cũ | Mỗi lần có tường mới có lỗ; tường cũ không bị khoét thêm |
| J14 | AutoCAD / mẫu bản vẽ không phải tiếng Anh: đổi kiểu Conceptual, chụp ảnh Conceptual | Đổi được, ảnh có màu; lỗi thì gửi danh sách kiểu Claude nêu |
| J15 | Hai hộp chạm mặt, "giao hai hộp" | Báo không giao được, bản vẽ không đổi, AutoCAD không treo |

J1–J9 của đợt 1 vẫn cần thử như cũ (J1 self-test, J4 thẻ xác nhận, J6 không thêm bước U, J7 "Chỉ xem", J8 máy gói 2D).

## Không làm

- Không thêm tính năng (sweep, loft, bo cạnh, UCS… là đợt 2). Không sửa logic gói, `activation/activation.json`, `License.cs`.

## Tiến độ

- [x] Rà soát + sửa lỗi + test (commit `16de01c`)
- [x] Tài liệu: file này, checklist J10–J15, `bo-cai/README.md`
- [x] CI Windows (1 lượt, xanh)
