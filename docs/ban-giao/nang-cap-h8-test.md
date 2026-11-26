# Bàn giao: Test & sửa đợt H8 — kết cấu (26/11/2026)

Thread `session_01D56HmFcYrzsme7GoBm8tNC` (người rà). Nhánh `full/nang-cap-h8-test`, tách từ `full/nang-cap-h8` @ `872ad6b` (code
`ca89b95`; thread viết đã sửa sẵn: số hiệu trùng tách dòng + báo, thanh dài hơn 11,7 m cộng nối chồng, thép gân dầm bẻ L 10d).
Commit cuối của nhánh này là điểm xuất phát của H9. Chưa chạy trên AutoCAD thật. Thử tay thêm: `docs/checklist-dot-h8.md` mục
"Test & sửa H8".

## 1. Lỗi đã sửa

| Mã | Mức | Lỗi | Đã sửa |
| --- | --- | --- | --- |
| H8T-01 | Vừa | Mặt cắt dầm ghi "MẶT CẮT 1:20" nhưng vẽ cùng cỡ với hình triển khai (1:50): in chung một khung nhìn thì mặt cắt thật ra 1:50, nhãn sai, mặt cắt nhỏ khó đọc | Mặt cắt vẽ phóng `scale / 20` lần (mặc định 2,5) để in cùng khung 1:50 đọc đúng 1:20; chữ giữ cỡ giấy |
| H8T-02 | Nhẹ | Kết quả `draw_rebar` (mục `bars`) ghi đai dầm "Ø6a150", thiếu a200 đoạn giữa (chữ trên bản vẽ đúng) | `BarKind.MidSpacing`: "Ø6a150/a200" |
| H8T-03 | Nhẹ | Cột: không kiểm khoảng hở giữa các thanh dọc cạnh (vd 20Ø25 trong cột 300 × 300 vẽ chồng lên nhau) | Báo lỗi khi khe hở giữa hai thanh nhỏ hơn một đường kính (như hàng thép dầm) |

## 2. Soát lại bản sửa của thread viết

- **Số hiệu trùng:** khác Ø / chiều dài / hình dạng / cấu kiện → dòng riêng + cảnh báo; cùng loại (vd COPY chi tiết) → cộng số
  thanh. Đúng (test).
- **Thanh dài hơn 11,7 m:** cộng `n` mối × 40d (dầm, sàn) / 35d (cột), lặp tới khi số mối không đổi; chiều dài và khối lượng
  tính từ chiều dài đã cộng. Đúng (12,5 m Ø20 → 1 mối, 13,3 m; 23 m Ø25 → 2 mối, 25 m).
- **Thép gân bẻ L 10d:** tự tính lại 2Ø18 nhịp 5 m: 5 000 + 2 × 30d (540) + 2 × 10d (180) = **6 440** ✓; 2Ø16: 5 000 + 960 + 320 =
  6 280. Lớp bảo vệ 25 nằm theo phương ngang dầm, không vào chiều dài thanh dọc (dùng cho vị trí thanh trong mặt cắt, chiều dài
  đai). Thép trơn vẫn móc 180° (không bẻ L). Không sinh lỗi mới.

## 3. Đã soát, không sửa

| Chỗ | Kết quả |
| --- | --- |
| Lớp bảo vệ sàn 20 / dầm 25 / cột 25, không nhỏ hơn Ø | đúng (`Cover`) |
| Lớp bảo vệ móng 40 / 70 | có trong `H8Numbers` và skill nhưng **chưa lệnh nào dùng** (chưa vẽ thép móng) — ghi để biết |
| Neo 30d, nối 40d / 35d, báo dưới mức tối thiểu (neo 15d / 200, nối 20d / 250) | đúng; dầm báo neo, cột báo nối |
| Móc thép trơn 180° (uốn 2,5d, thẳng 3d); đai 135°, thẳng max(6d, 75) | đúng (Ø6 75, Ø10 75, Ø14 84) |
| Khối lượng 0,00617·d², chiều dài làm tròn 10 mm, kg 2 số lẻ, tổng theo Ø + loại thép, cột hao hụt tắt | đúng |
| Ø ≤ 10 CB240-T, Ø ≥ 12 CB400-V; cây 11,7 m | đúng |
| Plugin không tự chọn thép chủ | đúng: thiếu `bottom` / `top` / `bars` thì báo lỗi, nhờ hỏi khách |
| Cỡ móng mẫu ghi "cỡ mẫu, kỹ sư quyết" | đúng (`sample_sizes`) |
| Đơn vị m / mm | đúng: mọi số mm chia `f` (mm / đơn vị), chữ theo cỡ giấy × tỉ lệ / `f` |
| UCS | `build_footing`, `draw_rebar` vẽ như World rồi dời theo UCS (CreateTools) |
| Gói | `build_footing` 3D / Full; `draw_rebar`, `rebar_schedule` 2D / Full; thẻ F5 cho Excel |
| `check_drawing` với `S-…` | không báo "ngoài chuẩn" (bản gộp H6-test + H7-test + H8) |
| Bốn chỗ "chưa chắc" | skill ghi *(đối chiếu)* "kỹ sư đối chiếu TCVN hiện hành"; mô tả lệnh và `note` kết quả cũng nói |
| Bảng số thường (`nang-cap-h8.md` mục 3) | khớp code: 50 mm, 50 – 1000, 40 thanh, 500 / 400, 2,5 / 8 mm, màu 8 / 9 / 7 / 1 / 7, max(1000, 3h) |
| **Nhẹ, để lại** | skill `ve-ket-cau` có `plan: 2d` nên máy gói 3D không có phần móng của skill (mô tả lệnh vẫn đủ); bảng số tiêu chuẩn ghi Ø32 6,31 kg/m, code ra 6,32 (0,00617 × 32² = 6,318, làm tròn) |

## 4. Test

- Lõi: **1671 / 1671** (thêm 5 ở `NangCapH8TestSuaTests.cs`).
- Trợ lý: **457 / 457**.
- Build `net48`, `net8.0-windows`, `net10.0-windows`: 0 lỗi.

## 5. CI

*(ghi sau khi chạy)*
