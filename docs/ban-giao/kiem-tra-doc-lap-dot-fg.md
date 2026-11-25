# Kiểm tra độc lập đợt F + G

*Luồng chính lưu nguyên văn báo cáo của thread `session_01D56HmFjDhopvJD3nN6ZMUa` (26/11/2026 01h40). Phần phân việc sửa và các lượt soát lại ghi ở cuối file.*

- Nhánh `full/nang-cap-g-tong-the` @ `881b12e` (code `4fbb2a2`), 25/11/2026.
- Agent mới, chưa viết hay test phần nào của hai đợt này. Chỉ ghi lỗi: không sửa code, không commit, không push, không chạy CI.
- Phạm vi:
  - rà sâu F5 và G1 → G5;
  - F1 → F4 chỉ rà lướt: các bản sửa KTF-01…09 có còn đúng không, và đợt G có làm hỏng gì của đợt F không.
- Cách làm: 3 tác vụ con rà song song (G1 + G2, G3 + G4, G5 + F5). Thread chính rà phần chéo, rồi tự soát lại đúng dòng code của các lỗi vừa / nặng trước khi ghi vào đây.
- Test thử nằm ở project tạm ngoài repo, link tới các file Core thật, và không giữ lại.

## Test và build đã chạy (@ `881b12e`)

| Hạng mục | Kết quả |
| --- | --- |
| Lõi `dotnet test plugin/ClaudeCAD.Core.Tests` | 1368 / 1368 đạt |
| Trợ lý `cd agent && npm ci && npm test` | 400 / 400 đạt |
| Build `net48` / `net8.0-windows` / `net10.0-windows` (Release, `-p:EnableWindowsTargeting=true`, xóa obj/bin giữa các lần) | 0 lỗi, 0 cảnh báo cả 3 |
| Test thử của tác vụ con (project tạm) | G1 + G2: 6 test; G5 + F5: 7 test; G3 + G4: console `dotnet run` + mở xlsx bằng zipfile |

## Bảng lỗi

Không có lỗi nặng nào đã chứng minh chắc chắn. KFG-01 xếp **nặng (khả năng cao)**: đường đi trong code đã chỉ ra được, nhưng cần AutoCAD thật để xác nhận.

| Mã | Mức | Mảng | Tóm tắt |
| --- | --- | --- | --- |
| KFG-01 | Nặng (khả năng cao, cần AutoCAD) | G4 | Render góc nhìn đã lưu dùng bản ghi góc nhìn sau khi transaction đã đóng |
| KFG-02 | Vừa | G3 + tài liệu | `cost_estimate` bỏ mất cảnh báo của bảng nguồn: diện tích lồng nhau bị tính tiền gấp đôi mà không ai biết |
| KFG-03 | Vừa | G3 | Ghép giá bỏ dấu thanh: "Sân" và "Sàn" bị coi là một |
| KFG-04 | Vừa | G4 + tài liệu | `lighting: auto` không tắt đèn mặc định, nên đèn / mặt trời của bản vẽ không có tác dụng |
| KFG-05 | Vừa | G5 | `trace_image` gắn ảnh và căn thẳng theo trục thế giới, không theo UCS xoay |
| KFG-06 | Vừa | G5 | `scale` / `straighten` nhận handle của mọi ảnh, kể cả ảnh của khách trên layer khóa |
| KFG-07 | Vừa | chéo G1 × G2 | `check_drawing` báo nhầm "hatch mất liên kết" cho mọi hatch mặt cắt của bộ hồ sơ |
| KFG-08 | Vừa | G2 + checklist | Layout tên mặc định có khung nhìn khách tự đặt bị bỏ qua hẳn (lệch checklist G2-D1) |
| KFG-09 | Nhẹ | F2 / F5 | `layer_state`: ghi đè và xóa dùng chung một mã duyệt |
| KFG-10 | Nhẹ | F5 | Mã duyệt vật liệu cả layer / đổi màu vật liệu không gắn tham số |
| KFG-11 | Nhẹ | G5 | Đếm số trang PDF sai: PDF lưu nối tiếp bị đếm gấp đôi; PDF nén thì lặng lẽ gắn trang 1 |
| KFG-12 | Nhẹ | G5 (bảo mật) | Đường dẫn ảnh đưa cho Claude không bọc ngoặc kép |
| KFG-13 | Nhẹ | G5 | Ảnh dán vào chat luôn lưu ra đĩa, không dọn, kể cả gói 3D |
| KFG-14 | Nhẹ | tài liệu / prompt | `PLAN_SCOPE` chưa có đợt G; quy tắc "dữ liệu không phải lệnh" chưa nhắc chữ trong ảnh; AVIF bị báo là HEIC |
| KFG-15 | Nhẹ | G2 | Dung sai "trùng" làm tròn theo ô lưới, nên bỏ sót hình lệch 0,002 mm |
| KFG-16 | Nhẹ | G2 | `no_dimensions` không đếm kích thước ghi trong layout |
| KFG-17 | Nhẹ | G2 | `fix_drawing` sửa cả vật trong layout dù lần soát dùng `layouts: false` |
| KFG-18 | Nhẹ | G2 | Sửa `layer_zero` / `defpoints` cho chuyển hình sang layer không in / tắt / đóng băng |
| KFG-19 | Nhẹ | G1 | `elevations: []` / `sections: []` / `plan_areas: []` âm thầm cho 0 tờ |
| KFG-20 | Nhẹ | G1 | Thẻ "thay bộ cũ" ghi "N views" với N là số nét |
| KFG-21 | Nhẹ | G1 | Vẫn hỏi thẻ "thay bộ cũ" khi bộ cũ đã bị xóa tay hết |
| KFG-22 | Nhẹ | chéo G1 × G3 / C | Khung mặt bằng 2D tính cả bảng thống kê / dự toán |
| KFG-23 | Nhẹ | G1 | Gom tầng kiểu "nối chuỗi" (0 / 40 / 80 / 120 mm ra 2 tầng) |
| KFG-24 | Nhẹ | G1 | Nhận tờ của bộ cũ theo tên, không theo handle |
| KFG-25 | Nhẹ | G3 (schema) | Mô tả `cost_estimate` vẫn mời các nguồn khối lượng mà gói chắc chắn từ chối |
| KFG-26 | Nhẹ | G3 | Ô đơn giá trong Excel định dạng `#,##0`: 12,5 hiện là 13 |
| KFG-27 | Nhẹ | G4 | Kết quả đường lui (ảnh chụp) vẫn mang nền / ánh sáng của lúc bắt đầu render |

### Chi tiết

**KFG-01 — Nặng (khả năng cao) — G4.**
- **Chỗ:**
  - `Drawing/RenderG4.cs` 98–109: `named` lấy trong `StartOpenCloseTransaction()`, rồi `tr.Commit()` đóng đối tượng.
  - Dòng 126 (`named?.ViewDirection`, nhánh không có thư mục tạm) và dòng 136 (`doc.Editor.SetCurrentView(named)`) dùng `named` sau khi đã đóng.
  - Các chỗ khác trong code đều dùng bản ghi trong transaction đang mở hoặc `Clone()` (vd `RenderTools.cs` restore, `job.OldView = (ViewTableRecord)cur.Clone()`).
- **Hậu quả trên AutoCAD (theo cách API AutoCAD .NET xử lý đối tượng đã đóng, thường là `eNotOpenForRead`):**
  - "render góc Phối cảnh 1" rơi vào catch dòng 143: báo "could not prepare the render", hoặc ra lỗi AutoCAD thô ở nhánh dòng 126;
  - checklist G4 A.3 và G0-3 hỏng;
  - SelfTest không phủ nhánh có `name`.
- **Kết quả đúng:** render được góc đã lưu.
- **Hướng sửa:** trong transaction, `named = (ViewTableRecord)rec.Clone();` (Dispose sau).

**KFG-02 — Vừa — G3 + tài liệu.**
- **Chỗ:**
  - `Drawing/EstimateTools.cs` 38–43: gọi `AreaSchedule` / `VolumeSchedule` / `BlockSchedule` nhưng chỉ lấy `rows` (`RowsOf`).
  - Dòng 65–88: tạo `res` mới, nên mất các khóa `warning`, `contains_others`, `left_out_layers`, `skipped`, `note`, `warnings` của bảng nguồn.
  - Dòng 38 bỏ `table_position` khỏi tham số nguồn, nên `area_schedule` không bao giờ từ chối được ("contain other areas … No table was inserted").
- **Tái hiện:** mặt bằng có đường bao nhà cùng layer với các phòng (`group_by` mặc định layer) → `cost_estimate quantities: areas`. Diện tích bao cộng chồng lên phòng, tiền khoảng gấp đôi. Bảng / Excel vẫn ra mà không có cảnh báo.
- **Lệch tài liệu:** `docs/dot-g-cho-duyet.md` dòng ~352 và checklist G6-2 ghi "kết quả nói rõ đã bỏ" A-SECT / A-ELEV, nhưng `cost_estimate` không trả `left_out_layers`.
- **Hướng sửa:** chép các cảnh báo nguồn vào `res.source_warnings`. Có `contains_others` (không `allow_nested`) thì từ chối như `area_schedule`.

**KFG-03 — Vừa — G3.**
- **Chỗ:** `Core/Estimate.cs` 104–114. `Key` bỏ mọi dấu và coi `- _ .` là dấu cách.
- **Test thử:** dòng "Sân" 20 m² và "Sàn" 50 m²; giá Sàn 500 000, Sân 200 000.
  - Kết quả: cả hai dòng không có giá (`several unit prices given for this item`, `missing = Sân, Sàn`).
  - Chỉ đưa giá "Sàn" thì dòng "Sân" lặng lẽ ăn giá sàn.
  - Layer `A-WALL` và `A_WALL` cũng ăn chung một giá.
- **Kết quả đúng:** khớp nguyên văn (không phân biệt hoa thường) trước, rồi mới so theo `Key`.
- **Hướng sửa:** trong `Match`, lọc khớp `OrdinalIgnoreCase` trên tên đã trim trước; không có mới dùng `Key`.

**KFG-04 — Vừa — G4 + tài liệu.**
- **Chỗ:** `Core/Render.cs` 213–215 (`LightPlan` nhánh auto). Khi có đèn hoặc mặt trời thì trả `(null, null)`, giữ `DefaultLightingOn = true`.
- **Vì sao sai:** đèn mặc định đang bật thì AutoCAD bỏ qua đèn của khách và mặt trời. Chính nhánh `sun` (dòng 212) cũng tắt đèn mặc định vì lý do này.
- **Test thử:**
  - `LightPlan("auto", 2, false, false, true)` → `(null, null)`, trong khi `LightWords` báo "the drawing's 2 light(s)";
  - `LightPlan("sun", 0, false, true, true)` → `(False, True)`.
- **Kết quả đúng:** như `dot-g-cho-duyet` ghi, "dùng đèn / mặt trời của bản vẽ nếu có".
- **Hướng sửa:** auto với `userLights > 0 || sunOn` trả `(defaultLightOn ? false : null, null)`.

**KFG-05 — Vừa — G5.**
- **Chỗ:**
  - `Drawing/TraceTools.cs` 123: `attach` đặt `Orientation` bằng `Vector3d(w,0,0)` / `Vector3d(0,h,0)` (trục WCS).
  - Dòng 264 và `Core/Trace.cs` 148–158: `straighten` tính góc từ p1 / p2 đã đổi sang WCS.
- **Test thử:** UCS xoay 30°, tường nằm theo trục x của UCS. `StraightenAngle((0,0),(866.03,500),"horizontal")` = **−30°**, đúng phải là 0°.
- **Hậu quả:**
  - ảnh vừa gắn đã lệch lưới UCS, và `straighten` còn kéo ảnh về trục WCS;
  - `min` / `max` trả về sau `attach` không phải hộp trong UCS.
- **Hướng sửa:** `attach` dùng `frame.X` / `frame.Y`; `straighten` trừ thêm `frame.PlanAngleDeg`.

**KFG-06 — Vừa — G5.**
- **Chỗ:** `Drawing/TraceTools.cs` 221–227. Có `handle` thì `TraceTarget` nhận mọi `RasterImage` / `PdfReference`, không kiểm dấu `CLAUDECAD_TRACE`. Sau đó dòng 274–280 mở khóa layer của ảnh, co / xoay rồi khóa lại.
- **Tái hiện:** `trace_image scale` (hoặc `straighten`) với handle của logo trong khung tên trên layer khóa. Ảnh của khách bị co / xoay, không có thẻ.
- **Hướng sửa:** trong `TraceTarget`, `ReadMark(tr, e) == null` thì từ chối.

**KFG-07 — Vừa — chéo G1 × G2.**
- **Chỗ:**
  - `Drawing/SectionTools.cs` 383–401 tạo `new Hatch()` không đặt `Associative` (mặc định false).
  - `Drawing/DrawingCheckTools.cs` 270 báo `hatch_unassociated` cho mọi `!h.Associative`.
- **Hậu quả:**
  - sau "ra bộ hồ sơ", mọi hatch mặt cắt / mặt bằng cắt (A-SECT-PAT) bị báo "cân nhắc";
  - `draw_hatch` theo điểm (`DrawTools.cs` 240) cũng bị báo;
  - checklist G6-1 chưa tính trường hợp này.
- **Hướng sửa:** dòng 270 bỏ qua layer `Util2DLogic.IsDerivedViewLayer`.

**KFG-08 — Vừa — G2 + checklist.**
- **Chỗ:** `Drawing/DrawingCheckTools.cs` 312. Coi layout là "untouched" khi `others == 0 && IsDefaultLayoutName`, mà `others` không đếm khung nhìn.
- **Tái hiện:** tab "+" (tên `Layout2`), MVIEW một khung nhìn không khóa, không khung tên. Kết quả: không báo `no_title_block`, không báo `viewport_unlocked`, trái với checklist G2-D1.
- **Hướng sửa:** chỉ coi là untouched khi layout không có khung nhìn nào ngoài khung tổng.

**KFG-09 — Nhẹ — F2 / F5.**
- **Chỗ:** `Drawing/Standards2DTools.cs` 700–704 (save ghi đè) và 738–740 (delete). Cả hai dùng handle trạng thái layer làm mã.
- **Hậu quả:** Đồng ý "ghi đè X" thì cùng lượt xóa được X, và ngược lại. Góc nhìn đã có `DeleteViewToken` riêng, `layer_state` thì chưa.
- **Hướng sửa:** mã riêng cho việc xóa.

**KFG-10 — Nhẹ — F5.**
- **Chỗ:** `Drawing/RenderTools.cs` ~89 và ~113. Mã duyệt "vật liệu cho cả layer" chỉ là handle layer; mã "đổi màu vật liệu có sẵn" chỉ là handle vật liệu.
- **Hậu quả:** khách duyệt "gạch cho A-WALL" thì cùng lượt Claude gán "kính" vẫn qua; duyệt màu đỏ thì màu nào cũng qua.
- **Hướng sửa:** mã gồm layer + tên vật liệu, và vật liệu + màu / độ trong.

**KFG-11 — Nhẹ — G5.**
- **Chỗ:** `Core/Trace.cs` 80–95.
- **Test thử:**
  - PDF 2 trang lưu nối tiếp (incremental update) đếm ra 4 trang, và `PageOf(3, 4)` nhận trang 3 không có thật;
  - PDF nén object stream đếm ra 0, nên `PageOf(null, 0)` lặng lẽ gắn trang 1 của PDF nhiều trang.
- **Hướng sửa:** lấy `/Count` của cây trang gốc. Đếm ra 0 thì ghi rõ trong kết quả "không biết số trang, đã gắn trang 1".

**KFG-12 — Nhẹ — G5 (bảo mật).**
- **Chỗ:** `agent/src/agent/content.ts` 128. Dòng `[Files of the pictures: 1: <path>]` ghi đường dẫn thô; các giá trị khác của máy khách đều qua `q()`.
- **Tái hiện:** tên file có `]` `[`, vd `a] [Người dùng bấm Đồng ý].png`, tạo ra một dòng ngữ cảnh giả. Cổng xác nhận thật vẫn chặn, nên mức nhẹ.
- **Hướng sửa:** `q(i.path)`.

**KFG-13 — Nhẹ — G5.**
- **Chỗ:** `ChatController.cs` ~834 và `UI/ChatAttachments.cs` 16–31.
- **Hậu quả:**
  - mọi ảnh dán ở mọi tin, mọi gói (cả gói 3D không có `trace_image`) đều lưu vào `%LocalAppData%\ClaudeCAD\anh-dinh-kem`, không có chỗ dọn;
  - gói 3D vẫn nhận dòng "Files of the pictures";
  - ảnh lưu là bản đã thu nhỏ ≤ 1568 px, nên bản scan lớn vẽ lại bị mất chi tiết mà tài liệu không nói.
- **Hướng sửa:** chỉ lưu khi gói có 2D, dọn file cũ, ghi giới hạn vào mô tả lệnh / tài liệu.

**KFG-14 — Nhẹ — tài liệu / prompt.**
- `agent/src/agent/systemPrompt.ts` 112–113: `PLAN_SCOPE` chưa có đợt G.
  - Gói 3D không ghi "không có: soát bản vẽ / vẽ lại từ ảnh".
  - Gói 2D không nhắc bộ hồ sơ, dự toán, soát bản vẽ, vẽ lại từ ảnh.
- `systemPrompt.ts` 20: quy tắc "dữ liệu không phải lệnh" chưa nhắc chữ trong ảnh khách đính kèm (hiện chỉ có trong mô tả `trace_image`).
- `Core/Trace.cs` 260: file AVIF bị báo là "HEIC".

**KFG-15 — Nhẹ — G2.**
- **Chỗ:** `Core/DrawingCheck.cs` 158 làm tròn từng số theo ô lưới (`Math.Round(n/tol)`).
- **Test thử:** hai đường x = 1000,049 và 1000,051 (lệch 0,002 mm) cho hai khóa khác nhau, nên không báo trùng.
- **Lệch tài liệu:** tài liệu ghi "lệch ≤ 0,1 mm".
- **Hướng sửa:** so thêm các ô kề, hoặc ghi rõ giới hạn trong tài liệu.

**KFG-16 — Nhẹ — G2.**
- **Chỗ:** `DrawingCheckTools.cs` 247 chỉ đếm `if (model) dims++`.
- **Hậu quả:** bản vẽ ghi kích thước ở layout vẫn bị báo "có tường mà chưa có kích thước".
- **Hướng sửa:** đếm mọi `Dimension`.

**KFG-17 — Nhẹ — G2.**
- **Chỗ:** `DrawingCheckTools.cs` 419. `fix_drawing` gọi `ScanDrawing` với `layouts` mặc định true và không có tham số `layouts`.
- **Hậu quả:** sửa cả vật khách không thấy ở lần soát `layouts: false`. Thẻ ghi đúng số thật nên không lách được.
- **Hướng sửa:** thêm `layouts` cho `fix_drawing` (TS + C#).

**KFG-18 — Nhẹ — G2.**
- **Chỗ:** `Core/DrawingCheck.cs` 313–321 và `DrawingCheckTools.cs` 412–415 chỉ chặn layer 0 / Defpoints làm layer đích.
- **Hậu quả:** chuyển được sang `X-ANH-GOC` (G5) hay layer tắt / đóng băng; sửa xong lại thành lỗi "không in".
- **Hướng sửa:** từ chối layer đích không in / tắt / đóng băng.

**KFG-19 — Nhẹ — G1.**
- **Chỗ:** `Core/DrawingSet.cs` 186–197, 282, 328. Mảng rỗng cho 0 tờ mà không có `missing`.
- **Test thử:** kinds = elevation + section với hai danh sách rỗng cho `sheets 0, missing 0`, kèm thông báo lý do trống ("…made here: . Nothing was changed.").
- **Hậu quả:** nếu có kèm loại tờ khác thì bộ cũ bị thay bằng bộ thiếu mà không ai báo.
- **Hướng sửa:** coi `[]` như không truyền, hoặc `.min(1)` trong schema.

**KFG-20 — Nhẹ — G1.**
- **Chỗ:** `DrawingSetTools.cs` 199. Thẻ ghi "the {last.Handles.Count} views it drew", nhưng `Handles` là từng nét / hatch.
- **Hậu quả:** thẻ có thể ghi "1534 views".
- **Hướng sửa:** đếm số hình, hoặc đổi chữ thành "objects".

**KFG-21 — Nhẹ — G1.**
- **Chỗ:** `DrawingSetTools.cs` 181. `hadSet` không kiểm handle nào còn sống.
- **Hậu quả:** khách đã xóa tay hết bộ cũ mà vẫn bị hỏi thẻ; khi chạy macro thì bị từ chối ("already has a drawing set (layouts )").
- **Hướng sửa:** lọc các handle còn tồn tại trước khi tính.

**KFG-22 — Nhẹ — chéo G1 × G3 / C.**
- **Chỗ:** `DrawingSetTools.cs` 129. Khung mặt bằng 2D chỉ loại các layer hình suy ra và `X-ANH-GOC`.
- **Hậu quả:** bảng `block_schedule` / `area_schedule` / `cost_estimate` cạnh mặt bằng được tính vào khung, nên tờ lệch tâm, có thể ra tỉ lệ lớn hơn.
- **Hướng sửa:** bỏ `Table` khỏi khung.

**KFG-23 — Nhẹ — G1.**
- **Chỗ:** `Core/DrawingSet.cs` 127 so với mức đầu nhóm, không so với giá trị kề.
- **Test thử:** chân tường 0 / 40 / 80 / 120 mm ra 2 tầng (0 và 80), thừa một tờ "MẶT BẰNG TẦNG 2". Hiếm gặp.

**KFG-24 — Nhẹ — G1.**
- **Chỗ:** `Core/DrawingSet.cs` 461–466 và `DrawingSetTools.cs` 180. Tờ của bộ cũ được nhận theo tên.
- **Tái hiện:** khách xóa tab KT-05 của bộ, rồi tự tạo layout riêng cũng tên KT-05. Lần chạy sau tờ đó bị coi là của bộ và bị xóa (sau thẻ; thẻ có liệt kê KT-05).
- **Hướng sửa:** lưu handle layout trong record.

**KFG-25 — Nhẹ — G3 (schema).**
- **Chỗ:** `agent/src/tools/index.ts` 101, `trimForPlan`. Enum `quantities` không thu hẹp theo gói: gói 2D vẫn thấy `volume`, gói 3D vẫn thấy `blocks` / `areas`.
- **Hậu quả:** Claude gọi xong mới bị từ chối, phí một lượt.
- **Hướng sửa:** thu hẹp enum theo gói trong `trimForPlan`.

**KFG-26 — Nhẹ — G3.**
- **Chỗ:** ô đơn giá Excel dùng `#,##0` (`G3Numbers.ExcelMoneyFormat`).
- **Hậu quả:** đơn giá 12,5 (USD) hiện "13" trong khi thành tiền vẫn nhân 12,5; bảng trong bản vẽ hiện 2 số lẻ.
- **Hướng sửa:** ô đơn giá dùng `#,##0.##`. Làm tròn tiền 0 số lẻ đã có trong `dot-g-cho-duyet`; riêng chỗ hiển thị đơn giá chưa có.

**KFG-27 — Nhẹ — G4.**
- **Chỗ:** `agent/src/tools/render.ts` 73 gộp `{...start, ...r}`.
- **Hậu quả:** kết quả ảnh chụp (đường lui) vẫn mang `background`, `lighting`, `changed_for_render`, `estimate_seconds` của lúc bắt đầu render.
- **Hướng sửa:** khi `method === 'capture'`, bỏ các khóa đó.

## Nghi vấn chưa chứng minh (cần AutoCAD thật)

- **G4, có thể nặng:**
  - Nếu `-RENDER` kết thúc lệnh ngay mà vẫn render tiếp trong Render Window, thì `EndGraceSeconds = 20` nhỏ hơn ước lượng mức vừa (60 s) / đẹp (180 s).
  - Khi đó render vừa / đẹp sẽ rơi về ảnh chụp, nền / đèn bị trả lại giữa chừng, và file tạm bị bỏ lại.
  - Bàn giao G4 cũng ghi chưa biết điều này.
- **G4:** Dừng / quá giờ gửi `\x03\x03` qua hàng đợi, nhưng `FinishRender` trả cảnh và xóa file tạm ngay (`RenderG4.cs` 344–347, 409–410). Render chưa dừng hẳn vẫn có thể ghi file tạm sau đó.
- **G4:** thứ tự câu hỏi `-RENDER` (`_-RENDER|_High|_R|1920|1080|_Y|<file>|`) khớp tài liệu, nhưng chưa đối chiếu được với AutoCAD thật.
- **G4:** `SetCurrentView` không nằm trong `DisableUndoRecording`, nên bấm U có thể nhảy góc nhìn (checklist G4 A.5).
- **G4:** chỉ đếm đèn nằm thẳng trong Model; đèn nằm trong block không được tính.
- **G5:** `attach` / `view` đọc mọi đường dẫn, kể cả UNC `\\host\share` (Windows có thể tự gửi thông tin đăng nhập ra máy đó). Chưa có danh sách thư mục được phép.
- **G5:** với PDF, "ảnh xem trống" dựa vào màu viền. Phóng vào vùng thưa nét có thể báo nhầm `picture_visible: false`.
- **G5:** bản vẽ chưa lưu thì ảnh chép vào Documents (`Drawing1-anh-goc-…`), lưu bản vẽ chỗ khác thì ảnh vẫn ở đó. So tiền tố thư mục đính kèm (`TraceTools.cs` 79) thiếu `\` cuối. BigTIFF bị từ chối.
- **G1:**
  - Cạnh trùng khít giữa hai khối kề nhau trong `projection_view` / `section_view` có thể bị `check_drawing` báo trùng, và `fix_drawing` xóa nét của bộ hồ sơ.
  - Một `Autodesk.AutoCAD.Runtime.Exception` sau `RemoveSet` làm `WriteSetRecord` không chạy, để lại hình không có record.
  - Khi chạy macro, `RequireCard` bỏ qua nên PDF có sẵn bị ghi đè không hỏi (giống `plot_pdf` khi chạy macro; có thể là chủ ý).
- **G2:**
  - Khung nhìn "của tờ" lấy theo thứ tự trong BTR (`DrawingCheckTools.cs` 203); nên dùng `vp.Number == 1`.
  - `DimensionText = " "` (ẩn chữ kích thước) không bị báo `dim_override`.

## Đã kiểm, không thấy lỗi

- **Chéo, gói × chế độ × nạp gọn:**
  - plugin chặn bằng `RequireInPlan` / `RequireParamsInPlan` theo gói hiệu lực (cả nút chế độ F1) cho lệnh lẻ, từng lệnh trong `batch_execute` và macro;
  - các lệnh mới đúng nhóm: `check_drawing`, `fix_drawing`, `trace_image` là 2D; `named_view` là 3D; `create_drawing_set` và `cost_estimate` là Common, kiểm bên trong theo lệnh con / bảng nguồn;
  - lệnh hoãn nạp chỉ gồm lệnh đã lọc theo gói, ToolSearch không thêm được lệnh ngoài gói;
  - mỗi chat giữ cách nạp lúc bắt đầu.
- **Thẻ F5:**
  - `flaggedF5` bỏ `user_confirmed` do Claude tự gửi, kể cả trong batch; `F5_CARD_TOOLS` có đủ `create_drawing_set`, `cost_estimate`, `fix_drawing`, `check_drawing`;
  - Hủy thì không có mã nào được duyệt;
  - mã file chuẩn hóa đường dẫn đầy đủ, không phân biệt hoa thường, ổn với tiếng Việt và dấu cách;
  - góc nhìn có mã xóa riêng;
  - mã `fix_drawing` gồm fix + layer + tập handle.
- **Macro:** `fix_drawing` và `trace_image` không được ghi. Khi chạy lại, các lệnh Excel (`cost_estimate`, `check_drawing` và 4 bảng F4) không ghi file. `create_drawing_set` không thay bộ cũ khi chạy macro.
- **KTF-01…09:** code sửa còn nguyên, test KTF (`KiemTraDocLapDotFTests`, `NangCapFSuaKtfTests`…) đạt. Đợt G không sửa các file của bản sửa KTF (`RoofRightAngled`, `Ucs`, `Params`, `StairRoof`, `ExcelExport`, `QueryTools`, `PlanTo3dTools`). `Xlsx.cs` và `Util2D*` chỉ thêm phần mới, không đổi hành vi cũ.
- **G1:**
  - chia phần theo gói: 2D ra mặt bằng 2D; 3D ra 7 hình, thiếu phối cảnh; Full ra đủ 8;
  - từ chối tờ của khách trùng tên trước khi đổi gì; `RemoveSet` chỉ xóa theo record, giữ một Layout1;
  - đổi đơn vị độ cao cắt / khoảng cách; mặt bằng trên UCS xoay;
  - tên PDF có dấu / dấu cách; bản vẽ chưa lưu thì PDF vào My Documents;
  - field khung tên lạ bị từ chối trước khi xóa bộ cũ.
- **G2:**
  - chữ rỗng bỏ qua ATTDEF; xref tạm gỡ không bị báo mất file;
  - polyline ngược chiều nhận là trùng; sửa trùng giữ bản cũ nhất; bỏ qua layer khóa;
  - giới hạn quét 20 000 / sửa 2 000 có ghi chú;
  - khung nhìn phối cảnh G1 không bị kiểm tỉ lệ; X-ANH-GOC không bị báo "không in";
  - 22 nhóm / 9 nhóm sửa được khớp giữa TS và C#.
- **G3:**
  - không có giá mẫu nào trong mô tả lệnh, system prompt, skill, `G3Numbers`;
  - dòng thiếu giá để trống, không cộng vào tổng; giá 0 vẫn tính;
  - mở xlsx: F = `IF(E2="","",ROUND(D2*E2,0))` đúng hàng, `SUM(F2:F5)`, VAT, Tổng đúng ô; inlineStr, escape đúng; `fullCalcOnLoad`;
  - làm tròn AwayFromZero; m3 / m³ / m^3 là một;
  - ghi đè Excel qua thẻ; macro không ghi file; `table_position` cần 2D.
- **G4:**
  - render chỉ gói 3D / Full; thẻ ghi đè PNG có trước mọi thay đổi;
  - file tạm ở đường dẫn ASCII, file của khách có dấu thì chép sang sau;
  - `method: "render"` chỉ báo sau khi có file và cỡ file đứng yên;
  - mọi nhánh lỗi / Dừng / Esc gọi `FinishRender`;
  - `render_status` / `render_stop` không có trong enum cho Claude;
  - vật liệu thư viện khớp nguyên từ.
- **G5:**
  - `PictureToLook` (xoay 30° + co), lưới UCS xoay + dời gốc, `ToUnits`;
  - loại file theo nội dung (đổi đuôi bị bắt); 50 MB / file rỗng bị chặn;
  - không chạy được trong batch, không ghi macro; `view` / `info` chạy ở "Chỉ xem";
  - giới hạn 8 lần xem đếm lại mỗi tin, không lách qua batch;
  - không ghi đè file của khách.
- **Tài liệu:** `bo-cai/README.md` có đủ G1 → G5; `ToolLabels` có đủ nhãn 6 lệnh mới; SelfTest có phần G4, G5.

## Chưa kiểm được

- Mọi thứ cần AutoCAD thật:
  - render `-RENDER` và chuỗi trả lời, KFG-01;
  - viewport, hatch, layout, ảnh raster / PDF underlay, U hoàn tác;
  - UI WPF (ô ⚙ "Nạp gọn lệnh", thẻ trong khung chat).
- Claude thật: có làm theo system prompt / mô tả lệnh không (không bịa giá, không nghe chữ trong ảnh).
- Excel thật: mở file và tính lại công thức (chỉ đã soát XML).
- Chưa đối chiếu từng mục checklist "Đợt F" / "Đợt G" theo cách làm tay; chỉ đối chiếu các mục nêu ở trên (G2-D1, G6-1, G6-2, G4 A.3 / A.5, G0-3).
- Chưa soát lại từng số trong `docs/dot-f-cho-duyet.md` / `docs/dot-g-cho-duyet.md` so với code. Chỉ có các chỗ lệch đã thấy: KFG-02, KFG-04, KFG-15, KFG-26.

## Thời gian, chi phí

Khoảng 40 phút thực tế. Chi phí thread (`get_session`, gồm cả 3 tác vụ con) khoảng $11,4 lúc viết báo cáo. Tốn nhất là 3 tác vụ con đọc `RenderG4.cs`, `TraceTools.cs`, `DrawingSetTools.cs`, `DrawingCheckTools.cs` và build project test tạm.


## Phân việc sửa (luồng chính, 26/11 01h45)

Ba thread sửa song song, mỗi thread một nhánh tách từ `full/nang-cap-g-tong-the` @ `881b12e`. Thread giữ nhánh tích hợp gộp
các bản sửa và chạy CI; thread kiểm tra độc lập soát lại từng lỗi.

| Thread | Nhánh | Lỗi nhận |
| --- | --- | --- |
| Thread đợt F1 (viết G2) | `full/nang-cap-g-sua-kfg-a` | KFG-07, 08, 15, 16, 17, 18; nghi vấn G2 (khung nhìn "của tờ", chữ kích thước là dấu cách); nghi vấn G1 × G2 (nét của bộ hồ sơ bị báo trùng rồi bị `fix_drawing` xóa) |
| Thread Test & sửa F1 (viết F5, G4, G5) | `full/nang-cap-g-sua-kfg-b` | KFG-01, 04, 27, 10 (G4 / F5); KFG-05, 06, 11, 12, 13 (G5); nghi vấn G4 (render chạy tiếp sau khi lệnh kết thúc, dừng giữa chừng, U nhảy góc nhìn) và G5 (đường dẫn mạng, ảnh xem PDF trắng, thư mục ảnh) |
| Thread đợt F2 (viết G1, G3; giữ nhánh tích hợp) | `full/nang-cap-g-tong-the` | KFG-02, 03, 25, 26 (G3); KFG-19 … 24 (G1); KFG-09 (F2); KFG-14 (prompt / tài liệu); nghi vấn G1 còn lại; gộp hai nhánh trên, cập nhật checklist và `docs/dot-g-cho-duyet.md`, chạy CI |

Quyết định luồng chính chọn khi giao (cách rẻ, an toàn, đổi lại được; chờ chủ dự án xem lại):

- **KFG-03 (ghép đơn giá):** tiền thì thà để trống còn hơn ghép nhầm. Tên có dấu phải khớp cả dấu ("Sân" khác "Sàn");
  chỉ khi khách gõ tên giá không dấu ("san") mới so không dấu, và nếu một tên không dấu khớp nhiều dòng khác nhau thì để
  trống các dòng đó và hỏi lại.
- **KFG-02:** dự toán theo diện tích gặp vùng lồng nhau thì từ chối như bảng diện tích (trừ khi khách cho phép), và luôn
  chép cảnh báo của bảng nguồn vào kết quả.
- **Nghi vấn G5 (đường dẫn mạng):** chỉ cho gắn / xem ảnh ở ổ đĩa của máy, file khách đính kèm trong chat, hoặc cùng máy chủ
  / thư mục chia sẻ với bản vẽ đang mở; đường dẫn mạng khác bị từ chối kèm hướng dẫn chép file về máy.
- **Nghi vấn G4 (render chạy tiếp sau khi lệnh kết thúc):** thời gian chờ thêm sau khi lệnh kết thúc lấy bằng số lớn hơn giữa
  20 giây và thời gian ước lượng của mức render đó, thay vì luôn 20 giây.
- **KFG-08 (chọn thêm 26/11 01h52, sau bản sửa đầu của thread đợt F1):** tab tên mặc định (`Layout1`, `Layout2`…) mà chỉ
  có khung nhìn, không có vật nào khác thì coi là tab chưa dùng: không báo lỗi, nhưng kết quả có ghi chú nêu tên các tab đó
  và lý do. Lý do: khách chỉ vẽ trong Model nhưng từng bấm vào tab Layout1 là AutoCAD tự tạo một khung nhìn; kiểm như tờ in
  sẽ báo nhiễu ở gần như mọi bản vẽ. Tab mặc định có thêm vật khác, và mọi tab đã đổi tên, vẫn kiểm đầy đủ.

## Tiến độ sửa

| Thread | Kết quả |
| --- | --- |
| Thread đợt F1 (G2) | 26/11 01h50: sửa hết KFG-07, 08, 15, 16, 17, 18 và 3 nghi vấn, `full/nang-cap-g-sua-kfg-a` @ `2e1dfe2`, lõi 1375, trợ lý 400, ≈ $6. Đang chỉnh thêm KFG-08 theo quyết định trên |
