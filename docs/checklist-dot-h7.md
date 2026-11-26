# Checklist thử tay đợt H7: ống MEP 3D (khoảng 25 phút)

*Đã gộp vào khối "Đợt H phần 2" của [`docs/checklist-tong-the.md`](checklist-tong-the.md) (test tổng thể đợt H phần 2), thử tay theo file đó.*

Máy gói 3D hoặc Full, AutoCAD 2022–2027. Bản vẽ có sẵn vài dầm 3D (vd dựng bằng "dựng dầm 220x400 theo trục…") hoặc tự
vẽ một khối hộp trên layer `A-BEAM-3D`.

*Số mặc định (đã duyệt 26/11/2026): PPR D25 / D32; uPVC D60 / D110, thông hơi D60; dốc 2 % (D60: 2,5 %); đỉnh ống cách đáy dầm 100 mm*
*(đáy sàn 300 mm); co 1,5D (ống gió 1 lần bề rộng); khoảng hở 50 mm. Thấy số nào chưa hợp thì ghi lại để chỉnh trong `H7Numbers`.*

## A. Đi ống tròn (~8 phút)

1. Gõ `CLAUDECAD_SELFTEST`. Dòng "Nâng cấp đợt H7: ống MEP 3D …" phải **đạt**: có 1 co, nhánh có tê, thống kê, ít nhất 1
   chỗ chạm dầm.
2. "Đi ống thoát nước xí từ (0,0) tới (4000,0) rồi tới (4000,3000), cao độ tim ống 2800". Cần thấy:
   - ống tròn 3D trên layer `P-DRAIN-3D` (xanh lá), có **một co** ở góc;
   - ống D110, dốc 2 % về điểm cuối (đầu cuối thấp hơn đầu đầu khoảng 140 mm); Claude nói độ dốc và cao độ hai đầu;
   - xoay góc nhìn (`3DORBIT` hoặc "xem phối cảnh tây nam") thấy co nối liền hai đoạn, không hở, không chồng.
3. "Đi ống cấp nước D25 từ (1000,0) tới (1000,-2000), cao độ 3000, nối vào ống vừa vẽ": nếu điểm đầu nằm trên ống kia,
   Claude vẽ **tê** ở chỗ nối (khối hơi to hơn ống chính). Không nằm trên ống thì Claude báo rõ.
4. "Đi ống thoát nước độ dốc 0,5%": Claude **cảnh báo** độ dốc thấp hơn mức tối thiểu của cỡ ống. Ống thoát sàn D60 không nói
   độ dốc: dốc 2,5 %, **không** có cảnh báo.
5. "Đi ống PCCC từ … tới …" mà **không nói cỡ**: Claude hỏi cỡ ống (không tự chọn).

## B. Ống gió (~5 phút)

1. "Đi ống gió cấp 400x300 từ (0,5000) tới (6000,5000) rồi lên tới (6000,5000,4000), cao độ 3200": ống hộp chữ nhật trên
   `M-DUCT-3D`, mặt rộng nằm ngang, co hộp ở chỗ rẽ lên; đoạn đứng **không xoắn** so với co (mặt rộng liền mạch).
2. "Đi ống gió hồi" mà không nói cỡ: Claude hỏi cỡ.
3. *(Test & sửa H7)* Ống gió 600x300 đi theo trục X, lên đứng, rồi đi theo trục **Y** ở trên: cả hai đoạn ngang đều có mặt rộng
   600 **nằm ngang** (đo chiều cao 300); Claude nói tiết diện phải xoay ở đầu ống đứng (thực tế cần đoạn chuyển tiếp).

## C. Đi dưới dầm, UCS (~4 phút)

1. "Đi ống cấp nước D32 dưới dầm từ … tới …" (tuyến đi dưới một dầm 3D): đỉnh ống cách đáy dầm 100 mm (đo bằng `DIST`), không chạm dầm.
2. Đặt UCS xoay 30° và cao +3000 (`UCS` → Z → 30, rồi dời gốc lên 3000), đi một ống theo tọa độ UCS: ống nằm đúng chỗ theo UCS.
3. *(Test & sửa H7)* Bản vẽ có dầm hai tầng chồng nhau, "đi ống dưới dầm" ở tầng trên: Claude báo có dầm ở nhiều cao độ, đã dùng
   dầm thấp nhất, nhờ cho cao độ nếu ống ở tầng khác.
4. *(Test & sửa H7)* UCS nghiêng (`UCS` → X → 90), đi ống theo mặt bằng kèm cao độ: Claude nói UCS đang nghiêng, nhờ cho điểm
   x, y, z hoặc đặt lại UCS; không vẽ gì sai chỗ.
5. *(Test & sửa H7)* "Đi ống thoát D110 qua các điểm (0,0,3000) (4000,0,2960) (4000,3000,2900)": Claude báo đoạn dốc nhất mức 1 %
   thấp hơn 1,2 %.

## D. Thống kê, Excel, va chạm (~6 phút)

1. "Thống kê ống": bảng theo hệ + cỡ ống, chiều dài (m), số đoạn, số co, số tê; dòng Tổng.
2. "Xuất thống kê ống ra Excel": file `.xlsx` cạnh bản vẽ; gọi lại lần hai thì Claude hỏi **thẻ ghi đè**.
3. "Thống kê ống có cộng hao hụt 5%": thêm cột "Chiều dài + hao hụt 5%".
4. "Soát ống va chạm với dầm cột": Claude kể chỗ **chạm** và chỗ **sát** (dưới khoảng hở), có handle hai bên; ống của cùng
   một tuyến, hay nhánh với ống chính của nó, không bị báo. Một ống đi chéo qua phòng, xa cột: **không** bị báo "sát".
6. *(Test & sửa H7)* Sau khi đi vài tuyến ống, "soát bản vẽ": layer `P-…-3D`, `M-DUCT-3D`, `F-SPRK-3D` **không** bị báo "ngoài chuẩn".
5. Máy gói 2D: hỏi "đi ống cấp nước 3D" → Claude nói gói này không có lệnh 3D.

## E. Giới hạn nói rõ với khách

- Claude nói rõ chỉ vẽ theo cỡ ống khách đưa (hoặc cỡ mặc định của loại ống), **không tính thủy lực / lưu lượng**.
- Hỏi "độ dốc tối thiểu ống D100 là bao nhiêu": Claude nói 1,2 % là số mặc định, kỹ sư đối chiếu TCVN hiện hành.
- U một lần xóa hết ống vừa vẽ của lượt.
