# Checklist thử tay đợt H7: ống MEP 3D (khoảng 25 phút)

Máy gói 3D hoặc Full, AutoCAD 2022–2027. Bản vẽ có sẵn vài dầm 3D (vd dựng bằng "dựng dầm 220x400 theo trục…") hoặc tự
vẽ một khối hộp trên layer `A-BEAM-3D`.

*Các cỡ ống mặc định, độ dốc, cao độ dưới dầm, bán kính co, khoảng hở 50 mm, tên layer / màu đang **chờ chủ dự án duyệt***
*(`docs/dot-h-so-tieu-chuan.md`, mục H7). Khi thử, thấy số nào chưa hợp thì ghi lại để chỉnh trong `H7Numbers`.*

## A. Đi ống tròn (~8 phút)

1. Gõ `CLAUDECAD_SELFTEST`. Dòng "Nâng cấp đợt H7: ống MEP 3D …" phải **đạt**: có 1 co, nhánh có tê, thống kê, ít nhất 1
   chỗ chạm dầm.
2. "Đi ống thoát nước xí từ (0,0) tới (4000,0) rồi tới (4000,3000), cao độ tim ống 2800". Cần thấy:
   - ống tròn 3D trên layer `P-DRAIN-3D` (xanh lá), có **một co** ở góc;
   - ống dốc dần về điểm cuối; Claude nói độ dốc và cao độ hai đầu;
   - xoay góc nhìn (`3DORBIT` hoặc "xem phối cảnh tây nam") thấy co nối liền hai đoạn, không hở, không chồng.
3. "Đi ống cấp nước D25 từ (1000,0) tới (1000,-2000), cao độ 3000, nối vào ống vừa vẽ": nếu điểm đầu nằm trên ống kia,
   Claude vẽ **tê** ở chỗ nối (khối hơi to hơn ống chính). Không nằm trên ống thì Claude báo rõ.
4. "Đi ống thoát nước độ dốc 0,5%": Claude **cảnh báo** độ dốc thấp hơn mức tối thiểu của cỡ ống.
5. "Đi ống PCCC từ … tới …" mà **không nói cỡ**: Claude hỏi cỡ ống (không tự chọn).

## B. Ống gió (~5 phút)

1. "Đi ống gió cấp 400x300 từ (0,5000) tới (6000,5000) rồi lên tới (6000,5000,4000), cao độ 3200": ống hộp chữ nhật trên
   `M-DUCT-3D`, mặt rộng nằm ngang, co hộp ở chỗ rẽ lên.
2. "Đi ống gió hồi" mà không nói cỡ: Claude hỏi cỡ.

## C. Đi dưới dầm, UCS (~4 phút)

1. "Đi ống cấp nước D32 dưới dầm từ … tới …" (tuyến đi dưới một dầm 3D): ống nằm ngay dưới đáy dầm, không chạm dầm.
2. Đặt UCS xoay 30° và cao +3000 (`UCS` → Z → 30, rồi dời gốc lên 3000), đi một ống theo tọa độ UCS: ống nằm đúng chỗ theo UCS.

## D. Thống kê, Excel, va chạm (~6 phút)

1. "Thống kê ống": bảng theo hệ + cỡ ống, chiều dài (m), số đoạn, số co, số tê; dòng Tổng.
2. "Xuất thống kê ống ra Excel": file `.xlsx` cạnh bản vẽ; gọi lại lần hai thì Claude hỏi **thẻ ghi đè**.
3. "Thống kê ống có cộng hao hụt 5%": thêm cột "Chiều dài + hao hụt 5%".
4. "Soát ống va chạm với dầm cột": Claude kể chỗ **chạm** và chỗ **sát** (dưới khoảng hở), có handle hai bên; ống của cùng
   một tuyến, hay nhánh với ống chính của nó, không bị báo.
5. Máy gói 2D: hỏi "đi ống cấp nước 3D" → Claude nói gói này không có lệnh 3D.

## E. Giới hạn nói rõ với khách

- Claude nói rõ chỉ vẽ theo cỡ ống khách đưa (hoặc cỡ mặc định của loại ống), **không tính thủy lực / lưu lượng**.
- U một lần xóa hết ống vừa vẽ của lượt.
