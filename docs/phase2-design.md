# Phase 2 — quyết định thiết kế

Spec §11 chỉ liệt kê phạm vi phase 2 (layout/paper space, viewport, in PDF, xref, dynamic block nâng cao, trim/extend hình học chính xác). Thiết kế dưới đây giữ nguyên phong cách v1: tool khai báo ở sidecar (zod + mô tả tiếng Anh), thực thi trong plugin qua cùng giao thức IPC, cùng mã lỗi, cùng quy tắc transaction / undo / xác nhận.

## Tool mới (17)

| Nhóm | Tool | Ghi chú |
| --- | --- | --- |
| Layout | `list_layouts`, `create_layout`, `set_current_layout`, `delete_layout` | dùng `LayoutManager`; `create_layout` có `copy_from`, `device`, `media` |
| Viewport | `add_viewport`, `set_viewport` | vị trí trên tờ giấy bằng mm; `scale` dạng `"1:100"`; `view_center` là điểm model; khóa viewport; VP freeze/thaw layer |
| In | `plot_pdf`, `list_plot_media` | `DWG To PDF.pc3`, mặc định A3 full bleed + `monochrome.ctb`, `scale` "fit" hoặc "1:100", area layout/extents/display/window |
| Xref | `attach_xref`, `list_xrefs`, `reload_xref`, `detach_xref`, `bind_xref` | overlay mặc định; trạng thái từ `GetHostDwgXrefGraph` |
| Hình học | `trim_entities`, `extend_entities` | xem bên dưới |
| Dynamic block | `get_dynamic_props`, `reset_dynamic_block` + tham số `dynamic_props` của `insert_block` | đặt thuộc tính ngay khi chèn; liệt kê allowed values (visibility, lookup) |

Thêm tham số chung `layout` cho mọi tool vẽ/ghi chú/chèn block (`Acad.Add` → `ToolContext.SpaceFor`) để vẽ khung tên, ghi chú trên layout; và cho bộ lọc `list_entities` / `count_entities`.

## Trim / extend chính xác

- **trim_entities**: với mỗi curve, lấy giao điểm với các cạnh cắt bằng `Curve.IntersectWith(..., Intersect.OnBothOperands, ...)`, đổi sang tham số (`GetParameterAtPoint`), tách bằng `GetSplitCurves`, bỏ đúng **mảnh gần điểm pick nhất** (giống thao tác pick của lệnh TRIM), các mảnh còn lại được thêm vào cùng không gian với thuộc tính gốc, curve cũ bị xóa → handle mới được trả về. Đường kín (circle, polyline kín) cần ≥ 2 giao điểm.
- **extend_entities**: giao điểm với biên tính bằng `Intersect.ExtendThis`; chọn điểm **gần đầu cần kéo nhất mà nằm ngoài curve hiện tại và gần đầu đó hơn đầu kia** (`Geometry.ExtensionTarget`, có unit test), rồi `Curve.Extend(extendStart, point)`. Đầu cần kéo: `start` / `end` / `both`, hoặc `auto` (theo pick point, nếu không có thì chọn đầu cần kéo ngắn hơn).
- Cả hai đều thuộc chốt xác nhận khi ảnh hưởng > 20 đối tượng.

## In PDF

- Chạy foreground: tạm đặt `BACKGROUNDPLOT = 0`, chuyển tới layout cần in nếu khác layout hiện hành, rồi trả lại như cũ trong `finally`.
- `PlotSettings` copy từ layout + `PlotSettingsValidator` (device, media, plot type, scale, centered, style sheet, rotation) → `PlotInfoValidator` (MatchEnabled) → `PlotFactory.CreatePublishEngine()` một trang.
- Chỉ ghi file PDF người dùng yêu cầu; mặc định cạnh file DWG (hoặc Documents nếu bản vẽ chưa lưu).
- Không chạy trong `batch_execute`, không mở undo group.

## Những gì chỉ kiểm chứng được trên AutoCAD thật

Toàn bộ API phase 2 (LayoutManager, Viewport.On, PlotEngine, xref graph, IntersectWith/GetSplitCurves/Extend, dynamic block properties) mới được kiểm chứng ở mức **biên dịch** với reference AutoCAD 2022. Xem mục "Phase 2" trong `docs/test-log.md`.
