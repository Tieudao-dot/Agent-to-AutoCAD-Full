# Bàn giao: sửa lỗi kiểm tra độc lập đợt F + G, phần G2 (soát / sửa bản vẽ) — 25/11/2026

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh` (người viết G2). Nhánh `full/nang-cap-g-sua-kfg-a`, tách từ `full/nang-cap-g-tong-the`
@ `881b12e`. Báo cáo lỗi: `docs/ban-giao/kiem-tra-doc-lap-dot-fg.md` (nhánh luồng chính). Chưa chạy CI riêng: thread giữ nhánh
tích hợp chạy CI sau khi gộp.

## 1. Từng mục

| Mã | Lỗi | Đã sửa thế nào | Test |
| --- | --- | --- | --- |
| KFG-07 (vừa) | Bộ hồ sơ có hatch mặt cắt (A-SECT-PAT), và `draw_hatch` vẽ hatch theo điểm / điểm bấm không giữ biên: các hatch này đều bị báo "hatch mất liên kết". | Bỏ qua hatch trên layer hình suy ra từ khối 3D (`Util2DLogic.IsDerivedViewLayer`). `draw_hatch` đánh dấu hatch nó vẽ không có biên (Xrecord `CLAUDECAD_PLAIN_HATCH`, chỉ khi hatch không liên kết); lần soát bỏ qua các hatch có dấu này. Hatch có biên mà mất biên vẫn bị báo. | `Hatches_of_the_views_and_hatches_drawn_without_boundary_are_not_unassociated` |
| KFG-08 (vừa) | Layout tên mặc định (`Layout2`…) có khung nhìn khách tự đặt bị bỏ qua hẳn, không một lời. | **Lần 2** (`2e1dfe2` + commit sau, cách luồng chính chọn): layout tên mặc định có bất kỳ vật nào ngoài khung nhìn thì kiểm đầy đủ. Layout tên mặc định **chỉ có khung nhìn** (AutoCAD tự tạo khi khách bấm vào tab) thì không báo lỗi, nhưng kết quả có ghi chú nêu tên các tab đó và bảo Claude: là tờ in thì gọi lại với `sheets` = tên tab. Tham số mới `sheets` (cho cả `check_drawing` và `fix_drawing`) kiểm đầy đủ các layout được nêu. Checklist G2-D1 viết lại, thêm D1b. | `A_default_layout_with_a_viewport_and_another_object_is_a_sheet`, `A_default_layout_with_only_a_viewport_is_named_in_a_note_not_reported` |
| KFG-15 (nhẹ) | "Trùng" so bằng cách làm tròn theo ô lưới 0,1 mm, nên hai hình lệch 0,002 mm nằm hai bên ranh ô thì không bị tính là trùng. | So thật "mỗi số lệch ≤ 0,1 mm": tìm ứng viên theo ô của số đầu và hai ô kề, rồi so từng số (`DrawingCheckLogic.DuplicateIndices(items, tol)`). Góc, tỉ lệ, bulge, nội dung chữ so đúng tới 6 số lẻ, không dùng dung sai độ dài (trước đây góc xoay 0,05 rad vẫn bị coi là trùng). | `Two_lines_0_002_mm_apart_across_a_grid_line_are_copies` |
| KFG-16 (nhẹ) | `no_dimensions` không đếm kích thước ghi trong layout. | Đếm kích thước ở mọi không gian được quét. Soát với `layouts: false` thì vẫn chỉ đếm Model. | `Dimensions_on_layouts_count_and_…` |
| KFG-17 (nhẹ) | `fix_drawing` sửa cả vật trong layout dù lần soát dùng `layouts: false`. | Thêm tham số `layouts` cho `fix_drawing` (TS + C#). Lần sửa quét đúng các không gian đó, nên mã thẻ tính trên đúng tập đối tượng. | cùng test trên |
| KFG-18 (nhẹ) | `layer_zero` / `defpoints` cho chuyển hình sang layer không in, tắt hoặc đóng băng (kể cả `X-ANH-GOC`). | Từ chối layer đích như vậy, nói rõ vì sao và bảo hỏi khách chọn layer khác (`TargetLayerStateError`). | `Objects_are_never_moved_onto_a_layer_that_does_not_print` |
| Nghi vấn G2: khung nhìn "của tờ" | Lấy khung đầu tiên trong block của layout. | Dùng `Layout.GetViewports()[0]` khi AutoCAD trả về được, không thì như cũ. Không dùng `Viewport.Number`, vì số này chỉ đúng khi layout đang mở (layout khác trả −1). | test KFG-08 |
| Nghi vấn G2: `DimensionText = " "` | Đúng là lỗi: một dấu cách làm AutoCAD không in chữ kích thước, nhưng lần soát không báo. | Có chữ đè mà không có `<>` thì báo, kể cả chỉ có dấu cách. Chỉ chuỗi rỗng mới là "không đè". Sửa tự động vẫn đưa về số đo, sau thẻ. | `A_dimension_text_of_one_space_hides_the_number_and_is_reported` |
| Nghi vấn G1 × G2: nét của bộ hồ sơ bị coi là trùng | Đúng có thể xảy ra: hai khối kề nhau cho cùng một cạnh hai lần trên hình suy ra, rồi `fix_drawing duplicates` xóa một nét của bộ hồ sơ. | Nét trên layer hình suy ra (A-SECT, A-SECT-PAT, A-SECT-BACK, A-ELEV) không đưa vào so trùng. Chạy lại `create_drawing_set` là có hình mới. | `Lines_of_the_views_from_the_model_are_never_called_copies` |

## 2. File

- Sửa:
  - `Core/DrawingCheck.cs`: `DupItem`, `DuplicateIndices(items, tol)`, `Exact`, `TargetLayerStateError`, `HidesMeasurement`;
  - `Drawing/DrawingCheckTools.cs`: phần quét / sửa;
  - `Drawing/DrawTools.cs`: 2 dòng `MarkPlainHatch`, file dùng chung;
  - `agent/src/tools/drawingCheck.ts`: 1 dòng `layouts`;
  - `docs/checklist-dot-g2.md`: D1.
- Mới: `plugin/ClaudeCAD.Core.Tests/NangCapGSuaKfgATests.cs` (8 test), file này.
- Không sửa file của thread khác ngoài `DrawTools.cs`.

## 3. Con số chờ chủ dự án duyệt

Không đổi số khách thấy. Dung sai "trùng" vẫn **0,1 mm**; nay đúng như tài liệu ghi ("lệch ≤ 0,1 mm"). Số kỹ thuật: góc / tỉ lệ
/ bulge so tới 6 số lẻ.

## 4. Quyết định tự chọn

| Tình huống | Đã chọn | Muốn đổi thì |
| --- | --- | --- |
| `Layout1` / `Layout2`… chỉ có khung nhìn (AutoCAD tự tạo khi khách bấm vào tab, không phân biệt được với khung khách tự đặt) | **luồng chính chọn 25/11:** không báo lỗi; ghi chú nêu tên tab và cách soát cả tab đó (`sheets`) | coi là tờ in luôn: bỏ điều kiện trong `DrawingCheckLogic.IsUnusedTab` (sẽ báo nhiễu ở hầu hết bản vẽ chỉ vẽ trong Model) |
| Hatch vẽ trước bản sửa này | hatch cũ không có dấu: vẫn bị báo nếu không liên kết | — |
| Kích thước chỉ có dấu cách | báo trong "kích thước bị gõ đè số" | tách nhóm riêng |
| Nét hình suy ra trùng nhau | không báo, không xóa | — |

## 5. Test

- Lõi: **1376 / 1376** xanh (thêm 9, gồm 1 test sau lần 2 của KFG-08). Trợ lý: **400 / 400** xanh.
- Build `net48`, `net8.0-windows`, `net10.0-windows` (xóa `obj` / `bin` giữa các lần): không lỗi, không cảnh báo.
- Không chạy CI riêng (UI không đổi).

## 6. Chưa kiểm được

- Chưa chạy trên AutoCAD thật:
  - `Layout.GetViewports()` với layout chưa mở bao giờ;
  - dấu `CLAUDECAD_PLAIN_HATCH` trên hatch;
  - kích thước " " in ra không có chữ.
- Thử tay: `docs/checklist-dot-g2.md` (D1, E…).

## 7. Lượt 2 (sau lượt soát lại 1 của kiểm tra độc lập, 26/11)

Gộp `full/nang-cap-g-tong-the` @ `20e3812` trước khi sửa.

| Mã | Lỗi | Đã sửa thế nào | Test |
| --- | --- | --- | --- |
| KFG-32 (nhẹ, phần còn lại của KFG-07) | `section_view` / `projection_view` cho đặt layer riêng (`layer` / `hatch_layer` / `behind_layer`). Trên layer riêng: hatch mặt cắt vẫn bị báo "mất liên kết", và nét vẫn được so trùng, nên `fix_drawing duplicates` có thể xóa nét của hình chiếu. | Hatch mặt cắt: gọi `MarkPlainHatch` sau `EvaluateHatch`. Nét: mọi nét hai lệnh này vẽ (qua `AddCurve`) được gắn XData `CLAUDECAD_VIEW`; lần so trùng bỏ qua nét có dấu này, dù nằm trên layer nào. | `Lines_and_hatches_of_a_view_on_a_layer_of_the_users_choice_are_marked` |
| KFG-34 (nhẹ) | So trùng chậm khi nhiều hình cùng ô: chỉ băm theo số đầu, nên các hình chung ô phải so từng cặp (20 000 đường cùng x mất 2,4 s). | Băm theo (x, y) của điểm đầu, xét 9 ô kề. Kết quả không đổi: một bản trùng không bao giờ cách quá một ô theo mỗi trục. Các test KFG-15 giữ nguyên và vẫn qua. | `Twenty_thousand_lines_of_one_x_are_checked_fast_and_give_the_same_answer` (dưới 1 s) |
| KFG-38 (nhẹ) | Test cũ còn dùng `ShapeKey` + `DuplicateIndices(IList<string>)` mà code không còn gọi; chú thích `<summary>` dính nhầm. | Chuyển test sang `DupItem`, thêm ca polyline vẽ ngược. Xóa hai hàm chết, sửa chú thích của `DupItem` và `TargetLayer…`. Ca polyline vẽ ngược còn lộ thêm một **lỗi thật**: bulge bị đổi dấu thành `-0` nên đường vẽ ngược không bao giờ khớp. Đã sửa trong `Exact` (-0 = 0). | `An_open_polyline_drawn_backwards_is_a_copy_through_DupItem`, `NangCapG2Tests.A_line_drawn_backwards…` (chuyển sang `DupItem`) |

Giới hạn: XData đi theo đối tượng khi chép. Nếu khách chép (COPY) một nét của hình chiếu rồi để đè lên chính nó, bản chép cũng
mang dấu và không bị báo trùng. Chỉ ảnh hưởng nét do hai lệnh hình chiếu / mặt cắt vẽ ra.

Test sau lượt 2: lõi **1412 / 1412**, trợ lý **407 / 407** (gồm test của hai thread kia sau khi gộp), build 3 target không lỗi, không cảnh báo. Không chạy CI riêng.

## 8. Lượt 3 (sau lượt soát lại 2, 26/11)

Gộp `full/nang-cap-g-tong-the` @ `fff7ffe` trước khi sửa.

| Mã | Lỗi | Đã sửa thế nào | Test |
| --- | --- | --- | --- |
| KFG-41 (nhẹ, có từ trước) | Đường gần thẳng đứng lệch rất ít không bị nhận là trùng. Chiều hai đầu được chọn bằng phép so x tuyệt đối, không có dung sai: A (0,0)→(0,1000) giữ nguyên, B (0,00001,0)→(0,1000) bị đảo. | Không chọn chiều nữa. Mỗi line / polyline hở mang cả hai cách đọc (`DupItem.Alt` / `AltGroup`: đầu → cuối và cuối → đầu, với polyline thì bulge dời chỗ và đổi dấu), và lần so tìm theo cả hai. Polyline kín giữ như cũ, một cách đọc. `LineNumbers` / `PolylineNumbers` được thay bằng `LineItem` / `PolylineItem`, test cũ chuyển theo, kết quả không đổi. | `A_nearly_vertical_line_meets_its_copy_whichever_way_each_was_drawn` |
| KFG-42 (nhẹ) | 20 000 đường chung một điểm đầu (nan quạt) vẫn phải so từng cặp, mất 1,9 s. | Băm thêm điểm giữa của dãy số (với line là điểm cuối), xét ô kề. Kết quả không đổi, vì một bản trùng không bao giờ cách quá một ô ở mỗi số. | `A_fan_of_twenty_thousand_lines_from_one_point_is_checked_fast` (dưới 1 s), cùng các test KFG-15 / KFG-34 |

Test sau lượt 3: lõi **1438 / 1438**, trợ lý **407 / 407**, build 3 target không lỗi, không cảnh báo. Không chạy CI riêng.
