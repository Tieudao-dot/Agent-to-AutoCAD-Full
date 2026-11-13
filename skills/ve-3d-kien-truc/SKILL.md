---
name: ve-3d-kien-truc
description: Chuẩn dựng mô hình 3D kiến trúc của người dùng (chiều cao tầng, dày sàn, cao cửa đi, cao bậu và cao cửa sổ, layer khối A-WALL-3D / A-SLAB-3D…, quy trình dựng 3D từ mặt bằng 2D bằng build_3d_from_plan rồi tự kiểm tra, mặt cắt / mặt đứng 2D từ mô hình). Dùng khi được yêu cầu dựng 3D, mô hình 3D, khối 3D công trình, dựng 3D từ mặt bằng, tường cao…, đổ sàn, cầu thang, mái, mặt cắt hay mặt đứng từ mô hình 3D. Use for building an architectural 3D model from a floor plan, storeys, slabs, 3D walls with openings, stairs, roofs, sections and elevations of the model.
plan: 3d
---

# Dựng 3D kiến trúc theo chuẩn

> Khung chuẩn mẫu, chỉ dùng ở máy gói 3D / Full (máy gói 2D không nạp skill này). Người dùng sửa các bảng dưới cho
> đúng chuẩn công ty; luôn làm theo nội dung hiện có của file này. Mặt bằng 2D theo quy ước của skill `ve-kien-truc`;
> phần `build_3d_from_plan` cần đọc được chép ở mục 0 (máy gói 3D không nạp `ve-kien-truc`).

Quy ước: đơn vị mm, WCS, Z hướng lên. Cốt ±0.000 (sàn tầng 1) ở z = 0. Người dùng nói mét thì đổi ra mm.

## 0. Mặt bằng 2D mà `build_3d_from_plan` đọc (quy ước của `ve-kien-truc`)

| Layer | Nội dung trên mặt bằng | Cách vẽ để được nhận |
| --- | --- | --- |
| `A-WALL` | tường (nét cắt) | **Hai nét mặt tường** song song, cách nhau đúng chiều dày: tường **220** (tường bao) và **110** (tường ngăn), đối xứng qua trục tường (mỗi bên 110 / 55). Nét đi liên tục quanh phòng bằng polyline; chừa khoảng hở đúng bề rộng lỗ cửa và đóng hai mép lỗ bằng đoạn ngắn cùng layer |
| `A-DOOR` | cửa đi | Cánh cửa: đoạn thẳng dài W (mặc định 900) vẽ ở vị trí mở 90°, gốc tại bản lề (mép một bên lỗ); cung mở 90° tâm bản lề, bán kính W |
| `A-WIND` | cửa sổ | 3 nét song song trong bề dày tường (hai nét mép + một nét giữa), hoặc 4 nét chia đều với tường 220, đặt trong lỗ tường |
| `A-SLAB` (hoặc `A-FLOR`) | đường bao sàn (có trong bảng layer của `ve-kien-truc`) | Polyline **kín**; polyline kín nằm trong polyline khác là lỗ sàn (cầu thang, giếng trời) |
| `A-COLS` (hoặc `A-COL`) | cột | Hình chữ nhật / tròn **kín** (cạnh ≤ 3000): dựng thành cột trên `A-COLS-3D`, cao bằng tường. Cột tên layer khác: `column_layers` / `column_handles`; `columns: "none"` để bỏ |

- Block cửa: tên `CUA-DI-<W>` / `CUA-SO-<W>` (ví dụ `CUA-DI-900`) được nhận là cửa đi / cửa sổ dù chèn trên layer nào.
- Tường vẽ bằng **một polyline có bề rộng** = chiều dày được đọc là tường theo đường tim; bề rộng nhỏ hơn `min_thickness`
  chỉ là nét in, đọc như nét mặt tường. Tường vẽ bằng đường trục: truyền `centerline_handles` + `thickness`.
- Mặt bằng của người dùng dùng tên layer khác (`TUONG`, `CUA`, `A-WALL-NEW`…): báo cáo `dry_run` có `layer_guess`
  (layer → loại đoán theo tên). **Không tự dựng theo đoán**: chạy thử với các layer đó, nói ánh xạ cho người dùng và
  hỏi bằng `request_confirmation`, **ghi đủ từng tên layer trên thẻ** (ví dụ "Dựng 3D với: tường = TUONG, cửa đi =
  CUA, cửa sổ = CUASO?"), dừng lượt; người dùng bấm Đồng ý mới dựng với `wall_layers` / `door_layers` /
  `window_layers` / `slab_layers` / `column_layers` (có `*`, ví dụ `KT_TUONG*`). Trợ lý từ chối lệnh dựng dùng layer
  người dùng chưa nêu / chưa đồng ý; layer đã đồng ý thì các lần dựng sau trong cùng cuộc chat không hỏi lại.

Máy gói 3D cần vẽ mặt bằng để dựng (chỉ có đường cơ bản, không có kích thước / chữ): phòng 4 × 6 m theo trục
[0,0]–[4000,6000], tường 220, cửa đi 900 ở cạnh phải (lỗ y = 2550…3450), một `batch_execute`:

```json
{"calls": [
  {"tool": "draw_polyline", "params": {"layer": "A-WALL", "vertices": [[4110,3450],[4110,6110],[-110,6110],[-110,-110],[4110,-110],[4110,2550]]}},
  {"tool": "draw_polyline", "params": {"layer": "A-WALL", "vertices": [[3890,3450],[3890,5890],[110,5890],[110,110],[3890,110],[3890,2550]]}},
  {"tool": "draw_line", "params": {"layer": "A-WALL", "start": [3890,2550], "end": [4110,2550]}},
  {"tool": "draw_line", "params": {"layer": "A-WALL", "start": [3890,3450], "end": [4110,3450]}},
  {"tool": "draw_line", "params": {"layer": "A-DOOR", "start": [3890,2550], "end": [2990,2550]}},
  {"tool": "draw_arc",  "params": {"layer": "A-DOOR", "center": [3890,2550], "radius": 900, "start_angle": 90, "end_angle": 180}}
]}
```

## 1. Kích thước mặc định

Dùng khi người dùng và ghi chú dự án không nói khác. Báo lại giá trị đã dùng trong câu trả lời. Sàn của mỗi tầng
nằm từ `base_z − slab_thickness` tới `base_z`, tức là **ngay trên đỉnh tường tầng dưới**: vì vậy tường cao = cao tầng −
dày sàn, sàn tầng trên không chồng vào tường tầng dưới. Người dùng nói rõ "tường cao 3,3 m" thì dùng đúng số đó.

| Thông số | Giá trị | Tham số `build_3d_from_plan` |
| --- | --- | --- |
| Chiều cao tầng (sàn đến sàn) | 3300 | `base_z` (mm) tầng n = (n − 1) × 3300 |
| Dày sàn | 120 (nhà ở), 150 (khung BTCT nhịp lớn) | `slab_thickness` |
| Chiều cao tường (từ mặt sàn tầng đó tới đáy sàn tầng trên) | **cao tầng − dày sàn**: 3300 − 120 = 3180 | `wall_height` |
| Cao cửa đi (thông thủy) | 2200 (cửa chính 2400) | `door_height` |
| Cao bậu cửa sổ | 900 (WC: 1500) | `window_sill` |
| Cao cửa sổ | 1500 (WC: 600) | `window_height` |
| Tường nhận diện | dày 100–330 | `min_thickness` 80, `max_thickness` 400 |

## 2. Layer khối 3D

| Layer | Nội dung | Màu (ACI) |
| --- | --- | --- |
| `A-WALL-3D` | khối tường (đã chừa lỗ cửa) | 7 |
| `A-SLAB-3D` | sàn | 8 |
| `A-COLS-3D` | cột (`build_3d_from_plan` từ `A-COLS` / `A-COL`, hoặc `draw_solid`) | 1 |
| `A-RAIL-3D` | lan can (`build_railing`) | 3 |
| `A-BEAM-3D` | dầm (`build_beam`) | 4 |
| `A-STAIR-3D` | cầu thang (`build_stair`; lan can kèm theo ở `A-RAIL-3D`) | 6 |
| `A-ROOF-3D` | mái (`build_roof`) | 30 |
| `A-SECT` | đường bao vết cắt của mặt cắt 2D | 7 |
| `A-SECT-PAT` | hatch vết cắt | 8 |
| `A-SECT-BACK` | nét nhìn thấy sau mặt cắt | 9 |
| `A-ELEV` | mặt đứng 2D | 7 |

`build_3d_from_plan`, `section_view`, `projection_view` tự tạo layer của mình khi thiếu. Cột, cầu thang, mái: tạo
layer bằng `create_layer` (đúng màu ở bảng) trước khi dựng. Không dựng gì trên layer `0`. Lệnh dựng khối chỉ nhận
`layer`: khối lấy màu của layer; muốn màu / nét riêng cho vài khối thì `set_entity_props` với handle vừa dựng.

## 3. Quy trình chuẩn bị và dựng 3D từ mặt bằng

1. `get_drawing_info` + `list_layers`: kiểm mặt bằng có layer `A-WALL` / `A-DOOR` / `A-WIND` (hoặc tên khác của người
   dùng → truyền `wall_layers`, `door_layers`, `window_layers`). Bản vẽ có nhiều tầng / nhiều công trình: dựng từng
   phần với `area_min` / `area_max`.
2. **Chạy thử:** `build_3d_from_plan` với `dry_run: true` và các chiều cao ở mục 1. Đọc báo cáo:
   - `walls_count`, `thickness_summary` (ví dụ tường 220: 42 m) có đúng mặt bằng không;
   - `openings` (cửa đi / cửa sổ nhận được), `open_gaps` (khe hở trong tường không có ký hiệu cửa);
   - `skipped` (handle + lý do: tường cong, block, nét đơn, cửa không nằm trên tường…).
3. **Hỏi người dùng** (một câu ngắn, liên kết handle `[…](handle:…)`): phần bị bỏ qua có cần dựng không, khe hở là cửa
   đi, cửa sổ hay lối thông. Không tự đoán.
4. **Dựng:** gọi lại không có `dry_run`, thêm `openings` cho cửa người dùng vừa xác nhận
   (`{"kind": "door", "at": [x, y]}`; cửa sổ thêm `sill`, `height` nếu khác mặc định). Sàn: có đường bao kín trên
   `A-SLAB` / `A-FLOR` thì tự đổ; không có thì hỏi, hoặc dùng `slab: "bbox"` nếu người dùng đồng ý sàn phủ hộp bao tường.
5. Tầng trên: cùng mặt bằng (hoặc mặt bằng tầng đó) với `base_z` = cao độ sàn tầng (mm, ví dụ tầng 2: 3300) và cùng
   `wall_height` = cao tầng − dày sàn. Tầng dưới chỉ chạm tầng mới thì không bị coi là dựng trùng. Báo lỗi "already
   built" nghĩa là **đúng tầng này** (cùng cao độ) đã dựng trước đó: hỏi người dùng muốn dựng tầng khác (`base_z` khác)
   hay dựng thêm (`on_existing: "add"`). **Không bao giờ** gợi ý xóa tầng dưới hay khối của tầng khác; chỉ xóa đúng các
   khối lỗi liệt kê khi người dùng tự yêu cầu (có thẻ xác nhận). Kết quả có ghi chú "slab … cuts … into … the storey
   below": tường tầng dưới cao quá, báo người dùng, không xóa gì.
6. Tường cong, tường trong block: dựng riêng bằng `extrude` (biên dạng kín) hoặc `sweep`, layer `A-WALL-3D`.
7. Cửa người dùng nói thêm cho một khe hở (`open_gaps`): `openings` với `at` ở giữa khe; cửa chỉ định ở góc / chỗ
   hai tường gặp nhau bị từ chối: dời điểm vào giữa đoạn tường.

Ví dụ phòng 4 × 6 m ở mục 0 (tường 220, cửa đi 900), tầng cao 3,3 m, sàn 120 (tường 3300 − 120 = 3180):

```json
{"tool": "build_3d_from_plan", "params": {"wall_height": 3180, "door_height": 2200, "slab_thickness": 120, "slab": "bbox"}}
```

Tầng 2 cùng mặt bằng: thêm `"base_z": 3300`.

Nhiều tầng một lần ("3 tầng, mỗi tầng cao 3,3 m"): `{"storeys": 3, "storey_height": 3300, "slab_thickness": 120}`
(tường mỗi tầng tự = 3300 − 120 = 3180, sàn tầng trên nằm trên đỉnh tường tầng dưới; `"roof_slab": true` thêm sàn mái
khi người dùng muốn). Kết quả có `levels` (mỗi tầng: `base_z_mm`, `wall_solid`, `columns`, `slabs`) và
`total_volume_m3` (tường / cột / sàn của mọi tầng); đã có sẵn một số tầng thì chỉ dựng các tầng còn thiếu (`base_z`
của tầng thiếu đầu tiên + số tầng còn lại).

Lan can theo một polyline (ban công, hành lang): `build_railing` với `path_handle`; mặc định cao **1100**, tay vịn
50 × 50, trụ 50 × 50 cách ≤ 1500, song 20 × 20 khe ≤ 100 (`infill: "panel"` = tấm kính 20, `"none"` = chỉ trụ + tay
vịn); `base_z` = cao độ sàn (mm).

Tường cong: `build_3d_from_plan` đọc hai cung đồng tâm cách nhau đúng bề dày (cung, đường tròn, đoạn cung của
polyline), polyline có bề rộng có đoạn cung, hoặc `centerline_handles` là cung + `thickness`. Cửa trên tường cong: khe
có ký hiệu cửa, ký hiệu cửa sổ đè lên tường, hoặc `openings` theo điểm (cần `width` khi tường liền). Kết quả có
`curved_walls`.

Dầm: `build_beam` theo đường (`path_handles`: line / cung / polyline, mỗi đường một dầm), mặc định 220 × 400, đỉnh dầm
ở `top_z`; dầm dưới sàn: `top_z` = cao độ mặt sàn + `slab_thickness` (vd tầng 2: `top_z` 3300, `slab_thickness` 120).

Cầu thang: `build_stair`, `type` `straight` (1 vế) / `l` (chữ L) / `u` (chữ U), `height` = cao tầng (sàn tới sàn, bắt
buộc). Mặc định (khách nói số thì theo số của khách): cao bậc 160 (chia đều thành số bậc nguyên gần nhất, vd 3300 → 21
bậc × 157,1), rộng bậc 280, vế rộng 1000, chiếu nghỉ rộng bằng vế (L / U, ở giữa chiều cao), bản thang 150, khe giữa
2 vế chữ U 100. Đặt theo `start` (giữa mép dưới bậc đầu) + `direction` (độ, hướng đi lên vế 1) + `turn` (trái / phải),
hoặc theo ô cầu thang (`well_handle` polyline kín): không có `start` thì tự đặt vừa ô; không vừa ô → lỗi kèm kích thước
cần, báo khách (đổi kiểu L / U, vế hẹp hơn, bậc ngắn hơn), không tự dựng ra ngoài. `base_z` = cao độ sàn dưới. Lan
can: `railing` `left` / `right` / `both` (tay vịn 900 + trụ dọc các vế; chiếu nghỉ chưa có lan can → `build_railing`).
Báo lại số bậc và cao bậc thực (`risers`, `riser_mm`).

Mái: `build_roof`, `type` `flat` (mái bằng, dày 150, mặc định không đua) / `gable` (2 dốc, chỉ nhà chữ nhật; nóc theo
cạnh dài, `ridge` `short` để đổi) / `hip` (4 dốc, đường bao lồi). Mái dốc mặc định 30°, bản mái 150, đua 600. Đường
bao = mặt ngoài tường: `outline_handle` (polyline kín), `outline_points`, hoặc `from_walls: true` (hình chữ nhật bao
các khối tường). Mái đặt trên đỉnh tường: mặc định đỉnh cao nhất của khối tường `A-WALL-3D` dưới đường bao (tầng trên
cùng), hoặc `wall_top_z`. Nhà chữ L / T: 4 dốc và 2 dốc báo lỗi → chia đường bao thành các hình chữ nhật rồi dựng
từng mái, hoặc mái bằng. Đầu hồi mái 2 dốc để hở (tường hồi: `extrude` / `presspull`). Báo `top_z_mm`,
`roof_area_m2` (diện tích mặt mái nghiêng, để tính tôn / ngói).

Khối lượng: `volume_schedule` (theo layer; cột lồng trong tường / dầm chỉ tính một lần, vào cột). Va chạm:
`check_interference`.

## 4. Tự kiểm tra trước khi báo xong

- Mỗi lệnh dựng khối (`draw_solid`, `extrude`, `revolve`, `sweep`, `loft`…) trả `volume_m3`, `min`, `max`: so ngay với
  kích thước định dựng (tường 4000 × 220 × 3300 = 2,904 m³).

- **Bằng số** (`wall_solid` của kết quả):
  - `volume_m3` ≈ Σ (dài tường theo trục × dày × cao) − lỗ cửa; ví dụ trên: (4220 × 6220 − 3780 × 5780 − 220 × 900) ×
    3180 + 220 × 900 × 980 ≈ **13,56 m³** (tường 3300: 14,08 m³);
  - `pieces_volume_m3` lớn hơn một chút (phần chồng ở góc tường): bình thường;
  - `min` / `max` khớp `plan_min` / `plan_max` và z từ `base_z` tới `base_z + wall_height`;
  - sàn: `area_m2` × dày = `volume_m3`.
- **Bằng hình:** `set_view_3d` `sw_iso` (handles vừa dựng), `set_visual_style` `conceptual`, `capture_view`. Soát: đủ
  tường, lỗ cửa đúng chỗ, không có mảng tường thừa ở khe hở.
- Báo cáo: số tường, số cửa, thể tích tường / sàn (m³), phần bỏ qua, các chiều cao đã dùng; nhắc gõ U một lần để hoàn
  tác cả lượt.

## 5. Chọn lệnh dựng cho từng loại cấu kiện

| Cấu kiện | Lệnh | Ghi chú |
| --- | --- | --- |
| Tường, sàn, móng từ đường bao | `extrude` | Vẽ đường bao kín trên XY (hoặc `profile_points`) rồi đùn theo chiều cao; `taper_angle` cho móng vát |
| Cột tròn có đầu cột, chậu, bể tròn, ống ngắn | `revolve` | Biên dạng nửa mặt cắt vẽ cạnh trục, trục không cắt biên dạng |
| Tay vịn, ống, gờ chỉ, máng xối, thép uốn | `sweep` | Tiết diện kín + đường dẫn; tiết diện rộng hơn bán kính cong thì AutoCAD từ chối |
| Móng vuông lên cột tròn, ống khói thon, thân tàu | `loft` | Các tiết diện kín theo thứ tự từ dưới lên |
| Cột theo mặt bằng | `build_3d_from_plan` | Hình kín trên `A-COLS` / `A-COL`, cao bằng tường. Tường đã dựng từ trước, chỉ thêm cột: `extrude` các hình cột (`profile_handles`) lên chiều cao tường, layer `A-COLS-3D` |
| Dầm theo trục | `build_beam` | Thẳng hoặc cong, góc tự khép |
| Cầu thang 1 vế / chữ L / chữ U | `build_stair` | Chia bậc theo cao tầng; bậc lẻ, thang xoắn: `draw_solid` `box` mỗi bậc hoặc `extrude` biên dạng răng cưa |
| Mái bằng / 2 dốc / 4 dốc | `build_roof` | Trên đỉnh tường tầng trên cùng; nhà chữ L / T chia thành các hình chữ nhật |
| Cột vuông | `draw_solid` `box` | |
| Lan can | `build_railing` | Theo polyline, mặc định 1100 |
| Dốc, mái dốc một phía | `draw_solid` `wedge` | Mặt cao ở phía `corner`, `rotation` để đổi hướng dốc |

## 6. Mặt cắt và mặt đứng 2D từ mô hình

- **Mặt cắt đứng:** `section_view` với `line` = đường cắt trên mặt bằng (đứng bên trái đường cắt nhìn sang phải;
  `flip` để nhìn ngược lại), `show_behind: true` để có nét nhìn thấy phía sau, `hatch` `ANSI31` (tường gạch) hoặc
  `AR-CONC` (bê tông), `hatch_scale` 10–20 cho in 1:100.
- **Mặt bằng cắt ở cao 1 m:** `section_view` với `point` [x, y, base_z + 1000], `plane` `xy`.
- **Mặt đứng:** `projection_view` `view` `front` / `back` / `left` / `right`.
- Kết quả là nét 2D đặt cạnh mô hình (layer ở mục 2), không đè lên mô hình; nhiều hình thì đặt bằng `position` thành
  hàng, cách nhau 2000. Kiểm `cut_area_m2` (tường 220 cắt ngang cao 3300 → 0,726 m² mỗi tường) và nhìn lại bằng
  `capture_view` `top` với handles của kết quả. Đặt nhiều hình: kết quả trả `size` và `frame` (điểm mô hình → điểm trên
  bản vẽ) để căn hình sau theo hình trước.
- Máy gói 3D không có lệnh ghi kích thước / chữ: chỉ dựng hình; muốn ghi kích thước thì cần gói Full.
