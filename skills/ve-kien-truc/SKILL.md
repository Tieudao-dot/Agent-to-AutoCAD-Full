---
name: ve-kien-truc
description: Chuẩn vẽ kiến trúc của người dùng (bảng layer A-*, tường 220/110, quy trình mặt bằng trục → cột → tường → cửa → kích thước → chữ, chiều cao chữ và dim style theo tỷ lệ in, ký hiệu cửa đi/cửa sổ). Dùng skill này mỗi khi được yêu cầu vẽ hoặc sửa bản vẽ kiến trúc - mặt bằng, phòng, căn hộ, tường, cột, lưới trục, cửa đi, cửa sổ, cầu thang, ghi kích thước kiến trúc, ghi tên phòng/diện tích. Use for any architectural floor-plan task (walls, doors, windows, columns, grid axes, room labels, architectural dimensions).
plan: 2d
---

# Vẽ kiến trúc theo chuẩn

> Đây là khung chuẩn mẫu. Người dùng có thể sửa các bảng bên dưới cho đúng chuẩn công ty — luôn làm theo nội dung hiện có của file này, không dùng giá trị khác khi file đã quy định.

Quy ước chung: đơn vị mm, WCS. Tỷ lệ in mặc định **1:100** (nếu người dùng nói tỷ lệ khác thì nhân lại theo bảng mục 4). Kích thước phòng người dùng nói ("phòng 4x6m") hiểu là **kích thước theo trục tường** trừ khi họ nói "thông thủy".

## 1. Quy trình chuẩn bị (luôn làm trước khi vẽ)

1. Gọi `get_drawing_info` và `list_layers` (thêm `list_styles` nếu sẽ ghi chữ/kích thước) để biết bản vẽ đang có gì.
2. So với bảng layer ở mục 2: tạo **trong một `batch_execute`** mọi layer còn thiếu bằng `create_layer` đúng tên, màu, linetype, lineweight. Không đổi thuộc tính của layer đã có trừ khi người dùng yêu cầu.
3. Nếu chưa có: tạo text style `KT-ARIAL` (`create_text_style` font "Arial", width_factor 0.85) và dim style `KT-100` theo mục 4 (`create_dim_style`), rồi `set_current_styles`.
4. Xác định gốc vẽ: bản vẽ trắng → góc dưới-trái trục đầu tiên tại [0, 0]; bản vẽ đã có đối tượng → đặt bên phải extents hiện có, cách 5000, trừ khi người dùng chỉ vị trí.
5. Sau đó mới vẽ, mỗi nhóm đối tượng một `batch_execute`.

## 2. Bảng layer chuẩn

| Layer | Nội dung | Màu (ACI) | Linetype | Lineweight (mm) |
| --- | --- | --- | --- | --- |
| `A-AXIS` | đường trục | 1 (đỏ) | CENTER | 0.18 |
| `A-AXIS-TAG` | vòng tròn + chữ ký hiệu trục | 1 | CONTINUOUS | 0.25 |
| `A-COL` | cột | 7 | CONTINUOUS | 0.50 |
| `A-WALL` | tường (nét cắt) | 7 | CONTINUOUS | 0.50 |
| `A-WALL-PAT` | hatch tường | 8 | CONTINUOUS | 0.09 |
| `A-DOOR` | cửa đi (cánh + cung mở) | 3 (xanh lá) | CONTINUOUS | 0.25 |
| `A-WIND` | cửa sổ | 4 (cyan) | CONTINUOUS | 0.25 |
| `A-STAIR` | cầu thang | 6 | CONTINUOUS | 0.25 |
| `A-FURN` | nội thất, thiết bị | 8 | CONTINUOUS | 0.18 |
| `A-DIM` | kích thước | 2 (vàng) | CONTINUOUS | 0.18 |
| `A-TEXT` | tên phòng, ghi chú | 7 | CONTINUOUS | 0.25 |
| `A-HIDDEN` | nét khuất | 8 | HIDDEN | 0.18 |
| `A-SLAB` | đường bao sàn: polyline **kín** (polyline kín nằm trong là lỗ sàn); dùng khi dựng 3D từ mặt bằng | 8 | CONTINUOUS | 0.18 |

## 3. Tường, cột, trục

- **Tường 220** (tường bao, tường gạch đặc): hai nét cách nhau 220, đối xứng qua trục tường (mỗi bên 110). **Tường 110** (tường ngăn): hai nét cách nhau 110, đối xứng qua trục.
- Vẽ mỗi nét tường bằng `draw_polyline` (layer `A-WALL`) đi liên tục quanh phòng; chừa khoảng hở đúng bằng bề rộng lỗ cửa, đóng hai mép lỗ bằng `draw_line` ngắn (cùng layer).
- Hatch tường (khi người dùng muốn tô): `draw_hatch` pattern `ANSI31`, scale 15, layer `A-WALL-PAT`, biên là polyline kín của phần tường.
- Cột BTCT: `draw_rectangle` tâm tại giao trục, kích thước mặc định 220x220 (hoặc theo người dùng), layer `A-COL`, hatch `SOLID` màu 8.
- Trục: `draw_line` (không dùng xline để in gọn) vượt ra ngoài công trình 1500 mỗi đầu, layer `A-AXIS`. Ký hiệu trục: vòng tròn đường kính 800 (`draw_circle` radius 400, layer `A-AXIS-TAG`) ở đầu trục, chữ trong vòng cao 350, justify `middle_center`. Trục đứng đánh số 1, 2, 3… từ trái sang; trục ngang đánh chữ A, B, C… từ dưới lên.
- Kích thước trục: một chuỗi `add_dim_linear` + `add_dim_continue` giữa các trục, cộng một dim tổng (`add_dim_baseline` hoặc dim riêng) ở ngoài, layer `A-DIM`.

## 4. Chữ và kích thước theo tỷ lệ in

Chiều cao **trên giấy** cố định; chiều cao **trong model** = chiều cao giấy × mẫu số tỷ lệ.

| Loại | Trên giấy | 1:50 | 1:100 | 1:200 |
| --- | --- | --- | --- | --- |
| Tên phòng | 3.5 mm | 175 | 350 | 700 |
| Diện tích, ghi chú | 2.5 mm | 125 | 250 | 500 |
| Ký hiệu trục (chữ trong vòng) | 3.5 mm | 175 | 350 | 700 |
| Chữ kích thước | 2.5 mm | dim style scale 50 | scale 100 | scale 200 |

Dim style `KT-100` (thay số 100 theo tỷ lệ): `create_dim_style` với `{"name": "KT-100", "scale": 100, "text_height": 2.5, "arrow": "archtick", "arrow_size": 1.2, "extension_offset": 2, "extension_beyond": 1.5, "dim_line_extension": 1, "text_gap": 0.8, "decimals": 0, "text_style": "KT-ARIAL"}`.

- Dim cách mép tường ngoài 800 (đường dim thứ nhất – chi tiết), dim tổng cách thêm 600.
- Ghi đủ **bốn phía** với phòng/căn đơn lẻ; phía có cửa ghi chuỗi chi tiết (mép tường – mép lỗ cửa – mép lỗ cửa – mép tường).

## 5. Ký hiệu cửa

**Cửa đi một cánh** (rộng W, mặc định 900): lỗ cửa rộng W trong tường; cánh cửa là một đoạn thẳng dài W vẽ ở vị trí **mở 90°**, vuông góc với tường, gốc tại bản lề (mép trong của một bên lỗ); cung mở là `draw_arc` tâm tại bản lề, bán kính W, từ đầu cánh tới mép lỗ bên kia (cung 90°). Layer `A-DOOR`. Cửa mở vào trong phòng, bản lề đặt sát góc phòng gần hơn nếu người dùng không nói.

**Cửa sổ** (rộng W): lỗ trong tường như cửa đi; vẽ 3 nét song song trong bề dày tường (hai nét mép tường + một nét giữa), hoặc 4 nét chia đều với tường 220. Layer `A-WIND`.

Khi cùng một loại cửa dùng nhiều lần: vẽ một lần, `define_block` tên `CUA-DI-<W>` / `CUA-SO-<W>` với base point tại bản lề, rồi `insert_block` (xoay theo hướng tường).

## 6. Chữ trong phòng

- Tên phòng (IN HOA, ví dụ "PHÒNG NGỦ") ở tâm phòng, `add_text` justify `middle_center`, cao theo bảng mục 4, layer `A-TEXT`, style `KT-ARIAL`.
- Dòng diện tích ngay dưới, cách 1.5 × chiều cao chữ: `S = 21.85 m²` — diện tích **thông thủy** (đo bằng `measure_area` của polyline nét tường trong, hoặc tính (dài − dày tường) × (rộng − dày tường)), 2 chữ số thập phân.

## 7. Ví dụ hoàn chỉnh: phòng 4x6 m, tường 220, cửa đi 900 ở cạnh dài, ghi kích thước (tỷ lệ 1:100)

Trục tường là hình chữ nhật [0,0]–[4000,6000]; cạnh dài là cạnh đứng; đặt cửa ở cạnh phải (x = 4000), giữa cạnh: lỗ cửa y = 2550…3450. Sau quy trình chuẩn bị (layer, `KT-ARIAL`, `KT-100`):

```json
{"calls": [
  {"tool": "draw_polyline", "params": {"layer": "A-WALL", "vertices": [[4110,3450],[4110,6110],[-110,6110],[-110,-110],[4110,-110],[4110,2550]]}},
  {"tool": "draw_polyline", "params": {"layer": "A-WALL", "vertices": [[3890,3450],[3890,5890],[110,5890],[110,110],[3890,110],[3890,2550]]}},
  {"tool": "draw_line", "params": {"layer": "A-WALL", "start": [3890,2550], "end": [4110,2550]}},
  {"tool": "draw_line", "params": {"layer": "A-WALL", "start": [3890,3450], "end": [4110,3450]}},
  {"tool": "draw_line", "params": {"layer": "A-DOOR", "start": [3890,2550], "end": [2990,2550]}},
  {"tool": "draw_arc",  "params": {"layer": "A-DOOR", "center": [3890,2550], "radius": 900, "start_angle": 90, "end_angle": 180}},
  {"tool": "add_dim_linear", "params": {"layer": "A-DIM", "p1": [-110,-110], "p2": [4110,-110], "dim_line_point": [2000,-910]}},
  {"tool": "add_dim_linear", "params": {"layer": "A-DIM", "p1": [-110,6110], "p2": [4110,6110], "dim_line_point": [2000,6910]}},
  {"tool": "add_dim_linear", "params": {"layer": "A-DIM", "p1": [-110,-110], "p2": [-110,6110], "dim_line_point": [-910,3000]}},
  {"tool": "add_dim_linear", "params": {"layer": "A-DIM", "p1": [4110,-110], "p2": [4110,2550], "dim_line_point": [4910,1200]}},
  {"tool": "add_dim_linear", "params": {"layer": "A-DIM", "p1": [4110,2550], "p2": [4110,3450], "dim_line_point": [4910,3000]}},
  {"tool": "add_dim_linear", "params": {"layer": "A-DIM", "p1": [4110,3450], "p2": [4110,6110], "dim_line_point": [4910,4800]}},
  {"tool": "add_dim_linear", "params": {"layer": "A-DIM", "p1": [4110,-110], "p2": [4110,6110], "dim_line_point": [5510,3000]}},
  {"tool": "add_text", "params": {"layer": "A-TEXT", "style": "KT-ARIAL", "position": [2000,3100], "text": "PHÒNG", "height": 350, "justify": "middle_center"}},
  {"tool": "add_text", "params": {"layer": "A-TEXT", "style": "KT-ARIAL", "position": [2000,2550], "text": "S = 21.85 m²", "height": 250, "justify": "middle_center"}}
]}
```

Kết thúc bằng `zoom_extents` và một câu báo cáo: số đối tượng, các layer đã dùng, nhắc gõ U một lần để hoàn tác cả lượt.

## 8. Kiểm tra trước khi báo xong

- Mọi đối tượng nằm đúng layer trong bảng mục 2 (không vẽ gì trên layer `0`).
- Chữ và dim đúng chiều cao theo tỷ lệ in.
- Lỗ cửa đúng bề rộng, cánh cửa và cung khớp hai mép lỗ.
