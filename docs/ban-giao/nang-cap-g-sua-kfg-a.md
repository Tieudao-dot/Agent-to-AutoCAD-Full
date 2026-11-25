# Bàn giao: sửa lỗi kiểm tra độc lập đợt F + G, phần G2 (soát / sửa bản vẽ) — 25/11/2026

Thread `session_01D56HmFAo4dZ7c8sbeNB9eh` (người viết G2). Nhánh `full/nang-cap-g-sua-kfg-a`, tách từ `full/nang-cap-g-tong-the`
@ `881b12e`. Báo cáo lỗi: `docs/ban-giao/kiem-tra-doc-lap-dot-fg.md` (nhánh luồng chính). Chưa chạy CI riêng: thread giữ nhánh
tích hợp chạy CI sau khi gộp.

## 1. Từng mục

| Mã | Lỗi | Đã sửa thế nào | Test |
| --- | --- | --- | --- |
| KFG-07 (vừa) | Bộ hồ sơ có hatch mặt cắt (A-SECT-PAT), và `draw_hatch` vẽ hatch theo điểm / điểm bấm không giữ biên: các hatch này đều bị báo "hatch mất liên kết". | Bỏ qua hatch trên layer hình suy ra từ khối 3D (`Util2DLogic.IsDerivedViewLayer`). `draw_hatch` đánh dấu hatch nó vẽ không có biên (Xrecord `CLAUDECAD_PLAIN_HATCH`, chỉ khi hatch không liên kết); lần soát bỏ qua các hatch có dấu này. Hatch có biên mà mất biên vẫn bị báo. | `Hatches_of_the_views_and_hatches_drawn_without_boundary_are_not_unassociated` |
| KFG-08 (vừa) | Layout tên mặc định (`Layout2`…) có khung nhìn khách tự đặt bị bỏ qua hẳn. | Chỉ bỏ qua layout tên mặc định khi không có gì: không vật nào, không khung nhìn nào ngoài khung của tờ. "Tờ in không có khung tên" tính theo cùng điều kiện. Ghi chú và checklist G2-D1 viết lại cho khớp. | `A_default_layout_with_a_viewport_of_the_user_is_a_sheet` |
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
| `Layout1` mặc định có khung nhìn AutoCAD tự tạo | nay **bị kiểm** như tờ in (theo hướng sửa KFG-08), nên có thể báo "chưa khung tên / khung nhìn chưa khóa" cho tab khách chưa dùng | không phân biệt được khung nhìn tự tạo với khung khách đặt |
| Hatch vẽ trước bản sửa này | hatch cũ không có dấu: vẫn bị báo nếu không liên kết | — |
| Kích thước chỉ có dấu cách | báo trong "kích thước bị gõ đè số" | tách nhóm riêng |
| Nét hình suy ra trùng nhau | không báo, không xóa | — |

## 5. Test

- Lõi: **1375 / 1375** xanh (thêm 8). Trợ lý: **400 / 400** xanh.
- Build `net48`, `net8.0-windows`, `net10.0-windows` (xóa `obj` / `bin` giữa các lần): không lỗi, không cảnh báo.
- Không chạy CI riêng (UI không đổi).

## 6. Chưa kiểm được

- Chưa chạy trên AutoCAD thật:
  - `Layout.GetViewports()` với layout chưa mở bao giờ;
  - dấu `CLAUDECAD_PLAIN_HATCH` trên hatch;
  - kích thước " " in ra không có chữ.
- Thử tay: `docs/checklist-dot-g2.md` (D1, E…).
