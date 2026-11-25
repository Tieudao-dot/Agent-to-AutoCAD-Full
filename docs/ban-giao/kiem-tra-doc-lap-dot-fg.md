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
| Thread đợt F1 (G2) | 26/11 01h50: sửa hết KFG-07, 08, 15, 16, 17, 18 và 3 nghi vấn, `full/nang-cap-g-sua-kfg-a` @ `2e1dfe2`, lõi 1375, trợ lý 400, ≈ $6. 01h56: chỉnh xong KFG-08 theo quyết định trên @ `cbc1b03` (có thêm tham số `sheets` để khách nhờ soát cả tab mặc định), lõi 1376, trợ lý 400, ≈ $2 |
| Thread Test & sửa F1 (G4, G5, F5) | 26/11 01h54: sửa hết KFG-01, 04, 05, 06, 10, 11, 12, 13, 27, phần `Trace.cs` của KFG-14 và các nghi vấn G4 / G5, `full/nang-cap-g-sua-kfg-b` @ `468dc09` (code `f48747c`), lõi 1388, trợ lý 404. Số mới chờ duyệt: giữ ảnh dán 7 ngày; chờ sau khi lệnh render kết thúc = số lớn hơn giữa 20 giây và thời gian ước lượng. Quyết định mới: ảnh dán vào bản vẽ chưa lưu thì nhờ khách lưu bản vẽ trước (không chép vào Documents nữa); đường dẫn mạng lạ bị chặn lúc gắn ảnh |

## Soát lại lượt 1 (26/11 02h17)

*Nguyên văn báo cáo soát lại của thread kiểm tra độc lập.*

Bản soát: `full/nang-cap-g-tong-the` @ `20e3812`, so với `881b12e`. Tớ không sửa code, không commit / push / CI.

Tóm tắt:
- 27 mã: 26 sửa đúng; KFG-07 sửa chưa đủ (còn hatch của `section_view` đặt layer riêng).
- Hai chỗ sửa đúng nhưng sinh lỗi mới ngay ở chỗ sửa: KFG-03 (mẫu `*`, lỗi mới KFG-30) và KFG-13 / nghi vấn đường dẫn G5 (lỗi mới KFG-28, 29).
- 11 lỗi mới KFG-28…38: 3 vừa, 8 nhẹ. Không có lỗi nặng.

### Test và build @ `20e3812`

| Hạng mục | Kết quả |
| --- | --- |
| Lõi | 1409 / 1409 đạt |
| Trợ lý | 407 / 407 đạt |
| Build net48 / net8.0-windows / net10.0-windows | 0 lỗi, 0 cảnh báo cả 3 |
| Test thử lượt trước, chạy lại trên bản mới (project tạm link Core) | G1 + G2: 3 test mới đạt. G5 + F5: ProbeTests + Probe2Tests (test `IncrementalUpdate` cũ fail chỉ vì nó kỳ vọng số sai cũ là 4, nay đúng là 2). G3 + G4: console chạy lại các ca |

Test bị sửa trong diff: 4 test, ở `nangcapg5.test.ts`, `NangCapF5Tests`, `NangCapG3Tests`, `NangCapG3TestSuaTests`. Mỗi test đổi đúng 1 kỳ vọng theo bản sửa (KFG-12 bọc ngoặc kép, KFG-10 mã layer + vật liệu, KFG-26 style 4 / `cellXfs` 5). Không có test nào bị nới cho qua.

Các file nhiều nhánh cùng sửa (`ChatController.cs`, `index.ts`, `systemPrompt.ts`, `F5.cs`, `Xlsx.cs`, `Standards2DTools.cs`, `ChatAttachments.cs`, `content.ts`): gộp sạch, không có chỗ đè nhau.

### 27 mã

| Mã | Kết luận | Bằng chứng ngắn |
| --- | --- | --- |
| KFG-01 | Sửa đúng; còn chưa Dispose ở nhánh từ chối (KFG-31) | `NamedViewCopy` clone trong transaction; self-test có thêm nhánh có tên |
| KFG-02 | Sửa đúng | Có `contains_others` mà không có `allow_nested` thì từ chối; `source_warnings` chép warning / warnings / contains_others / left_out_layers / skipped / note |
| KFG-03 | Sửa đúng với tên thường; mẫu `*` chưa đúng (KFG-30) | Sân / Sàn mỗi dòng đúng giá; "San" khi có cả hai thì để trống cả hai, có `ambiguous_prices`; A-WALL / A_WALL đúng; "Da" / "Đá" đúng; chữ tổ hợp đúng |
| KFG-04 | Sửa đúng | `LightPlan("auto",2,…,true)` = `(False, null)`; `LightsIn` đếm cả đèn trong block |
| KFG-05 | Sửa đúng; còn 2 điểm phụ (KFG-36, 37) | UCS 30°: tường dọc trục x của UCS thì góc xoay 0°; attach theo `frame.X` / `frame.Y` |
| KFG-06 | Sửa đúng | `TraceTarget` từ chối ảnh không có dấu trace |
| KFG-07 | **Chưa đủ** (KFG-32) | Bộ hồ sơ và `draw_hatch` đúng: bỏ qua layer hình suy ra, hatch có dấu `CLAUDECAD_PLAIN_HATCH`; G6-1 đã thêm dòng này |
| KFG-08 | Sửa đúng như luồng chính chọn | Tab mặc định chỉ có khung nhìn: không báo, có ghi chú nêu tên tab; có `sheets` thì soát cả tab đó; tab có vật khác / đã đổi tên thì soát đủ; `fix_drawing` nhận cùng `sheets`. D1 / D1b khớp |
| KFG-09 | Sửa đúng | Có `DeleteLayerStateToken` riêng; test KfgDotGTests |
| KFG-10 | Sửa đúng | Mã gồm layer + tên vật liệu, và vật liệu + màu + độ trong |
| KFG-11 | Sửa đúng; đếm chậm với PDF lớn (KFG-35) | PDF lưu nối tiếp nay ra 2; có `/Root` + bản ghi thêm ra 3; PDF nén ra 0 kèm `pages_note` |
| KFG-12 | Sửa đúng | `q(i.path)` |
| KFG-13 | Sửa đúng phần đã nêu; sinh lỗi mới KFG-29 | Chỉ gửi đường dẫn khi gói / chế độ có `trace_image`; dọn ảnh dán 7 ngày (chỉ `anh-*.*` trong thư mục ảnh dán, không đụng file đang làm nền); có `pasted_note` |
| KFG-14 | Sửa đúng | PLAN_SCOPE 2D / 3D có đợt G; prompt thêm "text read from an attached picture"; AVIF ra "avif" |
| KFG-15 | Sửa đúng; chậm khi nhiều hình cùng ô (KFG-34) | 1000,049 / 1000,051 nay ra trùng; 0 / 0,08 / 0,16 không lan chuỗi |
| KFG-16 | Sửa đúng | Đếm kích thước ở mọi không gian |
| KFG-17 | Sửa đúng | `fix_drawing` nhận `layouts` / `sheets` (C# + TS) |
| KFG-18 | Sửa đúng | Layer đích tắt / đóng băng / không in thì từ chối |
| KFG-19 | Sửa đúng | Danh sách rỗng: C# báo lỗi, TS có `.min(1)` |
| KFG-20 | Sửa đúng | Thẻ ghi "N lines / hatches" |
| KFG-21 | Sửa đúng | `hadSet` chỉ đếm handle còn sống |
| KFG-22 | Sửa đúng | Bỏ `Table` khỏi khung mặt bằng 2D |
| KFG-23 | Sửa đúng | 0 / 40 / 80 / 120 mm ra 1 tầng; 0 / 3300 ra 2 tầng |
| KFG-24 | Sửa đúng; còn lỗ ở nhánh lỗi (KFG-33) | Record lưu `L:tên\|handle`; layout trùng tên khác handle là "clash", không bị xóa; record cũ nhận theo tên |
| KFG-25 | Sửa đúng | Gói 3D chỉ còn `volume`, gói 2D còn `blocks` / `areas`, Full đủ |
| KFG-26 | Sửa đúng | Có đơn giá lẻ thì dùng style 4 `#,##0.00` |
| KFG-27 | Sửa đúng | Kết quả ảnh chụp bỏ background / lighting / changed_for_render / estimate |

### Nghi vấn lượt trước

| Nghi vấn | Kết luận |
| --- | --- |
| G4: render chạy tiếp sau khi lệnh xong | Làm đúng như luồng chính chọn: `EndGrace = max(20, ước lượng)`. Xem nhận xét cuối |
| G4: Dừng / quá giờ trả cảnh trước khi lệnh dừng hẳn | Giảm nhẹ: trả cảnh ngay vẫn giữ (có ghi lý do); file tạm cũ hơn 1 giờ được dọn ở lần render sau |
| G4: thứ tự câu trả lời `-RENDER` / `_R` lọt ra thành REDRAW | Chưa sửa; không hại |
| G4: bấm U nhảy góc nhìn | Code đã đặt `SetCurrentView` trong `DisableUndoRecording`; cần AutoCAD thật |
| G4: đèn trong block | Sửa: `LightsIn` đệ quy 8 tầng |
| G5: đường dẫn mạng / UNC | **Chưa đủ:** KFG-28, KFG-29 |
| G5: ảnh xem trống nhầm (PDF) | Sửa: màu nền cố định khớp `Capture` |
| G5: bản vẽ chưa lưu | Sửa: từ chối, nhờ khách lưu trước |
| G5: tiền tố thư mục thiếu `\`, BigTIFF | Sửa |
| G1: cạnh trùng của bộ hồ sơ bị `fix_drawing` xóa | Sửa: `DupItemOf` bỏ qua layer hình suy ra. Layer riêng của `section_view` vẫn còn (KFG-32) |
| G1: lỗi AutoCAD giữa chừng mất record | Sửa: `catch` ghi lại record rồi ném tiếp; sinh KFG-33 |
| G1: macro ghi đè PDF | Sửa: macro giữ PDF cũ, có ghi chú |
| G2: khung nhìn "của tờ" | Sửa: `Layout.GetViewports()[0]`; layout chưa mở lần nào cần AutoCAD thật |
| G2: override bằng một dấu cách | Sửa: `HidesMeasurement(" ")` = true |

### Lỗi mới

**KFG-28 — Vừa — G5 (bảo mật): luật đường dẫn bị lách bằng tiền tố thiết bị.**
- **Chỗ:**
  - `Core/Trace.cs` 87–95 (`UncShare`): chỉ đổi `\\?\UNC\`; mọi `\\?\…` / `\\.\…` khác trả null.
  - `Drawing/TraceTools.cs` 236–243 (`CheckPathAllowed`): chỉ kiểm `DriveType.Network` khi `root[1] == ':'`.
- **Tái hiện (test thử), bản vẽ ở `\\nas\duan\…`:**
  - `\\.\UNC\evil\x\a.png` → không bị từ chối;
  - `\\?\GLOBALROOT\Device\Mup\evil\x\a.png` → không bị từ chối;
  - `\\?\Z:\a.png` / `\\.\Z:\a.png` với Z: là ổ mạng ánh xạ: `GetPathRoot` = `\\?\Z:\`, bước kiểm ổ mạng bị bỏ qua, nên cho qua.
- **Hậu quả:** .NET mở được các đường này, và Windows kết nối SMB tới máy lạ. Đúng là việc luật mới định chặn: chữ trong ảnh / tài liệu dụ Claude gọi.
- **Đúng phải là:** chỉ ổ đĩa của máy, file đính kèm, hoặc UNC cùng máy chủ + share với bản vẽ.
- **Hướng sửa:** `UncShare` đổi cả `\\.\UNC\`; từ chối mọi `\\?\` / `\\.\` khác, trừ `\\?\<ổ>:\`, và dạng đó thì lấy ký tự ổ để kiểm `DriveType`.

**KFG-29 — Vừa — G5: file khách kéo vào chat từ thư mục mạng bị từ chối, và lời khuyên dẫn vào vòng lặp.**
- **Chỗ:**
  - `UI/ChatControl.cs` 1345 / 1356 (`ImageAttachment.FromFile`): file kéo vào giữ `SourcePath` gốc.
  - `CheckPathAllowed` chỉ miễn cho thư mục ảnh dán.
  - `Core/Trace.cs` ~262 / ~265 và checklist G5 mục 3 khuyên "chép file về máy **hoặc kéo vào khung chat**".
- **Tái hiện:** kéo `\\nas\anh\scan.jpg` (khác share với bản vẽ) vào chat → `trace_image attach` bị từ chối. Làm theo lời khuyên (kéo vào chat) thì bị từ chối lần nữa.
- **Lệch ý luồng chính chọn:** "file khách đính kèm" được phép.
- **Hướng sửa:** file kéo / chọn từ ổ mạng thì chép vào thư mục `anh-dinh-kem` (hoặc ghi danh sách đường dẫn khách đã đính kèm và cho qua); hoặc bỏ "kéo vào chat" khỏi lời khuyên.

**KFG-30 — Vừa — G3: mẫu `*` gõ không dấu vẫn khớp cả các tên chỉ khác dấu.**
- **Chỗ:** `Core/Estimate.cs` 177. Mẫu không dấu so theo `Key` (bỏ dấu); luật "khớp nhiều dòng khác nhau thì để trống" chỉ áp cho tên thường.
- **Test thử:** dòng "Sân phơi" và "Sàn gỗ", giá "San*" 9 → cả hai được giá 9, không ghi chú, không có `ambiguous_prices`.
- **Hậu quả:** đúng là trường hợp Sân / Sàn mà KFG-03 định chặn; lệch `dot-g-cho-duyet.md` ("không dấu chỉ khi … khớp một dòng").
- **Hướng sửa:** mẫu không dấu khớp các dòng mà phần chữ cố định khác dấu nhau thì đưa vào `Ambiguous`, để trống và kể ra.

**KFG-31 — Nhẹ–vừa — G4: bản sao góc nhìn không được Dispose ở nhánh từ chối bình thường.**
- **Chỗ:** `Drawing/RenderG4.cs` ~131–135. `named = NamedViewCopy(...)` tạo trước `ImagePath`, kiểm thư mục và `RequireOverwriteCard`; cả ba có thể ném lỗi trước mọi `try` / `Dispose`.
- **Tái hiện:** "render lại góc Phối cảnh 1" khi PNG đã có. Lần gọi đầu luôn bị từ chối để hỏi thẻ, và bản sao `ViewTableRecord` bị bỏ cho GC dọn.
- **Hậu quả:** đối tượng AutoCAD ngoài bản vẽ bị GC dọn thay vì Dispose có thể gây lỗi. Bàn giao `nang-cap-g-sua-kfg-b.md` ghi "Dispose ở mọi nhánh", lệch code.
- **Hướng sửa:** gọi `NamedViewCopy` sau `RequireOverwriteCard`, hoặc `try { … } catch { named?.Dispose(); throw; }`.

**KFG-32 — Nhẹ — G2 (KFG-07 chưa đủ): `section_view` / `projection_view` đặt layer riêng vẫn bị báo sai.**
- **Chỗ:**
  - `Drawing/SectionTools.cs` 297 / 483 cho đặt `layer` / `hatch_layer` / `behind_layer`;
  - `SectionTools.cs` ~383 không gọi `MarkPlainHatch` (chỉ `DrawTools.cs` 210 / 244 gọi).
- **Hậu quả:**
  - hatch mặt cắt trên layer riêng vẫn bị báo "mất liên kết";
  - nét trên layer riêng vẫn được so trùng, nên `fix_drawing duplicates` có thể xóa nét của hình chiếu.
- **Hướng sửa:** gọi `MarkPlainHatch` sau `EvaluateHatch` trong `SectionView`. Nét trùng: gắn dấu tương tự, hoặc ghi rõ trong tài liệu.

**KFG-33 — Nhẹ — G1: nhánh lỗi làm mất handle của tờ cũ, nên KFG-24 có thể quay lại.**
- **Chỗ:** `Drawing/DrawingSetTools.cs`, `catch` mới (~372–381) và `WriteSetRecord` (~494–497).
- **Nguyên nhân:** `keep.Layouts` gồm cả `last.Layouts` mà `RemoveSet` đã xóa; `WriteSetRecord` tính lại handle theo tên, tên không còn thì ghi handle `""`. `OurLayouts` gặp handle rỗng thì nhận theo tên.
- **Hậu quả:** sau một lần AutoCAD lỗi giữa chừng, layout khách tự tạo trùng tên tờ cũ (vd KT-05) lần sau bị coi là của bộ và bị xóa (sau thẻ).
- **Hướng sửa:** trong `catch` chỉ giữ tên còn tồn tại; hoặc `WriteSetRecord` bỏ tên không tìm thấy thay vì ghi handle rỗng.

**KFG-34 — Nhẹ — G2: so trùng chậm khi nhiều hình cùng ô.**
- **Chỗ:** `Core/DrawingCheck.cs`, `DuplicateIndices(IList<DupItem>, tol)` mới. Chỉ băm theo số đầu, các hình chung ô so từng cặp, tức O(n²).
- **Test thử:** 20 000 đường cùng x đầu, y khác nhau: 2,4 s riêng bước này. Có `MaxScan` chặn nên không treo.
- **Hướng sửa:** băm theo (x, y), xét 9 ô kề.

**KFG-35 — Nhẹ — G5: đếm trang PDF có thể treo AutoCAD vài giây.**
- **Chỗ:** `Core/Trace.cs` ~288, regex `(\d+)\s+(\d+)\s+obj\s*<<(?:(?!endobj).){0,4000}?/Type\s*/Page`. Không có thời hạn, chạy trên luồng chính, khi không đọc được `/Root`.
- **Test thử:** PDF giả 45,7 MB, 15 000 object: 7,8 s, ra 0 trang. PDF scan thật ít object nên nhanh.
- **Hướng sửa:** `matchTimeout` (vd 2 s, hết giờ coi là 0 trang, đã có `pages_note`), hoặc quét bằng `IndexOf`.

**KFG-36 — Nhẹ — G5: chỗ đặt ảnh mặc định đè lên bản vẽ khi UCS xoay lớn.**
- **Chỗ:** `Drawing/TraceTools.cs`, attach không có `insert`. `InsertPoint` (`Core/Trace.cs` 245–250) tính theo khung WCS, còn ảnh xoay quanh góc dưới trái nên lấn sang trái một đoạn h·sin θ.
- **Ví dụ:** ảnh vuông 10 m, khoảng cách 5 000 mm: θ > 30° là đè; θ = 90° nằm hẳn trên bản vẽ.
- **Hướng sửa:** tính chỗ đặt từ khung bản vẽ trong UCS, hoặc dời `at` thêm h·max(0, sin θ).

**KFG-37 — Nhẹ — G5: `min` / `max` của PDF sau scale / straighten và của action `info` vẫn là khung WCS.**
- **Chỗ:** `Corners()` với `PdfReference` và `TraceInfo` dùng `GeometricExtents`.
- **Hậu quả:** khi UCS xoay, hai điểm này không phải góc ảnh trong UCS; lệch với `corners` lúc attach.
- **Hướng sửa:** lưu `at`, `w`, `h` vào dấu trace, hoặc lấy góc từ `Position` + `Transform`.

**KFG-38 — Nhẹ — G2 (test / chú thích).**
- `NangCapG2Tests.cs` 101–110 vẫn test `ShapeKey` + `DuplicateIndices(IList<string>)` kiểu cũ, mà code không còn gọi. So trùng polyline chiều ngược qua `DupItemOf` mới không có test.
- `DrawingCheck.cs` ~194: summary cũ dính lên `class DupItem`; `TargetLayerStateError` có hai `<summary>`.
- **Hướng sửa:** chuyển test sang `DupItem`, xóa hàm chết, sửa chú thích.

### Nhận xét (không tính lỗi, vì đúng như luồng chính chọn)

- **G4, ngưỡng chờ:** nếu `-RENDER` kết thúc lệnh ngay rồi render chạy nền, thì ngưỡng cắt thực tế là thời gian *ước lượng* (vừa 60 s, đẹp 180 s), không còn là tối đa (300 / 900 s).
  - Render chậm hơn ước lượng sẽ thành ảnh chụp, trong khi dòng tiến độ vẫn báo "tự dừng ở 300 s".
  - Checklist G4 G.3 đã nhờ đo trên máy thật. Nếu đúng là chạy nền thì nên chờ tới mức tối đa.
- **G2:** `IsDerivedViewLayer` bỏ luôn việc so trùng cho nét khách tự vẽ trên A-ELEV / A-SECT. Rủi ro thấp, vì skill định nghĩa các layer này cho hình suy ra.
- **G5:**
  - Bản vẽ mở từ Z: (= `\\nas\duan`) với ảnh ghi `\\nas\duan\…` bị từ chối (`UncShare("Z:\…")` = null): sai theo hướng an toàn.
  - Đường tương đối gặp `File.Exists` trước lần kiểm thứ hai (`TraceTools.cs` 64): nếu thư mục hiện tại của AutoCAD trên ổ mạng thì có thể chạm máy kia trước khi bị từ chối. Nghi vấn, chưa chứng minh.

### Tài liệu

- **Checklist "Đợt G":** các mục G2-D1, D1b, G6-1, G6-2, G0-3, G4 A.3, G7-1/4/5/6, G4 G.1–G.5 khớp code. G4 A.5 (bấm U) khớp code nhưng chưa thử được.
  - Lệch: G5 mục 3 và lời khuyên "kéo vào chat" (KFG-29).
- **`dot-g-cho-duyet.md` mục sau kiểm tra độc lập:** khớp code, trừ ba chỗ:
  - câu "không dấu … khớp nhiều dòng thì để trống" không đúng với mẫu `*` (KFG-30);
  - "tờ của bộ cũ được nhận theo mã riêng" chưa đúng ở nhánh lỗi (KFG-33);
  - chưa ghi giới hạn của luật đường dẫn với dạng `\\.\` / `\\?\` (KFG-28).
- **Bàn giao `nang-cap-g-sua-kfg-b.md`:** câu "Dispose ở mọi nhánh" lệch code (KFG-31).

### Chưa kiểm được

- Cần AutoCAD thật:
  - render (chuỗi trả lời, render có chạy nền sau khi lệnh kết thúc không, U hoàn tác);
  - viewport của layout chưa mở lần nào;
  - ảnh raster / PDF underlay trên UCS xoay;
  - kéo file mạng vào khung chat (UI WPF).
- Excel thật tính lại công thức (chỉ soát XML).
- Claude thật làm theo prompt mới.

### Thời gian, chi phí

- Lượt soát lại khoảng 30 phút.
- Chi phí thread khoảng $19,7 (lượt trước khoảng $11,4, nên lượt này khoảng $8,3, gồm 3 tác vụ con tiếp tục từ lượt trước).
- Tốn nhất: 3 tác vụ con đọc diff và chạy lại project test tạm.

## Phân việc sửa lượt 2 (luồng chính, 26/11 02h20)

Kết quả lượt soát lại 1: 26 / 27 lỗi sửa đúng, KFG-07 sửa chưa đủ; 11 lỗi mới KFG-28 … 38 (3 vừa, 8 nhẹ), không có lỗi
nặng. Chia như lượt trước, mỗi thread gộp `full/nang-cap-g-tong-the` @ `20e3812` vào nhánh mình trước khi sửa.

| Thread | Nhánh | Lỗi nhận |
| --- | --- | --- |
| Thread đợt F1 (G2) | `full/nang-cap-g-sua-kfg-a` | KFG-32 (phần còn lại của KFG-07), 34, 38 |
| Thread Test & sửa F1 (G4, G5) | `full/nang-cap-g-sua-kfg-b` | KFG-28, 29, 31, 35, 36, 37; hai nhận xét G5 (đường tương đối, ổ mạng đã ánh xạ) |
| Thread đợt F2 (G1, G3; giữ nhánh tích hợp) | `full/nang-cap-g-tong-the` | KFG-30, 33; ba chỗ tài liệu lệch; gộp hai nhánh trên, CI |

Quyết định luồng chính giữ nguyên sau nhận xét của thread kiểm tra:

- **Chờ sau khi lệnh render kết thúc:** vẫn là số lớn hơn giữa 20 giây và thời gian ước lượng. Chưa ai biết lệnh render của
  AutoCAD kết thúc trước hay sau khi ảnh ra; nếu thử thật thấy lệnh kết thúc ngay còn ảnh ra sau thì đổi thành chờ tới thời
  gian tối đa (một chỗ: `RenderLogic.EndGrace`). Đây là mục cần chủ dự án thử tay trước tiên.

### Tiến độ sửa lượt 2

| Thread | Kết quả |
| --- | --- |
| Thread đợt F2 (G1, G3) | 26/11 02h23: CI 45682294964 của bản gộp lượt 1 (`20e3812`) xanh. Sửa KFG-30, 33 ở `6868a38`, lõi 1410, trợ lý 407. Chờ hai nhánh kia để gộp + CI |
| Thread đợt F1 (G2) | 02h25: sửa KFG-32, 34, 38 ở `full/nang-cap-g-sua-kfg-a` @ `deec2a4`, lõi 1412, trợ lý 407, ≈ $4. Lộ và sửa thêm một lỗi: polyline vẽ ngược chiều có cung tròn không bao giờ được nhận là trùng. Giới hạn đã ghi: khách chép nét của hình chiếu thì bản chép cũng mang dấu, nên không bị báo trùng |
| Thread Test & sửa F1 (G4, G5) | 02h26: sửa KFG-28, 29, 31, 35, 36, 37 và hai nhận xét G5 ở `full/nang-cap-g-sua-kfg-b` @ `adef96c` (code `271ae04`), lõi 1432, trợ lý 407. Quyết định mới: file khách kéo / chọn từ thư mục mạng được khung chat chép về máy (dọn sau 7 ngày) rồi dùng bản chép; bản vẽ mở từ ổ mạng đã ánh xạ mà ảnh ghi theo tên máy chủ thì vẫn từ chối, lời báo hướng dẫn ghi theo ký tự ổ |

## Soát lại lượt 2 (26/11 02h31)

*Nguyên văn báo cáo soát lại của thread kiểm tra độc lập.*

**Còn 1 lỗi mức vừa: KFG-39, do chính bản sửa KFG-29 sinh ra.** Bản chép file mạng giữ giờ sửa cũ của file gốc nên bị dọn ngay ở lần đính kèm sau.
Các mã khác của lượt 2 đều sửa đúng. Ngoài KFG-39 có thêm 3 lỗi nhẹ. Không có lỗi nặng.

Bản soát: `57b69a0`, so với `20e3812`. Tớ không sửa code, không commit / push / CI.

### Test và build @ `57b69a0`

- **Lõi:** 1436 / 1436 đạt.
- **Trợ lý:** 407 / 407 đạt.
- **Build net48 / net8.0-windows / net10.0-windows:** 0 lỗi, 0 cảnh báo cả 3.
- **Test bị sửa trong diff:**
  - `NangCapG2Tests`: chuyển sang `DupItem`, vẫn giữ ý cũ (vẽ ngược, lệch 0,01, khác layer).
  - `NangCapG5Tests`, `SuaKfgBTests`, `KfgDotGTests`: chuỗi kỳ vọng theo dòng mới `LocalFile` của `ChatController`.
  - Không có test nào bị nới cho qua.
- **Test thử của tớ chạy lại trên bản mới (project tạm link Core):**
  - G1 + G2: 4 test đạt; các ca của lượt 1 đã gộp vào trong đó.
  - G3 + G4: console chạy lại toàn bộ ca KFG-03 lượt 1, cộng 9 ca mẫu `*` mới.
  - G5: ProbeTests + Probe2 + Probe3. Có 2 test báo fail, nhưng là do chính test thử viết theo số sai cũ (PDF 4 trang) và một ca viết sai; code đúng.

### Từng mã

| Mã | Kết luận | Bằng chứng ngắn |
| --- | --- | --- |
| KFG-07 (phần còn lại = KFG-32) | Sửa đúng | `AddCurve` gắn `CLAUDECAD_VIEW` cho mọi nét `section_view` / `projection_view` trên layer nào cũng có; hatch mặt cắt có `MarkPlainHatch`. Dấu chỉ dùng trong `DupItemOf`, nên `not_bylayer` / `layer_zero` / `defpoints`… vẫn kiểm và sửa được nét hình chiếu (đúng chủ ý) |
| KFG-28 | Sửa đúng | Bị từ chối: `\\?\UNC\` / `\\.\UNC\` khác share, `//evil/x`, `\\host` không share, `\\.\pipe\`, `\\?\Volume{…}`, `\\?\GLOBALROOT`, dấu chấm cuối. Được mở: cùng share (không phân biệt hoa thường, cả dạng `//` và `\\.\UNC\`). `\\?\Z:\` / `\\.\Z:\` đổi thành `Z:\` rồi kiểm `DriveType`. `C:a.png`, `\a.png`, `\??\` bị từ chối trước khi đọc file |
| KFG-29 | **Chưa đủ:** sinh KFG-39 (vừa), KFG-40 (nhẹ) | Đúng: file mạng chỉ bị đọc lúc khách kéo / chọn; tên `tep-…` không ghi đè; `trace_image` vẫn chép file ra cạnh bản vẽ trước khi gắn, nên dọn 7 ngày không mất nền của bản vẽ đã lưu |
| KFG-30 | Sửa đúng | "San*" với "Sân phơi" + "Sàn gỗ": cả hai để trống, có `ambiguous_prices`. "San*" với "Sân phơi" + "Sân thượng": cả hai có giá. "Sân*" có dấu, `*` trơn, mẫu khớp một dòng, "*an", "Da*": đều đúng. Toàn bộ ca KFG-03 lượt 1 cho kết quả y như trước |
| KFG-31 | Sửa đúng | Mỗi đường đi Dispose `named` đúng một lần; không dùng sau Dispose; không còn nhánh rò |
| KFG-32 | Sửa đúng | Như dòng KFG-07 |
| KFG-33 | Sửa đúng | Nhánh lỗi chỉ ghi tờ của lần này cộng `oldLayouts`; `WriteSetRecord` bỏ tên không còn, không ghi handle rỗng; đường chạy thường không đổi |
| KFG-34 | Sửa đúng; còn chậm ở một dạng hiếm (KFG-42) | Lệch qua mép ô (0,002 cả x lẫn y; quanh 0 âm / dương; đúng 0,1; tọa độ 1e6) đều bắt được. Ba bản trùng ra [1,2]. 20 000 đường cùng x: 19 ms (trước 2,4 s) |
| KFG-35 | Sửa đúng | PDF giả 45,7 MB: 2,0 s (trước 7,8 s), ra 0 trang kèm `pages_note` |
| KFG-36 | Sửa đúng | UCS 90°, bản vẽ 10 000 × 5 000: ảnh đặt ở (10 000, 10 000), không đè; UCS World như cũ |
| KFG-37 | Sửa đúng (đọc code, chưa thử AutoCAD) | Góc ảnh lưu trong dấu trace; `MoveFrame` dùng đúng ma trận; `info` dùng `Corners`; dấu cũ thì lấy khung WCS |
| KFG-38 | Sửa đúng | `ShapeKey` / `DuplicateIndices(IList<string>)` đã xóa; sửa thêm lỗi bulge −0 (có test); chú thích chồng đã gỡ |
| Nhận xét G5 (đường tương đối; ổ ánh xạ) | Đúng | Tương đối bị từ chối trước khi đọc; lời báo hướng dẫn ghi theo ký tự ổ |
| 26 mã đúng ở lượt 1 | Vẫn đúng | Test thử cũ chạy lại cả 3 mảng đều ra như lượt 1 |

### Lỗi mới

**KFG-39 — Vừa — G5 (do bản sửa KFG-29): bản chép `tep-` bị dọn ngay ở lần đính kèm sau.**
- **Chỗ:** `UI/ChatAttachments.cs` 52–55. `CleanOld()` chạy rồi mới `File.Copy(path, file)`. `File.Copy` giữ giờ sửa của file nguồn (đã thử: nguồn sửa 30 ngày trước thì bản chép cũng "30 ngày tuổi"). Ở lần `LocalFile` / `Save` kế tiếp, `CleanOld()` xóa bản chép đó.
- **Tái hiện:**
  - kéo hai ảnh mạng cũ (sửa hơn 7 ngày) vào cùng một tin: lần chép thứ hai xóa bản chép thứ nhất;
  - hoặc kéo một ảnh mạng cũ, rồi dán ảnh ở tin sau, trước khi Claude gắn.
- **Hậu quả:** Claude nhận đường dẫn tới file đã mất và báo "no file". Đúng ca KFG-29 định chữa (bản scan cũ trên NAS).
- **Kết quả đúng:** bản chép được giữ 7 ngày tính từ lúc chép.
- **Hướng sửa:** sau `File.Copy` gọi `File.SetLastWriteTimeUtc(file, DateTime.UtcNow)`. Nên chép vào file tạm rồi đổi tên, để mất mạng giữa chừng không để lại file dở mà lần sau `File.Exists` lại dùng.

**KFG-40 — Nhẹ — G5 (do bản sửa KFG-29): chép file mạng chạy trên luồng của AutoCAD; file quá 50 MB bị bỏ mà không báo.**
- **Chỗ:** `ChatController.cs` (đường `ui.SendRequested` → `SendComposed` → `Send`), gọi `LocalFile` đồng bộ trên luồng chính.
- **Hậu quả:**
  - ổ mạng chậm thì AutoCAD đứng trong lúc chép (tới 50 MB), với mọi ảnh mạng kéo vào trên gói 2D / Full, kể cả khi khách không định vẽ lại;
  - file quá 50 MB: `LocalFile` trả null, nên Claude thấy "(none)" mà không biết lý do để nói với khách.
- **Hướng sửa:** chép trong `Task.Run` (hoặc chỉ chép khi `trace_image attach` cần); file quá lớn thì gửi kèm ghi chú lý do.

**KFG-41 — Nhẹ — G2 (có từ trước, không do bản sửa): đường gần thẳng đứng lệch rất ít không bị nhận là trùng.**
- **Chỗ:** `Core/DrawingCheck.cs` `LineNumbers` (~153–157) đảo chiều hai đầu bằng phép so x tuyệt đối, không có dung sai. `PolylineNumbers` cũng vậy.
- **Tái hiện:** A (0,0)→(0,1000); B (0,00001,0)→(0,1000). B bị đảo chiều, A thì không, nên kết quả "không trùng".
- **Lệch tài liệu:** `dot-g-cho-duyet.md` ghi lệch ≤ 0,1 mm "luôn" bị tính là trùng.
- **Hướng sửa:** `DuplicateIndices` so cả hai chiều của line / polyline hở; hoặc sửa chữ "luôn" trong tài liệu.

**KFG-42 — Nhẹ — G2: so trùng vẫn chậm khi rất nhiều hình chung điểm đầu.**
- **Chỗ:** `DuplicateIndices` (băm 9 ô theo điểm đầu).
- **Tái hiện:** 20 000 đường cùng một điểm đầu (nan quạt): 1,9 s, vì cùng rơi vào một ô nên so từng cặp. Có giới hạn quét 20 000 nên không treo; dạng bản vẽ này hiếm.
- **Hướng sửa:** băm thêm điểm thứ hai, hoặc chấp nhận và ghi vào bàn giao.

### Nghi vấn / nhận xét (không tính lỗi)

- **G3:** mỗi dòng gặp mẫu `*` không dấu thì `WildAccentClash` quét toàn bộ dòng và dựng regex mới, tức O(dòng²). Chưa đo; bảng dự toán thường gặp không đáng kể. Nếu sửa: tính một lần cho mỗi mẫu.
- **G3:** "San*" với "Sân phơi", "Sân thượng", "Sàn gỗ" làm cả ba dòng để trống, kể cả hai dòng Sân thống nhất với nhau. Chặt nhưng an toàn, khớp quyết định "nhập nhằng thì để trống và hỏi"; Claude có `ambiguous_prices` để hỏi lại.
- **G5:** `view` đọc `ActiveFileName` của ảnh có dấu trace mà không qua luật đường dẫn (DWG người khác gửi). AutoCAD đã tự nạp ảnh đó khi mở bản vẽ, nên rủi ro thấp.
- **G5:** `\\nas\duan.\` hoặc `\\nas\duan \` bị coi là share khác nên bị từ chối. Sai theo hướng an toàn.
- **G4:** thời gian chờ sau khi lệnh render kết thúc giữ như luồng chính chốt, không tính lỗi.

### Chưa kiểm được

- **Cần AutoCAD thật:**
  - góc ảnh PDF sau scale / straighten trên UCS xoay (KFG-37);
  - render (KFG-31 và ngưỡng chờ);
  - viewport layout chưa mở;
  - hatch `section_view` trên layer riêng thật sự không bị báo.
- **Cần Windows + ổ mạng thật:** kéo file từ NAS vào khung chat, độ trễ khi chép, `DriveType` của ổ ánh xạ.
- Excel thật; Claude thật.

### Thời gian, chi phí

- Lượt 2 khoảng 20 phút.
- Chi phí thread khoảng $28,8 (lượt 1 kết thúc khoảng $19,7, nên lượt này khoảng $9,1).
- Tốn nhất: 3 tác vụ con nạp lại ngữ cảnh và chạy project test tạm (PDF giả lớn).

## Phân việc sửa lượt 3 (luồng chính, 26/11 02h33)

Kết quả lượt soát lại 2: KFG-07 (phần còn lại) và 10 / 11 lỗi mới sửa đúng; KFG-29 sửa chưa đủ và sinh KFG-39 (vừa), KFG-40
(nhẹ); thêm KFG-41, 42 (nhẹ, phần so trùng). 26 lỗi đúng ở lượt 1 vẫn đúng. Không có lỗi nặng.

| Thread | Nhánh | Lỗi nhận |
| --- | --- | --- |
| Thread Test & sửa F1 (G5) | `full/nang-cap-g-sua-kfg-b` | KFG-39, 40 |
| Thread đợt F1 (G2) | `full/nang-cap-g-sua-kfg-a` | KFG-41, 42 |
| Thread đợt F2 (giữ nhánh tích hợp) | `full/nang-cap-g-tong-the` | gộp, tài liệu, CI |

Luồng chính chốt cách dừng vòng soát: lượt soát lại 3 chỉ soát bốn lỗi này và phần code vừa sửa. Hết lỗi mức vừa trở lên thì
chốt nhánh; lỗi nhẹ còn lại (nếu có) hoặc sửa ngay nếu rẻ, hoặc ghi vào danh sách "biết, chấp nhận" kèm lý do cho chủ dự án.

### Tiến độ sửa lượt 3

| Thread | Kết quả |
| --- | --- |
| Thread đợt F1 (G2) | 26/11 02h37: sửa KFG-41, 42 ở `full/nang-cap-g-sua-kfg-a` @ `3d68e9f`, lõi 1438, trợ lý 407, ≈ $3 |
| Thread Test & sửa F1 (G5) | 02h37: sửa KFG-39, 40 ở `full/nang-cap-g-sua-kfg-b` @ `47faaf2` (code `0dd8945`), lõi 1442, trợ lý 407. Cách làm đổi: lúc gửi tin không chép gì; khung chat ghi nhớ file khách kéo / chọn, đến lúc gắn ảnh mới chép file mạng về máy (giữ 7 ngày tính từ lúc chép). Quyết định mới: AutoCAD có thể chờ khi gắn file mạng lớn; danh sách file khách đã kéo chỉ giữ trong một phiên AutoCAD |
| Thread đợt F2 (giữ nhánh tích hợp) | CI 45685933989 (#70) của bản gộp lượt 2 + tài liệu (`fff7ffe`) xanh. Thread dừng 02h43 vì chạm giới hạn sử dụng 5 giờ (cả luồng chính cũng dừng tới 04h55); 04h56 luồng chính cho chạy tiếp: gộp hai nhánh lượt 3, tài liệu, CI |
