# Kế hoạch nâng cấp lệnh vẽ (2D + 3D)

*13/11/2026 · thread QnA soạn theo yêu cầu của chủ dự án, luồng chính lưu lại. **Chủ dự án đã chốt 5 điểm (13/11, cuối
file).** Mỗi đợt chỉ bắt đầu khi chủ dự án duyệt nội dung + chi phí ước của đợt đó.*

## Hiện trạng

- 89 lệnh 2D và lệnh chung, có từ bản gốc v0.9.3, đã dùng thật.
- 29 lệnh 3D (3 đợt), qua kiểm tra tự động và kiểm tra độc lập, **chưa thử trên AutoCAD thật**.
- Code đọc ở `full/test-tong-the` @ `19944df` (`agent/src/tools/*.ts`, `plugin/ClaudeCAD.Plugin/Core/Plans.cs`).

## Nguyên tắc

- Thử thật trước: bộ cài thử nội bộ đã có.
- Mỗi đợt một thread viết, kèm thread con "Test & sửa", mỗi thread 1 lượt CI.
- Cuối chuỗi: kiểm tra độc lập và test tổng thể.
- Giữ tên lệnh cũ, để macro khách đã ghi không hỏng.
- Đo lại token theo gói sau mỗi đợt.

## Các đợt (đề xuất)

| Đợt | Nội dung | Gói |
| --- | --- | --- |
| 0 | Chủ dự án thử theo `docs/checklist-tong-the.md` (bộ cài thử nội bộ); lỗi chuyển về thread phụ trách | — |
| A (3D, rẻ) | `build_3d_from_plan` tự nhận layer tên khác (hỏi lại người dùng); cột; nhiều tầng một lần; lan can; `array_rect` thêm số tầng; gộp 6 lệnh dựng khối (`draw_box`, `draw_cylinder`, `draw_sphere`, `draw_wedge`, `draw_torus`, `draw_pyramid`) thành 1 | 3D / Full |
| B (sửa hình 2D) | Lệnh mới: STRETCH, BREAK, LENGTHEN, DIVIDE / MEASURE, GROUP. Nâng cấp: bo / vát góc cho polyline và cung (hiện chỉ cho 2 đoạn thẳng), offset hai phía, MATCHPROP, nối đường có dung sai | GROUP, MATCHPROP: mọi gói. Còn lại: 2D / Full |
| C (2D) | Hatch bấm một điểm; tự ghi kích thước cả mặt bằng; thống kê block ra bảng, đếm theo thuộc tính; tổng diện tích phòng / layer ra bảng; tìm & thay chữ (có thẻ xác nhận); in nhiều layout vào một PDF | 2D / Full |
| D (3D) | Dầm; tường cong; PRESSPULL; xuất STL; bảng khối lượng theo layer; va chạm ra bảng | 3D / Full |
| E (lớn) | Cầu thang (1 vế / chữ L / chữ U); mái bằng / 2 mái / 4 mái. Cần chốt kiểu và số mặc định | 3D / Full |
| F (sau phát hành) | Mọi lệnh hiểu UCS (hiện theo WCS); vật liệu / render; block từ thư viện; bộ layout khung tên; layer state; lưu góc nhìn; bộ style chuẩn Việt Nam | — |

## Chi phí ước (QnA đoán, chưa đo)

- Theo kế hoạch 3D cũ: khoảng $5–15 mỗi đợt.
- A–D: khoảng $20–60, chưa kể test & sửa.
- Cả chuỗi A–E, kèm kiểm tra độc lập và test tổng thể: khoảng $50–120.
- Thử tay trên AutoCAD: thêm 20–40 phút mỗi đợt.

## Chủ dự án đã chốt (13/11/2026)

1. **Gộp 6 lệnh dựng khối thành 1** (bớt khoảng 2k token mỗi phiên), làm trong đợt A, trước khi phát hành: sau khi
   phát hành, đổi tên lệnh sẽ làm hỏng macro khách đã ghi.
2. **Nâng cấp xong mới phát hành:** không phát hành bản hiện tại trước. Phát hành sau đợt E (và kiểm tra độc lập, test
   tổng thể cuối chuỗi).
3. **Gói của lệnh mới theo đúng phương án A:**
   - sửa hình 2D mới (STRETCH, BREAK, LENGTHEN, DIVIDE / MEASURE) và các nâng cấp sửa hình 2D: 2D / Full;
   - GROUP, MATCHPROP: mọi gói;
   - chữ / kích thước / bảng / hatch / in: 2D / Full;
   - cột, dầm, cầu thang, mái, STL: 3D / Full.
4. **Thứ tự** A → B → C → D → E. F để sau phát hành.
5. **Duyệt chi theo từng đợt:** trước mỗi đợt luồng chính báo nội dung + chi phí ước, chủ dự án đồng ý mới làm; xong
   đợt báo thời gian và phần tốn nhất.
