---
name: ve-ha-tang
description: Chuẩn vẽ hạ tầng / quy hoạch của người dùng - layer tim đường, mép đường, vỉa hè, bó vỉa, cống, ranh giới; quy ước ghi cao độ và lý trình (Km0+125.50); vẽ tuyến bằng polyline qua điểm tọa độ thực (VN-2000, đổi trục X/Y); bảng tọa độ bằng add_table. Dùng khi được yêu cầu vẽ đường giao thông, tuyến đường, mặt cắt ngang, vỉa hè, cống thoát nước, hố ga, ranh giới lô đất, mốc tọa độ, cao độ, lý trình, quy hoạch. Use for roads, alignments, chainage, levels, drainage networks, plot boundaries and coordinate tables.
---

# Vẽ hạ tầng / quy hoạch theo chuẩn

> Khung chuẩn mẫu — người dùng điền chuẩn riêng. Luôn theo đúng nội dung hiện có của file này.

Quy ước hệ thống vẫn là **mm**; tọa độ thực tính bằng **mét** phải nhân 1000. Tỷ lệ in mặc định 1:500 cho mặt bằng tuyến.

## 1. Quy trình chuẩn bị (luôn làm trước khi vẽ)

1. Gọi `get_drawing_info` và `list_layers`. Xem `extents` để biết bản vẽ đang dùng tọa độ thực (số rất lớn) hay tọa độ cục bộ.
2. Tạo trong **một `batch_execute`** các layer còn thiếu theo bảng mục 2.
3. **Gốc tọa độ**: tọa độ VN-2000 cỡ hàng trăm nghìn – hàng triệu mét (×1000 thành 10⁸–10⁹ mm). Nếu bản vẽ trắng, hỏi người dùng một câu: vẽ theo tọa độ thực hay trừ một điểm gốc (ví dụ gốc = điểm đầu tuyến)? Nếu dùng gốc cục bộ, ghi một dòng `add_text` ở góc bản vẽ: "Gốc tọa độ: X = …, Y = … (VN-2000)".
4. **Đổi trục**: trong VN-2000, X là hướng Bắc, Y là hướng Đông. Trong AutoCAD: x_cad = Y × 1000, y_cad = X × 1000 (trừ gốc nếu có). Luôn nhắc lại quy ước này trong câu trả lời đầu tiên khi nhận bảng tọa độ.

## 2. Bảng layer chuẩn

| Layer | Nội dung | Màu | Linetype | Lineweight |
| --- | --- | --- | --- | --- |
| `C-ROAD-CL` | tim đường | 1 (đỏ) | CENTER | 0.25 |
| `C-ROAD-EDGE` | mép mặt đường | 7 | CONTINUOUS | 0.50 |
| `C-CURB` | bó vỉa | 7 | CONTINUOUS | 0.35 |
| `C-SIDEWALK` | mép vỉa hè, hatch vỉa hè | 8 | CONTINUOUS | 0.25 |
| `C-DRAIN` | cống, hố ga | 5 | DASHED | 0.35 |
| `C-BOUND` | ranh giới lô / chỉ giới | 6 | PHANTOM | 0.50 |
| `C-COORD` | mốc tọa độ, bảng tọa độ | 3 | CONTINUOUS | 0.18 |
| `C-STATION` | vạch và chữ lý trình | 2 | CONTINUOUS | 0.18 |
| `C-LEVEL` | ký hiệu và chữ cao độ | 2 | CONTINUOUS | 0.18 |
| `C-TEXT` | ghi chú chung | 7 | CONTINUOUS | 0.18 |

## 3. Tuyến đường

- Tim tuyến: `draw_polyline` qua các điểm tọa độ (đã đổi trục, ×1000), layer `C-ROAD-CL`. Đoạn cong nằm ngang: dùng `bulges` (bulge = tan(góc ở tâm / 4), dấu dương khi cong phải theo chiều đi).
- Mép đường, bó vỉa, vỉa hè: `offset_entity` từ tim tuyến về hai phía theo mặt cắt (ví dụ đường B = 7.5 m: mép đường ±3750, bó vỉa ±3750, mép vỉa hè ±(3750 + bề rộng hè)).
- Vỉa hè: hatch `AR-CONC` scale 50 hoặc `ANSI37` scale 500, layer `C-SIDEWALK`.

## 4. Lý trình

- Ghi dạng `Km0+125.50` (km + mét, 2 chữ số thập phân). Lý trình 0 tại điểm đầu tuyến (hoặc theo người dùng).
- Vạch lý trình mỗi 50 m (cọc chẵn) và tại điểm đầu/cuối, đỉnh cong: đoạn ngắn 2000 vuông góc tim tuyến, layer `C-STATION`; chữ cao 1250 (2.5 mm × 500), xoay song song vạch.
- Tính vị trí: dùng `get_entity` của tim tuyến (vertices) để tính chiều dài cộng dồn; với đoạn thẳng nội suy tuyến tính.

## 5. Cao độ

- Ghi `+12.350` (mét, 3 chữ số thập phân, có dấu + / −). Ký hiệu: tam giác đều cạnh 750 đỉnh chạm điểm + đường nằm ngang, chữ cao 1250, layer `C-LEVEL`. Có thể `define_block` `C-CAODO` với attribute `CAO_DO`.
- Cao độ thiết kế và tự nhiên ghi hai dòng: thiết kế trên, tự nhiên dưới (trong ngoặc).

## 6. Bảng tọa độ

`add_table` layer `C-COORD`, title "BẢNG TỌA ĐỘ MỐC", cột: `Điểm | X (m) | Y (m)` (giá trị VN-2000 gốc, 3 chữ số thập phân, **không** đổi trục trong bảng), `col_widths` [3000, 6000, 6000] cho 1:500, text_height 1250. Mỗi mốc trên bản vẽ: `draw_circle` R500 + `add_text` tên mốc cao 1250, layer `C-COORD`.

Ví dụ (gốc cục bộ X0 = 1 234 000, Y0 = 567 000; điểm M1 X = 1 234 012.500, Y = 567 045.250 → x_cad = 45 250, y_cad = 12 500):

```json
{"calls": [
  {"tool": "draw_circle", "params": {"layer": "C-COORD", "center": [45250, 12500], "radius": 500}},
  {"tool": "add_text", "params": {"layer": "C-COORD", "position": [46000, 13000], "text": "M1", "height": 1250}},
  {"tool": "add_table", "params": {"layer": "C-COORD", "position": [60000, 30000], "title": "BẢNG TỌA ĐỘ MỐC", "col_widths": [3000, 6000, 6000], "text_height": 1250,
    "data": [["Điểm", "X (m)", "Y (m)"], ["M1", "1234012.500", "567045.250"]]}}
]}
```

## 7. Báo cáo

Nêu chiều dài tuyến (m), số cọc lý trình, các layer đã dùng, quy ước gốc tọa độ/đổi trục đã áp dụng, và nhắc gõ U một lần để hoàn tác cả lượt.
