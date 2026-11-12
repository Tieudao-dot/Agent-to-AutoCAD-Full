---
name: ve-3d-kien-truc
description: Chuẩn dựng mô hình 3D kiến trúc của người dùng (chiều cao tầng, dày sàn, cao cửa đi, cao bậu và cao cửa sổ, layer khối A-WALL-3D / A-SLAB-3D…, quy trình dựng 3D từ mặt bằng 2D bằng build_3d_from_plan rồi tự kiểm tra, mặt cắt / mặt đứng 2D từ mô hình). Dùng khi được yêu cầu dựng 3D, mô hình 3D, khối 3D công trình, dựng 3D từ mặt bằng, tường cao…, đổ sàn, mặt cắt hay mặt đứng từ mô hình 3D. Use for building an architectural 3D model from a floor plan, storeys, slabs, 3D walls with openings, sections and elevations of the model.
plan: 3d
---

# Dựng 3D kiến trúc theo chuẩn

> Khung chuẩn mẫu, chỉ dùng ở máy gói 3D / Full (máy gói 2D không nạp skill này). Người dùng sửa các bảng dưới cho
> đúng chuẩn công ty; luôn làm theo nội dung hiện có của file này. Mặt bằng 2D theo skill `ve-kien-truc` (layer
> `A-WALL`, `A-DOOR`, `A-WIND`).

Quy ước: đơn vị mm, WCS, Z hướng lên. Cốt ±0.000 (sàn tầng 1) ở z = 0. Người dùng nói mét thì đổi ra mm.

## 1. Kích thước mặc định

Dùng khi người dùng và ghi chú dự án không nói khác. Báo lại giá trị đã dùng trong câu trả lời.

| Thông số | Giá trị | Tham số `build_3d_from_plan` |
| --- | --- | --- |
| Chiều cao tầng (sàn đến sàn) | 3300 | `base_z` tầng n = (n − 1) × 3300 |
| Chiều cao tường (tính từ mặt sàn tầng đó) | 3300 | `wall_height` |
| Dày sàn | 120 (nhà ở), 150 (khung BTCT nhịp lớn) | `slab_thickness` |
| Cao cửa đi (thông thủy) | 2200 (cửa chính 2400) | `door_height` |
| Cao bậu cửa sổ | 900 (WC: 1500) | `window_sill` |
| Cao cửa sổ | 1500 (WC: 600) | `window_height` |
| Tường nhận diện | dày 100–330 | `min_thickness` 80, `max_thickness` 400 |

## 2. Layer khối 3D

| Layer | Nội dung | Màu (ACI) |
| --- | --- | --- |
| `A-WALL-3D` | khối tường (đã chừa lỗ cửa) | 7 |
| `A-SLAB-3D` | sàn | 8 |
| `A-COLS-3D` | cột (khi dựng thêm bằng `draw_box`) | 1 |
| `A-STAIR-3D` | cầu thang | 6 |
| `A-ROOF-3D` | mái | 30 |
| `A-SECT` | đường bao vết cắt của mặt cắt 2D | 7 |
| `A-SECT-PAT` | hatch vết cắt | 8 |
| `A-SECT-BACK` | nét nhìn thấy sau mặt cắt | 9 |
| `A-ELEV` | mặt đứng 2D | 7 |

`build_3d_from_plan`, `section_view`, `projection_view` tự tạo layer của mình khi thiếu. Cột, cầu thang, mái: tạo
layer bằng `create_layer` trước khi dựng. Không dựng gì trên layer `0`.

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
5. Tầng trên: cùng mặt bằng (hoặc mặt bằng tầng đó) với `base_z` = cao độ sàn tầng. Báo lỗi "already built" nghĩa là
   tầng này đã dựng: hỏi người dùng xóa khối cũ (cần xác nhận) hay dựng thêm (`on_existing: "add"`).
6. Tường cong, tường trong block: dựng riêng bằng `extrude` (biên dạng kín) hoặc `sweep`, layer `A-WALL-3D`.

Ví dụ phòng 4 × 6 m, tường 220, cửa đi 900 của skill `ve-kien-truc`, tường cao 3,3 m:

```json
{"tool": "build_3d_from_plan", "params": {"wall_height": 3300, "door_height": 2200, "slab_thickness": 120, "slab": "bbox"}}
```

## 4. Tự kiểm tra trước khi báo xong

- **Bằng số** (`wall_solid` của kết quả):
  - `volume_m3` ≈ Σ (dài tường theo trục × dày × cao) − lỗ cửa; ví dụ trên: (4220 × 6220 − 3780 × 5780 − 220 × 900) ×
    3300 + 220 × 900 × 1100 ≈ **14,08 m³**;
  - `pieces_volume_m3` lớn hơn một chút (phần chồng ở góc tường): bình thường;
  - `min` / `max` khớp `plan_min` / `plan_max` và z từ `base_z` tới `base_z + wall_height`;
  - sàn: `area_m2` × dày = `volume_m3`.
- **Bằng hình:** `set_view_3d` `sw_iso` (handles vừa dựng), `set_visual_style` `conceptual`, `capture_view`. Soát: đủ
  tường, lỗ cửa đúng chỗ, không có mảng tường thừa ở khe hở.
- Báo cáo: số tường, số cửa, thể tích tường / sàn (m³), phần bỏ qua, các chiều cao đã dùng; nhắc gõ U một lần để hoàn
  tác cả lượt.

## 5. Mặt cắt và mặt đứng 2D từ mô hình

- **Mặt cắt đứng:** `section_view` với `line` = đường cắt trên mặt bằng (đứng bên trái đường cắt nhìn sang phải;
  `flip` để nhìn ngược lại), `show_behind: true` để có nét nhìn thấy phía sau, `hatch` `ANSI31` (tường gạch) hoặc
  `AR-CONC` (bê tông), `hatch_scale` 10–20 cho in 1:100.
- **Mặt bằng cắt ở cao 1 m:** `section_view` với `point` [x, y, base_z + 1000], `plane` `xy`.
- **Mặt đứng:** `projection_view` `view` `front` / `back` / `left` / `right`.
- Kết quả là nét 2D đặt cạnh mô hình (layer ở mục 2), không đè lên mô hình; nhiều hình thì đặt bằng `position` thành
  hàng, cách nhau 2000. Kiểm `cut_area_m2` (tường 220 cắt ngang cao 3300 → 0,726 m² mỗi tường) và nhìn lại bằng
  `capture_view` `top` với handles của kết quả.
- Máy gói 3D không có lệnh ghi kích thước / chữ: chỉ dựng hình; muốn ghi kích thước thì cần gói Full.
