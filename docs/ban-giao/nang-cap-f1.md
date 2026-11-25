# Bàn giao: Nâng cấp đợt F1

Nhánh `full/nang-cap-f1`, tách từ `full/nang-cap-f-goc` @ `2dbeee2` (= `full/nang-cap-tong-the` @ `012ed29` + file quyền).
Chủ dự án chốt làm đợt F ngày 25/11/2026 (F1 → F4 nối tiếp); F1 gồm 2 việc:

1. **Mọi lệnh hiểu hệ tọa độ đang dùng (UCS)**, giống lệnh gốc của AutoCAD.
2. **Nút chọn chế độ "Cả hai / 2D / 3D"** cho máy gói Full (chủ dự án chốt 14/11/2026).

Chưa thử trên AutoCAD thật: toàn bộ phần đụng AutoCAD (đổi hình vào UCS, đọc UCS hiện hành, điểm bấm chọn, nút trên
palette thật) chỉ được kiểm bằng build 3 target, test lõi / trợ lý và UI test WPF trên CI. Khối thử tay: phần
"Đợt F1" cuối `docs/checklist-tong-the.md`.

## Việc 1: lệnh hiểu UCS

### Khách thấy gì

- Khách xoay hoặc dời UCS (lệnh UCS của AutoCAD, hoặc nhờ Claude `set_ucs`) rồi nhờ "vẽ hình chữ nhật 4000 × 3000 tại
  gốc": hình nằm ở gốc UCS, cạnh song song trục UCS — như gõ RECTANG trong UCS đó. Chữ, kích thước, khối, khối 3D, cầu
  thang, mái, lan can, dầm xoay theo UCS. UCS mặt đứng (front): hình vẽ nằm trên mặt đứng.
- Số Claude đọc về (điểm, khung bao, góc chữ / block, cạnh / mặt khối) cũng theo UCS đó; kết quả có dòng `coordinates`
  nói rõ "theo UCS …, không phải WCS". `get_drawing_info` có mục `ucs` (gốc, trục, xoay bao nhiêu độ) khi UCS khác World.
- Điểm khách bấm chọn gửi cho Claude (nút Chọn điểm) là tọa độ UCS.
- **UCS là World: mọi thứ y như trước.** Không đổi tham số, không đổi kết quả, không thêm dòng nào (kiểm bằng toàn bộ
  test cũ, xem "Test").

### Cách làm (một chỗ chung, không sửa rải từng lệnh)

Lõi thuần ở `plugin/ClaudeCAD.Plugin/Core/Ucs.cs` (`UcsFrame`, `UcsScope`, `UcsLogic`), móc vào bộ điều phối lệnh
`Drawing/ToolDispatcher.cs` (`RunHandler`): mọi lệnh (một lệnh, từng lệnh trong `batch_execute`, macro) đi qua đây.
Mỗi lệnh thuộc một trong 3 kiểu (`UcsLogic.ModeOf`):

| Kiểu | Lệnh | Cách xử lý |
| --- | --- | --- |
| **Create** — dựng mới chỉ từ số | vẽ đường / cung / tròn / elip / spline / chữ nhật / đa giác / điểm / xline / ray / wipeout / revcloud, chữ, mtext, kích thước dài / thẳng hàng / góc (theo điểm) / tọa độ, mleader, bảng, chèn block, gắn xref, khối 3D (`draw_solid` + 6 tên cũ), lan can / dầm theo điểm, cầu thang (không theo ô thang có sẵn), mái theo `outline_points` + `wall_top_z` | Lệnh chạy **như UCS là World** (tham số giữ nguyên), rồi mọi hình mới của lệnh trong model space được đưa vào UCS bằng **một phép biến đổi** (`TransformBy`). Mặt phẳng, góc, hướng chữ, chiều trục khối tự theo UCS, đúng như lệnh AutoCAD. Kết quả lệnh tính trước khi biến đổi nên đã là số UCS |
| **Ref** — làm trên hình có sẵn | sửa hình (dời, chép, xoay, co giãn, đối xứng, mảng, offset, fillet, trim / extend, kéo giãn, bẻ gãy…), truy vấn (`list_entities`, `get_entity`, đo, khung bao, `get_solid_info`…), 3D trên khối có sẵn (extrude, revolve, sweep / loft, slice, rotate_3d, align, presspull…), `build_3d_from_plan`, mặt cắt / hình chiếu, hướng nhìn tùy chọn của `set_view_3d` (góc nhìn đặt sẵn top / front / sw_iso… vẫn theo WCS như mặc định của lệnh VIEW), hatch, kích thước theo đối tượng, các lệnh Create khi lấy hình có sẵn (cầu thang theo `well_handle`, dầm theo `path_handles`, lan can theo `path_handle`, kích thước góc theo 2 đường, mái theo tường) | Điểm trong tham số đổi UCS → WCS ngay lúc lệnh đọc (`Params.Point / Points`); hướng / độ dời (`displacement(s)`, `direction`, `normal`, `x_axis`, `y_axis`) chỉ xoay, không dời; `scale` (hệ số x, y, z) giữ nguyên. Điểm trả về đổi WCS → UCS (`Acad.J / J3`, `Params.ToJsonPoint`, cạnh / mặt khối); mọi cặp `min` / `max` (khung bao) được tính lại thành khung theo trục UCS. Xoay / mảng tròn quay quanh trục z của UCS; mảng chữ nhật xếp theo trục x, y, z của UCS. Khung chọn (`list_entities` window, `zoom_window`, `stretch_entities` corner1/2) là hình chữ nhật theo trục UCS |
| **Wcs** — giữ WCS có chủ đích | `set_ucs`, `reset_ucs`, kiểu hiển thị, `capture_view`, zoom extents, layout / viewport / in PDF (giấy có hệ riêng), layer, kiểu chữ / kích thước, xref (trừ gắn), xuất STL, lưu, purge, hoàn tác, `batch_execute` (từng lệnh bên trong tự theo kiểu của nó) | Không đổi gì |

Thêm cho kiểu Ref khi trục z của UCS vẫn thẳng đứng (chỉ dời / xoay mặt bằng): góc tuyệt đối `draw_hatch.angle`,
`edit_text.rotation` cộng góc xoay UCS; cao độ `build_3d_from_plan.base_z`, `build_roof.wall_top_z` cộng cao độ gốc UCS;
góc trong `get_entity` (xoay chữ / block, góc cung, góc line, góc hatch) đo từ trục x của UCS. Hatch theo điểm nằm ở cao
độ gốc UCS.

UCS lấy ở `Acad.UcsOf`: UCS hiện hành của model space (khung nhìn đang dùng). Đang ở layout giấy → coi như World (lệnh
trên giấy giữ tọa độ giấy như cũ).

### Macro (tự quyết, lý do)

- **Ghi:** lệnh ghi lúc UCS là World → file macro **y như trước** (không thêm gì). Ghi lúc UCS khác World → mỗi lệnh lưu
  kèm `"_ucs": {origin, x_axis, y_axis (WCS), name?}` = UCS lúc ghi; tham số vẫn là số khách / Claude đã đưa (theo UCS đó).
- **Chạy lại:** mỗi lệnh chạy **trong UCS đã lưu kèm nó**, bất kể UCS lúc chạy là gì; lệnh không có `_ucs` (mọi macro
  ghi trước F1, và macro ghi trong World) chạy theo **World**.
- Lý do: macro là "vẽ lại đúng như lần đó" mà không cần Claude. Nếu chạy theo UCS hiện hành, macro cũ (ghi bằng số WCS)
  sẽ ra sai chỗ ngay khi khách đang để UCS xoay — đúng điều chủ dự án dặn tránh. Lưu UCS lúc ghi thì cả macro cũ lẫn
  mới đều ra đúng chỗ đã vẽ. "Chạy macro tại điểm" vẫn dời cả cụm hình tới điểm bấm (độ dời tính bằng WCS, gửi kèm
  `_ucs: "world"`).
- `_ucs` không bị đổi handle khi chạy lại (không phải khóa handle), bị bỏ khỏi tham số trước khi tới lệnh.

### Giới hạn đã biết (ghi để thread Test & sửa / chủ dự án xem)

- UCS **nghiêng** (trục z không thẳng đứng): lệnh Create đúng hoàn toàn (vẽ trên mặt UCS). Lệnh Ref trên hình có sẵn:
  điểm / hướng đúng, nhưng hatch theo điểm, `build_3d_from_plan`, mái theo tường, cầu thang theo ô thang vẫn làm việc
  trên mặt bằng WCS (mặt bằng 2D luôn nằm ngang); góc / cao độ tuyệt đối không cộng thêm.
- Bộ lọc cạnh / mặt khối theo hướng (`parallel_z`, `top`…) vẫn theo trục WCS (z lên); điểm, pháp tuyến trong danh sách
  cạnh / mặt đã theo UCS.
- Khung "frame" của `section_view` / `projection_view` (quy tắc chiếu điểm ra hình) ghi rõ `frame_system: "WCS"`.
- Bảng (`block_schedule`, `area_schedule`, `table_position`) đặt đúng điểm UCS nhưng không xoay theo UCS.
- Kích thước tọa độ (`add_dim_ordinate`) đo từ gốc UCS (như AutoCAD).

### Phía trợ lý

`agent/src/agent/systemPrompt.ts`: "mm theo UCS hiện hành, World = WCS; get_drawing_info báo UCS khác World; kết quả có
`coordinates`"; quy tắc 3D: `set_ucs` / `reset_ucs` đổi hệ cho mọi lệnh sau, gốc / trục của chính chúng là WCS, không
tự đổi số giữa các hệ. Mô tả `point`, `set_ucs`, `reset_ucs`, `zoom_window`, `draw_line`, khung chọn: "current UCS". Các điểm cố ý không theo
UCS có mô tả riêng (`tools/common.ts`): `wcsPoint` (gốc / trục của `set_ucs`, `view_center` của viewport, khung in PDF:
WCS trên Model, đơn vị giấy trên layout), `paperPoint` (vị trí viewport trên tờ giấy).

## Việc 2: nút chế độ "Cả hai / 2D / 3D"

### Khách thấy gì

- Máy gói **Full**: hàng trên cùng của khung chat (cạnh nhãn "Gói: Full") có nút icon khối + chữ **Cả hai** / **2D** /
  **3D**. Bấm → 3 lựa chọn "Cả hai (2D và 3D)", "2D — chỉ lệnh 2D", "3D — chỉ lệnh 3D". Nút nằm ở hàng trên cùng nên
  **vẫn thấy khi thu gọn** khung chat. Máy gói 2D / 3D, máy chưa kích hoạt: không có nút.
- Mặc định **Cả hai**. Lựa chọn lưu trong `%LocalAppData%\ClaudeCAD\settings.json` (`work_mode`), mở AutoCAD lần sau
  vẫn giữ.
- Chọn 2D / 3D: Claude chỉ được nạp lệnh và hướng dẫn của nhóm đó — đúng bộ lệnh của gói 2D / gói 3D (`PlanLogic`).
- Ở chế độ 2D mà nhờ vẽ 3D (hoặc ngược lại): Claude trả lời kiểu "Đang ở chế độ 2D, đổi sang 3D hoặc Cả hai (nút chế độ
  trên khung chat) để vẽ 3D". **Không** nói gói, mua, nâng cấp hay liên hệ chủ plugin (system prompt, dòng ngữ cảnh mỗi
  tin, và lỗi NOT_IN_PLAN của plugin đều theo chế độ).
- **Đổi giữa chừng (tự quyết, ít rủi ro):** chế độ lấy **một lần lúc gửi mỗi tin**; lượt đang chạy giữ chế độ cũ đến
  hết, chế độ mới áp dụng từ tin sau. Khung chat ghi một dòng "Đã chuyển sang chế độ 2D (Claude chỉ dùng lệnh 2D): áp
  dụng từ tin nhắn sau, lượt đang chạy giữ chế độ cũ." (lúc rảnh: "… áp dụng từ tin nhắn tới, đoạn chat giữ nguyên.").
  Trợ lý khởi động lại Claude với bộ lệnh mới và **nối tiếp cùng cuộc chat** (như khi gói đổi) — không mất ngữ cảnh.
  Lý do: đổi bộ lệnh ngay giữa lượt sẽ làm lệnh Claude đang gọi bị từ chối nửa chừng; chờ tin sau thì không có trạng
  thái lưng chừng.
- **Macro không bị chế độ giới hạn:** macro chạy lại không dùng Claude (không tốn token), nên chạy đủ lệnh của gói máy
  (Full), dù đang ở chế độ 2D / 3D.
- Skill Claude soạn trên máy Full ở chế độ 2D / 3D vẫn ghi là skill Full (không bị gắn "plan: 2d").

### Code

- Lõi: `Core/Plans.cs` → `WorkModeLogic` (chuẩn hóa, chỉ hiện trên Full, gói hiệu lực theo chế độ, lời từ chối theo chế
  độ, dòng báo đổi). `Core/PaletteSettings.cs` → `WorkMode` (`work_mode`).
- Plugin: `ChatController.cs` (`_turnWorkMode` lấy lúc gửi tin; `service_plan` / `plan_tools` / `work_mode` gửi trợ lý;
  `ITurnHost.Plan` / `WorkMode` cho bộ điều phối; macro dùng gói thật; `OnWorkModeChanged`; `ShowPlan` hiện / ẩn nút).
  `Drawing/ToolDispatcher.cs` (lỗi NOT_IN_PLAN theo chế độ). `UI/ChatControl.cs` (`BuildModeButton`, `PickWorkMode`,
  `SetWorkMode`, sự kiện `WorkModeChanged`). `UI/Icons.cs` (icon `mode`: hình vuông phẳng trước một khối).
- Trợ lý: `ipc/protocol.ts` (`work_mode` chỉ nhận khi trùng `service_plan`), `agent/session.ts` (khóa gói gồm chế độ →
  đổi chế độ = khởi động lại cùng cuộc chat), `agent/systemPrompt.ts` (`workModeSection`), `agent/content.ts` (dòng
  "[Chế độ người dùng chọn: 2D (máy gói Full) …]"), `tools/index.ts` (skill soạn theo gói thật).

### Token lúc mở chat (đo như `test-tong-the-nang-cap.md`: lệnh gửi Claude + 5 lệnh thẻ, tên + mô tả + schema JSON + system prompt, ~3,5 ký tự / token)

| Máy Full | Số lệnh | Ký tự trước F1 (`2dbeee2`) | Ký tự sau F1 | ≈ token | So với Cả hai |
| --- | --- | --- | --- | --- | --- |
| Cả hai (như trước F1, chỉ có kiểu này) | 136 | 202 011 | 202 969 | 57,7k → 58,0k | — |
| Chế độ 2D | 105 | *(chưa có)* | 136 580 | 39,0k | **−19,0k (−33 %)** |
| Chế độ 3D | 93 | *(chưa có)* | 140 394 | 40,1k | **−17,9k (−31 %)** |

- Phần UCS thêm vào mô tả / system prompt: +958 ký tự (≈ +0,3k token) ở Cả hai; máy gói 2D thật +688 (135 669 →
  136 357), gói 3D thật +832 (139 339 → 140 171), ≈ +0,2k token.
- Chế độ 2D / 3D nhiều hơn gói 2D / 3D thật 223 ký tự (≈ 0,06k token): đoạn "Work mode" thay đoạn "service plan".

## Test

- Lõi `dotnet test plugin/ClaudeCAD.Core.Tests`: **952** (909 + 1 cũ trước F1 = 910; thêm 27 UCS `NangCapF1UcsTests.cs`, 15 chế
  độ `NangCapF1ModeTests.cs`). Mọi test cũ giữ nguyên, không sửa test nào; một test nguồn cũ (`Array_levels_stack_along_z`
  tìm chuỗi `Vector3d.ZAxis * (l * ls)`) vẫn qua vì nhánh World giữ đúng dòng đó.
- Trợ lý `cd agent && npm ci && npm test`: **307** (298 + 9 `test/nangcapf1.test.ts`). Test cũ `solids2.test.ts` tìm chữ
  "stay WCS" vẫn qua: câu mới "their own origin / axes stay WCS" (đúng nghĩa mới).
- UI test WPF (chỉ chạy trên CI Windows) `ClaudeCAD.UI.Tests/NangCapF1ModeUiTests.cs`: nút ẩn khi chưa Full / ẩn lại khi
  đổi gói, chọn chế độ báo đúng 1 lần / đánh dấu menu / nhớ trong cài đặt của palette, chế độ đã lưu hiện lúc mở, vẫn
  thấy khi thu gọn; ảnh `mode-2d-dark.png`, `mode-2d-light.png`, `mode-3d-compact.png`, `mode-menu.png` trong artifact
  `palette-screenshots`.
- Build 3 target: net48, net8.0-windows, net10.0-windows (xem phần CI cuối file).

## Chưa kiểm được

- Mọi thứ trên AutoCAD thật: hình vào đúng UCS (nhất là kích thước, bảng, mleader, block có thuộc tính sau
  `TransformBy`), đọc UCS / tên UCS ở khung nhìn đang dùng, điểm bấm chọn, macro ghi / chạy trong UCS, nút chế độ trên
  palette thật, Claude thật trả lời đúng câu "đổi chế độ" (không mời mua gói). Xem khối thử tay "Đợt F1".
