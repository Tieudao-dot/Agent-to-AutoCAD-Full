---
name: ve-ket-cau
description: Chuẩn vẽ kết cấu bê tông cốt thép của người dùng - bản vẽ thép dầm (triển khai + mặt cắt), mặt cắt cột, thép sàn, ký hiệu thanh trong vòng tròn, bảng thống kê thép (khối lượng theo đường kính), móng đơn / móng băng 3D. Dùng khi được yêu cầu vẽ thép dầm, cột, sàn, bố trí thép, thống kê thép, khối lượng thép, móng, móng băng, móng đơn, kết cấu. Use for reinforcement details, rebar schedules, footings and structural drawings.
plan: 2d
---

# Vẽ kết cấu bê tông cốt thép

> Bản vẽ hỗ trợ, **không phải tính toán kết cấu**: plugin chỉ vẽ và thống kê theo số người dùng đưa. **Không tự chọn** số
> thanh hay đường kính thép chủ; người dùng phải nói, vd "dầm 2Ø18 dưới, 2Ø16 trên", "cột 8Ø20". Kỹ sư vẫn kiểm.

Quy ước: mm. Số mặc định dưới đây đã được chủ dự án duyệt (26/11/2026).
Các chỗ ghi *(đối chiếu)* là số mặc định, **kỹ sư đối chiếu TCVN hiện hành** trước khi dùng.

## 1. Quy trình chuẩn bị (luôn làm trước khi vẽ)

1. Gọi `get_drawing_info` và `list_layers` để biết đơn vị, vùng bản vẽ đã có và các layer `S-*`.
2. Hỏi người dùng thép chủ nếu chưa có (không tự chọn). Đai / thép sàn không nói thì dùng thép mẫu (mục 3) và nói rõ.
3. `draw_rebar` cho từng cấu kiện: `element: "beam"` (triển khai + mặt cắt), `"column"` (mặt cắt, cần chiều cao cột
   `length`), `"slab"` (ô sàn `width` × `length`). Mỗi loại thanh có số hiệu trong vòng tròn và chữ "2Ø18 L=6440", "Ø8a200". Không dùng lại số hiệu đã có
   (bảng thép tách dòng và báo trùng).
4. `rebar_schedule` khi người dùng cần bảng: `table_position` để chèn bảng vào bản vẽ, `excel: true` khi muốn Excel. Không
   cộng hao hụt trừ khi người dùng nói % (`waste_percent`).
5. Móng 3D (gói 3D / Full): `build_footing` (`kind: "pad"` móng đơn, `"strip"` móng băng). Không nói cỡ thì dùng cỡ mẫu và
   nói "cỡ mẫu, kỹ sư quyết". Khối lượng bê tông: `volume_schedule`.

## 2. Số mặc định

| Nội dung | Số |
| --- | --- |
| Lớp bê tông bảo vệ | sàn 20, dầm 25, cột 25, móng có lót 40, móng không lót 70 *(đối chiếu)*; không nhỏ hơn đường kính thanh |
| Neo / nối chồng | neo 30d; nối 40d (dầm, sàn), 35d (cột). Báo khi dưới mức tối thiểu: neo ≥ 15d và ≥ 200; nối ≥ 20d và ≥ 250 *(đối chiếu)* |
| Móc | thép trơn (Ø ≤ 10, CB240-T): móc 180°, uốn trong 2,5d, thẳng 3d (cộng 6,25d mỗi móc vào chiều dài); đai: móc 135°, thẳng 6d và ≥ 75 *(đối chiếu)*; thép gân (CB400-V): bẻ L 10d khi neo (đầu thanh dầm vào gối) |
| Khối lượng | 0,00617 × d² kg/m; chiều dài thanh làm tròn 10 mm; kg 2 số lẻ; cây thép 11,7 m: thanh dài hơn thì đếm mối nối và cộng đoạn nối chồng vào chiều dài, khối lượng |
| Khe hở thép dầm | khe hở giữa hai thanh một hàng: thép dưới ≥ max(d, 25), thép trên ≥ max(d, 30); không đủ chỗ thì báo, gợi ý bớt thanh hoặc tăng bề rộng dầm |
| Khe hở thép cột | khe hở giữa hai thanh một cạnh cột ≥ max(d, 50); không đủ chỗ thì báo, gợi ý bớt thanh hoặc tăng cạnh cột *(đối chiếu)* |
| Đường kính | 6, 8, 10, 12, 14, 16, 18, 20, 22, 25, 28, 32 |
| Tỉ lệ | mặt cắt dầm / cột 1:20; triển khai dầm, thép sàn 1:50; mặt bằng móng 1:100 *(đối chiếu TCVN 5572)* |

## 3. Thép mẫu và cỡ móng mẫu (khi người dùng không nói)

- Sàn Ø8 a200; đai dầm Ø6 a150 ở 1/4 nhịp gần gối, a200 đoạn giữa; đai cột Ø6 a200.
- Móng đơn 1200 × 1200, đế cao 350, cổ móng bằng cỡ cột; móng băng rộng 1000, cao 300, sườn 300 × 600; bê tông lót dày 100,
  rộng hơn móng 100 mỗi bên.

## 4. Layer

| Layer | Nội dung |
| --- | --- |
| `S-CONC` | nét bê tông (mặt cắt, triển khai, ô sàn) |
| `S-REBAR` | thanh thép, đai |
| `S-REBAR-TEXT` | số hiệu thanh, chữ |
| `S-FOOT-3D`, `S-LEAN-3D` | móng 3D, bê tông lót |

## 5. Báo cáo

Nêu các loại thanh đã vẽ (số hiệu, ký hiệu, chiều dài, số thanh), thép mẫu đã dùng, cảnh báo neo / nối nếu có, và nhắc: bản
vẽ hỗ trợ, kỹ sư kiểm lại; gõ U một lần để hoàn tác cả lượt.
